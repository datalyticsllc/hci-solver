using System;

namespace Solver
{
	public class OptimizationRun
	{
		// Job tracking
		public int ID { get; set; }
		public int CustomerID { get; set; }
		public string Algorithm { get; set; }
		public DateTime ServiceDate { get; set; }
		public string Drivers { get; set; }
		public string Orders { get; set; }
	
		public string Tags { get; set; }

		public int GenerationsToRun { get; set; }
		public int Generations { get; set; }
		public string BestQuality { get; set; }
		public string BestSolution { get; set; }
		public string CurrentResults { get; set; }
		public DateTime CreateDate { get; set; }
		public int RunLock { get; set; }
		public int KillRun { get; set; }

		// cost-based optimization stuff
		public bool CostOptimization { get; set; }
		public decimal DistanceCostRatio { get; set; }
		public int AvgLegLength { get; set; }
		public int MaxLegLength { get; set; }
		public decimal AvgLegPenalty { get; set; }
		public decimal MaxLegPenalty { get; set; }
		public int MaxCapacity { get; set; }

		public string Message { get; set; }
		public bool Demo { get; set; }
	}
}

