using System;
using Lime;

namespace Tangerine.Core
{
	public interface IAnimationPositioner
	{
		void SetAnimationTime(Animation animation, double time, bool stopAnimations);
		void SetAnimationFrame(Animation animation, int frame, bool stopAnimations);
	}

	public class AnimationPositioner : IAnimationPositioner
	{
		public void SetAnimationTime(Animation animation, double time, bool stopAnimations)
		{
			Audio.GloballyEnable = false;
			try {
				ResetAnimations(animation.OwnerNode);
				animation.IsRunning = true;
				animation.Time = 0;
				animation.OwnerNode.SetTangerineFlag(TangerineFlags.IgnoreMarkers, true);
				// Advance animation on Threshold more than needed to ensure the last trigger will be processed.
				AdvanceAnimation(animation.OwnerNode, time + AnimationUtils.Threshold);
				// Set animation exactly on the given time.
				animation.Time = time;
				animation.OwnerNode.SetTangerineFlag(TangerineFlags.IgnoreMarkers, false);
				if (stopAnimations) {
					StopAnimations(animation.OwnerNode);
				}
			} finally {
				Audio.GloballyEnable = true;
			}
		}

		public void SetAnimationFrame(Animation animation, int frame, bool stopAnimations)
		{
			SetAnimationTime(animation, AnimationUtils.FramesToSeconds(frame),  stopAnimations);
		}

		private void AdvanceAnimation(Node node, double delta)
		{
			var animations = node.Components.Get<AnimationComponent>()?.Animations;
			while (delta > 0) {
				var clampedDelta = delta;
				if (animations != null) {
					// Clamp delta to make sure we aren't going to skip any marker or trigger.
					foreach (var animation in animations) {
						if (animation.IsRunning) {
							if (FindClosestFrameWithMarkerOrTrigger(animation, out var frame)) {
								clampedDelta = Math.Min(clampedDelta, CalcDelta(animation.Time, AnimationUtils.FramesToSeconds(frame)));
							}
						}
					}
					foreach (var animation in animations) {
						if (animation.IsRunning) {
							animation.AnimationEngine.AdvanceAnimation(animation, clampedDelta);
						}
					}
				}
				foreach (var child in node.Nodes) {
					AdvanceAnimation(child, clampedDelta * child.AnimationSpeed);
				}
				delta -= clampedDelta;
			}
		}

		private static double CalcDelta(double currentTime, double triggerTime)
		{
			if (triggerTime - currentTime > AnimationUtils.SecondsPerFrame - AnimationUtils.Threshold) {
				return triggerTime - currentTime - AnimationUtils.Threshold;
			} else {
				return triggerTime - currentTime + AnimationUtils.Threshold;
			}
		}

		private bool FindClosestFrameWithMarkerOrTrigger(Animation animation, out int frame)
		{
			var animationFrame = AnimationUtils.SecondsToFramesCeiling(animation.Time);
			frame = int.MaxValue;
			if (animation.Markers.Count > 0) {
				foreach (var marker in animation.Markers) {
					if (marker.Frame >= animationFrame) {
						frame = marker.Frame;
						break;
					}
				}
			}
			foreach (var abstractAnimator in animation.ValidatedEffectiveTriggerableAnimators) {
				if (abstractAnimator is IAnimator animator) {
					foreach (var k in animator.ReadonlyKeys) {
						if (k.Frame >= animationFrame) {
							if (k.Frame < frame) {
								frame = k.Frame;
							}
							break;
						}
					}
				}
			}
			return frame != int.MaxValue;
		}

		private void ResetAnimations(Node node)
		{
			foreach (var n in node.Nodes) {
				ResetAnimations(n);
			}
		}

		private void StopAnimations(Node node)
		{
			foreach (var animation in node.Animations) {
				animation.IsRunning = false;
			}
			foreach (var n in node.Nodes) {
				StopAnimations(n);
			}
		}
	}
}
