using System;
using System.Collections;
using System.Collections.Generic;

namespace Solver
{
	public class RouteDriversResponse
	{
		public RouteDriversResponse()
		{
			this.Drivers = new List<RouteDriver>();
		}

		public List<RouteDriver> Drivers { get; set; }
	}
}

