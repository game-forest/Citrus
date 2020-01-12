using System;
using Yuzu;

namespace Lime.Widgets.PolygonMesh.Topology
{
	[YuzuCompact]
	public struct Edge : IEquatable<Edge>
	{
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
}
