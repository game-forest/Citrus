using System;
using Lime;
using StbSharp;

namespace NanoVG
{
	public static unsafe class NvgUtility
	{
		internal static float sqrtf(float a)
		{
			return (float)(Math.Sqrt((float)(a)));
		}

		internal static float sinf(float a)
		{
			return (float)(Math.Sin((float)(a)));
		}

		internal static float tanf(float a)
		{
			return (float)(Math.Tan((float)(a)));
		}

		internal static float atan2f(float a, float b)
		{
			return (float)(Math.Atan2(a, b));
		}

		internal static float cosf(float a)
		{
			return (float)(Math.Cos((float)(a)));
		}

		internal static float acosf(float a)
		{
			return (float)(Math.Acos((float)(a)));
		}

		internal static float ceilf(float a)
		{
			return (float)(Math.Ceiling((float)(a)));
		}
		
		internal static int __mini(int a, int b)
		{
			return (int)((a) < (b) ? a : b);
		}

		internal static int __maxi(int a, int b)
		{
			return (int)((a) > (b) ? a : b);
		}

		internal static int __clampi(int a, int mn, int mx)
		{
			return (int)((a) < (mn) ? mn : ((a) > (mx) ? mx : a));
		}

		internal static float __minf(float a, float b)
		{
			return (float)((a) < (b) ? a : b);
		}

		internal static float __maxf(float a, float b)
		{
			return (float)((a) > (b) ? a : b);
		}

		internal static float __absf(float a)
		{
			return (float)((a) >= (0.0f) ? a : -a);
		}

		internal static float __signf(float a)
		{
			return (float)((a) >= (0.0f) ? 1.0f : -1.0f);
		}

		internal static float __clampf(float a, float mn, float mx)
		{
			return (float)((a) < (mn) ? mn : ((a) > (mx) ? mx : a));
		}

		internal static float __cross(float dx0, float dy0, float dx1, float dy1)
		{
			return (float)(dx1 * dy0 - dx0 * dy1);
		}

		internal static float __normalize(float* x, float* y)
		{
			float d = (float)(sqrtf((float)((*x) * (*x) + (*y) * (*y))));
			if ((d) > (1e-6f))
			{
				float id = (float)(1.0f / d);
				*x *= (float)(id);
				*y *= (float)(id);
			}

			return (float)(d);
		}
	}
}