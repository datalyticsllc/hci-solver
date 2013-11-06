using System;

namespace Solver
{
	public class RouteStop
	{
		public int ID { get; set; }
		//public string ExternalID { get; set; }
		//public string Name { get; set; }

		// geo-location
		public double? GeoLat { get; set; }
		public double? GeoLon { get; set; }

		// address
		public string Address { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string Zip { get; set; }
	}
}

