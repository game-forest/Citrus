using System;
using System.Linq;
using System.Runtime.InteropServices;
using Lime;

namespace Orange.FbxImporter
{
	[StructLayout(LayoutKind.Sequential)]
	public class Vec2
	{
		[MarshalAs(UnmanagedType.R4)]
		public float V1;

		[MarshalAs(UnmanagedType.R4)]
		public float V2;
	}

	[StructLayout(LayoutKind.Sequential)]
	public class Vec3 : Vec2
	{
		[MarshalAs(UnmanagedType.R4)]
		public float V3;
	}

	[StructLayout(LayoutKind.Sequential)]
	public class Vec4 : Vec3
	{
		[MarshalAs(UnmanagedType.R4)]
		public float V4;
	}

	[StructLayout(LayoutKind.Sequential)]
	public class Mat4x4
	{
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
		public double[] Data;

		public Matrix44 ToLime()
		{
			var values = Data.Select(v => (float)v).ToArray();
			return new Matrix44(
				m11: values[0],
				m12: values[1],
				m13: values[2],
				m14: values[3],
				m21: values[4],
				m22: values[5],
				m23: values[6],
				m24: values[7],
				m31: values[8],
				m32: values[9],
				m33: values[10],
				m34: values[11],
				m41: values[12],
				m42: values[13],
				m43: values[14],
				m44: values[15]
			);
		}
	}

	[StructLayout(LayoutKind.Sequential)]
	public class SizedArray
	{
		public uint Size;

		public IntPtr Array;
	}

	public static class FbxExtensions
	{
		public static T[] GetData<T>(this SizedArray sizedArray)
		{
			if (typeof(T) == typeof(int)) {
				return (T[])Convert.ChangeType(sizedArray.Array.ToIntArray((int)sizedArray.Size), typeof(T[]));
			}

			if (typeof(T) == typeof(double)) {
				return (T[])Convert.ChangeType(sizedArray.Array.ToFloatArray((int)sizedArray.Size), typeof(T[]));
			}

			if (typeof(T) == typeof(float)) {
				return (T[])Convert.ChangeType(sizedArray.Array.ToDoubleArray((int)sizedArray.Size), typeof(T[]));
			}

			if (typeof(T).IsClass) {
				return sizedArray.Array.ToStructArray<T>((int)sizedArray.Size);
			}

			return null;
		}

		public static Quaternion ToLimeQuaternion(this Vec4 vector)
		{
			return new Quaternion(vector.V1, vector.V2, vector.V3, vector.V4);
		}

		public static Color4 ToLimeColor(this Vec4 vector)
		{
			return Color4.FromFloats(vector.V1, vector.V2, vector.V3, vector.V4);
		}

		public static Vector4 ToLime(this Vec4 vector)
		{
			return new Vector4(vector.V1, vector.V2, vector.V3, vector.V4);
		}

		public static Vector3 ToLime(this Vec3 vector)
		{
			return new Vector3(vector.V1, vector.V2, vector.V3);
		}

		public static Vector2 ToLime(this Vec2 vector)
		{
			return new Vector2(vector.V1, vector.V2);
		}
	}
}
