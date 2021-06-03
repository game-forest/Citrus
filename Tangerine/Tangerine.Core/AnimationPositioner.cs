using System;
using System.Collections.Generic;
using System.Linq;
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
				//AdvanceAnimation(animation.OwnerNode, time + AnimationUtils.Threshold);
				AdvanceAnimationFast(animation, AnimationUtils.SecondsToFrames(time), processMarkers: false);
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
								clampedDelta = Math.Min(clampedDelta,
									CalcDelta(animation.Time, AnimationUtils.FramesToSeconds(frame)));
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

		private static double CalcDelta(double currentTime, double triggerTime)
		{
			if (triggerTime - currentTime > AnimationUtils.SecondsPerFrame - AnimationUtils.Threshold) {
				return triggerTime - currentTime - AnimationUtils.Threshold;
			} else {
				return triggerTime - currentTime + AnimationUtils.Threshold;
			}
		}

		struct TriggerData
		{
			public Keyframe<string> Keyframe;
			public int Order;
			public int FrameCount;
		}

		private void AdvanceAnimationFast(Animation animation, int frameCount, bool processMarkers)
		{
			var triggerComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate("Trigger");
			var triggerAnimators = animation.ValidatedEffectiveTriggerableAnimators.
				Where(i => i.TargetPropertyPathComparisonCode == triggerComparisonCode).
				OfType<Animator<string>>().ToList();
			var triggers = new Dictionary<Node, Dictionary<string, TriggerData>>();
			int currentFrame = animation.Frame;
			int order = 0;
			foreach (var (rangeBegin, rangeEnd) in EnumerateAnimationRanges(animation, currentFrame, frameCount, processMarkers)) {
				foreach (var triggerAnimator in triggerAnimators) {
					var node = (Node) triggerAnimator.Owner;
					if (!triggers.TryGetValue(node, out var nodeTriggers)) {
						nodeTriggers = new Dictionary<string, TriggerData>();
						triggers.Add(node, nodeTriggers);
					}
					foreach (var keyframe in triggerAnimator.ReadonlyKeys) {
						if (keyframe.Frame < rangeBegin || keyframe.Frame > rangeEnd) {
							continue;
						}
						nodeTriggers[keyframe.Value] = new TriggerData {
							Keyframe = keyframe,
							Order = order++,
							FrameCount = frameCount - (keyframe.Frame - rangeBegin)
						};
					}
				}
				frameCount -= rangeEnd - rangeBegin;
				currentFrame = rangeEnd;
			}
			animation.Frame = currentFrame;
			foreach (var (node, nodeTriggersDictionary) in triggers) {
				var nodeTriggers = nodeTriggersDictionary.Values.OrderBy(i => i.Order);
				foreach (var trigger in nodeTriggers) {
					foreach (var (animationToRun, runAtFrame) in ParseTrigger(node, trigger.Keyframe.Value)) {
						animationToRun.Frame = runAtFrame;
						AdvanceAnimationFast(animationToRun, trigger.FrameCount, processMarkers: true);
					}
				}
			}
		}

		private IEnumerable<(int, int)> EnumerateAnimationRanges(Animation animation, int startFrame, int frameCount, bool processMarkers)
		{
			while (true) {
				var jumped = false;
				if (processMarkers) {
					foreach (var marker in animation.Markers) {
						if (marker.Frame < startFrame || marker.Frame - startFrame > frameCount) {
							continue;
						}
						if (marker.Action == MarkerAction.Stop) {
							yield return (startFrame, marker.Frame);
							yield break;
						}
						if (marker.Action == MarkerAction.Jump) {
							if (animation.Markers.TryFind(marker.JumpTo, out var jumpToMarker)) {
								frameCount -= marker.Frame - startFrame;
								yield return (startFrame, marker.Frame);
								startFrame = jumpToMarker.Frame;
								jumped = true;
								break;
							}
						}
					}
				}
				if (!jumped) {
					yield return (startFrame, startFrame + frameCount);
					yield break;
				}
			}
		}

		private IEnumerable<(Animation, int)> ParseTrigger(Node node, string trigger)
		{
			if (trigger == null) {
				yield break;
			}
			if (trigger.IndexOf(',') >= 0) {
				foreach (var s in trigger.Split(',')) {
					if (ParseTriggerHelper(node, s.Trim(), out var a, out var t)) {
						yield return (a, t);
					}
				}
			} else {
				if (ParseTriggerHelper(node, trigger, out var a, out var t)) {
					yield return (a, t);
				}
			}
		}

		private bool ParseTriggerHelper(Node node, string markerWithOptionalAnimationId, out Animation animation, out int frame)
		{
			animation = null;
			frame = 0;
			if (markerWithOptionalAnimationId.IndexOf('@') >= 0) {
				var s = markerWithOptionalAnimationId.Split('@');
				if (s.Length == 2) {
					node.Animations.TryFind(s[1], out animation);
					markerWithOptionalAnimationId = s[0];
				}
			} else {
				animation = node.DefaultAnimation;
			}
			if (animation != null && animation.Markers.TryFind(markerWithOptionalAnimationId, out var marker)) {
				frame = marker.Frame;
				return true;
			}
			return false;
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
