using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedModel.Model
{
	[Serializable]
	public class Settings
	{
		/// <summary>
		/// In second
		/// </summary>
		public int Timeout { get; set; }

		/// <summary>
		/// It is the separator of img flow
		/// </summary>
		public string BarcodeValue { get; set; }
	}
}
