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
using Npgsql;
using NpgsqlTypes;
using Newtonsoft.Json;

namespace Solver
{
	public class OptimizationJob
	{
		static public Pubnub pubnub;
		static public bool deliveryStatus = false;
		static public string channel = "routing";

		private int _sleepDuration = 5000;
		private int _threadID = 0;
		private int _generation = 0;
		private int _runID = 0;

		private EvoStrat _evoStrat;

		// thread status
		private bool _running = false;
		private bool _ready = false;

		public OptimizationJob(int ThreadID, int? SleepDuration)
		{

			_threadID = ThreadID;

			if (SleepDuration.HasValue)
				_sleepDuration = SleepDuration.Value;

			InitializeMessaging ();

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

		private void PublishVrpRunMessage(int runId, int generations, string bestQuality, string bestSolution, string currentResults, string message)
		{
			string pMessage = "";

			pMessage = "ID=" + runId + "^Generations=" + generations.ToString () + "^BestQuality=" + bestQuality + "^BestSolution=" + bestSolution + "^Message=" + message;

			SendMessage (pMessage);
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

				OptimizationRun run = new OptimizationRun();
				RouteResponse response = null;
				List<Entity> routeEntities = new List<Entity> ();
				List<RouteLeg> routeLegs = new List<RouteLeg>();

				try{

					// First, let's check to see if there's any jobs to process
					response = GetNextJobToProcess();


					if (response != null)
					{
						// we've got a job!
						run.ID = response.routeId;
						_runID = response.routeId;
						_ready = false;
						
						Console.WriteLine ("Set RunID = " + run.ID);

						List<Int64> driverList = new List<Int64>();
						List<Int64> stopList = new List<Int64>();

						Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", DateTime.Now) + "] Thread " + _threadID + ": Optimization job " + run.ID + " started.");

						NpgsqlConnection conn = new NpgsqlConnection("Server=hci.cvwcpfnur8ep.us-east-1.rds.amazonaws.com;Port=5432;User Id=hci_user;Password=Nov299pe;Database=route;");
						conn.Open();

						try
						{


							// Update the driver entities
							foreach(Driver d in response.drivers){

								NpgsqlCommand command = new NpgsqlCommand(
									"SELECT * FROM update_entity(" + response.customerId.ToString() + "::bigint, " 
									+ d.name + "::varchar, " 
									+ d.location.latitude.ToString() + "::float8, " 
									+ d.location.longitude.ToString() + "::float8, " 
									+ "2::int2)", conn);

								command.CommandType = CommandType.Text;

								Object result = command.ExecuteScalar();

								// set the driver system id
								d.id = Convert.ToInt64(result);
								driverList.Add(Convert.ToInt64(result));

								// now add the driver to the entity list
								routeEntities.Add(new Entity{
									id = Convert.ToInt64(result),
									geoLat = d.location.latitude,
									geoLon = d.location.longitude
								});					               

							}

							Console.WriteLine("Drivers: " + string.Join<Int64>(",", driverList));

							// update the stop entities
							foreach(Task t in response.tasks){

								NpgsqlCommand command = new NpgsqlCommand(
									"SELECT * FROM update_entity(" + response.customerId.ToString() + "::bigint, " 
									+ t.stop.name + "::varchar, " 
									+ t.stop.location.latitude.ToString() + "::float8, " 
									+ t.stop.location.longitude.ToString() + "::float8, " 
									+ "1::int2)", conn);

								command.CommandType = CommandType.Text;

								Object result = command.ExecuteScalar();

								// set's the id to the stop ID, to lookup during the routeleg calculation
								t.id = Convert.ToInt64(result);
								stopList.Add(Convert.ToInt64(result));	

								// now add the stop to the entity list
								routeEntities.Add(new Entity{
									id = Convert.ToInt64(result),
									geoLat = t.stop.location.latitude,
									geoLon = t.stop.location.longitude
								});	

							}

							Console.WriteLine("Stops: " + string.Join<Int64>(",", stopList));

							// now get a list of route legs
							string drivers = string.Join<Int64>(",", driverList);
							string stops = string.Join<Int64>(",", stopList);

							NpgsqlCommand legCommand = new NpgsqlCommand("SELECT * FROM calc_entity_matrix('" + drivers + "','" + stops + "')", conn);


							NpgsqlDataReader dr = legCommand.ExecuteReader();
							while(dr.Read())
							{
								routeLegs.Add(new RouteLeg{
									FromID = Convert.ToInt32(dr.GetValue(0)),
									ToID = Convert.ToInt32(dr.GetValue(1)),
									DrivingDistance = Convert.ToDecimal(dr.GetValue(2)),
									DrivingTime = Convert.ToDecimal(dr.GetValue(3))
								});

							}


						}

						finally
						{
							conn.Close();
						}

						// Now calculate the route leg stuff
						Routing routing = new Routing();


						ICollection<RouteLeg> legs = routing.GetRouteLegs(routeEntities, routeLegs, 1, Routing.CostType.Distance);
						ICollection<DriverTaskMultiplier> legMultipliers = routing.GetRoutePenalties(response.drivers, response.tasks);

						int demand = 1;

						// set the alogorithm manually for demo
						run.GenerationsToRun = response.generations;
						run.Algorithm = "ES_MDVRPTW_v3.hl";
						run.CostOptimization = false;
						run.DistanceCostRatio = 1.0m;
						run.AvgLegLength = 20;
						run.AvgLegPenalty = 0;
						run.MaxLegLength = 30;
						run.MaxLegPenalty = 0;

						_evoStrat = new EvoStrat(
							run, 
							driverList,
							stopList,
							routeEntities,
							response.drivers, 
							response.tasks, 
							legs, 
							legMultipliers, 
							run.CostOptimization, 
							Convert.ToDouble(run.DistanceCostRatio), 
							Convert.ToDouble(run.AvgLegLength), 
							Convert.ToDouble(run.MaxLegLength),
							Convert.ToDouble(run.AvgLegPenalty), 
							Convert.ToDouble(run.MaxLegPenalty), 
							demand);

						_evoStrat.StatusUpdate += new EventHandler(Routing_StatusUpdate);
						_evoStrat.StartOptimization();


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
				}
				catch(Exception exc){
					// ignore error
					Thread.Sleep(_sleepDuration);
					var error = exc;
				}
			}  // loop until it gets a job
		}

