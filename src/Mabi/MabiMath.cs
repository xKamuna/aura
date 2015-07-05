// Copyright (c) Aura development team - Licensed under GNU GPL
// For more information, see license file in the main folder

using System;

namespace Aura.Mabi
{
	public static class MabiMath
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

		/// <summary>
		/// Converts Mabi's byte direction into a radian.
		/// </summary>
		/// <remarks>
		/// While entity packets use a byte from 0-255 for the direction,
		/// props are using radian floats.
		/// </remarks>
		/// <param name="direction"></param>
		/// <returns></returns>
		public static float ByteToRadian(byte direction)
		{
			return (float)Math.PI * 2 / 255 * direction;
		}

		/// <summary>
		/// Converts vector direction into Mabi's byte direction.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static byte DirectionToByte(double x, double y)
		{
			return (byte)(Math.Floor(Math.Atan2(y, x) / 0.02454369260617026));
		}

		/// <summary>
		/// Converts degree into Mabi's byte direction.
		/// </summary>
		/// <param name="degree"></param>
		/// <returns></returns>
		public static byte DegreeToByte(int degree)
		{
			return (byte)(degree * 255 / 360);
		}

		/// <summary>
		/// Converts vector direction into a radian.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		public static float DirectionToRadian(double x, double y)
		{
			return ByteToRadian(DirectionToByte(x, y));
		}

		/// <summary>
		/// Converts degree to radian.
		/// </summary>
		/// <param name="degree"></param>
		/// <returns></returns>
		public static float DegreeToRadian(int degree)
		{
			return (float)(Math.PI / 180f * degree);
		}
		/// <summary>
		/// Converts Mabi's byte direction into vector direction.
		/// </summary>
		/// <param name="directionByte"></param>
		/// <returns></returns>
		public static Vector2 ByteToDirection(byte directionByte)
		{
			float theta = ByteToRadian(directionByte); //Direction as radian, just makes the byte go into radian form.  2*pi*r for circumference, 255 because byte goes from 0-255, etc etc.
			Vector2 directionVector = new Vector2(Math.Cos(theta), Math.Sin(theta));  //Unitized direction vector.  May be optimized to retrieve common values from a Dictionary, so we don't have to use cos and sin (which can be expensive).
			return directionVector;
		}

		/// <summary>
		/// Calculates the stat bonus for eating food.
		/// </summary>
		/// <remarks>
		/// Formula: (Stat Boost * Hunger Filled) / (Hunger Fill * 20 * Current Age of Character)
		/// Reference: http://wiki.mabinogiworld.com/view/Food_List
		/// </remarks>
		/// <param name="boost"></param>
		/// <param name="hunger"></param>
		/// <param name="hungerFilled"></param>
		/// <param name="age"></param>
		/// <returns></returns>
		public static float FoodStatBonus(double boost, double hunger, double hungerFilled, int age)
		{
			return (float)((boost * hungerFilled) / (hunger * 20 * age));
		}
	}
}
