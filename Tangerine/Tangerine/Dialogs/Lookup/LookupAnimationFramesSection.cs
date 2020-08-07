using System.Linq;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine
{
	public class LookupAnimationFramesSection : LookupSection
	{
		private const string PrefixConst = ":";

		public override string Breadcrumb { get; } = "Go To Animation Frame";
		public override string Prefix => PrefixConst;
		public override string HelpText { get; } = $"Type '{PrefixConst}' to go to a frame in current animation";

		public LookupAnimationFramesSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (
				!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To Animation Frame function") ||
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Animation Frame function") ||
				!RequireNonEmptyTimelineOrAddAlertItem(
					lookupWidget,
					"Select any node with keyframes to use Go To Animation Frame function",
					out var lastFrameIndex
				)
			) {
				return;
			}
			var animation = Document.Current.Animation;
			var description = $"Animation:{(animation.IsLegacy ? "[Legacy]" : animation.Id)}; Node: {animation.OwnerNode}";
			for (var frame = 0; frame <= lastFrameIndex; frame++) {
				var frameClosed = frame;
				lookupWidget.AddItem(new LookupDialogItem(
					lookupWidget,
					frame.ToString(),
					description,
					() => {
						Document.Current.History.DoTransaction(() => {
							SetCurrentColumn.Perform(frameClosed, animation);
							CenterTimelineOnCurrentColumn.Perform();
						});
						Sections.Drop();
					}
				));
			}
		}

		private bool RequireNonEmptyTimelineOrAddAlertItem(LookupWidget lookupWidget, string alertText, out int lastFrameIndex)
		{
			lastFrameIndex = GetLastAnimationFrame();
			if (lastFrameIndex > 0) {
				return true;
			}
			lookupWidget.AddItem(new LookupDialogItem(
				lookupWidget,
				alertText,
				null,
				Sections.Drop
			));
			return false;
		}

		private static int GetLastAnimationFrame()
		{
			var lastFrameIndex = 0;
			foreach (var row in Document.Current.Rows) {
				var node = row.Components.Get<Core.Components.NodeRow>()?.Node;
				if (node == null) {
					continue;
				}
				foreach (var animator in node.Animators) {
					var key = animator.ReadonlyKeys.LastOrDefault();
					if (key != null && key.Frame > lastFrameIndex) {
						lastFrameIndex = key.Frame;
					}
				}
			}
			foreach (var marker in Document.Current.Animation.Markers) {
				if (marker.Frame > lastFrameIndex) {
					lastFrameIndex = marker.Frame;
				}
			}
			return lastFrameIndex;
		}
	}
}
