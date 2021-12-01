using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline.Operations.CompoundAnimations
{
	public static class DragAnimationClips
	{
		public static void Perform(IntVector2 offset, bool removeOriginals)
		{
			var items = Document.Current.VisibleSceneItems.ToList();
			if (offset.Y > 0) {
				items.Reverse();
			}
			foreach (var item in items) {
				var track = item.Components.Get<AnimationTrackSceneItem>()?.Track;
				if (track?.EditorState().Locked != false) {
					continue;
				}
				var clips = track.Clips.Where(i => i.IsSelected).ToList();
				var keys = new List<IKeyframe>();
				if (
					track.Animators.TryFind(
						nameof(AnimationTrack.Weight), out var weightAnimator, Document.Current.AnimationId
					)
				) {
					keys = weightAnimator.ReadonlyKeys
						.Where(k => clips.Any(c => c.BeginFrame <= k.Frame && k.Frame <= c.EndFrame))
						.ToList();
				}
				if (removeOriginals) {
					foreach (var key in keys) {
						RemoveKeyframe.Perform(weightAnimator, key.Frame);
					}
				}
				foreach (var clip in clips) {
					if (removeOriginals) {
						AnimationClipToolbox.RemoveClip(track, clip);
					} else {
						SetProperty.Perform(clip, nameof(AnimationClip.IsSelected), false);
					}
				}
				int itemCount = Document.Current.VisibleSceneItems.Count;
				var destItemIndex = (item.GetTimelineSceneItemState().Index + offset.Y).Clamp(0, itemCount - 1);
				var destItem = Document.Current.VisibleSceneItems[destItemIndex];
				var destTrack = destItem.Components.Get<AnimationTrackSceneItem>()?.Track;
				foreach (var clip in clips) {
					var newClip = clip.Clone();
					newClip.BeginFrame += offset.X;
					newClip.EndFrame += offset.X;
					newClip.IsSelected = true;
					AnimationClipToolbox.InsertClip(destTrack, newClip);
				}
				foreach (var k in keys) {
					var key = k.Clone();
					key.Frame += offset.X;
					SetKeyframe.Perform(destTrack, nameof(AnimationTrack.Weight), Document.Current.Animation, key);
				}
			}
		}
	}
}
