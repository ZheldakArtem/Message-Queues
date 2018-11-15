using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Topshelf;

namespace FileProcessingService
{
	class Program
	{
		static void Main(string[] args)
		{
			var currentDir = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
			var inDir = Path.Combine(currentDir, "in");
			var outDir = Path.Combine(currentDir, "out");
			var wrongFileNamingOutDir = Path.Combine(currentDir, "wrongFileNamingOut");
			var invalidSequence = Path.Combine(currentDir, "invalidSequence");

			var config = new LoggingConfiguration();
			var fileTarget = new FileTarget()
			{
				Name = "Default",
				FileName = Path.Combine(currentDir, "log.txt"),
				Layout = "${date} ${message} ${onexception:inner=${exception:format=toString}}"
			};

			config.AddTarget(fileTarget);

			config.AddRuleForAllLevels(fileTarget);

			var logFactory = new LogFactory(config);

			HostFactory.Run(
				conf => {
					conf.Service<FileService>(
					   s =>
					   {
						   s.ConstructUsing(() => new FileService(inDir, outDir, wrongFileNamingOutDir, invalidSequence));
						   s.WhenStarted(serv => serv.Start());
						   s.WhenStopped(serv => serv.Stop());
					   }).UseNLog(logFactory);
					conf.SetServiceName("FileProcessingService");
					conf.SetDisplayName("File Processing Service");
					conf.StartAutomaticallyDelayed();
					conf.RunAsLocalSystem();
				});
		}
	}
}
