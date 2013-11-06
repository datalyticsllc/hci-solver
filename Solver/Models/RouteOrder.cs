using System;

namespace Solver
{
	public class RouteOrder
	{
		public int ID { get; set; }  // ex: Visit ID
		public int StopID { get; set; }  // ex: Patient ID

		// geo-location
		public double? GeoLat { get; set; }
		public double? GeoLon { get; set; }

		// address
		public string Address { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string Zip { get; set; }

		// order duration
		public decimal ServiceTime { get; set; }

		// optimization tags
		public string Tags { get; set; }
	}
}

