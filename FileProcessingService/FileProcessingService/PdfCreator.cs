using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using ZXing;

namespace FileProcessingService
{
	class PdfCreatorr
	{

		private Document _document;
		private PdfDocumentRenderer _renderer;
		private IList<string> _filePathCollection = new List<string>();
		public string CurrentBarcodeFilePath { get; private set; }

		public event EventHandler CallbackWhenReadyToSave;

		public event EventHandler CallbackWhenSecuenceHasWrongFileExtention;

		/// <summary>
		/// True if sequence has file with wrong extention
		/// </summary>
		public bool HasWrongFileExtention { get; set; }

		public IList<string> GetAllImageFilePath
		{
			get
			{
				return _filePathCollection;
			}
		}

		public bool IsPdfReady { get; set; }

		public PdfCreatorr()
		{
			_document = new Document();
			_renderer = new PdfDocumentRenderer();
		}

		public void PushFile(string fullFileName)
		{
			if (_filePathCollection.Any(p => p == fullFileName))
			{
				return;
			}

			if (!HasBarcode(fullFileName))
			{
				Regex rx = new Regex(@"\.(png|jpg)");
				var extention = Path.GetExtension(fullFileName);

				if (!rx.Match(extention).Success)
				{
					HasWrongFileExtention = true;
				}

				_filePathCollection.Add(fullFileName);
			}
			else
			{
				CurrentBarcodeFilePath = fullFileName;

				if (this.HasWrongFileExtention)
				{
					this.CallbackWhenSecuenceHasWrongFileExtention?.Invoke(this, new EventArgs());

				}
				else
				{
					CreatePdfDocument(_filePathCollection);
					this.CallbackWhenReadyToSave?.Invoke(this, new EventArgs());
				}
			}
		}

		public void CreatePdfDocument(IList<string> getFileNames)
		{
			_document = new Document();

			foreach (var chunk in _filePathCollection)
			{
				var section = _document.AddSection();
				var image = section.AddImage(chunk);

				image.Height = _document.DefaultPageSetup.PageHeight;
				image.Width = _document.DefaultPageSetup.PageWidth;
			}
		}

		private bool HasBarcode(string fullFileName)
		{
			var reader = new BarcodeReader() { AutoRotate = true };
			Result res = null;

			using (var bmp = (Bitmap)Bitmap.FromFile(fullFileName))
			{
				res = reader.Decode(bmp);
			}

			return res != null ? true : false;
		}

		public void Save(string dir)
		{
			var randomName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
			_renderer.Document = _document;

			if (_document.Sections.Count > 0)
			{
				_renderer.RenderDocument();
				_renderer.Save(dir + "\\" + randomName + ".pdf");
			}
		}

		public void Reset()
		{
			_filePathCollection = new List<string>();
			IsPdfReady = false;
			_document = new Document();
			_renderer = new PdfDocumentRenderer();
			CurrentBarcodeFilePath = string.Empty;
		}
	}
}
