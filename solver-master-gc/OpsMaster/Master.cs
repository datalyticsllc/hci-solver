using System;
using System.Threading;
using ServiceStack.ServiceClient.Web;
using Solver;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace OpsMaster
{
	public class Master
	{

		private int _sleepDuration = 5000;

		// thread status
		private bool _running = false;
		private bool _ready = false;

		public Master (int? SleepDuration)
		{
			if (SleepDuration.HasValue)
				_sleepDuration = SleepDuration.Value;

		}

		public void StartMaster()
		{
			_running = true;
			_ready = true;

			while (_running && _ready) {

				List<OptimizationRun> pendingJobs = new List<OptimizationRun> ();

				using (JsonServiceClient client = new JsonServiceClient())
				{
					RouteJobsResponse response = client.Get<RouteJobsResponse>("http://api.datalyticsllc.com/routing/routejobs/");
					if(response.RouteJobs.Count > 0)
					{
						pendingJobs = response.RouteJobs;

						Console.WriteLine (pendingJobs.Count.ToString() + " pending jobs found...starting optimization slaves.");
					}
				};

				if (pendingJobs.Count > 0) {

					foreach (OptimizationRun job in pendingJobs) {

						long serverId = DateTime.Now.Ticks;

						Console.WriteLine ("*** Attempting to start Google Instance: slave-" + serverId.ToString() + " ***");

						var proc = new Process {
							StartInfo = new ProcessStartInfo {

								FileName = "createinstance.sh",
								Arguments = "slave-" + serverId.ToString(),
								UseShellExecute = false,
								RedirectStandardOutput = true,
								CreateNoWindow = true
							}
						};

						proc.Start ();

						while (!proc.StandardOutput.EndOfStream) {
							string line = proc.StandardOutput.ReadLine();
							// do something with line
						}

					}

					// now sleep the timer for 30 seconds to give the machines time to come back up
					Thread.Sleep(60000);

				} else {

					Console.WriteLine("[" + string.Format("{0:d/M/yyyy HH:mm:ss}", DateTime.Now) + "] : No job to process.");
					Thread.Sleep(_sleepDuration);

				}
			}
		}

	}
}

