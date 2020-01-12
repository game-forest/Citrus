using System;
using Yuzu;

namespace Lime.Widgets.PolygonMesh.Topology
{
	[YuzuCompact]
	public struct Face : IEquatable<Face>
	{
		[YuzuMember("0")]
		public ushort Index0;

		[YuzuMember("1")]
		public ushort Index1;

		[YuzuMember("2")]
		public ushort Index2;

		public int this[int index]
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
	}
}
