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
	public class FileService
	{
		private FileSystemWatcher _watcher;
		private string _inDir;
		private string _outDir;
		private string _outWrongFileNamingDir;
		private string _invalidFileSequenceDir;
		private Thread _workThread;
		private ManualResetEvent _stopWork;
		private AutoResetEvent _newFileEvent;
		private PdfCreatorr _pdfCreator;


		public FileService(string inDir, string outDir, string outWrongFileNamingDir, string invalidFileSequenceDir)
		{
			this._inDir = inDir;
			this._outDir = outDir;
			this._outWrongFileNamingDir = outWrongFileNamingDir;
			this._invalidFileSequenceDir = invalidFileSequenceDir;

			if (!Directory.Exists(this._inDir))
			{
				Directory.CreateDirectory(this._inDir);
			}

			if (!Directory.Exists(this._outDir))
			{
				Directory.CreateDirectory(this._outDir);
			}

			if (!Directory.Exists(this._outWrongFileNamingDir))
			{
				Directory.CreateDirectory(this._outWrongFileNamingDir);
			}

			if (!Directory.Exists(this._invalidFileSequenceDir))
			{
				Directory.CreateDirectory(this._invalidFileSequenceDir);
			}

			_watcher = new FileSystemWatcher(this._inDir);
			_watcher.Created += Watcher_Created;
			_workThread = new Thread(WorkProcedure);
			_stopWork = new ManualResetEvent(false);
			_newFileEvent = new AutoResetEvent(false);
			_pdfCreator = new PdfCreatorr();

			_pdfCreator.CallbackWhenReadyToSave += SavePdfDocument;
			_pdfCreator.CallbackWhenSecuenceHasWrongFileExtention += MoveAllFileSequenceToOtherDir;
		}

		private void MoveAllFileSequenceToOtherDir(object sender, EventArgs e)
		{
			foreach (var fullFilePath in _pdfCreator.GetAllImageFilePath)
			{
				if (TryOpen(fullFilePath, 5))
				{
					File.Move(fullFilePath, Path.Combine(this._invalidFileSequenceDir, Path.GetFileName(fullFilePath)));
				}
			}

			if (TryOpen(_pdfCreator.CurrentBarcodeFilePath, 5))
			{
				File.Delete(_pdfCreator.CurrentBarcodeFilePath);
			}
			_pdfCreator.Reset();
		}

		private void SavePdfDocument(object sender, EventArgs e)
		{
			_pdfCreator.Save(_outDir);
			foreach (var fullFilePath in _pdfCreator.GetAllImageFilePath)
			{
				if (TryOpen(fullFilePath, 5))
				{
					File.Delete(fullFilePath);
				}
			}

			if (TryOpen(_pdfCreator.CurrentBarcodeFilePath, 5))
			{
				File.Delete(_pdfCreator.CurrentBarcodeFilePath);
			}

			_pdfCreator.Reset();

		}

		private void WorkProcedure(object obj)
		{

			List<string> fileNameValidList;
			List<string> sortedFileList;
			do
			{
				fileNameValidList = CatchWrongFiles(_inDir, _outWrongFileNamingDir);

				sortedFileList = fileNameValidList.OrderBy(s => int.Parse(Path.GetFileNameWithoutExtension(s).Split('_')[1])).ToList();

				foreach (var file in sortedFileList)
				{
					if (_stopWork.WaitOne(TimeSpan.Zero))
					{
						return;
					}

					var outFile = Path.Combine(_outDir, Path.GetFileName(file));

					if (TryOpen(file, 5))
					{
						_pdfCreator.PushFile(file);
					}
				}

			} while (WaitHandle.WaitAny(new WaitHandle[] { _stopWork, _newFileEvent }, 5000) != 0);

		}

		private List<string> CatchWrongFiles(string targetDir, string outDir)
		{
			Regex regex = new Regex(@".*_\d+\.");

			List<string> result = new List<string>();

			foreach (var file in Directory.EnumerateFiles(targetDir))
			{
				if (!regex.Match(Path.GetFileName(file)).Success)
				{
					MoveToWrongFileDirectory(outDir, file);
				}
				else if (Path.GetFileName(file).Split('_').Length > 2)
				{
					MoveToWrongFileDirectory(outDir, file);
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
			if (TryOpen(file, 10))
			{
				File.Move(file, Path.Combine(outWrongDir, Path.GetFileName(file)));
			}
		}

		private void Watcher_Created(object sender, FileSystemEventArgs e)
		{
			_newFileEvent.Set();
		}

		public void Start()
		{
			_workThread.Start();
			_watcher.EnableRaisingEvents = true;
		}

		public void Stop()
		{
			_watcher.EnableRaisingEvents = false;
			_stopWork.Set();
			_workThread.Join();

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
	}
}
