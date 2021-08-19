using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.Core
{
	public class AnimationFastForwarder
	{
		private readonly HashSet<AnimationRange> processedAnimationRanges = new HashSet<AnimationRange>();

		public void FastForward(Animation animation, int frame, int frameCount, bool stopAnimations)
		{
			var animationStates = new List<AnimationState>();
			BuildAnimationStates(animationStates, animation, frame, frameCount);
			ApplyAnimationStates(animationStates, animation, stopAnimations);
			// The following code is intended for code that
			// changes the node hierarchy while the animation is scrolling.
			var hierarchyChanged = false;
			HierarchyChangedEventHandler h = e => hierarchyChanged = true;
			var nodeManager = animation.OwnerNode.Manager;
			if (nodeManager != null) {
				nodeManager.HierarchyChanged += h;
				nodeManager.Update(0);
				nodeManager.HierarchyChanged -= h;
				if (hierarchyChanged) {
					animationStates.Clear();
					BuildAnimationStates(animationStates, animation, frame, frameCount);
					ApplyAnimationStates(animationStates, animation, stopAnimations);
				}
			}
		}

		public void BuildAnimationStates(
			List<AnimationState> animationStates,
			Animation animation,
			int frame,
			int frameCount,
			bool processMarkers = false
		) {
			processedAnimationRanges.Clear();
			BuildAnimationStatesHelper(
				processedAnimationRanges, animationStates, animation, frame, frameCount, processMarkers
			);
		}

		public static void ApplyAnimationStates(
			List<AnimationState> animationStates, Animation currentAnimation, bool stopAnimations
		) {
			// Apply current animation state last so all triggers will have correct values on the inspector pane.
			var orderedStates = animationStates.OrderByDescending(
				s => s.Animation == currentAnimation ? -1 : s.FrameCount
			);
			foreach (var s in orderedStates) {
				s.Animation.Frame = s.Frame;
				s.Animation.IsRunning = !stopAnimations && s.IsRunning;
			}
		}

		struct TriggerData
		{
			public Keyframe<string> Keyframe;
			public int FrameCount;
		}

		[NodeComponentDontSerialize]
		class TriggerDataComponent : NodeComponent
		{
			public readonly Dictionary<string, TriggerData> Triggers = new Dictionary<string, TriggerData>();
		}

		public struct AnimationState
		{
			public Animation Animation;
			public int Frame;
			public int FrameCount;
			public bool IsRunning;
		}

		struct AnimationRange : IEquatable<AnimationRange>
		{
			public Animation Animation;
			public int Frame;
			public int FrameCount;

			public bool Equals(AnimationRange other)
			{
				return Animation == other.Animation && Frame == other.Frame && FrameCount == other.FrameCount;
			}

			public override int GetHashCode() => Animation.GetHashCode() ^ Frame ^ FrameCount;

			public override bool Equals(object obj) => obj is AnimationRange range && Equals(range);
		}

		private readonly int triggerComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate("Trigger");

		private void BuildAnimationStatesHelper(
			HashSet<AnimationRange> processedAnimationRanges,
			List<AnimationState> animationStates,
			Animation animation,
			int frame,
			int frameCount,
			bool processMarkers
		) {
			var range = new AnimationRange {
				Animation = animation,
				Frame = frame,
				FrameCount = frameCount
			};
			if (!processedAnimationRanges.Add(range)) {
				return;
			}
			if (!animation.AnimationEngine.AreEffectiveAnimatorsValid(animation)) {
				animation.AnimationEngine.BuildEffectiveAnimators(animation);
			}
			var triggerAnimators = animation.EffectiveTriggerableAnimators
				.Where(i => i.TargetPropertyPathComparisonCode == triggerComparisonCode)
				.OfType<Animator<string>>()
				.ToList();
			AdvanceCurrentFrameAndBuildTriggers(
				animation, frameCount, processMarkers, triggerAnimators, ref frame, out bool isRunning
			);
			ExecuteTriggers(processedAnimationRanges, animationStates, triggerAnimators);
			var state = new AnimationState {
				Animation = animation,
				FrameCount = frameCount,
				Frame = frame,
				IsRunning = isRunning
			};
			var j = animationStates.FindIndex(i => i.Animation == animation);
			if (j < 0) {
				animationStates.Add(state);
			} else if (animationStates[j].FrameCount > state.FrameCount) {
				animationStates[j] = state;
			}
		}

		private void ExecuteTriggers(
			HashSet<AnimationRange> processedAnimationsRanges,
			List<AnimationState> animationStates,
			List<Animator<string>> triggerAnimators
		) {
			foreach (var triggerAnimator in triggerAnimators) {
				var node = (Node)triggerAnimator.Owner;
				var triggers = node.Components.Get<TriggerDataComponent>().Triggers;
				var sortedTriggers = triggers.Values.ToList();
				sortedTriggers.Sort((a, b) => a.FrameCount - b.FrameCount);
				triggers.Clear();
				foreach (var trigger in sortedTriggers) {
					foreach (var (animationToRun, runAtFrame) in ParseTrigger(node, trigger.Keyframe.Value)) {
						BuildAnimationStatesHelper(
							processedAnimationsRanges,
							animationStates,
							animationToRun,
							runAtFrame,
							trigger.FrameCount,
							processMarkers: true
						);
					}
				}
			}
		}

		private static void AdvanceCurrentFrameAndBuildTriggers(
			Animation animation,
			int frameCount,
			bool processMarkers,
			List<Animator<string>> triggerAnimators,
			ref int currentFrame,
			out bool isRunning
		) {
			isRunning = false;
			foreach (
				var (rangeBegin, rangeEnd, isRunning_, remainedFrameCount) in
				EnumerateAnimationRangesWithLoopReduction(animation, currentFrame, frameCount, processMarkers)
			) {
				isRunning = isRunning_;
				foreach (var triggerAnimator in triggerAnimators) {
					var node = (Node)triggerAnimator.Owner;
					var triggers = node.Components.GetOrAdd<TriggerDataComponent>().Triggers;
					foreach (var keyframe in triggerAnimator.ReadonlyKeys) {
						if (keyframe.Frame < rangeBegin) {
							continue;
						}
						if (keyframe.Frame > rangeEnd) {
							break;
						}
						triggers[keyframe.Value ?? ""] = new TriggerData {
							Keyframe = keyframe,
							FrameCount = remainedFrameCount - (keyframe.Frame - rangeBegin),
						};
					}
				}
				currentFrame = rangeEnd;
			}
		}

		private static IEnumerable<(int, int, bool, int)> EnumerateAnimationRangesWithLoopReduction(
			Animation animation, int startFrame, int frameCount, bool processMarkers
		) {
			var previousRange = (-1, -1, false);
			foreach (var range in EnumerateAnimationRanges(animation, startFrame, frameCount, processMarkers)) {
				var (rangeBegin, rangeEnd, isRunning) = range;
				var rangeLength = rangeEnd - rangeBegin;
				if (range == previousRange) {
					if (rangeLength == 0) {
						yield return (rangeBegin, rangeEnd, isRunning, 0);
						yield break;
					}
					if (frameCount >= rangeLength) {
						yield return (rangeBegin, rangeEnd, isRunning, rangeLength + frameCount % rangeLength);
					}
					frameCount %= rangeLength;
					rangeEnd = rangeBegin + frameCount;
					yield return (rangeBegin, rangeEnd, isRunning, frameCount);
					yield break;
				} else {
					yield return (rangeBegin, rangeEnd, isRunning, frameCount);
				}
				previousRange = range;
				frameCount -= rangeLength;
			}
		}

		private static IEnumerable<(int, int, bool)> EnumerateAnimationRanges(
			Animation animation, int startFrame, int frameCount, bool processMarkers
		) {
			while (true) {
				var jumped = false;
				if (processMarkers) {
					foreach (var marker in animation.Markers) {
						if (marker.Frame < startFrame || marker.Frame - startFrame > frameCount) {
							continue;
						}
						if (marker.Action == MarkerAction.Stop) {
							yield return (startFrame, marker.Frame, false);
							yield break;
						}
						if (marker.Action == MarkerAction.Jump) {
							if (animation.Markers.TryFind(marker.JumpTo, out var jumpToMarker)) {
								frameCount -= marker.Frame - startFrame;
								yield return (startFrame, marker.Frame, true);
								startFrame = jumpToMarker.Frame;
								jumped = true;
								break;
							}
						}
					}
				}
				if (!jumped) {
					yield return (startFrame, startFrame + frameCount, true);
					yield break;
				}
			}
		}

		private static IEnumerable<(Animation, int)> ParseTrigger(Node node, string trigger)
		{
			trigger ??= "";
			if (trigger.Contains(',')) {
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

		private static bool ParseTriggerHelper(
			Node node, string markerWithOptionalAnimationId, out Animation animation, out int frame
		) {
			animation = null;
			frame = 0;
			if (markerWithOptionalAnimationId.Contains('@')) {
				var s = markerWithOptionalAnimationId.Split('@');
				if (s.Length == 2) {
					node.Animations.TryFind(s[1], out animation);
					markerWithOptionalAnimationId = s[0];
				}
			} else {
				animation = node.DefaultAnimation;
				if (string.IsNullOrWhiteSpace(markerWithOptionalAnimationId)) {
					frame = 0;
					return true;
				}
			}
			if (animation != null && animation.Markers.TryFind(markerWithOptionalAnimationId, out var marker)) {
				frame = marker.Frame;
				return true;
			}
			return false;
		}
	}
}
