using System;
using ServiceStack.ServiceHost;

namespace Solver
{
	public class GetRouteLegs: IReturn<RouteLegsResponse>
	{
		public string StopList { get; set; }
		public string DriverList { get; set; }
	}
}

