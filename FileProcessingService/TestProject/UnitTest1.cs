using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigraDoc.DocumentObjectModel;
using MigraDoc.Rendering;
using ZXing;

namespace TestProject
{
	[TestClass]
	public class UnitTest1
	{
		/// <summary>
		/// 
		/// </summary>
		[TestMethod]
		public void TestMethod1()
		{
			var document = new Document();
			var section = document.AddSection();
			var fileName = @"D:\WORK\Mentoring2018\4. Windows Services (Module 4)\Tasks\FileProcessingService\FileProcessingService\Image\image_0_bar.png";
			var image = section.AddImage(fileName);
			image.Height = document.DefaultPageSetup.PageHeight;
			image.Width = document.DefaultPageSetup.PageWidth;
			section.AddPageBreak();

			var render = new PdfDocumentRenderer();

			render.Document = document;
			render.RenderDocument();

			render.Save(@"D:\WORK\Mentoring2018\4. Windows Services (Module 4)\Tasks\FileProcessingService\FileProcessingService\Image\image_0_bar.pdf");

			Console.WriteLine();
		}

		/// <summary>
		/// 
		/// </summary>
		[TestMethod]
		public void ZXingTest()
		{
			var reader = new BarcodeReader() { AutoRotate = true };
			foreach (var file in Directory.GetFiles(@"D:\WORK\Mentoring2018\4. Windows Services (Module 4)\Tasks\FileProcessingService\FileProcessingService\Image\"))
			{
				var bmp = (Bitmap)Bitmap.FromFile(file);
				var res = reader.Decode(bmp);
				Console.WriteLine();
			}
		}

		/// <summary>
		/// valid naming <prefix>_<number>.<png>|<jpeg>
		/// </summary>
		[TestMethod]
		public void ValidFileTemplateTest()
		{
			string[] validNames = new string[] { "21d_1.png", "wef213_12.jpeg" };

			Regex regex = new Regex(@".*_\d+\.(png|jpeg)");

			int counter = 0;

			foreach (var name in validNames)
			{
				if (regex.Match(name).Success)
				{
					counter++;
				}
			}

			Assert.AreEqual(validNames.Length, counter);
		}

		/// <summary>
		/// 
		/// </summary>
		[TestMethod]
		public void InvalidFileTemplateTest()
		{
			string[] invalidNames = new string[] { "21d_1A.png", "wef213_12.txt" };

			Regex regex = new Regex(@".*_\d+\.(png|jpeg)");

			int counter = 0;

			foreach (var name in invalidNames)
			{
				if (regex.Match(name).Success)
				{
					counter++;
				}
			}

			Assert.AreEqual(0, counter);
		}
	}
}
