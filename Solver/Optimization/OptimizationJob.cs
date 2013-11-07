using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data;
using System.Data.SqlClient;
using System.Xml;
using System.Threading;
using System.Configuration;
using ServiceStack.ServiceClient.Web;
using PubNubMessaging.Core;
using System.IO;
using System.Net;
using System.Diagnostics;

namespace Solver
{
	public class OptimizationJob
	{
		static public Pubnub pubnub;
		static public bool deliveryStatus = false;
		static public string channel = "routing";

		private int _sleepDuration = 5000;
		private string _connectionString;
		private int _threadID = 0;
		private int _generation = 0;
		private int _runID = 0;

		private EvoStrat _evoStrat;

		// thread status
		private bool _running = false;
		private bool _ready = false;

		public OptimizationJob(int ThreadID, string ConnectionString, int? SleepDuration)
		{
			if (string.IsNullOrEmpty(ConnectionString))
				throw new ArgumentException("OptimizationJob: ConnectionString parameter missing.");
			else
				_connectionString = ConnectionString;

			_threadID = ThreadID;

			if (SleepDuration.HasValue)
				_sleepDuration = SleepDuration.Value;

			//InitializeMessaging ();
			testCall ();

		}

		public void testCall(){

			using (JsonServiceClient client = new JsonServiceClient())
			{
				//Console.WriteLine ("Getting next job to process");

				RouteResponse response = client.Get<RouteResponse>("http://homecareintel.com/services/route/26");

				if(response.drivers.Count > 0)
				{
					Console.WriteLine ("We have entities! [0]:" + response.drivers[0].name);
				}
			};

		}

		public void InitializeMessaging()
		{
			pubnub = new Pubnub("pub-c-efa16124-0af8-46ae-b518-3ce6d735c9db", 
			                    "sub-c-05ff316c-f560-11e2-a11a-02ee2ddab7fe");
		}

		public void SendMessage(string message)
		{
			pubnub.Publish<string>(channel + _runID.ToString(), message, DisplayReturnMessage);
		}

		static void DisplayReturnMessage(string result)
		{
			//Console.WriteLine(result);
		}

		public void Stop()
		{
			_running = false;
			_ready = false;
		}

		public void StartOptimization()
		{
			_running = true;
			_ready = true;

			while (_running && _ready)
			{

				OptimizationRun run = null;

				using (JsonServiceClient client = new JsonServiceClient())
				{
					//Console.WriteLine ("Getting next job to process");

					RouteJobsResponse response = client.Get<RouteJobsResponse>("http://api.datalyticsllc.com/routing/routejobs/-1");
					if(response.RouteJobs.Count > 0)
					{
						run = response.RouteJobs [0];
						_runID = run.ID;

						Console.WriteLine ("Set RunID = " + _runID);
					}
				};

				if (run != null)
				{
					// we've got a job! don't get the next job off this thread...we're done.  
					_ready = false;
					Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", DateTime.Now) + "] Thread " + _threadID + ": Optimization job " + run.ID + " started.");

					// need to dynamically switch to the customer database to get the list of drivers and orders
					ICollection<RouteDriver> drivers = new List<RouteDriver>();
					ICollection<RouteOrder> orders = new List<RouteOrder>();
					JsonServiceClient client = new JsonServiceClient("http://api.datalyticsllc.com/coordcare");

					RouteDriversResponse responseDrivers = client.Get(new GetRouteDrivers { Drivers = run.Drivers, ServiceDate = run.ServiceDate});
					if(responseDrivers.Drivers.Count > 0)
					{
						drivers = responseDrivers.Drivers;
					}
			
					RouteOrdersResponse responseOrders = client.Get(new GetRouteOrders { Orders = run.Orders});
					if(responseOrders.Orders.Count > 0)
					{
						orders = responseOrders.Orders;
					}


					// get a list of stops from the orders (ie. patients from the visits).  Used for RouteLeg lookup
					ICollection<RouteStop> stops = orders.Select(x => new RouteStop(){
						ID = x.StopID, GeoLat = x.GeoLat, GeoLon = x.GeoLon,
						Address = x.Address, City = x.City, State = x.State, Zip = x.Zip
					}).Distinct().ToList();
				    

					// So we pass the route legs when we call the optimizer, or calculate here?
					Routing routing = new Routing();


					ICollection<RouteLeg> routeLegs = routing.GetRouteLegs(stops, drivers, run.CustomerID, Routing.CostType.Distance);


					Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", DateTime.Now) + "] Thread " + _threadID + ": Parsing optimization tags - '" + run.Tags + "'.");
					IEnumerable<OptimizationTag> optimizationTags = OptimizationTagParser.Parse(run.Tags);

					ICollection<RouteOrderMultiplier> legMultipliers = routing.GetRouteMultipliers(orders, drivers, optimizationTags);


					int demand = run.MaxCapacity > 0 ? 1 : 0;

					// set the alogorithm manually for demo
					run.Algorithm = "ES_MDVRPTW_v3.hl";

					_evoStrat = new EvoStrat(
						run, 
						drivers, 
						orders, 
						routeLegs, 
						legMultipliers, 
						run.CostOptimization, 
						Convert.ToDouble(run.DistanceCostRatio), 
						Convert.ToDouble(run.AvgLegLength), 
						Convert.ToDouble(run.MaxLegLength),
					    Convert.ToDouble(run.AvgLegPenalty), 
						Convert.ToDouble(run.MaxLegPenalty), 
						demand, 
						run.MaxCapacity);

					_evoStrat.StatusUpdate += new EventHandler(Routing_StatusUpdate);
					_evoStrat.StartOptimization();

					// Set the start time for the optimization
					UpdateStartTime(run.ID);


				}
				else
				{
					//Stop ();

					// No jobs, so terminate instance!
					Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", DateTime.Now) + "] Thread " + _threadID + ": No job to process...");

