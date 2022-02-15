using System.Collections.Generic;
#if PROFILER
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime
{
	internal class RenderList
	{
		public readonly List<IRenderBatch> Batches = new List<IRenderBatch>();
		private IRenderBatch lastBatch;

		public bool Empty => lastBatch == null;

		public RenderBatch<TVertex> GetBatch<TVertex>(
			ITexture texture1, ITexture texture2, IMaterial material, int vertexCount, int indexCount
		)
			where TVertex : unmanaged
		{
			var atlas1 = texture1?.AtlasTexture;
			var atlas2 = texture2?.AtlasTexture;
			var b = lastBatch as RenderBatch<TVertex>;
			if (
				b == null
				|| b.Texture1 != atlas1
			    || b.Texture2 != atlas2
			    || b.Material != material
			    || b.Material.PassCount != 1
				|| b.LastVertex + vertexCount > b.Mesh.Vertices.Length
				|| b.LastIndex + indexCount > b.Mesh.Indices.Length
			) {
				b = RenderBatch<TVertex>.Acquire(b, vertexCount, indexCount);
				b.Texture1 = atlas1;
				b.Texture2 = atlas2;
				b.Material = material;
				Batches.Add(b);
				lastBatch = b;
			}
			var mesh = b.Mesh;
			mesh.VertexCount += vertexCount;
			mesh.IndexCount += indexCount;
#if PROFILER
			b.ProfilingInfo.ProcessNode(RenderObjectOwnerInfo.CurrentNode);
#endif // PROFILER
			return b;
		}

		public void Render()
		{
			foreach (var batch in Batches) {
				batch.Render();
			}
		}

		public void Clear()
		{
			if (lastBatch == null) {
				return;
			}
			foreach (var i in Batches) {
				i.Release();
			}
			Batches.Clear();
			lastBatch = null;
		}

		public void Flush()
		{
			if (lastBatch != null) {
				Render();
				Clear();
			}
		}
	}
}
