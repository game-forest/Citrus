using System;
using System.Collections.Generic;

namespace Lime
{
	public class AnimationEngine
	{
		public static bool JumpAffectsRunningMarkerId = false;

		public virtual bool TryRunAnimation(Animation animation, string markerId, double animationTimeCorrection = 0)
		{
			return false;
		}
		public virtual void AdvanceAnimation(Animation animation, double delta) { }

		/// <summary>
		/// 1. Refreshes animation.EffectiveAnimators;
		/// 2. Applies each animator at currentTime;
		/// 3. Executes triggers in given range.
		/// The range is [previousTime, currentTime) or [previousTime, currentTime]
		/// depending on executeTriggersAtCurrentTime flag.
		/// This method doesn't depend on animation.Time value.
		/// </summary>
		public virtual void ApplyAnimatorsAndExecuteTriggers(
			Animation animation, double previousTime, double currentTime, bool executeTriggersAtCurrentTime
		) { }
		public virtual bool AreEffectiveAnimatorsValid(Animation animation) => false;
		public virtual void BuildEffectiveAnimators(Animation animation) { }
		public virtual void RaiseStopped(Animation animation) { }
	}

	public class AnimationEngineDelegate : AnimationEngine
	{
		public Func<Animation, string, double, bool> OnRunAnimation;
		public Action<Animation, double> OnAdvanceAnimation;
		public Action<Animation, double, double, bool> OnApplyEffectiveAnimatorsAndBuildTriggersList;

		public override bool TryRunAnimation(Animation animation, string markerId, double animationTimeCorrection = 0)
		{
			return (OnRunAnimation != null) && OnRunAnimation(animation, markerId, animationTimeCorrection);
		}

		public override void AdvanceAnimation(Animation animation, double delta)
		{
			OnAdvanceAnimation?.Invoke(animation, delta);
		}

		public override void ApplyAnimatorsAndExecuteTriggers(
			Animation animation, double previousTime, double currentTime, bool executeTriggersAtCurrentTime
		) {
			OnApplyEffectiveAnimatorsAndBuildTriggersList?.Invoke(
				animation, previousTime, currentTime, executeTriggersAtCurrentTime
			);
		}
	}

	public class DefaultAnimationEngine : AnimationEngine
	{
		public static DefaultAnimationEngine Instance = new DefaultAnimationEngine();

		public override bool TryRunAnimation(Animation animation, string markerId, double animationTimeCorrection = 0)
		{
			var frame = 0;
			if (markerId != null) {
				var marker = animation.Markers.TryFind(markerId);
				if (marker == null) {
					return false;
				}
				frame = marker.Frame;
			}
			// Easings may give huge animationTimeCorrection values, clamp it.
			animationTimeCorrection = Mathf.Clamp(
				animationTimeCorrection,
				-AnimationUtils.SecondsPerFrame + AnimationUtils.Threshold,
				0
			);
			animation.Time = AnimationUtils.FramesToSeconds(frame) + animationTimeCorrection;
			animation.RunningMarkerId = markerId;
			animation.IsRunning = true;
			return true;
		}

		public override void AdvanceAnimation(Animation animation, double delta)
		{
			var previousTime = animation.Time;
			var currentTime = previousTime + delta;
			animation.TimeInternal = currentTime;
			animation.MarkerAhead = animation.MarkerAhead ?? FindMarkerAhead(animation, previousTime);
			if (animation.MarkerAhead == null || currentTime < animation.MarkerAhead.Time) {
				ApplyAnimatorsAndExecuteTriggers(
					animation, previousTime, currentTime, executeTriggersAtCurrentTime: false
				);
			} else {
				var marker = animation.MarkerAhead;
				animation.MarkerAhead = null;
				ProcessMarker(animation, marker, previousTime, currentTime);
			}
		}

		protected static Marker FindMarkerAhead(Animation animation, double time)
		{
			if (animation.Markers.Count > 0) {
				var frame = AnimationUtils.SecondsToFramesCeiling(time);
				foreach (var marker in animation.Markers) {
					if (marker.Frame >= frame) {
						return marker;
					}
				}
			}
			return null;
		}

		protected virtual void ProcessMarker(
			Animation animation, Marker marker, double previousTime, double currentTime
		) {
			switch (marker.Action) {
				case MarkerAction.Jump:
					var gotoMarker = animation.Markers.TryFind(marker.JumpTo);
					if (gotoMarker != null && gotoMarker != marker) {
						var delta = animation.Time - AnimationUtils.FramesToSeconds(animation.Frame);
						animation.TimeInternal = gotoMarker.Time;
						if (JumpAffectsRunningMarkerId) {
							animation.RunningMarkerId = gotoMarker.Id;
						}
						AdvanceAnimation(animation, delta);
					}
					break;
				case MarkerAction.Stop:
					animation.TimeInternal = AnimationUtils.FramesToSeconds(marker.Frame);
					ApplyAnimatorsAndExecuteTriggers(
						animation, previousTime, animation.Time, executeTriggersAtCurrentTime: true
					);
					animation.IsRunning = false;
					break;
				case MarkerAction.Play:
					ApplyAnimatorsAndExecuteTriggers(
						animation, previousTime, currentTime, executeTriggersAtCurrentTime: false
					);
					break;
			}
			marker.CustomAction?.Invoke();
		}

