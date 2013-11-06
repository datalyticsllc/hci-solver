using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HeuristicLab.Common;
using HeuristicLab.Core;
using HeuristicLab.Data;
using HeuristicLab.Optimization;
using HeuristicLab.Problems.TravelingSalesman;
using HeuristicLab.Problems.VehicleRouting.Datalytics;
using HeuristicLab.Algorithms.EvolutionStrategy;
using System.IO;


namespace Solver
{
	public class EvoStrat
	{
		// Configuration
		private StrategyState _strategyState = StrategyState.Stopped;
		private double _elapsedTime = 0;  // elapsed time tracking (may not need if host handles database calls)
		public class StatusUpdateEventArgs : EventArgs
		{
			public OptimizationRun Run { get; set; }
			public StrategyState State { get; set; }
			public DateTime UpdateTime { get; set; }
			public string Message { get; set; }
		}
		public enum StrategyState
		{
			Stopped, Running, Paused, Completed
		}


		// Problem
		private List<int> _stopIds = new List<int>();  // stores the visit IDs
		private List<int> _driverIds = new List<int>();  // stores the staff IDs
		private List<RouteOrder> _orders = new List<RouteOrder>();
		private List<RouteDriver> _drivers = new List<RouteDriver>();
		private DoubleMatrix _coordinates = new DoubleMatrix();
		private IntArray _vehicleAssignment = new IntArray();
		private List<RouteEntity> _routeEntities = new List<RouteEntity>();
		private List<RouteLeg> _routeLegs = new List<RouteLeg>();
		private List<RouteOrderMultiplier> _mutipliers = new List<RouteOrderMultiplier>();

		// cost-optimization stuff
		private bool _costOptimization = false;
		private double _distanceCostRatio = 0.0;
		private double _avgLegLength = 0.0;
		private double _maxLegLength = 0.0;
		private double _avgLegPenalty = 0.0;
		private double _maxLegPenalty = 0.0;
		private double _capacity = 0.0;
		private double _demand = 0.0;

		// Run vars
		HeuristicLab.Algorithms.EvolutionStrategy.EvolutionStrategy algorithm;
		private OptimizationRun _run = new OptimizationRun();

		// External Events
		public event EventHandler StatusUpdate;
		protected virtual void OnStatusUpdate(StatusUpdateEventArgs e)
		{
			EventHandler handler = StatusUpdate;
			if (handler != null)
			{
				handler(this, e);
			}
		}

		public EvoStrat(OptimizationRun run, ICollection<RouteDriver> drivers, ICollection<RouteOrder> orders, ICollection<RouteLeg> routeLegs, ICollection<RouteOrderMultiplier> routeMutipliers, 
		                bool costOptimization, double distanceCostRatio, double avgLegLength, double maxLegLength, double avgLegPenalty, double maxLegPenalty, double demand, double capacity)
		{
			_run = run;
			_drivers = drivers.ToList();
			_orders = orders.ToList();
			_routeLegs = routeLegs.ToList();
			_mutipliers = routeMutipliers.ToList();
			_costOptimization = costOptimization;
			_distanceCostRatio = distanceCostRatio;
			_avgLegLength = avgLegLength;
			_avgLegPenalty = avgLegPenalty;
			_maxLegLength = maxLegLength;
			_maxLegPenalty = maxLegPenalty;
			_demand = demand;
			_capacity = capacity;
		}

		public void StartOptimization()
		{
			UpdateRun("Starting Optimization", StrategyState.Running);

			// setup the Entity List
			_routeEntities.AddRange(RouteEntity.Convert(_drivers));

			// add an entity for each visit, but we add the patient id, so our distance matrix will work.
			// we do this the handle multiple orders (visits) to the same stop (patient)
			foreach (var o in _orders)
			{
				_routeEntities.Add(new RouteEntity { TypeID = 1, ID = o.StopID, GeoLat = o.GeoLat, GeoLon = o.GeoLon });
			}

			_driverIds = (from s in _drivers select s.ID).ToList ();
			_stopIds = (from s in _orders select s.ID).ToList ();


			//TODO: We need to change where the algorithms are stored.  Ideally, we'd have them 
			// in a folder in the project directory
			//string algorithmPath = @"\Algorithms\ES_MDVRPTW_v3.hl";

			string algorithmPath = Path.Combine(Path.Combine(Directory.GetCurrentDirectory(), "Algorithms"), _run.Algorithm);

			Console.WriteLine("Loading algorithm: " + algorithmPath);

			//ContentManager.Initialize(new PersistenceContentManager());
			var content = ContentManager.Load(algorithmPath);

			LoadingCompleted(content);

		}

