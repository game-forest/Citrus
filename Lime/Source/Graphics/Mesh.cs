using System;
using System.Collections.Generic;
using System.Numerics;
using System.Linq;
using System.Runtime.InteropServices;
using Yuzu;
using System.Runtime.CompilerServices;

namespace Lime
{
	internal static class MeshBufferPools
	{
		public static readonly BufferPool Vertex = new BufferPool(BufferType.Vertex, true, 64 * 1024);
		public static readonly BufferPool Index = new BufferPool(BufferType.Index, true, 64 * 1024);
	}

	[YuzuSpecializeWith(typeof(Lime.Mesh3D.Vertex))]
	public unsafe partial class Mesh<T> : IMesh, IDisposable where T : unmanaged
	{
		private bool disposed;

		[YuzuMember]
		public int[] AttributeLocations;

		[YuzuMember]
		public T[] Vertices;

		[YuzuMember]
		[TangerineKeyframeColor(21)]
		public ushort[] Indices;

		[YuzuMember]
		public PrimitiveTopology Topology = PrimitiveTopology.TriangleList;

		public int VertexCount = -1;
		public int IndexCount = -1;

		public MeshDirtyFlags DirtyFlags = MeshDirtyFlags.All;

		private VertexInputLayout inputLayout;
		private Buffer vertexBuffer;
		private Buffer indexBuffer;

		public void Dispose()
		{
			if (!disposed) {
				if (vertexBuffer != null) {
					var vertexBufferCopy = vertexBuffer;
					Window.Current.InvokeOnRendering(() => {
						MeshBufferPools.Vertex.ReleaseBuffer(vertexBufferCopy);
					});
					vertexBuffer = null;
				}
				if (indexBuffer != null) {
					var indexBufferCopy = indexBuffer;
					Window.Current.InvokeOnRendering(() => {
						MeshBufferPools.Index.ReleaseBuffer(indexBufferCopy);
					});
					indexBuffer = null;
				}
				disposed = true;
			}
		}

		public Mesh<T> ShallowClone()
		{
			return (Mesh<T>)MemberwiseClone();
		}

		IMesh IMesh.ShallowClone() => ShallowClone();

		public void Draw(int startVertex, int vertexCount)
		{
			PreDraw();
			PlatformRenderer.Draw(Topology, startVertex, vertexCount);
		}

		public void DrawIndexed(int startIndex, int indexCount, int baseVertex = 0)
		{
			PreDraw();
			PlatformRenderer.DrawIndexed(Topology, startIndex, indexCount, baseVertex);
		}

		private void PreDraw()
		{
			UpdateBuffers();
			UpdateInputLayout();
			PlatformRenderer.SetVertexInputLayout(inputLayout);
			PlatformRenderer.SetVertexBuffer(0, vertexBuffer, 0);
			PlatformRenderer.SetIndexBuffer(indexBuffer, 0, IndexFormat.Index16Bits);
		}

		public int GetEffectiveVertexCount()
		{
			return VertexCount >= 0 ? VertexCount : Vertices?.Length ?? 0;
		}

		public int GetEffectiveIndexCount()
		{
			return IndexCount >= 0 ? IndexCount : Indices?.Length ?? 0;
		}

		private void UpdateBuffers()
		{
			if ((DirtyFlags & MeshDirtyFlags.Vertices) != 0) {
				var vertexCount = GetEffectiveVertexCount();
				if (vertexBuffer == null || vertexBuffer.Size != vertexCount * sizeof(T)) {
					if (vertexBuffer != null) {
						MeshBufferPools.Vertex.ReleaseBuffer(vertexBuffer);
					}
					vertexBuffer = MeshBufferPools.Vertex.AcquireBuffer(vertexCount * sizeof(T));
				}
				vertexBuffer.SetData(0, Vertices, 0, vertexCount, BufferSetDataMode.Discard);
				DirtyFlags &= ~MeshDirtyFlags.Vertices;
			}
			if ((DirtyFlags & MeshDirtyFlags.Indices) != 0) {
				var indexCount = GetEffectiveIndexCount();
				if (indexBuffer == null || indexBuffer.Size != indexCount * sizeof(ushort)) {
					if (indexBuffer != null) {
						MeshBufferPools.Index.ReleaseBuffer(indexBuffer);
					}
					indexBuffer = MeshBufferPools.Index.AcquireBuffer(indexCount * sizeof(ushort));
				}
				indexBuffer.SetData(0, Indices, 0, indexCount, BufferSetDataMode.Discard);
				DirtyFlags &= ~MeshDirtyFlags.Indices;
			}
		}

		private void UpdateInputLayout()
		{
			if (inputLayout == null || (DirtyFlags & MeshDirtyFlags.AttributeLocations) != 0) {
				var bindings = new[] {
					new VertexInputLayoutBinding {
						Slot = 0,
						Stride = sizeof(T)
					}
				};
				var attributes = new List<VertexInputLayoutAttribute>();
				foreach (var elementDescription in GetElementDescriptions()) {
					attributes.Add(new VertexInputLayoutAttribute {
						Slot = 0,
						Location = AttributeLocations[attributes.Count],
						Offset = elementDescription.Offset,
						Format = elementDescription.Format,
					});
				}
				inputLayout = VertexInputLayout.New(bindings, attributes.ToArray());
				DirtyFlags &= ~MeshDirtyFlags.AttributeLocations;
			}
		}

