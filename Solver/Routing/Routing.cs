using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ServiceStack.ServiceClient.Web;
using System.Data;
using Npgsql;

namespace Solver
{
	public class Routing
	{
		private const string BING_API_KEY = "AnM8e3-5TdMQZ_UsIU1GNYfhKPvoeK2v4w0oPeR49rwmj3K9xCFqAfpi8NUfiMnI";

		// Signals the routing engine what trip "cost" factor to optimize on.  Examples are: distance, time, environmental, maintenance, etc.
		public enum CostType
		{
			Distance,
			Time // not yet used
		}

		// Get distance for every possible combination of driver/stop
		public ICollection<RouteLeg> GetRouteLegs(ICollection<Entity> Entities, List<RouteLeg> RouteLegs, int CustomerID, CostType CostType)
		{
			BingServices bing = new BingServices(BING_API_KEY);

			// find any gaps in calculations, marked with -1 distance calc
			if (RouteLegs.Where (x => x.DrivingDistance == -1).Count () > 0) {
				// fill in the gaps
				foreach (RouteLeg calc in RouteLegs.Where(x => x.DrivingDistance == -1)) {

					// check if the from and to resources are the same resource
					// also, distance = 0 if they are the same ID, -1 if missing the record
					// don't need to calculate the distances between the drivers
					if (!(calc.FromID == calc.ToID) && !(calc.FromTypeID == 2) && !(calc.ToTypeID == 2)) {

						Entity fromEntity = Entities.Where (x => x.id == calc.FromID).FirstOrDefault ();
						Entity toEntity = Entities.Where (x => x.id == calc.ToID).FirstOrDefault ();

						// get distance and time
						double[] bingResult = bing.GetDrivingDistanceAndTimeBetween2Points (
							Convert.ToDouble (fromEntity.geoLat),
							Convert.ToDouble (fromEntity.geoLon),
							Convert.ToDouble (toEntity.geoLat),
							Convert.ToDouble (toEntity.geoLon));

						InsertRouteLeg (calc.FromID, calc.ToID, Convert.ToDecimal (bingResult [0]), Convert.ToDecimal (bingResult [1]));

						// update result list
						calc.DrivingDistance = Convert.ToDecimal (bingResult [0]);
						calc.DrivingTime = Convert.ToDecimal (bingResult [1]);

					}
				}

			}

			List<RouteLeg> legs = new List<RouteLeg>();

			foreach (RouteLeg c in RouteLegs)
			{
				legs.Add(new RouteLeg{
					FromID = c.FromID,
					FromTypeID = c.FromTypeID,
					ToID = c.ToID,
					ToTypeID = c.ToTypeID,
					DrivingDistance = c.DrivingDistance,
					DrivingTime = c.DrivingTime
				});
			}

			return legs;
		}

		public void InsertRouteLeg(Int64 FromId, Int64 ToId, Decimal Distance, Decimal Duration)
		{

			NpgsqlConnection conn = new NpgsqlConnection("Server=hci.cvwcpfnur8ep.us-east-1.rds.amazonaws.com;Port=5432;User Id=hci_user;Password=Nov299pe;Database=route;");
			conn.Open();

			try
			{
				NpgsqlCommand command = new NpgsqlCommand(
					"INSERT INTO entity_leg(from_id, to_id, distance, duration) " + 
					"VALUES(" + FromId + ", " + ToId + ", " + Distance + ", " + Duration + ")", conn); 

				command.CommandType = CommandType.Text;

				command.ExecuteScalar();
			}
			finally
			{
				conn.Close ();
			}

		}

		public ICollection<DriverTaskMultiplier> GetRoutePenalties(ICollection<Driver> Drivers, ICollection<Task> Tasks)
		{
			List<DriverTaskMultiplier> result = new List<DriverTaskMultiplier>();

			// Set route order multipliers (drivers and stops)
			foreach (Task task in Tasks)
			{
				// Create multiplier record for every driver for this order
				foreach(Driver driver in Drivers)
				{
					result.Add(new DriverTaskMultiplier(){
						TaskName = task.name,
						DriverName = driver.name,
						AdjustmentMultiplier = CalculateDriverTaskMultiplier(driver, task)
					});
				}
			}



			return result;
		}

		private double CalculateDriverMultiplier(Driver Driver, IEnumerable<OptimizationTag> Multipliers)
		{
			double result = 1.0;

			// TODO: This is for "global" preferences applied to all visits for the driver, ie: paytype, iniation costs, etc

//			string[] driverTags = Driver.Tags.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
//			foreach (string tag in driverTags)
//			{
//				OptimizationTag optimizationTag = (from m in Multipliers where m.Name == tag && !m.IsExclusive select m).FirstOrDefault();
//				if (optimizationTag != null)
//				{
//					result += (optimizationTag.Multiplier - 1.0f); // multiplier is additive, not compounding
//				}
//			}

			return result;
		}

		private double CalculateDriverTaskMultiplier(Driver Driver, Task Task)
		{
			double result = 1.0;

			// check the tag preferences
			foreach (Pref pref in Task.stop.prefs)
			{

				if (pref.exclude) 
				{
					// don't want this tag

					if(Driver.tags.Contains(pref.tag))
					{
						if (pref.required) {
							// oops, this is a bad one, basically put it out of range
							return 1000;  // we're done, just send back 1000
						} else {
							result += pref.penalty / 100.0 * pref.multiplier; // convert to % of multiplier
						}

					}

				} else 
				{
					// want this tag
					if(!Driver.tags.Contains(pref.tag))
					{
						if (pref.required) {
							// oops, this is a bad one
							return 1000;  // we're done, just send back 1000
						} else {
							result += pref.penalty / 100.0 * pref.multiplier;  // convert to % of multiplier
						}
					}

				}

			}

			// now check to make sure the driver can perform the service
			if (!Driver.services.Contains (Task.service.name) && Task.service.name != "Ignore") {
				// can't perform the service, so penalize
				return 1000;  // we're done, just send back 1000
			}

//			if (Task.stop.name == "124") {
//
//				Console.WriteLine (Driver.name + ": " + result);
//			}

			return result;  // only soft constraint penalties were found
		}

	}
}
