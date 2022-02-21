using System;

namespace Lime.NanoVG
{
	internal unsafe class PathCache : IDisposable
	{
		public readonly Buffer<Path> Paths = new Buffer<Path>(16);
		public readonly Buffer<Vertex> Vertices = new Buffer<Vertex>(256);
		public Rectangle Bounds;
		public Point* Points = (Point*)RawMemory.Allocate(sizeof(Point) * 128);
		public int PointsNumber;
		public int PointsCount;

		public PathCache()
		{
			PointsNumber = 0;
			PointsCount = 128;
		}

		public void Dispose()
		{
			if (Points != null) {
				RawMemory.Free(Points);
				Points = null;
			}
		}
	}
}
