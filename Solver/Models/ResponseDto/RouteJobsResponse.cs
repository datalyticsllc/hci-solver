using System;
using System.Collections;
using System.Collections.Generic;

namespace Solver
{
	public class RouteJobsResponse
	{
		public RouteJobsResponse()
		{
			this.RouteJobs = new List<OptimizationRun>();
		}

		public List<OptimizationRun> RouteJobs { get; set; }

	}
}

