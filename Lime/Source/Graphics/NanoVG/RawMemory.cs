using System;
using System.Runtime.InteropServices;

namespace Lime.NanoVG
{
	internal static unsafe class RawMemory
	{
		public static void* Allocate(int size)
		{
			return Marshal.AllocHGlobal(size).ToPointer();
		}

		public static void Free(void* a)
		{
			Marshal.FreeHGlobal(new IntPtr(a));
		}

		public static void CopyMemory(void* a, void* b, int size)
		{
			GraphicsUtility.CopyMemory((IntPtr)a, (IntPtr)b, size);
		}

		public static void FillMemory(void* ptr, int value, int size)
		{
			GraphicsUtility.FillMemory((IntPtr)ptr, value, size);
		}

		public static void* Realloc(void* a, int newSize)
		{
			if (a == null) {
				return Allocate(newSize);
			}
			return Marshal.ReAllocHGlobal(new IntPtr(a), new IntPtr(newSize)).ToPointer();
		}
	}
}