		public override void RaiseStopped(Animation animation)
		{
			animation.Stopped?.Invoke();

			var savedAction = animation.AssuredStopped;
			animation.AssuredStopped = null;
			savedAction?.Invoke();
		}

		public override void ApplyAnimatorsAndExecuteTriggers(
			Animation animation, double previousTime, double currentTime, bool executeTriggersAtCurrentTime
		) {
			if (!AreEffectiveAnimatorsValid(animation)) {
				BuildEffectiveAnimators(animation);
			}
			foreach (var a in animation.EffectiveAnimators) {
				a.Apply(currentTime);
			}
			foreach (var a in animation.EffectiveTriggerableAnimators) {
				a.ExecuteTriggersInRange(previousTime, currentTime, executeTriggersAtCurrentTime);
			}
		}

		public override bool AreEffectiveAnimatorsValid(Animation animation)
		{
			if (animation.IsCompound) {
				if (animation.EffectiveAnimators == null) {
					return false;
				}
				foreach (var track in animation.Tracks) {
					foreach (var clip in track.Clips) {
						var clipAnimation = clip.CachedAnimation;
						if (clipAnimation == null) {
							if (clip.FindAnimation() != null) {
								return false;
							}
						} else if (
							clipAnimation.IdComparisonCode != clip.AnimationIdComparisonCode ||
							clipAnimation.Owner != animation.Owner || !AreEffectiveAnimatorsValid(clipAnimation)
						) {
							return false;
						}
					}
				}
				return true;
			} else {
				return
					animation.OwnerNode.DescendantAnimatorsVersion == animation.EffectiveAnimatorsVersion &&
					animation.EffectiveAnimators != null;
			}
		}

		public override void BuildEffectiveAnimators(Animation animation)
		{
			if (animation.IsCompound) {
				BuildEffectiveAnimatorsForCompoundAnimation(animation);
			} else {
				BuildEffectiveAnimatorsForSimpleAnimation(animation);
			}
#if TANGERINE
			(animation.EffectiveAnimatorsSet ??= new HashSet<IAbstractAnimator>()).Clear();
			foreach (var animator in animation.EffectiveAnimators) {
				animation.EffectiveAnimatorsSet.Add(animator);
			}
#endif
		}

		private static void BuildEffectiveAnimatorsForCompoundAnimation(Animation animation)
		{
			(animation.EffectiveAnimators ??= new List<IAbstractAnimator>()).Clear();
			(animation.EffectiveTriggerableAnimators ??= new List<IAbstractAnimator>()).Clear();
#if TANGERINE
			// Necessary to edit track weights in the inspector.
			animation.EffectiveAnimatorsVersion++;
			foreach (var track in animation.Tracks) {
				foreach (var animator in track.Animators) {
					animation.EffectiveAnimators.Add(animator);
				}
			}
#endif
			var animationBindings =
				new Dictionary<AnimatorBinding, (IAbstractAnimator Animator, AnimationTrack Track)>();
			var trackBindings = new Dictionary<AnimatorBinding, IChainedAnimator>();
			foreach (var track in animation.Tracks) {
				trackBindings.Clear();
				foreach (var clip in track.Clips) {
					var clipAnimation = clip.FindAnimation();
					clip.CachedAnimation = clipAnimation;
					if (clipAnimation == null) {
						continue;
					}
					if (!clipAnimation.AnimationEngine.AreEffectiveAnimatorsValid(clipAnimation)) {
						clipAnimation.AnimationEngine.BuildEffectiveAnimators(clipAnimation);
					}
					foreach (var a in clipAnimation.EffectiveAnimators) {
						if (trackBindings.TryGetValue(new AnimatorBinding(a), out var chained)) {
							chained.Add(clip, a);
						} else {
							chained = AnimatorRegistry.Instance.CreateChainedAnimator(a.ValueType);
							chained.Add(clip, a);
							trackBindings.Add(new AnimatorBinding(a), chained);
						}
					}
				}
				foreach (var a in trackBindings.Values) {
					if (animationBindings.TryGetValue(new AnimatorBinding(a), out var i)) {
						if (i.Animator is IBlendedAnimator blended) {
							blended.Add(track, a);
						} else {
							animationBindings.Remove(new AnimatorBinding(a));
							blended = AnimatorRegistry.Instance.CreateBlendedAnimator(a.ValueType);
							blended.Add(i.Track, i.Animator);
							blended.Add(track, a);
							animationBindings.Add(new AnimatorBinding(a), (blended, track));
						}
					} else {
						animationBindings.Add(new AnimatorBinding(a), (a, track));
					}
				}
			}
			foreach (var b in animationBindings.Values) {
				var a = b.Animator;
				if (animation.HasEasings()) {
					var a2 = AnimatorRegistry.Instance.CreateEasedAnimator(a.ValueType);
					a2.Initialize(animation, a);
					a = a2;
				}
				animation.EffectiveAnimators.Add(a);
				if (a.IsTriggerable) {
					animation.EffectiveTriggerableAnimators.Add(a);
				}
			}
		}