		public void StopJob()
		{
			algorithm.Stop();
		}

		private void LoadingCompleted(IStorableContent content)
		{

			//if (error != null) throw error;
			UpdateRun("Algorithm loading complete", StrategyState.Running);

			if (content is HeuristicLab.Algorithms.EvolutionStrategy.EvolutionStrategy)
			{
				algorithm = content as HeuristicLab.Algorithms.EvolutionStrategy.EvolutionStrategy;
				VehicleRoutingProblem problem = algorithm.Problem as VehicleRoutingProblem;

				if (problem == null)
				{
					Console.WriteLine("Unsupported problem type detected.");
					return;
				}

				SetProblemInstance(problem);

				ParameterizeProblem(problem);
				ParameterizeAlgorithm(algorithm);

				RegisterAlgorithmEventHandlers(algorithm);

				try
				{
					algorithm.Start();
				}
				catch (Exception exc)
				{
					Console.WriteLine("Error Loading: " + exc.Message);
				}
			}
			else
			{
				// TODO: update the run status to error...
				//_strategyState = StrategyState.Stopped;
			}


		}

		private void SetProblemInstance(VehicleRoutingProblem problem)
		{

			//var pi = problem.ProblemInstance.Clone() as HeuristicLab.Problems.VehicleRouting.ProblemInstances.MDCVRPTWProblemInstance;
			HeuristicLab.Problems.VehicleRouting.Datalytics.ProblemInstances.MDCVRPTWProblemInstance pi = new HeuristicLab.Problems.VehicleRouting.Datalytics.ProblemInstances.MDCVRPTWProblemInstance();

			pi.Cities.Value = _orders.Count;
			pi.Depots.Value = _drivers.Count;
			pi.Vehicles.Value = _drivers.Count;  // only allow 1 vehicle per depot
			pi.DistanceFactor.Value = 1.0;
			pi.FleetUsageFactor.Value = 0.0;
			pi.UseDistanceMatrix.Value = true;

			pi.Capacity = new DoubleArray(GetCapacity(_drivers.Count));
			pi.Coordinates = new DoubleMatrix(GetCoordinates());

			_coordinates = pi.Coordinates;

			//string patientList = string.Join(",", _stopIds);
			//string staffList = string.Join(",", _driverIds);

			UpdateRun("Calculating Distance Matrix", StrategyState.Running);

			//Routing.Routing routing = new Routing.Routing();
			//_routeLegs = routing.GetRouteLegs(patientList, staffList, "Distance");

			pi.DistanceMatrix = CreateDistanceMatrix();
			pi.TimeMatrix = CreateTimeMatrix();
			pi.QualityMatrix = CreateQualityMatrix();

			// cost-based functionality
			pi.CostOptimization.Value = _costOptimization;
			pi.DistanceToCostRatio.Value = _distanceCostRatio;
			pi.PerStopCost = CreatePerVisitCostArray();
			pi.PerMileCost = CreatePerMileCostArray();
			pi.AvgLegLength.Value = _avgLegLength;
			pi.AvgLegPenality.Value = _avgLegPenalty;
			pi.MaxLegLength.Value = _maxLegLength;
			pi.MaxLegPenality.Value = _maxLegPenalty;

			UpdateRun("Distance Matrix Loaded", StrategyState.Running);

			pi.Demand = new DoubleArray(GetDemand(_orders.Count));

			double dueTime = 1.7976931348623157E308;  // Max double, we need this for some reason, 0 didn't work

			pi.DueTime = new DoubleArray(GetDueTime(_orders.Count + _drivers.Count, dueTime));
			pi.ServiceTime = new DoubleArray(GetServiceTime(_orders.Count));
			pi.ReadyTime = new DoubleArray(GetReadyTime(_orders.Count + _drivers.Count));
			pi.VehicleDepotAssignment = new IntArray(SetVehicleDepotAssignment(_drivers.Count));

			// set the vehicle assigment array
			_vehicleAssignment = pi.VehicleDepotAssignment;

			problem.SetProblemInstance(pi);

			UpdateRun("Problem Instance Set", StrategyState.Running);

		}

