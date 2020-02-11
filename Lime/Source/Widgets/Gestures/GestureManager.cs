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

		public void Update(float delta)
		{
			CurrentIteration++;
			UpdateGestures(delta);
			if (context.NodeCapturedByMouse != activeNode) {
				activeNode = context.NodeCapturedByMouse;
				// In case active node will change to null, which means capture button is released we need to
				// preserve double click gestures because it's recognition state spans across capture-button-release-boundary.
				var doubleClickGestures = activeGestures.Where(g => g is DoubleClickGesture).ToList();
				// We need to continue updating drag gestures with motion strategy currently active even if activeNode has been changed
				// or they'll stop moving by inertial drag otherwise.
				var dragGesturesChangingByMotion = activeGestures.Where(g => g is DragGesture gd && gd.IsChangingByMotion()).ToList();
				CancelGestures();
				if (activeNode != null) {
					activeGestures.AddRange(EnumerateGestures(activeNode));
					UpdateGestures(delta);
				} else {
					activeGestures.AddRange(doubleClickGestures);
				}
				dragGesturesChangingByMotion = dragGesturesChangingByMotion.Where(g => !activeGestures.Contains(g)).ToList();
				activeGestures.AddRange(dragGesturesChangingByMotion);
			}
		}

		private void UpdateGestures(float delta)
		{
			foreach (var gesture in activeGestures) {
				if (gesture is ClickGesture cg) {
					cg.Deferred = false;
					foreach (var g in activeGestures) {
						cg.Deferred |= g is DragGesture dg && dg.ButtonIndex == cg.ButtonIndex;
					}
				}
				if (gesture.OnUpdate(delta)) {
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
				}
			}
		}
	}
}
