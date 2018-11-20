using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ServiceBus;
using Microsoft.ServiceBus.Messaging;
using SharedModel.Model;

namespace FileProcessingService
{
	public class FileService : IDisposable
	{
		#region private fields
		private const string DocQueueName = "DocQueue";
		private const string SubscriptionName = "FileServiceSubscription";
		private readonly FileSystemWatcher watcher;
		private readonly string inDir;
		private readonly string outDir;
		private readonly string outWrongFileNamingDir;
		private readonly string invalidFileSequenceDir;
		private readonly Thread workThread;
		private readonly Thread stateThread;
		private readonly ManualResetEvent stopWork;
		private readonly AutoResetEvent newFileEvent;
		private readonly PdfCreator pdfCreator;
		private string barcodeSeparator;
		private string timeout;
		private Timer timer;
		private Status status;
		private SubscriptionClient subscriber;
		#endregion

		public FileService(string inDir, string outDir, string outWrongFileNamingDir, string invalidFileSequenceDir)
		{
			this.inDir = inDir;
			this.outDir = outDir;
			this.outWrongFileNamingDir = outWrongFileNamingDir;
			this.invalidFileSequenceDir = invalidFileSequenceDir;

			this.InitializeWorkFolder(inDir, outDir, outWrongFileNamingDir, invalidFileSequenceDir);

			this.watcher = new FileSystemWatcher(this.inDir);
			this.watcher.Created += this.Watcher_Created;
			this.workThread = new Thread(this.WorkProcedure);
			this.stateThread = new Thread(this.StatusNotifierProcedure);
			this.stopWork = new ManualResetEvent(false);
			this.newFileEvent = new AutoResetEvent(false);
			this.pdfCreator = new PdfCreator();

			this.pdfCreator.CallbackWhenReadyToSendToQueue += this.SendPdfDocument;
			this.pdfCreator.CallbackWhenSecuenceHasWrongFileExtention += this.MoveAllFileSequenceToOtherDir;

			this.barcodeSeparator = ConfigurationManager.AppSettings["barcode"];
			this.timeout = ConfigurationManager.AppSettings["timeout"];
		}

		public void Start()
		{
			this.InitializeTopicSubscription();

			this.stateThread.Start();
			this.workThread.Start();
			this.watcher.EnableRaisingEvents = true;
		}

		public void Stop()
		{
			this.watcher.EnableRaisingEvents = false;
			this.stopWork.Set();
			this.stateThread.Join();
			this.workThread.Join();
			this.subscriber.Close();
		}

