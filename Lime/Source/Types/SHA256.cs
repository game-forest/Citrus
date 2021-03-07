using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Yuzu;

namespace Lime
{
	[YuzuCompact]
	[StructLayout(LayoutKind.Sequential, Pack = 1, Size = 32)]
	public struct SHA256 : IEquatable<SHA256>, IComparable<SHA256>
	{
		[YuzuMember("0")]
		public ulong A;

		[YuzuMember("1")]
		public ulong B;

		[YuzuMember("2")]
		public ulong C;

		[YuzuMember("3")]
		public ulong D;

		private static readonly ThreadLocal<System.Security.Cryptography.SHA256> sha256
			= new ThreadLocal<System.Security.Cryptography.SHA256>(System.Security.Cryptography.SHA256.Create);

		private static readonly ThreadLocal<UTF8Encoding> utf8 =
			new ThreadLocal<UTF8Encoding>(() => new UTF8Encoding(
				encoderShouldEmitUTF8Identifier: false,
				throwOnInvalidBytes: true
			));

		public static unsafe SHA256 Compute(string source)
		{
			const int MaxStackSize = 256;
			var bufferSize = utf8.Value.GetByteCount(source);
			byte[] rentedFromPool = null;
			var buffer =
				bufferSize > MaxStackSize
					? new Span<byte>(rentedFromPool = ArrayPool<byte>.Shared.Rent(bufferSize), 0, bufferSize)
					: stackalloc byte[bufferSize];
			try {
				utf8.Value.GetBytes(source.AsSpan(), buffer);
				var result = new SHA256();
				if (
					!sha256.Value.TryComputeHash(
						buffer, new Span<byte>((byte*) &result, sizeof(SHA256)), out var written
					) || written != sizeof(SHA256)
				) {
					throw new InvalidOperationException();
				}
				return result;
			} finally {
				if (rentedFromPool != null) {
					ArrayPool<byte>.Shared.Return(rentedFromPool);
				}
			}
		}

		public static unsafe SHA256 Compute<T>(T[] source, int start, int length) where T: unmanaged
		{
			var result = new SHA256();
			if (!sha256.Value.TryComputeHash(
					MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>(source, start, length)),
					new Span<byte>((byte*)&result, sizeof(SHA256)),
					out var written
				) || written != sizeof(SHA256)
			) {
				throw new InvalidOperationException();
			}
			return result;
		}

		public static unsafe SHA256 Compute<T>(params T[] source) where T: unmanaged
		{
			var result = new SHA256();
			if (!sha256.Value.TryComputeHash(
					MemoryMarshal.Cast<T, byte>(new ReadOnlySpan<T>(source)),
					new Span<byte>((byte*)&result, sizeof(SHA256)),
					out var written
				) || written != sizeof(SHA256)
			) {
				throw new InvalidOperationException();
			}
			return result;
		}

		public static bool operator ==(SHA256 lhs, SHA256 rhs)
		{
			return lhs.A == rhs.A && lhs.B == rhs.B && lhs.C == rhs.C && lhs.D == rhs.D;
		}

		public static bool operator !=(SHA256 lhs, SHA256 rhs) => !(lhs == rhs);

		public bool Equals(SHA256 other)
		{
			return A == other.A && B == other.B && C == other.C && D == other.D;
		}

		public override bool Equals(object obj) => obj is SHA256 other && Equals(other);

		public int CompareTo(SHA256 other)
		{
			if (A != other.A) {
				return A.CompareTo(other.A);
			}
			if (B != other.B) {
				return B.CompareTo(other.B);
			}
			if (C != other.C) {
				return C.CompareTo(other.C);
			}
			return D.CompareTo(other.D);
		}

		public override int GetHashCode() => HashCode.Combine(A, B, C, D);
	}
}
