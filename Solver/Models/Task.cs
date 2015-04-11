using System;
using System.Collections.Generic;

namespace Solver
{
	public class Task
	{
		public long id { get; set; }
		public string name { get; set; }
		public Stop stop { get; set; }
		public Service service { get; set; }
	}
}

