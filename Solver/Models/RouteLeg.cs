using System;

namespace Solver
{
	public class RouteLeg
	{
		public int FromID { get; set; }
		public int FromTypeID { get; set; }
		public int ToID { get; set; }
		public int ToTypeID { get; set; }
		public decimal DrivingDistance { get; set; }
		public decimal DrivingTime { get; set; }
	}
}

