using System;
using System.Collections;
using System.Collections.Generic;

namespace Solver
{
	public class RouteLegsResponse
	{
		public RouteLegsResponse()
		{
			this.RouteLegs = new List<RouteLeg>();
		}

		public List<RouteLeg> RouteLegs { get; set; }
	}
}

