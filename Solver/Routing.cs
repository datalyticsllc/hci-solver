using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
		/*
		// This method will attempt to geocode and drivers or stops with missing coordinates and will filter out those that were not able to be geocoded.
		public void GeocodeRouteEntities(ref ICollection<RouteStop> Stops, ref ICollection<RouteDriver> Drivers, ref ICollection<RouteOrder> Orders, int CustomerID)
		{
			BingServices bing = new BingServices(BING_API_KEY);

			// get the connection string for the customer, so we can query the RouteLeg table with their drivers and stops
			CentralUnitOfWork centralUnitOfWork = new CentralUnitOfWork("data source=coordcare.com;Initial Catalog=CoordCareCentral;User Id=CoordCareWeb;Password=n8481m;Persist Security Info=True");

			using (RoutingUnitOfWork unitOfWork = new RoutingUnitOfWork(centralUnitOfWork.ScheduleCustomerRepository.GetByID(CustomerID).ConnectionString))
			{
				// Get drivers and geolocate any missing           
				List<RouteDriver> drivers = unitOfWork.GetRouteDrivers(string.Join(",", from u in Drivers select u.ID.ToString())).ToList();
				foreach (RouteDriver driver in drivers)
				{
					if (!driver.GeoLat.HasValue || !driver.GeoLon.HasValue)
					{
						// geo-locate this driver
						double lat;
						double lon;

						RouteDriver updater = unitOfWork.RouteDriverRepository.GetByID(driver.ID);
						if (bing.GeocodeAddress(driver.Address, driver.City, driver.State, driver.Zip, out lat, out lon, BingServices.GeocodeConfidence.Medium) != BingServices.GeocodeConfidence.Zero)
						{
							// update the ref driver, we we'll have the geocode for the GetRouteLegs step
							var dToUpdate = Drivers.Where(x => x.ID == driver.ID);
							foreach(var d in dToUpdate)
							{
								d.GeoLat = lat;
								d.GeoLon = lon;
							}


							updater.GeoLat = lat;
							updater.GeoLon = lon;
							unitOfWork.Save();
						}
						else
						{
							// unable to geocode, remove this driver from the list
							Drivers.Remove((from d in Drivers where d.ID == driver.ID select d).FirstOrDefault());
						}
					}
				}

				// Get patients and geolocate any missing           
				List<RouteStop> stops = unitOfWork.GetRouteStops(string.Join(",", from u in Stops select u.ID.ToString())).ToList();
				foreach (RouteStop stop in stops)
				{
					if (!stop.GeoLat.HasValue || !stop.GeoLon.HasValue)
					{
						// geo-locate this stop
						double lat;
						double lon;

						RouteStop updater = unitOfWork.RouteStopRepository.GetByID(stop.ID);
						if (bing.GeocodeAddress(stop.Address, stop.City, stop.State, stop.Zip, out lat, out lon, BingServices.GeocodeConfidence.Medium) != BingServices.GeocodeConfidence.Zero)
						{
							// update the ref stop, we we'll have the geocode for the GetRouteLegs step
							var sToUpdate = Stops.Where(x => x.ID == stop.ID);
							foreach (var s in sToUpdate)
							{
								s.GeoLat = lat;
								s.GeoLon = lon;
							}

							updater.GeoLat = lat;
							updater.GeoLon = lon;
							unitOfWork.Save();
						}
						else
						{
							// unable to geocode, remove this stop from the list
							Stops.Remove((from s in Stops where s.ID == stop.ID select s).FirstOrDefault());

							// need to remove the order associated with the stop that was removed
							Orders.Remove((from o in Orders where o.StopID == stop.ID select o).FirstOrDefault());
						}
					}
				}
			}
		}

		// Get distance for every possible combination of driver/stop
		public ICollection<RouteLeg> GetRouteLegs(ICollection<RouteStop> Stops, ICollection<RouteDriver> Drivers, int CustomerID, CostType CostType)
		{
			BingServices bing = new BingServices(BING_API_KEY);

			// get the connection string for the customer, so we can query the RouteLeg table with their drivers and stops
			CentralUnitOfWork centralUnitOfWork = new CentralUnitOfWork("data source=coordcare.com;Initial Catalog=CoordCareCentral;User Id=CoordCareWeb;Password=n8481m;Persist Security Info=True");

			using (RoutingUnitOfWork unitOfWork = new RoutingUnitOfWork(centralUnitOfWork.ScheduleCustomerRepository.GetByID(CustomerID).ConnectionString))
			{
				// get existing driving distances and times from database
				ICollection<RouteLeg> result = unitOfWork.CalculateDrivingMatrix(
					string.Join(",", from p in Stops select p.ID.ToString()),
					string.Join(",", from u in Drivers select u.ID.ToString()));

				//var test = result.Where(x => x.FromID == 27 && x.FromTypeID == 27).ToList();


				// find any gaps in calculations, marked with -1 distance calc
				if (result.Where(x => x.DrivingDistance == -1).Count() > 0)
				{
					// fill in the gaps
					foreach (RouteLeg calc in result.Where(x => x.DrivingDistance == -1))
					{
						// check if the from and to resources are the same resource
						if (!(calc.FromID == calc.ToID && calc.FromTypeID == calc.ToTypeID))
						{
							// get the patient or user involved
							if (calc.FromTypeID == 1 && calc.ToTypeID == 1)
							{
								RouteStop fromPatient = Stops.Where(x => x.ID == calc.FromID).FirstOrDefault();
								RouteStop toPatient = Stops.Where(x => x.ID == calc.ToID).FirstOrDefault();

								// Ensure that entities involved are routable
								EnsureEntityIsRoutable(fromPatient);
								EnsureEntityIsRoutable(toPatient);

								// get distance and time
								double[] bingResult = bing.GetDrivingDistanceAndTimeBetween2Points(
									Convert.ToDouble(fromPatient.GeoLat),
									Convert.ToDouble(fromPatient.GeoLon),
									Convert.ToDouble(toPatient.GeoLat),
									Convert.ToDouble(toPatient.GeoLon));

								// store result
								unitOfWork.RouteLegRepository.Insert(new RouteLeg()
								                                     {
									DrivingDistance = Convert.ToDecimal(bingResult[0]),
									DrivingTime = Convert.ToDecimal(bingResult[1]),
									FromID = calc.FromID,
									FromTypeID = 1, // patient
									ToID = calc.ToID,
									ToTypeID = 1 // patient
										// LastModifiedDate = DateTime.Now
								});
								unitOfWork.Save();

								// update result list
								calc.DrivingDistance = Convert.ToDecimal(bingResult[0]);
								calc.DrivingTime = Convert.ToDecimal(bingResult[1]);
							}
							else if (calc.FromTypeID == 1 && calc.ToTypeID == 2)
							{
								RouteStop fromPatient = Stops.Where(x => x.ID == calc.FromID).FirstOrDefault();
								RouteDriver toUserAccount = Drivers.Where(x => x.ID == calc.ToID).FirstOrDefault();

								// Ensure that entities involved are routable
								EnsureEntityIsRoutable(fromPatient);
								EnsureEntityIsRoutable(toUserAccount);

								// get distance and time
								double[] bingResult = bing.GetDrivingDistanceAndTimeBetween2Points(
									Convert.ToDouble(fromPatient.GeoLat),
									Convert.ToDouble(fromPatient.GeoLon),
									Convert.ToDouble(toUserAccount.GeoLat),
									Convert.ToDouble(toUserAccount.GeoLon));

								// store result
								unitOfWork.RouteLegRepository.Insert(new RouteLeg()
								                                     {
									DrivingDistance = Convert.ToDecimal(bingResult[0]),
									DrivingTime = Convert.ToDecimal(bingResult[1]),
									FromID = calc.FromID,
									FromTypeID = 1, // patient
									ToID = calc.ToID,
									ToTypeID = 2 // user
										//LastModifiedDate = DateTime.Now
								});
								unitOfWork.Save();

								// update result list
								calc.DrivingDistance = Convert.ToDecimal(bingResult[0]);
								calc.DrivingTime = Convert.ToDecimal(bingResult[1]);
							}
							else if (calc.FromTypeID == 2 && calc.ToTypeID == 2)
							{
								RouteDriver fromUserAccount = Drivers.Where(x => x.ID == calc.FromID).FirstOrDefault();
								RouteDriver toUserAccount = Drivers.Where(x => x.ID == calc.ToID).FirstOrDefault();

								// Ensure that entities involved are routable
								EnsureEntityIsRoutable(fromUserAccount);
								EnsureEntityIsRoutable(toUserAccount);

								// get distance and time
								double[] bingResult = bing.GetDrivingDistanceAndTimeBetween2Points(
									Convert.ToDouble(fromUserAccount.GeoLat),
									Convert.ToDouble(fromUserAccount.GeoLon),
									Convert.ToDouble(toUserAccount.GeoLat),
									Convert.ToDouble(toUserAccount.GeoLon));

								// store result
								unitOfWork.RouteLegRepository.Insert(new RouteLeg()
								                                     {
									DrivingDistance = Convert.ToDecimal(bingResult[0]),
									DrivingTime = Convert.ToDecimal(bingResult[1]),
									FromID = calc.FromID,
									FromTypeID = 2, // user
									ToID = calc.ToID,
									ToTypeID = 2 // user
										//LastModifiedDate = DateTime.Now
								});
								unitOfWork.Save();

								// update result list
								calc.DrivingDistance = Convert.ToDecimal(bingResult[0]);
								calc.DrivingTime = Convert.ToDecimal(bingResult[1]);
							}
							else// if((calc.FromResourceID.Contains("u") && calc.ToResourceID.Contains("p"))
							{
								RouteDriver fromUserAccount = Drivers.Where(x => x.ID == calc.FromID).FirstOrDefault();
								RouteStop toPatient = Stops.Where(x => x.ID == calc.ToID).FirstOrDefault();

								// Ensure that entities involved are routable
								EnsureEntityIsRoutable(fromUserAccount);
								EnsureEntityIsRoutable(toPatient);

								// get distance and time
								double[] bingResult = bing.GetDrivingDistanceAndTimeBetween2Points(
									Convert.ToDouble(fromUserAccount.GeoLat),
									Convert.ToDouble(fromUserAccount.GeoLon),
									Convert.ToDouble(toPatient.GeoLat),
									Convert.ToDouble(toPatient.GeoLon));

								// store result
								unitOfWork.RouteLegRepository.Insert(new RouteLeg()
								                                     {
									DrivingDistance = Convert.ToDecimal(bingResult[0]),
									DrivingTime = Convert.ToDecimal(bingResult[1]),
									FromID = calc.FromID,
									FromTypeID = 2, // user
									ToID = calc.ToID,
									ToTypeID = 1 // patient
										//LastModifiedDate = DateTime.Now
								});
								unitOfWork.Save();

								// update result list
								calc.DrivingDistance = Convert.ToDecimal(bingResult[0]);
								calc.DrivingTime = Convert.ToDecimal(bingResult[1]);
							}
						}
					}
				}

				List<RouteLeg> legs = new List<RouteLeg>();

				foreach (RouteLeg c in result)
				{
					legs.Add(new RouteLeg
					         {
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
		}

*/
		public ICollection<RouteOrderMultiplier> GetRouteMultipliers(ICollection<RouteOrder> Orders, ICollection<RouteDriver> Drivers, IEnumerable<OptimizationTag> Multipliers)
		{
			List<RouteOrderMultiplier> result = new List<RouteOrderMultiplier>();

			// Set all driver multipliers (globally applied to any of a driver's possible visits)
			Dictionary<int, float> driverMultipliers = new Dictionary<int, float>();
			foreach (RouteDriver driver in Drivers)
			{
				// Add multiplier to lookup
				driverMultipliers.Add(driver.ID, CalculateDriverMultiplier(driver, Multipliers));
			}

			// Set route order multipliers (drivers and stops)
			foreach (RouteOrder order in Orders)
			{
				// Create multiplier record for every driver for this order
				foreach(RouteDriver driver in Drivers)
				{
					result.Add(new RouteOrderMultiplier()
					           {
						RouteOrderID = order.ID,
						RouteDriverID = driver.ID,
						AdjustmentMultiplier = CalculateDriverOrderMultiplier(driver, order, Multipliers) 
							+ (driverMultipliers[driver.ID] - 1.0f)
					});
				}
			}

			return result;
		}

		private float CalculateDriverMultiplier(RouteDriver Driver, IEnumerable<OptimizationTag> Multipliers)
		{
			float result = 1.0f;

			string[] driverTags = Driver.Tags.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string tag in driverTags)
			{
				OptimizationTag optimizationTag = (from m in Multipliers where m.Name == tag && !m.IsExclusive select m).FirstOrDefault();
				if (optimizationTag != null)
				{
					result += (optimizationTag.Multiplier - 1.0f); // multiplier is additive, not compounding
				}
			}

			return result;
		}

		private float CalculateDriverOrderMultiplier(RouteDriver Driver, RouteOrder Order, IEnumerable<OptimizationTag> Multipliers)
		{
			float result = 1.0f;

			string[] orderTags = Order.Tags.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries);
			foreach (string tag in orderTags)
			{
				OptimizationTag optimizationTag = (from m in Multipliers where 
				                                   (m.Name == tag) 
				                                   || (m.Name.Contains("*") && tag.StartsWith(m.Name.Substring(0, m.Name.IndexOf("*")))) // handle wildcards
				                                   && m.IsExclusive 
				                                   select m)
					.FirstOrDefault();

				if (optimizationTag != null && !Driver.Tags.Contains(tag))
				{
					// If optimization tag is not found for the driver, apply multiplier
					result += (optimizationTag.Multiplier - 1.0f); // multiplier is additive, not compounding

				}


			}

			return result;
		}

		private static void EnsureEntityIsRoutable(RouteDriver entity)
		{
			if(!entity.GeoLat.HasValue || !entity.GeoLon.HasValue)
				throw new RoutingEntityException("Driver missing geocoding coordinates.", entity.ID);
		}

		private static void EnsureEntityIsRoutable(RouteStop entity)
		{
			if (!entity.GeoLat.HasValue || !entity.GeoLon.HasValue)
				throw new RoutingEntityException("Stop missing geocoding coordinates.", entity.ID);
		}
	}
}
