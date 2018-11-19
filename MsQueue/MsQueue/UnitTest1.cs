using System;
using System.IO;
using Microsoft.ServiceBus.Messaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedModel.Model;

namespace MsQueue
{
	[TestClass]
	public class UnitTest1
	{
		[TestMethod]
		public void SendSharedModelTest()
		{
			var model = new StateModelData()
			{
				CurrentSettings = new Settings()
				{
					BarcodeValue = "testBarcode",
					Timeout = 1000
				},
				Status = Status.Waiting
			};


			var client = QueueClient.Create("customqueue");

			client.Send(new BrokeredMessage(model));
			client.Close();

		}

		[TestMethod]
		public void RetrieveModelTest()
		{
			var client = QueueClient.Create("customqueue");
			var res = client.Receive();
			var data = res.GetBody<StateModelData>();
		}

		[TestMethod]
		public void SendMethod1()
		{

			var client = QueueClient.Create("customqueue");

			client.Send(new BrokeredMessage("Hello World!!!"));
			client.Close();
		}

		[TestMethod]
		public void ReceiveMethod2()
		{
			BrokeredMessage message = null;
			var client = QueueClient.Create("customqueue", ReceiveMode.ReceiveAndDelete);
			message = client.Receive();

			var data = message.GetBody<string>();

			Console.WriteLine("{0} - {1}", message.MessageId, data);

			client.Close();
		}

		[TestMethod]
		public void TestGetByteFromFile()
		{
			var b = File.ReadAllBytes(@"D:\WORK\Mentoring2018\5. Message Queues(Module 5)\Tasks\MsQueue\MsQueue\Image\image_0.jpg");

			File.WriteAllBytes(@"D:\WORK\Mentoring2018\5. Message Queues(Module 5)\Tasks\MsQueue\MsQueue\Image\new.jpg", b);

			Console.WriteLine();
		}

	}
}