					//Process shutdown = new Process(); 
					//shutdown.StartInfo.FileName = "shutdown.sh"; 
					//shutdown.Start();


					Thread.Sleep(_sleepDuration);
				}
			}  // loop until it gets a job
		}

		private void Routing_StatusUpdate(object sender, EventArgs e)
		{
			EvoStrat.StatusUpdateEventArgs evt = (EvoStrat.StatusUpdateEventArgs)e;

			// Note: Both methods below work, we just need to determine which we want to use
			// Update: looks like the EF version isn't as stable, since it's holding on to contexts and such, use SP version.
			try
			{

				// Stored ProcVersion
				if(evt.State == EvoStrat.StrategyState.Completed || evt.State == EvoStrat.StrategyState.Stopped)
				{
					UpdateVrpRun(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);
					PublishVrpRunMessage(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);
				
					// Set the stop time for the optimization
					UpdateStopTime(evt.Run.ID);

					// now restart the listener
					if(evt.State == EvoStrat.StrategyState.Stopped)
					{
						StartOptimization();
					}
				}
				else if(evt.Run.Generations != _generation) // only update status once per generation
				{
					_generation = evt.Run.Generations;

					UpdateVrpRun(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);
					PublishVrpRunMessage(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);

					switch (evt.State)
					{
						case EvoStrat.StrategyState.Completed:
						_ready = true;
						Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", evt.UpdateTime) + "] Thread " + _threadID + " completed. (" + evt.Run.Generations + ")");
						break;
						case EvoStrat.StrategyState.Paused:
						Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", evt.UpdateTime) + "] Thread " + _threadID + " paused. (" + evt.Run.Generations + ")");
						break;
						case EvoStrat.StrategyState.Running:
						Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", evt.UpdateTime) + "] Thread " + _threadID + ": " + evt.Message + " (" + evt.Run.Generations + ")");
						break;
						case EvoStrat.StrategyState.Stopped:
						_ready = true;
						Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", evt.UpdateTime) + "] Thread " + _threadID + ": Optimization job " + evt.Run.ID + " completed. (" + evt.Run.Generations + ")");
						break;
					}
				}
			}
			catch (Exception exc)
			{
				var ignore = true;
			}
			//TODO: Add switch() statement to handle all StrategyState states
		}

		// Thread-safe method to get the next optimization job to run
		private OptimizationRun GetNextOptimizationJob()
		{
			OptimizationRun run = null;

			SqlConnection conn = new SqlConnection("Data Source=CoordCare.com;Initial Catalog=Optimization;Integrated Security=False;User ID=sa;Password=Nov299pe");
			SqlCommand cmd;
			SqlDataAdapter sda;
			DataSet tables = new DataSet();

			conn.Open();

			// get the next
			cmd = new SqlCommand();
			cmd.Connection = conn;
			cmd.CommandType = CommandType.StoredProcedure;
			cmd.CommandText = "GetNextOptimizationRun";

			tables = new DataSet();

			sda = new SqlDataAdapter(cmd);
			sda.Fill(tables);

			if (tables.Tables[0].Rows.Count == 0)
			{
				conn.Close();
				return null;
			}
			else
			{
				foreach (DataRow r in tables.Tables[0].Rows)
				{
					try
					{

						run = new OptimizationRun();

						run.ID = Convert.ToInt32(r["ID"].ToString());
						run.CustomerID = Convert.ToInt32(r["CustomerID"].ToString());
						run.GenerationsToRun = Convert.ToInt32(r["GenerationsToRun"].ToString());
						run.ServiceDate = Convert.ToDateTime(r["ServiceDate"].ToString());
						run.Drivers = r["Drivers"].ToString();
						run.Orders = r["Orders"].ToString();
						run.Algorithm = r["Algorithm"].ToString();
						run.Demo = r["Demo"] == null ? false : Convert.ToInt32(r["Demo"]) == 1;
						run.Tags = r["Tags"].ToString();
						run.CostOptimization = Convert.ToBoolean(r["CostOptimization"].ToString());
						run.DistanceCostRatio = Convert.ToDecimal(r["DistanceCostRatio"].ToString());
						run.AvgLegLength = Convert.ToInt32(r["AvgLegLength"].ToString());
						run.MaxLegLength = Convert.ToInt32(r["MaxLegLength"].ToString());
						run.AvgLegPenalty = Convert.ToDecimal(r["AvgLegPenalty"].ToString());
						run.MaxLegPenalty = Convert.ToDecimal(r["MaxLegPenalty"].ToString());
						run.MaxCapacity = Convert.ToInt32(r["MaxCapacity"].ToString());
					}
					catch (Exception exc)
					{
						Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", DateTime.Now) + "] Thread " + _threadID + ": Error -- " + exc.ToString());
					}
				}

				conn.Close();

			}

			return run;
		}

		private void PublishVrpRunMessage(int runId, int generations, string bestQuality, string bestSolution, string currentResults, string message)
		{
			string pMessage = "";

			pMessage = "ID=" + runId + "^Generations=" + generations.ToString () + "^BestQuality=" + bestQuality + "^BestSolution=" + bestSolution + "^Message=" + message;

			SendMessage (pMessage);
		}

		// Thread-save method to update an optimization job
		private void UpdateVrpRun(int runId, int generations, string bestQuality, string bestSolution, string currentResults, string message)
		{

			SqlCommand cmd;
			DataSet tables = new DataSet();

			try
			{
				SqlConnection conn = new SqlConnection("Data Source=CoordCare.com;Initial Catalog=Optimization;Integrated Security=False;User ID=sa;Password=Nov299pe");

				conn.Open();

				cmd = new SqlCommand();
				cmd.Connection = conn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "UpdateOptimizationRun";
				cmd.Parameters.AddWithValue("@ID", runId);
				cmd.Parameters.AddWithValue("@BestQuality", bestQuality == null ? "" : bestQuality);
				cmd.Parameters.AddWithValue("@Generations", generations);
				cmd.Parameters.AddWithValue("@BestSolution", bestSolution == null ? "" : bestSolution);
				cmd.Parameters.AddWithValue("@CurrentResults", currentResults == null ? "" : currentResults);
				cmd.Parameters.AddWithValue("@Status", message);

				var killRun = cmd.ExecuteScalar();

				conn.Close();

				if (Convert.ToInt32(killRun) == 1)
				{
					_evoStrat.StopJob();
				}
			}
			catch (Exception exc)
			{
				var error = exc.ToString();
			}
		}

		// Thread-save method to update an optimization job
		private void UpdateStartTime(int runId)
		{

			SqlCommand cmd;

			try
			{
				SqlConnection conn = new SqlConnection("Data Source=CoordCare.com;Initial Catalog=Optimization;Integrated Security=False;User ID=sa;Password=Nov299pe");

				conn.Open();

				cmd = new SqlCommand();
				cmd.Connection = conn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "SetOptimizationRunStartTime";
				cmd.Parameters.AddWithValue("@ID", runId);
				cmd.Parameters.AddWithValue("@StartTime", DateTime.Now);

				cmd.ExecuteScalar();

				conn.Close();

			}
			catch (Exception exc)
			{
				var error = exc.ToString();
			}
		}

		// Thread-save method to update an optimization job
		private void UpdateStopTime(int runId)
		{

			SqlCommand cmd;

			try
			{
				SqlConnection conn = new SqlConnection("Data Source=CoordCare.com;Initial Catalog=Optimization;Integrated Security=False;User ID=sa;Password=Nov299pe");

				conn.Open();

				cmd = new SqlCommand();
				cmd.Connection = conn;
				cmd.CommandType = CommandType.StoredProcedure;
				cmd.CommandText = "SetOptimizationRunStopTime";
				cmd.Parameters.AddWithValue("@ID", runId);
				cmd.Parameters.AddWithValue("@StopTime", DateTime.Now);

				cmd.ExecuteScalar();

				conn.Close();

			}
			catch (Exception exc)
			{
				var error = exc.ToString();
			}
		}

	}
}