		private RouteResponse GetNextJobToProcess(){

			RouteResponse result = null;

			NpgsqlConnection conn = new NpgsqlConnection("Server=hci.cvwcpfnur8ep.us-east-1.rds.amazonaws.com;Port=5432;User Id=hci_user;Password=Nov299pe;Database=route;");
			conn.Open();

			try
			{

				NpgsqlCommand legCommand = new NpgsqlCommand("SELECT * FROM get_next_route()", conn);

				NpgsqlDataReader dr = legCommand.ExecuteReader();
				while(dr.Read())
				{
					var jsonObject = dr.GetValue(1).ToString();

					result = JsonConvert.DeserializeObject<RouteResponse>(jsonObject);
					result.routeId = Convert.ToInt32(dr.GetValue(0));

				}
			}
			catch(Exception exc){
				var error = exc;
			}
			finally {
				conn.Close ();
			}

			return result;
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
					//UpdateVrpRun(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);
					PublishVrpRunMessage(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);

					// now restart the listener
					if(evt.State == EvoStrat.StrategyState.Stopped)
					{
						StartOptimization();
					}
				}
				else if(evt.Run.Generations != _generation) // only update status once per generation
				{
					_generation = evt.Run.Generations;

					//UpdateVrpRun(evt.Run.ID, evt.Run.Generations, evt.Run.BestQuality, evt.Run.BestSolution, evt.Run.CurrentResults, evt.Message);
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
			catch
			{
				//var ignore = true;
			}

		}

	}
}
