using System;
using StbSharp;
using Lime;

namespace NanoVG
{
	internal unsafe class PathCache : IDisposable
	{
		public readonly Buffer<Path> Paths = new Buffer<Path>(16);
		public readonly Buffer<Vertex> Vertexes = new Buffer<Vertex>(256);
		public FontStashSharp.Bounds Bounds = new FontStashSharp.Bounds();
		public NvgPoint* Points = (NvgPoint*)CRuntime.malloc((ulong)(sizeof(NvgPoint) * 128));
		public int PointsNumber, PointsCount;

		public PathCache()
		{
			PointsNumber = 0;
			PointsCount = 128;
		}

		public void Dispose()
		{
			if (Points != null)
			{
				CRuntime.free(Points);
				Points = null;
			}
		}
	}
}