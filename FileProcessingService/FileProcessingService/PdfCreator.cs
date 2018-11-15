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
	public class PdfCreator
	{
		private Document document;
		private PdfDocumentRenderer renderer;
		private IList<string> filePathCollection = new List<string>();

		public PdfCreator()
		{
			this.document = new Document();
			this.renderer = new PdfDocumentRenderer();
		}

		public event EventHandler CallbackWhenReadyToSave;

		public event EventHandler CallbackWhenSecuenceHasWrongFileExtention;

		public string CurrentBarcodeFilePath { get; set; }

		/// <summary>
		/// Gets true if sequence has file with wrong extention
		/// </summary>
		public bool HasWrongFileExtention { get; set; }

		public IList<string> GetAllImageFilePath
		{
			get
			{
				return this.filePathCollection;
			}
		}

		public bool IsPdfReady { get; set; }

		public void CreatePdfDocument(IList<string> getFileNames)
		{
			this.document = new Document();

			foreach (var chunk in this.filePathCollection)
			{
				var section = this.document.AddSection();
				var image = section.AddImage(chunk);

				image.Height = this.document.DefaultPageSetup.PageHeight;
				image.Width = this.document.DefaultPageSetup.PageWidth;
			}
		}

		public void PushFile(string fullFileName)
		{
			if (this.filePathCollection.Any(p => p == fullFileName))
			{
				return;
			}

			if (!this.HasBarcode(fullFileName))
			{
				Regex rx = new Regex(@"\.(png|jpg)");
				var extention = Path.GetExtension(fullFileName);

				if (!rx.Match(extention).Success)
				{
					this.HasWrongFileExtention = true;
				}

				this.filePathCollection.Add(fullFileName);
			}
			else
			{
				this.CurrentBarcodeFilePath = fullFileName;

				if (this.HasWrongFileExtention)
				{
					this.CallbackWhenSecuenceHasWrongFileExtention?.Invoke(this, new EventArgs());
				}
				else
				{
					this.CreatePdfDocument(this.filePathCollection);
					this.CallbackWhenReadyToSave?.Invoke(this, new EventArgs());
				}
			}
		}

		public void Save(string dir)
		{
			var randomName = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());
			this.renderer.Document = this.document;

			if (this.document.Sections.Count > 0)
			{
				this.renderer.RenderDocument();
				this.renderer.Save(dir + "\\" + randomName + ".pdf");
			}
		}

		public void Reset()
		{
			this.filePathCollection = new List<string>();
			this.IsPdfReady = false;
			this.document = new Document();
			this.renderer = new PdfDocumentRenderer();
			this.CurrentBarcodeFilePath = string.Empty;
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
	}
}
