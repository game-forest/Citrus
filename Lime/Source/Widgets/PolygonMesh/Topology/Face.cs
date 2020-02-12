using System;
using Yuzu;

namespace Lime.Widgets.PolygonMesh.Topology
{
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
	}
}
