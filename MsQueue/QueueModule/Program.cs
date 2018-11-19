using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace QueueModule
{
	public class Program
	{
		public static void Main(string[] args)
		{
			HostFactory.Run(conf =>
			{
				conf.Service<MainProcessingService>(s =>
				{
					s.ConstructUsing(() => new MainProcessingService());
					s.WhenStarted(st => st.Start());
					s.WhenStopped(st => st.Stop());
				});
				conf.SetServiceName("MainProcessingService");
				conf.SetDisplayName("Main Processing Service");
				conf.StartAutomaticallyDelayed();
				conf.RunAsLocalSystem();
			});
		}
	}
}