		[ThreadStatic]
		private static ElementDescription[] elementDescriptions;

		private static ElementDescription[] GetElementDescriptions()
		{
			if (elementDescriptions == null) {
				elementDescriptions = GetElementDescriptionsFromReflection().ToArray();
			}
			return elementDescriptions;
		}

		private static IEnumerable<ElementDescription> GetElementDescriptionsFromReflection()
		{
			int offset = 0;
			var result = GetElementDescription(typeof(T), ref offset);
			if (result != null) {
				yield return result;
				yield break;
			}
			foreach (var field in typeof(T).GetFields()) {
				var attrs = field.GetCustomAttributes(typeof(FieldOffsetAttribute), false);
				if (attrs.Length > 0) {
					offset = (attrs[0] as FieldOffsetAttribute).Value;
				}
				result = GetElementDescription(field.FieldType, ref offset);
				if (result != null) {
					yield return result;
				} else {
					throw new InvalidOperationException();
				}
			}
		}

		private static ElementDescription GetElementDescription(Type type, ref int offset)
		{
			ElementDescription result = null;
			if (type == typeof(float)) {
				result = new ElementDescription { Format = Format.R32_SFloat, Offset = offset };
				offset += 4;
			} else if (type == typeof(Vector2)) {
				result = new ElementDescription { Format = Format.R32G32_SFloat, Offset = offset };
				offset += 8;
			} else if (type == typeof(Vector3)) {
				result = new ElementDescription { Format = Format.R32G32B32_SFloat, Offset = offset };
				offset += 12;
			} else if (type == typeof(Vector4)) {
				result = new ElementDescription { Format = Format.R32G32B32A32_SFloat, Offset = offset };
				offset += 16;
			} else if (type == typeof(Color4)) {
				result = new ElementDescription { Format = Format.R8G8B8A8_UNorm, Offset = offset };
				offset += 4;
			} else if (type == typeof(Mesh3D.BlendIndices)) {
				result = new ElementDescription { Format = Format.R32G32B32A32_SFloat, Offset = offset };
				offset += 16;
			} else if (type == typeof(Mesh3D.BlendWeights)) {
				result = new ElementDescription { Format = Format.R32G32B32A32_SFloat, Offset = offset };
				offset += 16;
			}
			return result;
		}

		private class ElementDescription
		{
			public Format Format;
			public int Offset;
		}
	}

	internal class BufferPool : IDisposable
	{
		private BufferType bufferType;
		private bool dynamic;
		private Stack<Buffer>[] pools;

		public BufferPool(BufferType bufferType, bool dynamic, int maxSize)
		{
			this.bufferType = bufferType;
			this.dynamic = dynamic;
			pools = new Stack<Buffer>[GetSizeClass(maxSize) + 1];
			for (var i = 0; i < pools.Length; i++) {
				pools[i] = new Stack<Buffer>();
			}
		}

		public void Dispose()
		{
			foreach (var p in pools) {
				while (p.Count > 0) {
					p.Pop().Dispose();
				}
			}
		}

		public Buffer AcquireBuffer(int size)
		{
			System.Diagnostics.Debug.Assert(size > 0);
			var sizeClass = GetSizeClass(size);
			if (sizeClass >= pools.Length) {
				return new Buffer(bufferType, size, dynamic);
			}
			if (pools[sizeClass].Count > 0) {
				return pools[sizeClass].Pop();
			}
			return new Buffer(bufferType, GetMaxSize(sizeClass), dynamic);
		}

		public void ReleaseBuffer(Buffer buffer)
		{
			if (buffer.Type != bufferType || buffer.Dynamic != dynamic) {
				throw new ArgumentException(nameof(buffer));
			}
			var sizeClass = GetSizeClass(buffer.Size);
			if (sizeClass < pools.Length) {
				if (buffer.Size != GetMaxSize(sizeClass)) {
					throw new ArgumentException(nameof(buffer));
				}
				pools[sizeClass].Push(buffer);
			} else {
				buffer.Dispose();
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetSizeClass(int size)
		{
			System.Diagnostics.Debug.Assert(size > 0);
			// TODO: Use BitOperations.LeadingZeroCount when .net6 is available:
			// return 32 - BitOperations.LeadingZeroCount(((uint)size - 1) >> 4);
			return Log2SoftwareFallback((size - 1) >> 4) + 1;
		}

		// https://stackoverflow.com/questions/10439242/count-leading-zeroes-in-an-int32/10439333#10439333
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static int Log2SoftwareFallback(int x)
		{
			unchecked {
				x |= x >> 1;
				x |= x >> 2;
				x |= x >> 4;
				x |= x >> 8;
				x |= x >> 16;
				// Count the ones: http://aggregate.org/MAGIC/#Population%20Count%20(Ones%20Count)
				x -= x >> 1 & 0x55555555;
				x = (x >> 2 & 0x33333333) + (x & 0x33333333);
				x = (x >> 4) + x & 0x0f0f0f0f;
				x += x >> 8;
				x += x >> 16;
				//subtract # of 1s from 32
				return (x & 0x0000003f) - 1;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private static int GetMaxSize(int sizeClass)
		{
			return 16 << sizeClass;
		}
	}
}
