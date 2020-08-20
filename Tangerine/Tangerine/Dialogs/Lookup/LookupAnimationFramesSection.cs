using System.Collections.Generic;
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
				!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to use Go To Animation Frame function")
			) {
				return;
			}
			var animation = Document.Current.Animation;
			var description = $"Animation:{(animation.IsLegacy ? "[Legacy]" : animation.Id)}; Node: {animation.OwnerNode}";
			var lastFrameIndex = GetLastAnimationFrame();
			lookupWidget.AddItem(new LookupDialogItem(
				$"Type the frame number to go to a particular frame on timeline{(lastFrameIndex > 0 ? $" (last animation frame is {lastFrameIndex})" : null)}",
				description,
				() => {
					if (!int.TryParse(lookupWidget.FilterText, out var frame) || frame < 0) {
						AlertDialog.Show($"Can not parse \"{lookupWidget.FilterText}\" into frame index");
						return;
					}
					Document.Current.History.DoTransaction(() => {
						SetCurrentColumn.Perform(frame, animation);
						CenterTimelineOnCurrentColumn.Perform();
					});
					Sections.Drop();
				}
			));
		}

		protected override IEnumerable<LookupItem> ApplyLookupFilter(string text, IReadOnlyList<LookupItem> items) => items;

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
