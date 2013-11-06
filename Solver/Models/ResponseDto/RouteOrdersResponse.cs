using System;
using System.Collections;
using System.Collections.Generic;

namespace Solver
{
	public class RouteOrdersResponse
	{
		public RouteOrdersResponse()
		{
			this.Orders = new List<RouteOrder>();
		}

		public List<RouteOrder> Orders { get; set; }
	}
}

