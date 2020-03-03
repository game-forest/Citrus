using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TooltipProcessor : ITaskProvider
	{
		private static Tooltip tooltip => Tooltip.Instance;

#if MAC || MONOMAC
		private static float offsetCoefficient = -2f;
#else
		private static float offsetCoefficient = 1f;
#endif

		public IEnumerator<object> Task()
		{
			while (true) {
				yield return null;
				var node = WidgetContext.Current.NodeUnderMouse;
				var component = node?.Components.Get<TooltipComponent>();
				if (string.IsNullOrEmpty(component?.GetText?.Invoke())) {
					continue;
				}
				bool isNodeChanged() => node != WidgetContext.Current.NodeUnderMouse;
				yield return tooltip.Delay(component.ShowDelay, isNodeChanged);
				if (!isNodeChanged()) {
					if (tooltip.IsVisible) {
						tooltip.Hide();
					}
					var pos =
						Application.Input.DesktopMousePosition +
						offsetCoefficient * new Vector2(0, node.AsWidget?.Height ?? 0);
					while (!isNodeChanged()) {
						yield return null;
						if (!tooltip.IsVisible) {
							tooltip.Show(component.GetText(), pos);
						} else if (component.HideDelay > float.Epsilon) {
							yield return tooltip.Delay(component.HideDelay, isNodeChanged);
							tooltip.Hide();
							while (!isNodeChanged()) yield return null;
						}
					}
					tooltip.Hide();
				}
			}
		}
	}
}
