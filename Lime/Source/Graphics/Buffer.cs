using System;
using Lime.Graphics.Platform;

namespace Lime
{
	public unsafe class Buffer : IDisposable
	{
		private IPlatformBuffer platformBuffer;

		public BufferType Type { get; private set; }
		public int Size { get; private set; }
		public bool Dynamic { get; private set; }
		public bool IsDisposed { get; private set; }

		public Buffer(BufferType type, int size, bool dynamic)
		{
			Type = type;
			Size = size;
			Dynamic = dynamic;
		}

		~Buffer()
		{
			DisposeInternal();
		}

		internal IPlatformBuffer GetPlatformBuffer()
		{
			if (platformBuffer == null) {
				platformBuffer = PlatformRenderer.Context.CreateBuffer(Type, Size, Dynamic);
			}
			return platformBuffer;
		}

		public void SetData(int offset, IntPtr data, int size, BufferSetDataMode mode)
		{
			GetPlatformBuffer().SetData(offset, data, size, mode);
		}

		public void SetData<T>(int offset, T[] data, int startIndex, int count, BufferSetDataMode mode) where T : unmanaged
		{
			fixed (T* pData = data) {
				SetData(offset, new IntPtr(pData) + startIndex * sizeof(T), count * sizeof(T), mode);
			}
		}

		public void Dispose()
		{
			DisposeInternal();
			IsDisposed = true;
			GC.SuppressFinalize(this);
		}

		private void DisposeInternal()
		{
			if (platformBuffer != null) {
				var platformBufferCopy = platformBuffer;
				Window.Current.InvokeOnRendering(() => {
					platformBufferCopy.Dispose();
				});
				platformBuffer = null;
			}
		}
	}

	public enum BufferType
	{
		Vertex,
		Index
	}

	public enum BufferSetDataMode
	{
		Default,
		Discard
	}
}
