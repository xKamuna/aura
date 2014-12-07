using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura.Shared.Mabi.Const
{
	public static class Direction
	{
		public static readonly float North = 4.712385f;
		public static readonly float East = 3.14159f;
		public static readonly float South = 1.570795f;
		public static readonly float West = 0f;

		// For Conenience
		public static readonly float Unknown = 999f;

		public static DirectionNames ParseFloatAsDirection(float pFloat)
		{
			if (pFloat == North) return DirectionNames.North;
			if (pFloat == East)	return DirectionNames.East;
			if (pFloat == South) return DirectionNames.South;
			if (pFloat == West) return DirectionNames.West;

			return DirectionNames.Unknown;
		}

		public static float ParseDirectionAsFloat(DirectionNames pDirection)
		{
			if (pDirection == DirectionNames.North) return North;
			if (pDirection == DirectionNames.East) return East;
			if (pDirection == DirectionNames.South) return South;
			if (pDirection == DirectionNames.West) return West;

			return Unknown;
		}
	}

	public enum DirectionNames : int
	{
		Unknown = 0,
		North,
		East,
		South,
		West,
	}
}
