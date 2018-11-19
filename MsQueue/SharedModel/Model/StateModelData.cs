﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedModel.Model
{
	[Serializable]
	public class StateModelData
	{
		public Status Status { get; set; }
		public Settings CurrentSettings { get; set; }
	}
}
