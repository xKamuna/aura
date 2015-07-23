// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Aura.Mabi
{
	//Vector2 class for holding doubles.
	public class Vector2
	{
		public double X { get; set; }
		public double Y { get; set; }
		public Vector2()
		{
			X = 0;
			Y = 0;
		}
		public Vector2(double x, double y)
		{
			X = x;
			Y = y;
		}
		public double SquareLength()
		{
			return X * X + Y * Y;
		}
		public double Length()
		{
			return Math.Sqrt(SquareLength());
		}
		public double Normalize()
		{
			double length = Length();
			double inverseLength = 1.0f / length;
			X *= inverseLength;
			Y *= inverseLength;
			return length;
		}
		public double Dot(Vector2 rhs)
		{
			return (X * rhs.X + Y * rhs.Y);
		}
		/// <summary>
		/// Checks if a point is inside a cone.
		/// </summary>
		public static bool IsPointInsideCone(Vector2 origin, Vector2 direction, Vector2 point, double radianAngle, int maxDistance)
		{
			Vector2 distanceVector = new Vector2(point.X - origin.X, point.Y - origin.Y);

			double length = distanceVector.Normalize(); //Returns not the normalized distance vector, but the length of the vector.  A shortcut.

			if (length > maxDistance)
			{
				return false;
			}

			return (direction.Dot(distanceVector) >= Math.Cos(radianAngle));
		}
	}
}
