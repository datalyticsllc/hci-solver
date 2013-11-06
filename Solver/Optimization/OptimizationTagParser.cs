using System;
using System.Collections;
using System.Collections.Generic;

namespace Solver
{
	public class OptimizationTagParser
	{
		public static IEnumerable<OptimizationTag> Parse(string Tags)
		{
			List<OptimizationTag> result = new List<OptimizationTag>();

			foreach (string tag in Tags.Split(new string[] { "|" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (tag.Contains("+"))
				{
					result.Add(new OptimizationTag()
					           {
						IsExclusive = false,
						Name = tag.Substring(0, tag.IndexOf("+")),
						Multiplier = float.Parse(tag.Substring(tag.IndexOf("+") + 1))
					});
				}
				else
				{
					result.Add(new OptimizationTag()
					           {
						IsExclusive = true,
						Name = tag.Substring(0, tag.IndexOf("-")),
						Multiplier = float.Parse(tag.Substring(tag.IndexOf("-") + 1))
					});
				}
			}

			return result;
		}
	}
}

