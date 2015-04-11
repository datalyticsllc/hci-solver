using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Data.SqlClient;
using System.Data;
using System.Threading;
//using Google.Apis;
//using Google.Apis.Compute.v1beta15;
//using Google.Apis.Compute.v1beta15.Data;
using System.Diagnostics;

namespace OpsMaster
{
	class MainClass
	{

		private static int _sleepDuration = 5000;


		private static bool _alive = true;

		public static void Main (string[] args)
		{
			Console.WriteLine ("*** Optimization Master Service Started ***");

			Console.CancelKeyPress += new ConsoleCancelEventHandler(CancelKeyPress_EventHandler);

			Master m = new Master(_sleepDuration);

			Thread master = new Thread(new ThreadStart(m.StartMaster));
			master.Start();

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
