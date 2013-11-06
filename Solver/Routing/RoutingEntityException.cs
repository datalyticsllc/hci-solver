using System;

namespace Solver
{
	[Serializable]
	public class RoutingEntityException : ApplicationException
	{
		public int EntityID { get; set; }
		public int EntityTypeID { get; set; }

		public RoutingEntityException(string message, Exception innerException) : base(message, innerException)
		{
		}

		public RoutingEntityException(string message)
			: base(message)
		{
		}

		public RoutingEntityException(string message, int entityID)
			: base(message)
		{
			this.EntityID = entityID;
		}

		public RoutingEntityException(string message, int entityID, int entityTypeID) : base(message)
		{
			this.EntityID = entityID;
			this.EntityTypeID = entityTypeID;
		}
	}
}

