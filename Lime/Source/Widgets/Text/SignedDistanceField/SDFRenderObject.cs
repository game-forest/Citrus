using System.Collections;
using System.Collections.Generic;
#if PROFILER
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime.SignedDistanceField
{
	internal class SDFRenderObject : TextRenderObject
	{
		public Sprite.IMaterialProvider MaterialProvider;
		public Vector2 Offset;

		public override void Render()
		{
			Renderer.Transform1 = Matrix32.Translation(Offset) * LocalToWorldTransform;
			SpriteList.Render(Color, MaterialProvider);
		}
	}

	public class SDFRenderObjectList : RenderObject
	{
		public List<TextRenderObject> Objects = new List<TextRenderObject>();

		public override void Render()
		{
			foreach (var ro in Objects) {
#if PROFILER
				RenderObjectOwnerInfo.PushState(ro.OwnerInfo);
#endif // PROFILER
				ro.Render();
#if PROFILER
				RenderObjectOwnerInfo.PopState();
#endif // PROFILER
			}
		}

		protected override void OnRelease()
		{
			foreach (var ro in Objects) {
				ro.Release();
			}
			Objects.Clear();
			base.OnRelease();
		}
	}
}
