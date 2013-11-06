using System;
using System.Collections.Generic;

namespace Solver
{
	public class Driver
	{
		public string name { get; set; }
		public Location location { get; set; }
		public int maxStops { get; set; }
		public int maxWorkTime { get; set; }
		public TimeWindow timeWindow { get; set; }
		public List<string> tags { get; set; }
		public List<Pref> prefs { get; set; }

	}
}

