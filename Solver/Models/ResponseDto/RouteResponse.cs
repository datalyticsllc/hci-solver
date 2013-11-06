using System;
using System.Collections;
using System.Collections.Generic;

namespace Solver
{
	public class RouteResponse
	{
		public int customerId { get; set; }
		public int generations { get; set; }
		public List<Pref> prefs { get; set; }
		public List<Driver> drivers { get; set; }
		public List<Task> tasks { get; set; }
	}
}

