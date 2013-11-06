using System;
using System.Collections.Generic;

namespace Solver
{
	public class Service
	{
		public string name { get; set; }
		public string driver { get; set; }
		public int duration { get; set; }
		public List<Pref> prefs { get; set; }
	}
}

