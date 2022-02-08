using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TooltipProcessor : ITaskProvider
	{
		private static Tooltip Tooltip => Tooltip.Instance;

#if MAC || MONOMAC
		private static float offsetCoefficient = -2f;
#else
		private static float offsetCoefficient = 1f;
#endif

		private static Node NodeUnderMouse
		{
			get
			{
				if (Application.WindowUnderMouse == null) {
					return null;
				}
				using (Application.WindowUnderMouse.Context.Activate().Scoped()) {
					return WidgetContext.Current.NodeUnderMouse;
				}
			}
		}

		public IEnumerator<object> Task()
		{
			while (true) {
				yield return null;
				var node = NodeUnderMouse;
				var component = node?.Components.Get<TooltipComponent>();
				if (string.IsNullOrEmpty(component?.GetText?.Invoke())) {
					continue;
				}
				bool IsNodeChanged() => node != NodeUnderMouse;
				yield return Tooltip.Delay(component.ShowDelay, IsNodeChanged);
				if (!IsNodeChanged()) {
					if (Tooltip.IsVisible) {
						Tooltip.Hide();
					}
					var pos =
						Application.Input.DesktopMousePosition +
						offsetCoefficient * new Vector2(0, node.AsWidget?.Height ?? 0);
					while (!IsNodeChanged()) {
						yield return null;
						if (!Tooltip.IsVisible) {
							Tooltip.Show(component.GetText(), pos);
						} else if (component.HideDelay > float.Epsilon) {
							yield return Tooltip.Delay(component.HideDelay, IsNodeChanged);
							Tooltip.Hide();
							while (!IsNodeChanged()) {
								yield return null;
							}
						}
					}
					Tooltip.Hide();
				}
			}
		}
	}
}
