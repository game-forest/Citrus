﻿using System;
using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	// TODO: Replace with Matrix32
	[StructLayout(LayoutKind.Sequential)]
	public struct Transform
	{
		public float T1, T2, T3, T4, T5, T6;

		public void Zero()
		{
			T1 = T2 = T3 = T4 = T5 = T6 = 0;
		}

		public void Set(Transform src)
		{
			T1 = src.T1;
			T2 = src.T2;
			T3 = src.T3;
			T4 = src.T4;
			T5 = src.T5;
			T6 = src.T6;
		}

		public void SetIdentity()
		{
			T1 = 1.0f;
			T2 = 0.0f;
			T3 = 0.0f;
			T4 = 1.0f;
			T5 = 0.0f;
			T6 = 0.0f;
		}

		public void SetTranslate(float tx, float ty)
		{
			T1 = 1.0f;
			T2 = 0.0f;
			T3 = 0.0f;
			T4 = 1.0f;
			T5 = tx;
			T6 = ty;
		}

		public void SetScale(float sx, float sy)
		{
			T1 = sx;
			T2 = 0.0f;
			T3 = 0.0f;
			T4 = sy;
			T5 = 0.0f;
			T6 = 0.0f;
		}

		public void SetRotate(float a)
		{
			var cs = MathF.Cos(a);
			var sn = MathF.Sin(a);
			T1 = cs;
			T2 = sn;
			T3 = -sn;
			T4 = cs;
			T5 = 0.0f;
			T6 = 0.0f;
		}

		public void SetSkewX(float a)
		{
			T1 = 1.0f;
			T2 = 0.0f;
			T3 = MathF.Tan(a);
			T4 = 1.0f;
			T5 = 0.0f;
			T6 = 0.0f;
		}

		public void SetSkewY(float a)
		{
			T1 = 1.0f;
			T2 = MathF.Tan(a);
			T3 = 0.0f;
			T4 = 1.0f;
			T5 = 0.0f;
			T6 = 0.0f;
		}

		public void Multiply(ref Transform s)
		{
			var t0 = T1 * s.T1 + T2 * s.T3;
			var t2 = T3 * s.T1 + T4 * s.T3;
			var t4 = T5 * s.T1 + T6 * s.T3 + s.T5;
			T2 = T1 * s.T2 + T2 * s.T4;
			T4 = T3 * s.T2 + T4 * s.T4;
			T6 = T5 * s.T2 + T6 * s.T4 + s.T6;
			T1 = t0;
			T3 = t2;
			T5 = t4;
		}

		public void Premultiply(ref Transform s)
		{
			var s2 = s;
			s2.Multiply(ref this);
			Set(s2);
		}

		public Transform BuildInverse()
		{
			var det = T1 * T4 - T3 * T2;
			var inv = new Transform();
			if (det > -1e-6 && det < 1e-6) {
				inv.SetIdentity();
				return inv;
			}
			var inverseDeterminant = 1.0f / det;
			inv.T1 = T4 * inverseDeterminant;
			inv.T3 = -T3 * inverseDeterminant;
			inv.T5 = (T3 * T6 - T4 * T5) * inverseDeterminant;
			inv.T2 = -T2 * inverseDeterminant;
			inv.T4 = T1 * inverseDeterminant;
			inv.T6 = (T2 * T5 - T1 * T6) * inverseDeterminant;
			return inv;
		}

		public void TransformPoint(out float dx, out float dy, float sx, float sy)
		{
			dx = sx * T1 + sy * T3 + T5;
			dy = sx * T2 + sy * T4 + T6;
		}
	}
}