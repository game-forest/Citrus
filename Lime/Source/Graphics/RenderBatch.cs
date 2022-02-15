using System;
using System.Collections.Generic;
#if PROFILER
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime
{
	public interface IRenderBatch
	{
		int LastVertex { get; set; }
		int StartIndex { get; set; }
		int LastIndex { get; set; }
		ITexture Texture1 { get; set; }
		ITexture Texture2 { get; set; }
		IMaterial Material { get; set; }
		void Render();
		void Release();
	}

	public class RenderBatch<TVertex> : IRenderBatch
		where TVertex : unmanaged
	{
		public const int MinVertices = 400;
		public const int MinIndices = 600;
		public const int MaxIndices = ushort.MaxValue;

		private static Stack<RenderBatch<TVertex>> batchPool = new Stack<RenderBatch<TVertex>>();
		private static List<Mesh<TVertex>> meshPool = new List<Mesh<TVertex>>();
		private bool ownsMesh;

		public ITexture Texture1 { get; set; }
		public ITexture Texture2 { get; set; }
		public IMaterial Material { get; set; }
		public int LastVertex { get; set; }
		public int StartIndex { get; set; }
		public int LastIndex { get; set; }
		public Mesh<TVertex> Mesh { get; set; }

#if PROFILER
		public RenderBatchProfilingInfo ProfilingInfo;
#endif // PROFILER

		private void Clear()
		{
			Texture1 = null;
			Texture2 = null;
			Material = null;
			StartIndex = LastIndex = LastVertex = 0;
			if (Mesh != null) {
				if (ownsMesh) {
					ReleaseMesh(Mesh);
				}
				Mesh = null;
			}
			ownsMesh = false;
		}

		public void Render()
		{
#if PROFILER
			if (ProfilingInfo.IsInsideOverdrawMaterialScope) {
				OverdrawMaterialScope.Enter();
			}
#endif // PROFILER
			PlatformRenderer.SetTexture(0, Texture1);
			PlatformRenderer.SetTexture(1, Texture2);
			for (int i = 0; i < Material.PassCount; i++) {
				Material.Apply(i);
				Mesh.DrawIndexed(StartIndex, LastIndex - StartIndex);
			}
#if PROFILER
			if (ProfilingInfo.IsInsideOverdrawMaterialScope) {
				OverdrawMaterialScope.Leave();
			}
#endif // PROFILER
		}

		public static RenderBatch<TVertex> Acquire(RenderBatch<TVertex> origin, int vertexCount, int indexCount)
		{
			var batch = batchPool.Count == 0 ? new RenderBatch<TVertex>() : batchPool.Pop();
			if (
				origin != null
				&& origin.LastVertex + vertexCount <= origin.Mesh.Vertices.Length
				&& origin.LastIndex + indexCount <= origin.Mesh.Indices.Length
			) {
				batch.Mesh = origin.Mesh;
				batch.StartIndex = origin.LastIndex;
				batch.LastVertex = origin.LastVertex;
				batch.LastIndex = origin.LastIndex;
			} else {
				batch.ownsMesh = true;
				batch.Mesh = AcquireMesh(vertexCount, indexCount);
			}
#if PROFILER
			batch.ProfilingInfo.Initialize();
#endif // PROFILER
			return batch;
		}

		public void Release()
		{
			Clear();
			batchPool.Push(this);
		}

		private static Mesh<TVertex> AcquireMesh(int vertexCount, int indexCount)
		{
			if (indexCount > MaxIndices) {
				throw new InvalidOperationException("Too many indices");
			}
			Mesh<TVertex> mesh;
			for (int i = meshPool.Count - 1; i >= 0; i--) {
				mesh = meshPool[i];
				if (mesh.Vertices.Length >= vertexCount && mesh.Indices.Length >= indexCount) {
					Toolbox.Swap(meshPool, i, meshPool.Count - 1);
					meshPool.RemoveAt(meshPool.Count - 1);
					mesh.VertexCount = 0;
					mesh.IndexCount = 0;
					return mesh;
				}
			}
			vertexCount = Math.Max(vertexCount * 2, MinVertices);
			indexCount = Math.Clamp(indexCount * 2, MinIndices, ushort.MaxValue);
			mesh = new Mesh<TVertex> {
				Vertices = new TVertex[vertexCount],
				Indices = new ushort[indexCount],
				AttributeLocations = new int[] {
					ShaderPrograms.Attributes.Pos1,
					ShaderPrograms.Attributes.Color1,
					ShaderPrograms.Attributes.UV1,
					ShaderPrograms.Attributes.UV2,
				},
				VertexCount = 0,
				IndexCount = 0,
			};
			return mesh;
		}

		private static void ReleaseMesh(Mesh<TVertex> item)
		{
			meshPool.Add(item);
		}
	}
}
