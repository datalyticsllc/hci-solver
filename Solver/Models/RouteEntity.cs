using System;
using System.Collections.Generic;

namespace Solver
{
	public class RouteEntity
	{
		public int ID { get; set; }
		public int TypeID { get; set; }

		// geo-location
		public double? GeoLat { get; set; }
		public double? GeoLon { get; set; }

		// address
		public string Address { get; set; }
		public string City { get; set; }
		public string State { get; set; }
		public string Zip { get; set; }

		public static RouteEntity Convert(RouteStop incoming)
		{
			return new RouteEntity()
			{
				ID = incoming.ID,
				TypeID = 1, // Stops are type 1
				GeoLat = incoming.GeoLat,
				GeoLon = incoming.GeoLon,
				Address = incoming.Address,
				City = incoming.City,
				State = incoming.State,
				Zip = incoming.Zip
			};
		}

		public static IEnumerable<RouteEntity> Convert(IEnumerable<RouteStop> incoming)
		{
			List<RouteEntity> result = new List<RouteEntity>();
			foreach (RouteStop stop in incoming)
			{
				result.Add(new RouteEntity()
				           {
					ID = stop.ID,
					TypeID = 1, // Stops are type 1
					GeoLat = stop.GeoLat,
					GeoLon = stop.GeoLon,
					Address = stop.Address,
					City = stop.City,
					State = stop.State,
					Zip = stop.Zip
				});
			}

			return result;
		}

		public static RouteEntity Convert(RouteDriver incoming)
		{
			return new RouteEntity()
			{
				ID = incoming.ID,
				TypeID = 2, // Drivers are type 2
				GeoLat = incoming.GeoLat,
				GeoLon = incoming.GeoLon,
				Address = incoming.Address,
				City = incoming.City,
				State = incoming.State,
				Zip = incoming.Zip
			};
		}

		public static IEnumerable<RouteEntity> Convert(IEnumerable<RouteDriver> incoming)
		{
			List<RouteEntity> result = new List<RouteEntity>();
			foreach (RouteDriver driver in incoming)
			{
				result.Add(new RouteEntity()
				           {
					ID = driver.ID,
					TypeID = 2, // Drivers are type 2
					GeoLat = driver.GeoLat,
					GeoLon = driver.GeoLon,
					Address = driver.Address,
					City = driver.City,
					State = driver.State,
					Zip = driver.Zip
				});
			}

			return result;
		}
	}
}

