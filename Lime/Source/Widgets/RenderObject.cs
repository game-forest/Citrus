using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
#if PROFILER
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime
{
	public abstract class RenderObject
	{
		internal bool Free = true;

#if PROFILER
		public RenderObjectOwnerInfo OwnerInfo;
#endif // PROFILER

		public abstract void Render();

		public void Release()
		{
			if (Free) return;
			try {
				OnRelease();
			} finally {
				Free = true;
			}
		}

		protected virtual void OnRelease() { }
	}

	public class RenderObjectList : IReadOnlyList<RenderObject>, IEnumerable<RenderObject>
	{
		private List<RenderObject> objects = new List<RenderObject>();

		public int Count => objects.Count;

		public RenderObject this[int index] => objects[index];

		public void Add(RenderObject obj)
		{
			objects.Add(obj);
		}

		public void Clear()
		{
			foreach (var obj in objects) {
				obj.Release();
			}
			objects.Clear();
		}

		public void Render()
		{
			foreach (var ro in objects) {
				ro.Render();
			}
		}

		public List<RenderObject>.Enumerator GetEnumerator() => objects.GetEnumerator();

		IEnumerator<RenderObject> IEnumerable<RenderObject>.GetEnumerator() => GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
	}

	public abstract class WidgetRenderObject : RenderObject
	{
		public Matrix32 LocalToWorldTransform;
		public Blending Blending;
		public ShaderId Shader;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public void CaptureRenderState(Widget widget)
		{
			LocalToWorldTransform = widget.LocalToWorldTransform;
			Blending = widget.GlobalBlending;
			Shader = widget.GlobalShader;
		}

		protected void PrepareRenderState()
		{
			Renderer.Transform1 = LocalToWorldTransform;
			Renderer.Blending = Blending;
			Renderer.Shader = Shader;
		}
	}

	public static class RenderObjectPool<T> where T: RenderObject, new()
	{
		private static T[] items = new T[1] { new T() };
		private static int index;

		public static T Acquire()
		{
			for (int i = 0; i < items.Length; i++) {
				var item = items[index++];
				if (index == items.Length)
					index = 0;
				if (item.Free) {
					item.Free = false;
#if PROFILER
					item.OwnerInfo.Reset();
#endif // PROFILER
					return item;
				}
			}
			System.Array.Resize(ref items, items.Length * 2);
			index = items.Length / 2;
			for (int i = index; i < items.Length; i++) {
				items[i] = new T();
			}
			items[index].Free = false;
#if PROFILER
			items[index].OwnerInfo.Reset();
#endif // PROFILER
			return items[index];
		}
	}
}
