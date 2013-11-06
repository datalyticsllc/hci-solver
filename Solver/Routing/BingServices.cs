using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Solver
{
	public class BingServices
	{
		private BingRouteService.Credentials _credentials; // e.g. "AnM8e3-5TdMQZ_UsIU1GNYfhKPvoeK2v4w0oPeR49rwmj3K9xCFqAfpi8NUfiMnI"
		private BingGeocodeService.Credentials _geoCredentials;

		public enum GeocodeConfidence
		{
			Zero, Low, Medium, High
		}

		public BingServices(string BingAppID)
		{
			_credentials = new BingRouteService.Credentials();
			_credentials.ApplicationId = BingAppID;

			_geoCredentials = new BingGeocodeService.Credentials();
			_geoCredentials.ApplicationId = BingAppID;
		}

		public double GetDrivingDistanceBetween2Points(double lat1, double lon1, double lat2, double lon2)
		{
			BingRouteService.RouteService routing = new BingRouteService.RouteService();
			BingRouteService.RouteRequest routeRequest = new BingRouteService.RouteRequest();
			routeRequest.Credentials = _credentials;            
			routeRequest.UserProfile = new BingRouteService.UserProfile { DistanceUnit = BingRouteService.DistanceUnit.Mile };

			BingRouteService.RouteOptions routeOptions = new BingRouteService.RouteOptions();

			routeOptions.Optimization = BingRouteService.RouteOptimization.MinimizeTime;
			routeOptions.TrafficUsage = BingRouteService.TrafficUsage.None;
			routeOptions.Mode = BingRouteService.TravelMode.Driving;

			routeRequest.Options = routeOptions;
			routeRequest.Waypoints = new BingRouteService.Waypoint[2];

			BingRouteService.Waypoint wp1 = new BingRouteService.Waypoint { Location = new BingRouteService.Location { Latitude = lat1, Longitude = lon1 } };
			routeRequest.Waypoints[0] = wp1;

			BingRouteService.Waypoint wp2 = new BingRouteService.Waypoint { Location = new BingRouteService.Location { Latitude = lat2, Longitude = lon2 } };
			routeRequest.Waypoints[1] = wp2;

			BingRouteService.RouteResponse routeResponse = routing.CalculateRoute(routeRequest);

			return routeResponse.Result.Summary.Distance;
		}

		// Gets the distance (in miles) and time (in seconds) and returns them in an array of double
		public double[] GetDrivingDistanceAndTimeBetween2Points(double lat1, double lon1, double lat2, double lon2)
		{
			BingRouteService.RouteService routing = new BingRouteService.RouteService();

			BingRouteService.RouteRequest routeRequest = new BingRouteService.RouteRequest();
			routeRequest.Credentials = _credentials;
			routeRequest.UserProfile = new BingRouteService.UserProfile { DistanceUnit = BingRouteService.DistanceUnit.Mile };

			BingRouteService.RouteOptions routeOptions = new BingRouteService.RouteOptions();

			routeOptions.Optimization = BingRouteService.RouteOptimization.MinimizeTime;
			routeOptions.TrafficUsage = BingRouteService.TrafficUsage.None;
			routeOptions.Mode = BingRouteService.TravelMode.Driving;

			routeRequest.Options = routeOptions;
			routeRequest.Waypoints = new BingRouteService.Waypoint[2];

			BingRouteService.Waypoint wp1 = new BingRouteService.Waypoint { Location = new BingRouteService.Location { Latitude = lat1, Longitude = lon1 } };
			routeRequest.Waypoints[0] = wp1;

			BingRouteService.Waypoint wp2 = new BingRouteService.Waypoint { Location = new BingRouteService.Location { Latitude = lat2, Longitude = lon2 } };
			routeRequest.Waypoints[1] = wp2;

			BingRouteService.RouteResponse routeResponse = routing.CalculateRoute(routeRequest);

			return new double[] { routeResponse.Result.Summary.Distance, routeResponse.Result.Summary.TimeInSeconds };
		}

		// Geocodes the given address and returns the confidence level
		public GeocodeConfidence GeocodeAddress(string address, string city, string state, string zip, out double lat, out double lon, GeocodeConfidence minConfidence)
		{
			if(string.IsNullOrEmpty(address))
				throw new ArgumentException("Missing 'address' parameter.");
			if(string.IsNullOrEmpty(city))
				throw new ArgumentException("Missing 'city' parameter.");
			if(string.IsNullOrEmpty(state))
				throw new ArgumentException("Missing 'state' parameter.");
			if(string.IsNullOrEmpty(zip))
				throw new ArgumentException("Missing 'zip' parameter.");

			// Reference geocode client
			BingGeocodeService.GeocodeService client = new BingGeocodeService.GeocodeService ();

			// Set the options to only return high confidence results 
			BingGeocodeService.ConfidenceFilter[] filters = new BingGeocodeService.ConfidenceFilter[1];
			filters[0] = new BingGeocodeService.ConfidenceFilter();
			filters[0].MinimumConfidence = TranslateGeocodeConfidence(minConfidence);

			// Build geocode request
			BingGeocodeService.GeocodeRequest request = new BingGeocodeService.GeocodeRequest();
			request.Query = address + ", " + city + ", " + state + ", " + zip;
			request.Credentials = _geoCredentials;

			// Issue request
			BingGeocodeService.GeocodeResponse response = client.Geocode(request);

			// Set out params
			if (response.Results.Length > 0)
			{
				lat = response.Results[0].Locations[0].Latitude;
				lon = response.Results[0].Locations[0].Longitude;
				return TranslateBingConfidence(response.Results[0].Confidence);
			}
			else
			{
				lat = 0;
				lon = 0;
				return 0; // no confidence, bad result
			}
		}

		private static GeocodeConfidence TranslateBingConfidence(BingGeocodeService.Confidence confidence)
		{
			GeocodeConfidence result = GeocodeConfidence.Zero;
			switch (confidence)
			{
				case BingGeocodeService.Confidence.High:
				result = GeocodeConfidence.High;
				break;
				case BingGeocodeService.Confidence.Medium:
				result = GeocodeConfidence.Medium;
				break;
				case BingGeocodeService.Confidence.Low:
				result = GeocodeConfidence.Low;
				break;
			}

			return result;
		}

		private static BingGeocodeService.Confidence TranslateGeocodeConfidence(GeocodeConfidence confidence)
		{
			BingGeocodeService.Confidence result = BingGeocodeService.Confidence.Low;
			switch (confidence)
			{
				case GeocodeConfidence.High:
				result = BingGeocodeService.Confidence.High;
				break;
				case GeocodeConfidence.Medium:
				result = BingGeocodeService.Confidence.Medium;
				break;
			}

			return result;
		}
	}
}
