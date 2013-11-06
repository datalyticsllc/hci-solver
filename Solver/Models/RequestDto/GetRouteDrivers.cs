using System;
using ServiceStack.ServiceHost;

namespace Solver
{
	public class GetRouteDrivers: IReturn<RouteDriversResponse>
	{
		public string Drivers { get; set; }
		public DateTime ServiceDate { get; set; }
	}
}