		public bool TryOpen(string fileNmae, int numberOfAttempt)
		{
			for (int i = 0; i < numberOfAttempt; i++)
			{
				try
				{
					FileStream file = File.Open(fileNmae, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

					file.Close();

					return true;
				}
				catch (IOException)
				{
					Task.Delay(2000);
				}
			}

			return false;
		}

		public void Dispose()
		{
			this.watcher.Dispose();
			this.stopWork.Dispose();
			this.newFileEvent.Dispose();
			this.timer.Dispose();
		}

		private void MoveAllFileSequenceToOtherDir(object sender, EventArgs e)
		{
			this.ClearPdfCreatorFilePathes();

			this.pdfCreator.Reset();
		}

		private void SendPdfDocument(object sender, EventArgs e)
		{
			QueueClient client = null;
			byte[] pdf = null;
			try
			{
				pdf = this.pdfCreator.GetPdf(this.outDir);

				this.ClearPdfCreatorFilePathes();
				this.pdfCreator.Reset();

				client = QueueClient.Create(DocQueueName);
				client.Send(new BrokeredMessage(pdf));
			}
			catch (MessageSizeExceededException ex)
			{
				this.StartToProcessChunkDoc(pdf, client);

				Console.WriteLine(ex.Message); ////log
			}
			catch (Exception ex)
			{
				throw ex;
			}
			finally
			{
				client.Close();
			}
		}

		private void ClearPdfCreatorFilePathes()
		{
			foreach (var fullFilePath in this.pdfCreator.GetAllImageFilePath)
			{
				if (this.TryOpen(fullFilePath, 5))
				{
					File.Delete(fullFilePath);
				}
			}

			if (this.TryOpen(this.pdfCreator.CurrentBarcodeFilePath, 5))
			{
				File.Delete(this.pdfCreator.CurrentBarcodeFilePath);
			}
		}

		private void WorkProcedure()
		{
			List<string> fileNameValidList;
			List<string> sortedFileList;
			do
			{
				fileNameValidList = this.SortOutFile(this.inDir, this.outWrongFileNamingDir);

				sortedFileList = fileNameValidList.OrderBy(s => int.Parse(Path.GetFileNameWithoutExtension(s).Split('_')[1])).ToList();

				foreach (var file in sortedFileList)
				{
					this.status = Status.InProcess;
					if (this.stopWork.WaitOne(TimeSpan.Zero))
					{
						return;
					}

					if (this.TryOpen(file, 5))
					{
						this.pdfCreator.PushFile(file);
					}
				}

				this.status = Status.Waiting;
			} while (WaitHandle.WaitAny(new WaitHandle[] { this.stopWork, this.newFileEvent }, 5000) != 0);
		}

		private void StatusNotifierProcedure()
		{
			this.timer = this.CreateTimer();
		}

		private List<string> SortOutFile(string targetDir, string outDir)
		{
			Regex regex = new Regex(@".*_\d+\.");

			List<string> result = new List<string>();

			foreach (var file in Directory.EnumerateFiles(targetDir))
			{
				if (!regex.Match(Path.GetFileName(file)).Success)
				{
					this.MoveToWrongFileDirectory(outDir, file);
				}
				else if (Path.GetFileName(file).Split('_').Length > 2)
				{
					this.MoveToWrongFileDirectory(outDir, file);
				}
				else
				{
					result.Add(file);
				}
			}

			return result;
		}

		private void MoveToWrongFileDirectory(string outWrongDir, string file)
		{
			if (this.TryOpen(file, 10))
			{
				File.Move(file, Path.Combine(outWrongDir, Path.GetFileName(file)));
			}
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			this.newFileEvent.Set();
		}

		private void InitializeWorkFolder(string inDir, string outDir, string outWrongFileNamingDir, string invalidFileSequenceDir)
		{
			if (!Directory.Exists(this.inDir))
			{
				Directory.CreateDirectory(this.inDir);
			}

			if (!Directory.Exists(this.outDir))
			{
				Directory.CreateDirectory(this.outDir);
			}

			if (!Directory.Exists(this.outWrongFileNamingDir))
			{
				Directory.CreateDirectory(this.outWrongFileNamingDir);
			}

			if (!Directory.Exists(this.invalidFileSequenceDir))
			{
				Directory.CreateDirectory(this.invalidFileSequenceDir);
			}
		}

		private void InitializeTopicSubscription()
		{
			var topicName = ConfigurationManager.AppSettings["topic"];

			var namespaceManager = NamespaceManager.Create();
			var subscription = new SubscriptionDescription(topicName, SubscriptionName);
			if (!namespaceManager.SubscriptionExists(topicName, SubscriptionName))
			{
				namespaceManager.CreateSubscription(subscription);
			}

			this.subscriber = SubscriptionClient.Create(topicName, SubscriptionName, ReceiveMode.ReceiveAndDelete);

			this.subscriber.OnMessage(msg =>
			{
				var data = msg.GetBody<StateModelData>();

				// change barcode value
				this.barcodeSeparator = data.CurrentSettings.BarcodeValue;

				// dispose prev timer and create the new timer to start the method immediately
				this.timer.Dispose();
				this.timer = this.CreateTimer();
			});
		}

		private void TimerCallBackMethod(object obj)
		{
			try
			{
				string queueName = ConfigurationManager.AppSettings["centralQueue"];

				var client = QueueClient.Create(queueName);
				var state = new StateModelData()
				{
					Status = this.status,
					CurrentSettings = new Settings()
					{
						Timeout = int.Parse(ConfigurationManager.AppSettings["timeout"]),
						BarcodeValue = this.barcodeSeparator
					}
				};

				client.Send(new BrokeredMessage(state));
				client.Close();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
			}
		}

		private Timer CreateTimer()
		{
			int timer = int.Parse(ConfigurationManager.AppSettings["timer"]);

			return new Timer(this.TimerCallBackMethod, 0, 0, timer);
		}

		private void StartToProcessChunkDoc(byte[] pdf, QueueClient client)
		{
			byte[] signal = new byte[1];

			signal[0] = 0;
			client.Send(new BrokeredMessage(signal));

			List<byte> chunk = new List<byte>();

			int count = 0;

			for (int i = 0, j = 0; i < pdf.Length; i++, j++)
			{
				if (j < 200000)
				{
					count++;
					chunk.Add(pdf[i]);
				}
				else
				{
					client.Send(new BrokeredMessage(chunk.ToArray()));

					j = 0;
					chunk = new List<byte>();
					chunk.Add(pdf[i]);
				}
			}

			if (chunk.Count > 0)
			{
				client.Send(new BrokeredMessage(chunk.ToArray()));
			}

			signal[0] = 1;
			client.Send(new BrokeredMessage(signal));
		}
	}
}
