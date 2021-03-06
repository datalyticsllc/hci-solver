using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using HeuristicLab.Common;
using HeuristicLab.Core;
using HeuristicLab.Data;
using System.Net;
using System.Data.SqlClient;
using System.Data;

namespace Solver
{
	class Program
	{
		private static int _sleepDuration = 5000;

		private static Thread[] _threadArray;
		private const int NUMBER_OF_THREADS = 1;

		private static bool _alive = true;

		static void Main(string[] args)
		{
			Console.WriteLine("*** Control-C To Quit v2.0 ***");


			Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPress_EventHandler);

			// create a single instance of the ContentManager...
			ContentManager.Initialize(new PersistenceContentManager());

			_threadArray = new Thread[NUMBER_OF_THREADS];
			for (int index = 1; index <= NUMBER_OF_THREADS; index++)
			{
				OptimizationJob oj = new OptimizationJob(index, _sleepDuration);
				_threadArray[index - 1] = new Thread(new ThreadStart(oj.StartOptimization));
				_threadArray[index - 1].Start();
			}

			// keep the console open...praise be to Scotty
			while (_alive)
			{
				Console.ReadKey();
			}

		}


		protected static void CancelKeyPress_EventHandler(object sender, ConsoleCancelEventArgs args)
		{
			Console.WriteLine("*** QUIT COMMAND RECEIVED ***");
			_alive = false;
			Environment.Exit(0);
		}
	}
}
