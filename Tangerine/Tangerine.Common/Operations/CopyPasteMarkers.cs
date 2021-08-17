using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.Common.Operations
{
	public static class CopyPasteMarkers
	{
		private const string MarkerContainerAnimationId = "72beccac-81e0-493e-ae23-8d7aafad6fb8";

		public static void CopyMarkers(IEnumerable<Marker> markers)
		{
			var stream = new MemoryStream();
			var container = new Frame();
			var animation = new Animation { Id = MarkerContainerAnimationId };
			container.Animations.Add(animation);
			foreach (var marker in markers) {
				animation.Markers.Add(Cloner.Clone(marker));
			}
			TangerinePersistence.Instance.WriteObject(null, stream, container, Persistence.Format.Json);
			Clipboard.Text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
		}

		public static bool TryPasteMarkers(Animation animation, int? pasteAtFrame, bool expandAnimation)
		{
			var data = Clipboard.Text;
			if (string.IsNullOrEmpty(data)) {
				return false;
			}
			Frame container;
			try {
				var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(data));
				container = TangerinePersistence.Instance.ReadObject<Frame>(null, stream);
			} catch {
				return false;
			}
			var markerContainerAnimation = container.Animations.FirstOrDefault(i => i.Id == MarkerContainerAnimationId);
		    if (markerContainerAnimation == null) {
		        return false;
		    }
		    var markersToPaste = markerContainerAnimation.Markers
			    .Select(i => i.Clone())
			    .OrderBy(i => i.Frame)
			    .ToList();
		    if (markersToPaste.Count == 0) {
			    return true;
		    }
		    pasteAtFrame ??= markersToPaste[0].Frame;
			    var range = markersToPaste[^1].Frame - markersToPaste[0].Frame + 1;
			if (expandAnimation) {
			    ShiftMarkersAndKeyframes(animation, pasteAtFrame.Value, range);
		    }
		    var d = pasteAtFrame.Value - markersToPaste[0].Frame;
		    foreach (var marker in markersToPaste) {

		    }
			// Firstly insert all non-Jump markers in order to correctly resolve Jump markers dependencies.
			// It doesn't fix a case when Jump marker jumps to Jump marker because
			// it doesn't make any sense and will clutter the code.
			PasteMarkers(markersToPaste.Where(m => m.Action != MarkerAction.Jump));
			PasteMarkers(markersToPaste.Where(m => m.Action == MarkerAction.Jump));
			return true;

			void PasteMarkers(IEnumerable<Marker> markers)
			{
				foreach (var marker in markers) {
					marker.Frame += d;
					var existingMarkerIndex = animation.Markers.FindIndex(i => i.Frame == marker.Frame);
					if (existingMarkerIndex >= 0) {
						UnlinkSceneItem.Perform(
							Document.Current.GetSceneItemForObject(animation.Markers[existingMarkerIndex]));
					}
					//int index = animation.Markers.FindIndex(m => m.Frame > pasteAtFrame);
					//if (index < 0) {
					//	index = animation.Markers.Count;
					//}
					LinkSceneItem.Perform(
						Document.Current.GetSceneItemForObject(animation),
						animation.Markers.GetInsertionIndexByFrame(marker.Frame),
						Document.Current.SceneTreeBuilder.BuildMarkerSceneItem(marker)
					);
				}
			}
		}

		public static void ShiftMarkersAndKeyframes(Animation animation, int startFrame, int delta)
		{
			foreach (var marker in animation.Markers) {
				if (marker.Frame >= startFrame) {
					SetProperty.Perform(marker, nameof(marker.Frame), marker.Frame + delta);
				}
			}
			foreach (var animator in animation.ValidatedEffectiveAnimators.OfType<IAnimator>()) {
				var changed = false;
				foreach (var keyframe in animator.Keys) {
					if (keyframe.Frame >= startFrame) {
						changed = true;
						SetProperty.Perform(keyframe, nameof(IKeyframe.Frame), keyframe.Frame + delta);
					}
				}
				if (changed) {
					DelegateOperation.Perform(
						() => animator.IncreaseVersion(),
						() => animator.IncreaseVersion(),
						true
					);
				}
			}
		}
	}
}
