using System;
using System.Runtime.InteropServices;

namespace StbSharp
{
	internal static unsafe class CRuntime
	{
		public static void* malloc(ulong size)
		{
			return malloc((long) size);
		}

		public static void* malloc(long size)
		{
			var ptr = Marshal.AllocHGlobal((int) size);

			return ptr.ToPointer();
		}
		
		public static void memcpy(void* a, void* b, ulong size)
		{
			Lime.GraphicsUtility.CopyMemory((IntPtr)a, (IntPtr)b, (int)size);
		}
		
		public static void free(void* a)
		{
			var ptr = new IntPtr(a);
			Marshal.FreeHGlobal(ptr);
		}

		public static void memset(void* ptr, int value, ulong size)
		{
			Lime.GraphicsUtility.FillMemory((IntPtr)ptr, value, (int)size);
		}
		
		public static void* realloc(void* a, long newSize)
		{
			if (a == null)
			{
				return malloc(newSize);
			}

			var ptr = new IntPtr(a);
			var result = Marshal.ReAllocHGlobal(ptr, new IntPtr(newSize));

			return result.ToPointer();
		}

		public static void* realloc(void* a, ulong newSize)
		{
			return realloc(a, (long) newSize);
		}

		public static int abs(int v)
		{
			return Math.Abs(v);
		}

		public static double pow(double a, double b)
		{
			return Math.Pow(a, b);
		}

		public static float fabs(double a)
		{
			return (float) Math.Abs(a);
		}

		public static double ceil(double a)
		{
			return Math.Ceiling(a);
		}

		public static double floor(double a)
		{
			return Math.Floor(a);
		}

		public static double log(double value)
		{
			return Math.Log(value);
		}

		public static double exp(double value)
		{
			return Math.Exp(value);
		}

		public static double cos(double value)
		{
			return Math.Cos(value);
		}

		public static double acos(double value)
		{
			return Math.Acos(value);
		}

		public static double sin(double value)
		{
			return Math.Sin(value);
		}

		public static double ldexp(double number, int exponent)
		{
			return number * Math.Pow(2, exponent);
		}

		public static double sqrt(double val)
		{
			return Math.Sqrt(val);
		}

		public static double fmod(double x, double y)
		{
			return x % y;
		}

		public static ulong strlen(sbyte* str)
		{
			ulong res = 0;
			var ptr = str;

			while (*ptr != '\0')
			{
				ptr++;
			}

			return ((ulong) ptr - (ulong) str - 1);
		}
	}
}