using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class BoneAsistantPresenter
	{
		private readonly SceneView sv;
		private const float RectSize = 15;

		public BoneAsistantPresenter(SceneView sceneView)
		{
			sv = sceneView;
			sceneView.Frame.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
		}

		void Render(Widget canvas)
		{
			if (!Document.Current.PreviewScene) {
				var helper = SceneView.Instance.Components.Get<CreateBoneHelper>();
				if (helper != null && helper.HitTip != null) {
					var t = sv.CalcTransitionFromSceneSpace(canvas);
					var hull = new Rectangle(helper.HitTip.Value * t, helper.HitTip.Value * t)
						.ExpandedBy(new Thickness(RectSize))
						.ToQuadrangle();
					for (int i = 0; i < 4; i++) {
						var a = hull[i];
						var b = hull[(i + 1) % 4];
						Renderer.DrawLine(a, b, Color4.Green);
					}
				}
			}
		}
	}
}