		private void ParameterizeAlgorithm(HeuristicLab.Algorithms.EvolutionStrategy.EvolutionStrategy algorithm)
		{
			//set all parameters of the algorithm, e.g.
			algorithm.MaximumGenerations = new IntValue(_run.GenerationsToRun);
		}

		private void ParameterizeProblem(VehicleRoutingProblem problem)
		{
			//set all parameters of the problem, e.g. 

			//problem.MaximizationParameter = false;

		}

		private double[,] GetCoordinates()
		{


			// depot (staff) coords are loaded in the first n slots, the patients afterwards
			double[,] coords = new double[_drivers.Count + _orders.Count, 2];

			// add the driver coords first
			int i = 0;
			foreach (RouteDriver d in _drivers)
			{
				//_driverIds.Add(d.ID);
				coords[i, 0] = Convert.ToDouble(d.GeoLat);
				coords[i, 1] = Convert.ToDouble(d.GeoLon);
				i++;
			}

			// now add the stop coords
			foreach (RouteOrder s in _orders)
			{
				// _stopIds.Add(s.ID);
				coords[i, 0] = Convert.ToDouble(s.GeoLat);
				coords[i, 1] = Convert.ToDouble(s.GeoLon);
				i++;
			}

			return coords;
		}

		private DoubleMatrix CreateDistanceMatrix()
		{

			DoubleMatrix distanceMatrix = new DoubleMatrix(_routeEntities.Count, _routeEntities.Count);

			for (int i = 0; i < distanceMatrix.Rows; i++)
			{
				for (int j = 0; j < distanceMatrix.Columns; j++)
				{
					var start = _routeEntities[i];
					var end = _routeEntities[j];

					RouteLeg leg = _routeLegs.Where(x => x.FromID == start.ID
					                                && x.FromTypeID == start.TypeID
					                                && x.ToID == end.ID && x.ToTypeID == end.TypeID).FirstOrDefault();

					distanceMatrix[i, j] = Convert.ToDouble(leg.DrivingDistance);
				}
			}

			return distanceMatrix;
		}

		private DoubleMatrix CreateTimeMatrix()
		{

			DoubleMatrix timeMatrix = new DoubleMatrix(_routeEntities.Count, _routeEntities.Count);

			for (int i = 0; i < timeMatrix.Rows; i++)
			{
				for (int j = 0; j < timeMatrix.Columns; j++)
				{
					var start = _routeEntities[i];
					var end = _routeEntities[j];

					RouteLeg leg = _routeLegs.Where(x => x.FromID == start.ID
					                                && x.FromTypeID == start.TypeID
					                                && x.ToID == end.ID && x.ToTypeID == end.TypeID).FirstOrDefault();

					timeMatrix[i, j] = Convert.ToDouble(leg.DrivingTime / 60);
				}
			}

			return timeMatrix;
		}

		private DoubleMatrix CreateQualityMatrix()
		{

			DoubleMatrix qualityMatrix = new DoubleMatrix(_driverIds.Count, _stopIds.Count);

			for (int i = 0; i < qualityMatrix.Rows; i++)
			{
				for (int j = 0; j < qualityMatrix.Columns; j++)
				{
					var start = _driverIds[i];
					var end = _stopIds[j];

					// replace with the new RouteLegMultiplier
					RouteOrderMultiplier mutiplier = _mutipliers.Where(x => x.RouteDriverID == start && x.RouteOrderID == end).FirstOrDefault();

					qualityMatrix[i, j] = Convert.ToDouble(mutiplier.AdjustmentMultiplier);

				}
			}

			return qualityMatrix;
		}

