using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	public class GestureManager
	{
		protected readonly WidgetContext context;
		private readonly List<Gesture> activeGestures = new List<Gesture>();
		private Node activeNode;
		public int CurrentIteration { get; private set; }

		public GestureManager(WidgetContext context)
		{
			this.context = context;
		}

		internal float AccumulatedDelta;

		public void Update(float delta)
		{
			AccumulatedDelta += delta;
			CurrentIteration++;
			UpdateGestures();
			if (context.NodeCapturedByMouse != activeNode) {
				activeNode = context.NodeCapturedByMouse;
				// We need to continue updating drag gestures with motion strategy currently active even if activeNode has been changed
				// or they'll stop moving by inertial drag otherwise.
				var dragGesturesChangingByMotion = activeGestures.Where(g => g is DragGesture gd && gd.IsChangingByMotion()).ToList();
				CancelGestures();
				if (activeNode != null) {
					activeGestures.AddRange(EnumerateGestures(activeNode));
					UpdateGestures();
				}
				dragGesturesChangingByMotion = dragGesturesChangingByMotion.Where(g => !activeGestures.Contains(g)).ToList();
				activeGestures.AddRange(dragGesturesChangingByMotion);
			}
		}

		private void UpdateGestures()
		{
			foreach (var gesture in activeGestures) {
				if (gesture is ClickGesture cg) {
					cg.Deferred = false;
					foreach (var g in activeGestures) {
						cg.Deferred |= g is DragGesture dg && dg.ButtonIndex == cg.ButtonIndex;
					}
				}
				if (gesture.OnUpdate()) {
					switch (gesture) {
						case DragGesture dg: {
							foreach (var g in activeGestures) {
								if (g == gesture) {
									continue;
								}
								var clickGesture = g as ClickGesture;
								if (clickGesture?.ButtonIndex == dg.ButtonIndex) {
									g.OnCancel(dg);
								}
								if (dg.Exclusive) {
									(g as DragGesture)?.OnCancel(dg);
								}
							}
							break;
						}
						case DoubleClickGesture dcg: {
							foreach (var g in activeGestures) {
								if (g != dcg) {
									g.OnCancel(dcg);
								}
							}
							break;
						}
						case LongTapGesture ltg: {
							foreach (var g in activeGestures) {
								if (g == gesture) {
									continue;
								}
								var longTapGesture = g as LongTapGesture;
								if (longTapGesture?.ButtonIndex == ltg.ButtonIndex) {
									g.OnCancel(ltg);
								}
							}
							break;
						}
					}
				}
			}
		}

		private void CancelGestures()
		{
			foreach (var g in activeGestures) {
				g.OnCancel(null);
			}
			activeGestures.Clear();
		}

		protected virtual IEnumerable<Gesture> EnumerateGestures(Node node)
		{
			var noMoreClicks = new bool[3];
			var noMoreDoubleClicks = new bool[3];
			for (; node != null; node = node.Parent) {
				if (node.HasGestures()) {
					foreach (var g in node.Gestures) {
						if (g is ClickGesture cg) {
							if (noMoreClicks[cg.ButtonIndex]) {
								continue;
							}
							noMoreClicks[cg.ButtonIndex] = true;
						}
						if (g is DoubleClickGesture dcg) {
							if (noMoreDoubleClicks[dcg.ButtonIndex]) {
								continue;
							}
							noMoreDoubleClicks[dcg.ButtonIndex] = true;
						}
						yield return g;
					}
					if (node.Components.Contains<DisableAncestralGesturesComponent>()) {
						yield break;
					}
				}
			}
		}
	}

	public class DisableAncestralGesturesComponent : NodeComponent
	{
	}
}
