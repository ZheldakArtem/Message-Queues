using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace FileProcessingService
{
	public class FileService : IDisposable
	{
		private readonly FileSystemWatcher watcher;
		private readonly string inDir;
		private readonly string outDir;
		private readonly string outWrongFileNamingDir;
		private readonly string invalidFileSequenceDir;
		private readonly Thread workThread;
		private readonly ManualResetEvent stopWork;
		private readonly AutoResetEvent newFileEvent;
		private readonly PdfCreator pdfCreator;

		public FileService(string inDir, string outDir, string outWrongFileNamingDir, string invalidFileSequenceDir)
		{
			this.inDir = inDir;
			this.outDir = outDir;
			this.outWrongFileNamingDir = outWrongFileNamingDir;
			this.invalidFileSequenceDir = invalidFileSequenceDir;

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

			this.watcher = new FileSystemWatcher(this.inDir);
			this.watcher.Created += this.Watcher_Created;
			this.workThread = new Thread(this.WorkProcedure);
			this.stopWork = new ManualResetEvent(false);
			this.newFileEvent = new AutoResetEvent(false);
			this.pdfCreator = new PdfCreator();

			this.pdfCreator.CallbackWhenReadyToSave += this.SavePdfDocument;
			this.pdfCreator.CallbackWhenSecuenceHasWrongFileExtention += this.MoveAllFileSequenceToOtherDir;
		}

		public void Start()
		{
			this.workThread.Start();
			this.watcher.EnableRaisingEvents = true;
		}

		public void Stop()
		{
			this.watcher.EnableRaisingEvents = false;
			this.stopWork.Set();
			this.workThread.Join();
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
					Thread.Sleep(2000);
				}
			}

			return false;
		}

		public void Dispose()
		{
			this.watcher.Dispose();
			this.stopWork.Dispose();
			this.newFileEvent.Dispose();
		}

		private void MoveAllFileSequenceToOtherDir(object sender, EventArgs e)
		{
			foreach (var fullFilePath in this.pdfCreator.GetAllImageFilePath)
			{
				if (this.TryOpen(fullFilePath, 5))
				{
					File.Move(fullFilePath, Path.Combine(this.invalidFileSequenceDir, Path.GetFileName(fullFilePath)));
				}
			}

			if (this.TryOpen(this.pdfCreator.CurrentBarcodeFilePath, 5))
			{
				File.Delete(this.pdfCreator.CurrentBarcodeFilePath);
			}

			this.pdfCreator.Reset();
		}

		private void SavePdfDocument(object sender, EventArgs e)
		{
			this.pdfCreator.Save(this.outDir);
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

			this.pdfCreator.Reset();
		}

		private void WorkProcedure(object obj)
		{
			List<string> fileNameValidList;
			List<string> sortedFileList;
			do
			{
				fileNameValidList = this.CatchWrongFiles(this.inDir, this.outWrongFileNamingDir);

				sortedFileList = fileNameValidList.OrderBy(s => int.Parse(Path.GetFileNameWithoutExtension(s).Split('_')[1])).ToList();

				foreach (var file in sortedFileList)
				{
					if (this.stopWork.WaitOne(TimeSpan.Zero))
					{
						return;
					}

					var outFile = Path.Combine(this.outDir, Path.GetFileName(file));

					if (this.TryOpen(file, 5))
					{
						this.pdfCreator.PushFile(file);
					}
				}

			} while (WaitHandle.WaitAny(new WaitHandle[] { this.stopWork, this.newFileEvent }, 5000) != 0);
		}

		private List<string> CatchWrongFiles(string targetDir, string outDir)
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
	}
}