		private DoubleArray CreatePerVisitCostArray()
		{

			DoubleArray perVisitCostArray = new DoubleArray(_driverIds.Count);

			for (int i = 0; i < perVisitCostArray.Length; i++)
			{
				perVisitCostArray[i] = Convert.ToDouble(_drivers[i].PerVisitCost);

			}

			return perVisitCostArray;
		}

		private DoubleArray CreatePerMileCostArray()
		{

			DoubleArray perMileCostArray = new DoubleArray(_driverIds.Count);

			for (int i = 0; i < perMileCostArray.Length; i++)
			{
				perMileCostArray[i] = Convert.ToDouble(_drivers[i].PerMileCost);

			}

			return perMileCostArray;
		}

		private int[] SetVehicleDepotAssignment(int staff)
		{
			int[] depots = new int[staff];

			for (int i = 0; i < staff; i++)
			{
				depots[i] = i;
			}

			return depots;
		}

		private double[] GetCapacity(int staff)
		{
			double[] capacity = new double[staff];

			for (int i = 0; i < staff; i++)
			{
				capacity[i] = _capacity;
			}

			return capacity;
		}

		private double[] GetDueTime(int count, double dueTimeVal)
		{
			double[] dueTime = new double[count];

			for (int i = 0; i < count; i++)
			{
				if (i < _drivers.Count)
				{
					dueTime[i] = Convert.ToDouble(_drivers[i].Availability);
				}
				else
				{
					dueTime[i] = dueTimeVal;
				}
			}

			return dueTime;
		}

		private double[] GetServiceTime(int count)
		{
			double[] serviceTime = new double[count];

			for (int i = 0; i < count; i++)
			{
				serviceTime[i] = Convert.ToDouble(_orders[i].ServiceTime);
			}

			return serviceTime;
		}

		private double[] GetReadyTime(int count)
		{
			double[] readyTime = new double[count];

			for (int i = 0; i < count; i++)
			{
				readyTime[i] = 0;
			}

			return readyTime;
		}

		private double[] GetDemand(int patients)
		{
			double[] demand = new double[patients];

			for (int i = 0; i < patients; i++)
			{
				demand[i] = _demand;
			}

			return demand;
		}

		private void RegisterAlgorithmEventHandlers(EngineAlgorithm alg)
		{
			//alg.ExecutionStateChanged += new EventHandler(ExecutionStateChanged);
			alg.ExecutionTimeChanged += new EventHandler(ExecutionTimeChanged);
			alg.Stopped += new EventHandler(Stopped);
			//alg.Started += new EventHandler(Started);
			alg.Paused += new EventHandler(Paused);
		}

		private void DeregisterAlgorithmEventHandlers(EngineAlgorithm alg)
		{
			//alg.ExecutionStateChanged -= new EventHandler(ExecutionStateChanged);
			alg.ExecutionTimeChanged -= new EventHandler(ExecutionTimeChanged);
			alg.Stopped -= new EventHandler(Stopped);
			//alg.Started += new EventHandler(Started);
			alg.Paused += new EventHandler(Paused);
		}

		private void ExecutionTimeChanged(object sender, EventArgs e)
		{
			// maybe we should check to see if the execution time has been longer than say 5 secs?
			EngineAlgorithm alg = sender as EngineAlgorithm;

			// only update on certain intervals, so we don't tax the db unnecessarily
			var results = alg.Results.Clone() as ResultCollection;

			try
			{
				foreach (var s in results)
				{

					if (s.Name == "BestQuality")
					{
						// debug
						//var test = _pi.QualityMatrix;

						// get algorithm output
						string bestQuality = results["BestQuality"].Value.ToString();
						string generations = results["Generations"].Value.ToString();
						string bestSolution = GetBestSolution(results);
						string currentResults = GetCurrentResults(results);

						// only call the database on the last update
						//if(_run.GenerationsToRun == Convert.ToInt32(generations))
						//{

							_run.Generations = Convert.ToInt32(generations);
							_run.BestQuality = bestQuality;
							_run.BestSolution = bestSolution;
							_run.CurrentResults = currentResults;

							//Console.WriteLine("Thread1 -> Time: " + alg.ExecutionTime + ", Quality: " + alg.Results["BestQuality"].Value.ToString());
							UpdateRun("Quality: " + alg.Results["BestQuality"].Value.ToString(), StrategyState.Running);
						
						//}
					}
				}
			}
			catch (Exception exc)
			{
				bool ignore = true;
			}

		}

