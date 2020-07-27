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

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null) {
				lookupWidget.AddItem(
					"Open any project to use Go To Animation Frame function",
					() => {
						new FileOpenProject();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			if (Document.Current == null) {
				lookupWidget.AddItem(
					"Open any document to use Go To Animation Frame function",
					() => {
						new FileOpen();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}
			var lastFrameIndex = GetLastAnimationFrame();
			if (lastFrameIndex == 0) {
				lookupWidget.AddItem("Select any node with keyframes to use Go To Animation Frame function", LookupDialog.Sections.Drop);
				return;
			}
			for (var frame = 0; frame <= lastFrameIndex; frame++) {
				var animationClosed = Document.Current.Animation;
				var frameClosed = frame;
				lookupWidget.AddItem(
				$"Frame {frame} in {animationClosed.Owner.Owner}",
				() => {
					Document.Current.History.DoTransaction(() => {
						SetCurrentColumn.Perform(frameClosed, animationClosed);
						CenterTimelineOnCurrentColumn.Perform();
					});
					LookupDialog.Sections.Drop();
				});
			}
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
			return lastFrameIndex;
		}
	}
}
