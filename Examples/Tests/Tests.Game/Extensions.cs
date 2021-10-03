using System;
using Lime;

namespace Tests
{
	public static class Extensions
	{
		[Obsolete("Use handler?.Invoke() instead")]
		public static void SafeInvoke(this Action handler)
		{
			handler?.Invoke();
		}

		public static void ExpandToContainer(this Widget widget)
		{
			if (widget.ParentWidget != null) {
				widget.Size = widget.ParentWidget.Size;
			}
			widget.Anchors = Anchors.LeftRightTopBottom;
		}
	}
}
