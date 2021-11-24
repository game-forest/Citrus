using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.Core
{
	public class AnimationFastForwarder
	{
		private readonly List<AnimationState> animationStates = new List<AnimationState>();
		private readonly Dictionary<Animation, List<Edge>> incomingEdges = new Dictionary<Animation, List<Edge>>();
		private readonly Dictionary<Animation, List<Edge>> outgoingEdges = new Dictionary<Animation, List<Edge>>();
		private readonly bool cacheParsedTriggersInAnimations;

		private class AnimationParsedTriggersTableComponent : Component
		{
			public int EffectiveAnimatorsVersion = -1;
			public ParsedTriggersTable Table;
		}

		private struct Edge
		{
			public Animation Animation;
			public ushort[] Triggers;
		}

		private struct AnimationState
		{
			public Animation Animation;
			public int Frame;
			public int TickStoppedOn;
			public int Depth;
		}

		public AnimationFastForwarder(bool cacheParsedTriggersInAnimations = true)
		{
			this.cacheParsedTriggersInAnimations = cacheParsedTriggersInAnimations;
		}

		public void FastForwardSafe(Animation animation, int currentTick, bool stopAnimations)
		{
			FastForward(animation, currentTick, stopAnimations);
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
					FastForward(animation, currentTick, stopAnimations);
				}
			}
		}

		private void FastForward(Animation animation, int currentTick, bool stopAnimations)
		{
			BuildAnimationStatesRecursively(animation, currentTick, ignoreMarkers: true);

			// The order of applying of the animation states is as follows:
			// Stopped animations go first in the order they were stopped.
			// The current animation goes last so all triggers will have the correct values in the inspector pane.
			animationStates.Sort((a, b) => {
				var d = GetStateOrder(a) - GetStateOrder(b);
				return d == 0 ? a.Depth - b.Depth : d;
			});
			foreach (var s in animationStates) {
				s.Animation.Frame = s.Frame;
				s.Animation.IsRunning = !stopAnimations && s.TickStoppedOn < 0;
			}
			foreach (var edges in incomingEdges.Values) {
				foreach (var e in edges) {
					ArrayPool<ushort>.Shared.Return(e.Triggers);
				}
			}
			animationStates.Clear();
			incomingEdges.Clear();
			outgoingEdges.Clear();

			int GetStateOrder(AnimationState state)
			{
				return state.Animation == animation
					? ushort.MaxValue + 1
					: (state.TickStoppedOn >= 0 ? state.TickStoppedOn : ushort.MaxValue);
			}
		}

		private void BuildAnimationStatesRecursively(Animation animation, int currentTick, bool ignoreMarkers)
		{
			foreach (var edge in GetOutgoingEdges(animation).ToList()) {
				RemoveEdge(animation, edge.Animation);
				BuildAnimationStatesRecursively(edge.Animation, currentTick, ignoreMarkers: false);
			}
			BuildAnimationState(animation, currentTick, ignoreMarkers, out var newOutgoingEdges);
			foreach (var edge in newOutgoingEdges) {
				AddEdge(animation, edge.Animation, edge.Triggers);
				BuildAnimationStatesRecursively(edge.Animation, currentTick, ignoreMarkers: false);
			}
		}

		private void BuildAnimationState(
			Animation animation,
			int currentTick,
			bool isTopLevelAnimation,
			out List<Edge> newOutgoingEdges
		) {
			var animationState = new AnimationState {
				Animation = animation,
				TickStoppedOn = -1,
				Depth = GetNodeDepth(animation.OwnerNode)
			};
			try {
				if (!isTopLevelAnimation && GetIncomingEdges(animation).Count == 0) {
					newOutgoingEdges = new List<Edge>();
					return;
				}
				var unitedTriggers = ArrayPool<ushort>.Shared.Rent(currentTick + 1);
				Array.Fill(unitedTriggers, ushort.MaxValue, 0, currentTick + 1);
				if (isTopLevelAnimation) {
					unitedTriggers[0] = 0;
				} else {
					foreach (var edge in GetIncomingEdges(animation).OrderBy(e => GetNodeDepth(e.Animation.OwnerNode))) {
						var edgeTriggers = edge.Triggers;
						for (int t = 0; t <= currentTick; t++) {
							if (edgeTriggers[t] != ushort.MaxValue) {
								unitedTriggers[t] = edgeTriggers[t];
							}
						}
					}
				}
				var outgoingTriggers = new List<ushort[]>();
				ParsedTriggersTable parsedTriggers;
				if (cacheParsedTriggersInAnimations) {
					if (!animation.AnimationEngine.AreEffectiveAnimatorsValid(animation)) {
						animation.AnimationEngine.BuildEffectiveAnimators(animation);
					}
					var parsedTriggersComponent = animation.Components.GetOrAdd<AnimationParsedTriggersTableComponent>();
					if (
						parsedTriggersComponent.EffectiveAnimatorsVersion != animation.EffectiveAnimatorsVersion
						|| parsedTriggersComponent.Table == null
					) {
						parsedTriggersComponent.EffectiveAnimatorsVersion = animation.EffectiveAnimatorsVersion;
						parsedTriggersComponent.Table?.Dispose();
						parsedTriggersComponent.Table = new ParsedTriggersTable(animation);
					}
					parsedTriggers = parsedTriggersComponent.Table;
				} else {
					parsedTriggers = new ParsedTriggersTable(animation);
				}
				using var caret = new Caret(animation, isTopLevelAnimation);
				ushort currentFrame = 0;
				var running = false;
				for (int t = 0; t <= currentTick; t++) {
					if (unitedTriggers[t] != ushort.MaxValue) {
						currentFrame = unitedTriggers[t];
						running = true;
						animationState.TickStoppedOn = -1;
					}
					if (running) {
						var index = parsedTriggers.GetFirstKeyframeIndex(currentFrame);
						while (index != ushort.MaxValue) {
							var key = parsedTriggers.Keyframes[index];
							while (outgoingTriggers.Count <= key.AnimationIndex) {
								outgoingTriggers.Add(null);
							}
							if (outgoingTriggers[key.AnimationIndex] == null) {
								var triggers = ArrayPool<ushort>.Shared.Rent(currentTick + 1);
								Array.Fill(triggers, ushort.MaxValue, 0, currentTick + 1);
								outgoingTriggers[key.AnimationIndex] = triggers;
							}
							outgoingTriggers[key.AnimationIndex][t] = key.Frame;
							index = key.NextKeyframeIndex;
						}
						var nextFrame = caret.NextFrame(currentFrame);
						if (nextFrame == currentFrame) {
							running = false;
							animationState.TickStoppedOn = t;
						}
						if (t < currentTick) {
							currentFrame = nextFrame;
						}
					}
				}
				newOutgoingEdges = new List<Edge>();
				for (int i = 0; i < outgoingTriggers.Count; i++) {
					if (outgoingTriggers[i] != null) {
						newOutgoingEdges.Add(new Edge {
							Animation = parsedTriggers.TriggeredAnimations[i],
							Triggers = outgoingTriggers[i]
						});
					}
				}
				animationState.Frame = currentFrame;
				ArrayPool<ushort>.Shared.Return(unitedTriggers);
				if (!cacheParsedTriggersInAnimations) {
					parsedTriggers.Dispose();
				}
			} finally {
				var index = animationStates.FindIndex(s => s.Animation == animation);
				if (index >= 0) {
					animationStates[index] = animationState;
				} else {
					animationStates.Add(animationState);
				}
			}
		}

		private static int GetNodeDepth(Node node)
		{
			var depth = 0;
			var p = node.Parent;
			while (p != null) {
				depth++;
				p = p.Parent;
			}
			return depth;
		}

		private void AddEdge(Animation from, Animation to, ushort[] triggers)
		{
			GetIncomingEdges(to).Add(new Edge { Animation = from, Triggers = triggers });
			GetOutgoingEdges(from).Add(new Edge { Animation = to, Triggers = triggers });
		}

		private void RemoveEdge(Animation from, Animation to)
		{
			Remove(GetIncomingEdges(to), from, returnToPool: true);
			Remove(GetOutgoingEdges(from), to, returnToPool: false);

			void Remove(List<Edge> edges, Animation animation, bool returnToPool)
			{
				for (int i = 0; i < edges.Count; i++) {
					if (edges[i].Animation == animation) {
						if (returnToPool) {
							ArrayPool<ushort>.Shared.Return(edges[i].Triggers);
						}
						edges.RemoveAt(i);
						break;
					}
				}
			}
		}

		private List<Edge> GetIncomingEdges(Animation animation)
		{
			if (!incomingEdges.TryGetValue(animation, out var edges)) {
				edges = new List<Edge>();
				incomingEdges.Add(animation, edges);
			}
			return edges;
		}

		private List<Edge> GetOutgoingEdges(Animation animation)
		{
			if (!outgoingEdges.TryGetValue(animation, out var edges)) {
				edges = new List<Edge>();
				outgoingEdges.Add(animation, edges);
			}
			return edges;
		}

		private class ParsedTriggersTable : IDisposable
		{
			private static readonly int triggerComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate("Trigger");
			private readonly Animation animation;
			private ushort[][] table;

			public readonly List<Animation> TriggeredAnimations = new List<Animation>();
			public readonly List<Keyframe> Keyframes = new List<Keyframe>();

			public struct Keyframe
			{
				public int AnimationIndex;
				public ushort Frame;
				public ushort NextKeyframeIndex;
			}

			public ParsedTriggersTable(Animation animation)
			{
				this.animation = animation;
				table = ArrayPool<ushort[]>.Shared.Rent(256);
				Array.Clear(table, 0, 256);
			}

			public int GetFirstKeyframeIndex(int frame)
			{
				if (table[frame >> 8] is {} page) {
					return page[frame & 0xFF];
				}
				table[frame >> 8] = page = ArrayPool<ushort>.Shared.Rent(256);
				Array.Fill(page, ushort.MaxValue, 0, 256);
				int baseFrame = frame & 0xFF00;
				if (!animation.AnimationEngine.AreEffectiveAnimatorsValid(animation)) {
					animation.AnimationEngine.BuildEffectiveAnimators(animation);
				}
				var triggerAnimators = animation.EffectiveTriggerableAnimators
					.Where(i => i.TargetPropertyPathComparisonCode == triggerComparisonCode)
					.OfType<Animator<string>>()
					.ToList();
				foreach (var triggerAnimator in triggerAnimators) {
					var node = (Node)triggerAnimator.Owner;
					foreach (var key in triggerAnimator.ReadonlyKeys) {
						if (key.Frame < baseFrame || key.Frame > baseFrame + 255) {
							continue;
						}
						foreach ((var triggerAnimation, int triggerFrame) in ParseTrigger(node, key.Value)) {
							int animationIndex = 0;
							foreach (var a in TriggeredAnimations) {
								if (a == triggerAnimation) {
									break;
								}
								animationIndex++;
							}
							if (animationIndex == TriggeredAnimations.Count) {
								TriggeredAnimations.Add(triggerAnimation);
							}
							var i = page[key.Frame - baseFrame];
							page[key.Frame - baseFrame] = (ushort)Keyframes.Count;
							Keyframes.Add(new Keyframe {
								AnimationIndex = animationIndex,
								Frame = (ushort)triggerFrame,
								NextKeyframeIndex = i
							});
						}
					}
				}
				return page[frame & 0xFF];
			}

			public void Dispose()
			{
				for (int i = 0; i < table.Length; i++) {
					if (table[i] != null) {
						ArrayPool<ushort>.Shared.Return(table[i]);
						table[i] = null;
					}
				}
				ArrayPool<ushort[]>.Shared.Return(table, clearArray: true);
				table = null;
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
				Node node,
				string markerWithOptionalAnimationId,
				out Animation animation,
				out int frame
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

		private class Caret : IDisposable
		{
			private readonly Animation animation;
			private readonly bool ignoreMarkers;
			private ushort[][] table;

			public Caret(Animation animation, bool ignoreMarkers)
			{
				this.animation = animation;
				this.ignoreMarkers = ignoreMarkers;
				table = ArrayPool<ushort[]>.Shared.Rent(256);
				Array.Clear(table, 0, 256);
			}

			public ushort NextFrame(ushort frame)
			{
				if (table[frame >> 8] is {} page) {
					return page[frame & 0xFF];
				}
				table[frame >> 8] = page = ArrayPool<ushort>.Shared.Rent(256);
				int currentFrame = frame & 0xFF00;
				var markerAhead = FindMarkerAhead(currentFrame);
				Marker markerJumpTo = null;
				for (var i = 0; i < 256; i++, currentFrame++) {
					page[i] = (ushort)(currentFrame + 1);
					if (markerAhead != null && markerAhead.Frame == currentFrame) {
						if (markerAhead.Action == MarkerAction.Stop) {
							page[i] = (ushort)currentFrame;
						} else if (markerAhead.Action == MarkerAction.Jump) {
							if (markerJumpTo == null || markerAhead.JumpTo != markerJumpTo.Id) {
								animation.Markers.TryFind(markerAhead.JumpTo, out markerJumpTo);
							}
							if (markerJumpTo != null) {
								page[i] = (ushort)markerJumpTo.Frame;
							}
						}
						markerAhead = FindMarkerAhead(currentFrame + 1);
					}
				}
				return page[frame & 0xFF];
			}

			private Marker FindMarkerAhead(int frame)
			{
				if (animation.Markers.Count > 0 && !ignoreMarkers) {
					foreach (var marker in animation.Markers) {
						if (marker.Frame >= frame) {
							return marker;
						}
					}
				}
				return null;
			}

			public void Dispose()
			{
				for (int i = 0; i < table.Length; i++) {
					if (table[i] != null) {
						ArrayPool<ushort>.Shared.Return(table[i]);
						table[i] = null;
					}
				}
				ArrayPool<ushort[]>.Shared.Return(table, clearArray: true);
				table = null;
			}
		}
	}
}
