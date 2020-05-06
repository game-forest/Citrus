using System;
using Yuzu;

namespace Lime.Widgets.PolygonMesh.Topology
{
	public interface ITopologyPrimitive
	{
		ushort this[int index] { get; }
		int Count { get; }
	}

	public struct Vertex : ITopologyPrimitive
	{
		public ushort Index;
		public ushort this[int index] => Index;
		public int Count => 1;
	}

	[YuzuCompact]
	public struct Edge : IEquatable<Edge>, ITopologyPrimitive
	{
		public class EdgeInfo
		{
			public bool IsConstrained;
			public bool IsFraming;
		}

		[YuzuMember("0")]
		public ushort Index0;

		[YuzuMember("1")]
		public ushort Index1;

		public ushort this[int index]
		{
			get
			{
				switch (index) {
					case 0:
						return Index0;
					case 1:
						return Index1;
				}
				throw new IndexOutOfRangeException();
			}
		}

		public int Count => 2;

		public Edge(ushort index0, ushort index1)
		{
			Index0 = index0;
			Index1 = index1;
		}

		public bool Equals(Edge other)
		{
			return
				(Index0 == other.Index0 && Index1 == other.Index1) ||
				(Index0 == other.Index1 && Index1 == other.Index0);
		}

		public override int GetHashCode()
		{
			return
				(Index0, Index1).GetHashCode() +
				(Index1, Index0).GetHashCode();
		}
	}

	[YuzuCompact]
	public struct Face : IEquatable<Face>, ITopologyPrimitive
	{
		public class FaceInfo
		{
			public bool IsConstrained0;
			public bool IsConstrained1;
			public bool IsConstrained2;
			public bool IsFraming0;
			public bool IsFraming1;
			public bool IsFraming2;

			public (bool IsFraming, bool IsConstrained) this[int index]
			{
				get
				{
					switch (index) {
						case 0:
							return (IsFraming0, IsConstrained0);
						case 1:
							return (IsFraming1, IsConstrained1);
						case 2:
							return (IsFraming2, IsConstrained2);
					}
					throw new IndexOutOfRangeException();
				}
			}
		}

		public Face(ushort index0, ushort index1, ushort index2)
		{
			Index0 = index0;
			Index1 = index1;
			Index2 = index2;
		}

		[YuzuMember("0")]
		public ushort Index0;

		[YuzuMember("1")]
		public ushort Index1;

		[YuzuMember("2")]
		public ushort Index2;

		public ushort this[int index]
		{
			get
			{
				switch (index) {
					case 0:
						return Index0;
					case 1:
						return Index1;
					case 2:
						return Index2;
				}
				throw new IndexOutOfRangeException();
			}
		}

		public int Count => 3;

		public bool Equals(Face other) =>
			Index0 == other.Index0 &&
			Index1 == other.Index1 &&
			Index2 == other.Index2 ||

			Index0 == other.Index0 &&
			Index1 == other.Index2 &&
			Index2 == other.Index1 ||

			Index1 == other.Index1 &&
			Index0 == other.Index2 &&
			Index2 == other.Index0 ||

			Index2 == other.Index2 &&
			Index0 == other.Index1 &&
			Index1 == other.Index0;

		public override int GetHashCode()
		{
			return
				(Index0, Index1, Index2).GetHashCode() +
				(Index2, Index0, Index1).GetHashCode() +
				(Index1, Index2, Index0).GetHashCode();
		}

		public override string ToString() => $"{Index0}, {Index1}, {Index2}";
	}
}
