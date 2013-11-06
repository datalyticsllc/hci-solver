using System;
using ServiceStack.ServiceHost;

namespace Solver
{
	public class GetRouteOrders: IReturn<RouteOrdersResponse>
	{
		public string Orders { get; set; }
	}
}

