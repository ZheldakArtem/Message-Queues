using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using NLog;
using SharedModel.Model;

namespace QueueModule
{
	public class MainProcessingService
	{
		private readonly FileSystemWatcher watcher;
		private string outDir;
		private string settingsDir;
		private bool workWithChunkDoc = false;
		private List<byte> chunksBuffer = new List<byte>();
		private QueueClient docQueueClient;
		private QueueClient centralManagementClient;
		private Logger errorLog;
		private Logger infoLog;

		public MainProcessingService()
		{
			try
			{
				this.InitializeServiceBus();

				this.InitializeDirectories();

				this.InitializeLogInstances();

				this.watcher = new FileSystemWatcher(this.settingsDir);
				this.watcher.Changed += this.ChangeSettingCallBack;
			}
			catch (Exception ex)
			{
				this.errorLog.Error(ex, "Something was wrong when initializing method were called");
				throw ex;
			}
		}

		public void Start()
		{
			this.infoLog.Info("Service was started");

			this.docQueueClient = QueueClient.Create(ConfigurationManager.AppSettings["queue"], ReceiveMode.ReceiveAndDelete);

			this.docQueueClient.OnMessage(this.ProcessRetrievedMessage);

			this.centralManagementClient = QueueClient.Create(ConfigurationManager.AppSettings["centralQueue"], ReceiveMode.ReceiveAndDelete);

			this.centralManagementClient.OnMessage(msg =>
			{
				var data = msg.GetBody<StateModelData>();
				this.infoLog.Info("Service retrieved message from central queue");
			});

			this.watcher.EnableRaisingEvents = true;
		}

		public void Stop()
		{
			this.infoLog.Info("Service was stopped");
			this.docQueueClient.Close();
			this.centralManagementClient.Close();
			this.watcher.EnableRaisingEvents = false;
		}

		private void ProcessRetrievedMessage(BrokeredMessage msg)
		{
			var randomName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
			var newPdf = this.outDir + "\\" + randomName + ".pdf";
			var body = msg.GetBody<byte[]>();

			try
			{
				// 0  - determines start of sequences of chunks
				// 1 - determines end of sequences of chunks
				if (body.Length == 1 && body[0] == 0)
				{
					this.workWithChunkDoc = true;
				}
				else if (body.Length == 1 && body[0] == 1)
				{
					this.workWithChunkDoc = false;
				}

				if (!this.workWithChunkDoc)
				{
					if (this.chunksBuffer.Count > 0)
					{
						File.WriteAllBytes(newPdf, this.chunksBuffer.ToArray());

						this.chunksBuffer = new List<byte>(); // clear buffer
					}
					else
					{
						File.WriteAllBytes(newPdf, body);
					}
				}
				else
				{
					this.chunksBuffer.AddRange(body);
				}
			}
			catch (Exception ex)
			{
				this.errorLog.Error(ex, "ProcessRetrievedMessage method catch some error");
			}
		}

		private void ChangeSettingCallBack(object sender, FileSystemEventArgs e)
		{
			bool accessed = false;
			while (!accessed)
			{
				try
				{
					this.infoLog.Info("Setting file was changed");
					XmlSerializer f = new XmlSerializer(typeof(StateModelData));
					StateModelData stateData = null;
					using (FileStream fs = new FileStream(this.settingsDir + "\\" + ConfigurationManager.AppSettings["settings"], FileMode.OpenOrCreate))
					{
						stateData = (StateModelData)f.Deserialize(fs);
					}

					var client = TopicClient.Create(ConfigurationManager.AppSettings["topic"]);
					client.Send(new BrokeredMessage(stateData));
					client.Close();
					this.infoLog.Info("New setting was sent to all subscribers");
					accessed = true;
				}
				catch (IOException ex)
				{
					this.errorLog.Error(ex, "Cannot read setting file");
					Task.Delay(2000);
				}
			}
		}

		private void InitializeServiceBus()
		{
			var docQueueName = ConfigurationManager.AppSettings["queue"];
			var centralManagementQueue = ConfigurationManager.AppSettings["centralQueue"];
			var docTopic = ConfigurationManager.AppSettings["topic"];
			var nsp = NamespaceManager.Create();

			if (!nsp.QueueExists(docQueueName))
			{
				nsp.CreateQueue(docQueueName);
			}

			if (!nsp.TopicExists(docTopic))
			{
				nsp.CreateTopic(docTopic);
			}

			if (!nsp.QueueExists(centralManagementQueue))
			{
				nsp.QueueExists(centralManagementQueue);
			}
		}

		private void InitializeDirectories()
		{
			var currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			this.outDir = Path.Combine(currentDir, "out");

			if (!Directory.Exists(this.outDir))
			{
				Directory.CreateDirectory(this.outDir);
			}

			this.settingsDir = Path.Combine(currentDir, "settings");
			if (!Directory.Exists(this.settingsDir))
			{
				Directory.CreateDirectory(this.settingsDir);
			}
		}
		
		private void InitializeLogInstances()
		{
			this.errorLog = LogManager.GetLogger("Error");
			this.infoLog = LogManager.GetLogger("Info");
		}
	}
}
