using System;
using System.Runtime.InteropServices;
using System.Threading;
using Yuzu;

namespace Lime
{
	[YuzuCompact]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 20)]
	public struct SHA1
	{
		[YuzuMember("0")]
		public ulong A;

		[YuzuMember("1")]
		public ulong B;

		[YuzuMember("2")]
		public uint C;

		private static readonly ThreadLocal<System.Security.Cryptography.SHA1> sha1 =
			new ThreadLocal<System.Security.Cryptography.SHA1>(System.Security.Cryptography.SHA1.Create);

		public static unsafe SHA1 Compute<T>(params T[] source) where T: struct
		{
			var result = new SHA1();
			if (!sha1.Value.TryComputeHash(
				MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>(source)),
				new Span<byte>((byte*)&result, sizeof(SHA1)),
				out var written) ||
				written != sizeof(SHA1))
			{
				throw new InvalidOperationException();
			}
			return result;
		}

		public static bool operator ==(SHA1 lhs, SHA1 rhs)
		{
			return lhs.A == rhs.A && lhs.B == rhs.B && lhs.C == rhs.C;
		}

		public static bool operator !=(SHA1 lhs, SHA1 rhs)
		{
			return !(lhs == rhs);
		}
	}
}