		private void Paused(object sender, EventArgs e)
		{
			EngineAlgorithm alg = sender as EngineAlgorithm;

			Console.WriteLine("Execution paused");
			alg.Stop();
		}

		private void Stopped(object sender, EventArgs e)
		{
			EngineAlgorithm alg = sender as EngineAlgorithm;

			// only update on certain intervals, so we don't tax the db unnecessarily
			var results = alg.Results.Clone() as ResultCollection;

			foreach (var s in results)
			{

				if (s.Name == "BestQuality")
				{
					// get algorithm output
					string bestQuality = results["BestQuality"].Value.ToString();
					string generations = results["Generations"].Value.ToString();
					string bestSolution = GetBestSolution(results);
					string currentResults = GetCurrentResults(results);

					_run.Generations = Convert.ToInt32(generations);
					_run.BestQuality = bestQuality;
					_run.BestSolution = bestSolution;
					_run.CurrentResults = currentResults;

					//Console.WriteLine("Thread1 -> Time: " + alg.ExecutionTime + ", Quality: " + alg.Results["BestQuality"].Value.ToString());
					UpdateRun("Optimization Completed", StrategyState.Stopped);
				}
			}

			DeregisterAlgorithmEventHandlers(alg);

			//UpdateRun("Optimization completed", StrategyState.Stopped);
		}

		private string GetBestSolution(ResultCollection results)
		{
			string result = string.Empty;

			try
			{
				if (results["Best VRP Solution"] == null)
					return null;

				// otherwise we have a valid solution, so update the database
				VRPSolution solution = results["Best VRP Solution"].Value as VRPSolution;

				var tours = solution.Solution.GetTours();

				bool firstDriver = true;

				for (int i = 0; i < tours.Count; i++)
				{
					if (firstDriver)
					{
						firstDriver = false;
					}
					else
					{
						result += "|";
					}

					int tourIndex = solution.Solution.GetTourIndex(tours[i]);
					int vehicle = solution.Solution.GetVehicleAssignment(tourIndex);
					int depot =  _vehicleAssignment[vehicle];

					result += _driverIds[depot].ToString() + ":";

					var stops = string.Empty;

					bool first = true;

					foreach (var s in tours[i].Stops)
					{
						if (first)
							first = false;
						else
						{
							stops += ",";
						}

						stops += _stopIds[s - 1].ToString();

					}

					result += stops;

				}
			}
			catch (Exception exc)
			{
				//alg.Stop();
				_strategyState = StrategyState.Stopped;
				return null;
			}

			return result;
		}

		private string GetCurrentResults(ResultCollection results)
		{
			string currentResults = string.Empty;

			bool first = true;

			foreach (var s in results)
			{
				if (s.Name != "Overloads" && s.Name != "TardinessValues" && s.Name != "TravelTimes"
				    && s.Name != "Distances" && s.Name != "VehiclesUtilizedValues" && s.Name != "Best VRP Solution")
				{
					if (first)
					{
						first = false;
					}
					else
					{
						currentResults += "|";
					}


					currentResults += s.Name + ":" + s.Value;
				}
			}

			return currentResults;
		}

		private void UpdateRun(string message, StrategyState state)
		{

			try
			{
				_strategyState = state;

				// Trigger update event
				StatusUpdate(this,
				             new StatusUpdateEventArgs()
				             {
					Run = _run,
					Message = message,
					State = this._strategyState,
					UpdateTime = DateTime.Now
				});
			}
			catch (Exception exc)
			{
				_strategyState = StrategyState.Stopped;
				var error = exc.ToString();
			}
		}

	}
}