		private struct AnimatorBinding : IEquatable<AnimatorBinding>
		{
			public IAnimable Animable;
			public int TargetPropertyPathComparisonCode;

			public AnimatorBinding(IAbstractAnimator animator)
			{
				Animable = animator.Animable;
				TargetPropertyPathComparisonCode = animator.TargetPropertyPathComparisonCode;
			}

			public bool Equals(AnimatorBinding other)
			{
				return Animable == other.Animable
					&& TargetPropertyPathComparisonCode == other.TargetPropertyPathComparisonCode;
			}

			public override int GetHashCode()
			{
				unchecked {
					var r = -511344;
					r = r * -1521134295 + Animable?.GetHashCode() ?? 0;
					r = r * -1521134295 + TargetPropertyPathComparisonCode;
					return r;
				}
			}
		}

		private static void BuildEffectiveAnimatorsForSimpleAnimation(Animation animation)
		{
			(animation.EffectiveAnimators ??= new List<IAbstractAnimator>()).Clear();
			(animation.EffectiveTriggerableAnimators ??= new List<IAbstractAnimator>()).Clear();
			animation.EffectiveAnimatorsVersion = animation.OwnerNode.DescendantAnimatorsVersion;
			AddEffectiveAnimatorsRecursively(animation.OwnerNode);

			if (!animation.IsLegacy && animation.ApplyZeroPose) {
				var animatorBindings = new HashSet<AnimatorBinding>();
				foreach (var a in animation.EffectiveAnimators) {
					animatorBindings.Add(new AnimatorBinding(a));
				}
				AddZeroPoseAnimatorsRecursively(animatorBindings, animation.OwnerNode);
			}

			void AddEffectiveAnimatorsRecursively(Node node)
			{
				foreach (var child in node.Nodes) {
					// Optimization: avoid calling Animators.GetEnumerator()
					// for empty collection since it allocates memory
					if (child.Animators.Count > 0) {
						foreach (var a in child.Animators) {
							if (a.AnimationId == animation.Id) {
								var a2 = (IAbstractAnimator)a;
								if (animation.HasEasings()) {
									var a3 = AnimatorRegistry.Instance.CreateEasedAnimator(a.ValueType);
									a3.Initialize(animation, a);
									a2 = a3;
								}
								animation.EffectiveAnimators.Add(a2);
								if (a2.IsTriggerable) {
									animation.EffectiveTriggerableAnimators.Add(a2);
								}
							}
						}
					}
					var stopRecursion = animation.IsLegacy;
					foreach (var a in child.Animations) {
						if (a.IdComparisonCode == animation.IdComparisonCode) {
							stopRecursion = true;
							break;
						}
					}
					if (!stopRecursion) {
						AddEffectiveAnimatorsRecursively(child);
					}
				}
			}

			void AddZeroPoseAnimatorsRecursively(HashSet<AnimatorBinding> animatorBindings, Node node)
			{
				foreach (var child in node.Nodes) {
					// Optimization: avoid calling Animators.GetEnumerator()
					// for empty collection since it allocates memory
					if (child.Animators.Count > 0) {
						foreach (var a in child.Animators) {
							if (a.AnimationId == Animation.ZeroPoseId) {
								if (!animatorBindings.Contains(new AnimatorBinding(a))) {
									animation.EffectiveAnimators.Add(a);
								}
							}
						}
					}
					var stopRecursion = false;
					foreach (var a in child.Animations) {
						if (a.IdComparisonCode == Animation.ZeroPoseIdComparisonCode) {
							stopRecursion = true;
							break;
						}
					}
					if (!stopRecursion) {
						AddZeroPoseAnimatorsRecursively(animatorBindings, child);
					}
				}
			}
		}
	}

	public class FastForwardAnimationEngine : DefaultAnimationEngine
	{
		public override void AdvanceAnimation(Animation animation, double delta)
		{
			var previousTime = animation.Time;
			var currentTime = previousTime + delta;
			animation.TimeInternal = currentTime;
			animation.MarkerAhead = animation.MarkerAhead ?? FindMarkerAhead(animation, previousTime);
			if (animation.MarkerAhead == null || currentTime < animation.MarkerAhead.Time) {
				// Do nothing
			} else {
				var marker = animation.MarkerAhead;
				animation.MarkerAhead = null;
				ProcessMarker(animation, marker, previousTime, currentTime);
			}
		}
	}
}
