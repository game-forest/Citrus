using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Lime;

namespace Tangerine.UI.Docking
{
	public class WindowDragBehaviour
	{
		private static DockHierarchy AppPlacement => DockHierarchy.Instance;
		private DockSite requestedSite;
		private PanelPlacement requestedPlacement;
		private Vector2 positionOffset;
		private readonly WindowPlacement windowPlacement;
		private readonly Placement placement;
		public static bool IsActive { get; private set; }
		public WindowInput Input { get; }

		private WindowDragBehaviour(Placement placement)
		{
			this.placement = placement;
			windowPlacement = DockManager.Instance.Model.GetWindowByPlacement(placement);
			((WindowWidget)WidgetContext.Current.Root).Tasks.Add(MoveTask());
			Input = CommonWindow.Current.Input;
			Application.Input.Simulator.SetKeyState(Key.Mouse0, true);
		}

		private IEnumerator<object> MoveTask()
		{
			// Skip several frames here to let Input system respong to artificially setting
			// Mouse0 key state to true with Input Simulator, so dragging will continue,
			// disregaring clearing mouse keys state on window deactivate.
			yield return null;
			yield return null;
			while (true) {
				if (Input.IsMousePressed()) {
					RefreshPlacementAndSite();
					yield return null;
				} else {
					OnMouseRelease();
					yield break;
				}
			}
		}

		public static void CreateFor(Placement placement, Vector2 positionOffset)
		{
			new WindowDragBehaviour(placement) {
				positionOffset = positionOffset,
			};
			IsActive = true;
		}

		private void OnMouseRelease()
		{
			ResetDockComponents();
			if (requestedSite != DockSite.None && !CoreUserPreferences.Instance.LockLayout) {
				DockManager.Instance.DockPlacementTo(placement, requestedPlacement, requestedSite, -1f);
			}
			IsActive = false;
		}

		private IEnumerable<Panel> GetPanels()
		{
			foreach (var panel in AppPlacement.Panels) {
				var panelPlacement = DockManager.Instance.Model.FindPanelPlacement(panel.Id);
				if (!panelPlacement.Hidden &&
					panel.ContentWidget.GetRoot() is WindowWidget &&
					panelPlacement != placement &&
					!panelPlacement.IsDescendantOf(placement)
				) {
					yield return panel;
				}
			}
		}

		private void RefreshPlacementAndSite()
		{
			var mousePosition = Application.DesktopMousePosition;
			var offset = positionOffset;
			if (Application.Platform == PlatformId.Mac) {
				mousePosition.Y -= windowPlacement.WindowWidget.Window.DecoratedSize.Y;
				offset.Y = -offset.Y;
			}
			ResetDockComponents();
			var cachedSite = requestedSite;
			requestedSite = DockSite.None;
			windowPlacement.WindowWidget.Window.ClientPosition = mousePosition - offset;
			foreach (var p in GetPanels()) {
				var placement = AppPlacement.FindPanelPlacement(p.Id);
				var bounds = p.PanelWidget.CalcAABBInWindowSpace();
				var winPlacement = DockManager.Instance.Model.GetWindowByPlacement(placement);
				var requestedDockingComponent = winPlacement.WindowWidget.Components.Get<RequestedDockingComponent>();
				if (requestedDockingComponent == null) {
					continue;
				}

				var clientMousePos = winPlacement.WindowWidget.Window.Input.MousePosition;
				if (!bounds.Contains(clientMousePos)) {
					continue;
				}

				CalcSiteAndRect(clientMousePos, bounds, out DockSite site, out Rectangle? rect);
				if (placement.Id == windowPlacement.Root.GetPanelPlacements().First().Id ||
					placement.Id == DockManager.DocumentAreaId &&
					site == DockSite.Fill
				) {
					site = DockSite.None;
					rect = null;
					requestedPlacement = null;
				}
				if (cachedSite != site || requestedPlacement?.Id != placement.Id) {
					DockHierarchy.Instance.InvalidateWindows();
				}
				requestedSite = site;
				requestedDockingComponent.Bounds = rect;
				requestedPlacement = placement;
				break;
			}
			if (cachedSite != requestedSite) {
				DockHierarchy.Instance.InvalidateWindows();
			}
		}

		private static void ResetDockComponents()
		{
			foreach (var win in AppPlacement.VisibleWindowPlacements) {
				win.WindowWidget.Components.Get<RequestedDockingComponent>().Bounds = null;
			}
		}

		private static void CalcSiteAndRect(
			Vector2 position, Rectangle originRect, out DockSite site, out Rectangle? rect
		) {
			var pos = (position - originRect.A) / originRect.Size;
			site = DockSite.None;
			const float offset = 0.25f;
			rect = new Rectangle();
			var bottomRect = new Rectangle(new Vector2(0, 1 - offset), Vector2.One);
			var leftRect = new Rectangle(Vector2.Zero, new Vector2(offset, 1));
			var rightRect = new Rectangle(new Vector2(1 - offset, 0), Vector2.One);
			var centerRect = new Rectangle(new Vector2(offset, offset), new Vector2(1 - offset, 1 - offset));
			var topRect = new Rectangle(Vector2.Zero, new Vector2(1, offset));
			if (centerRect.Contains(pos)) {
				rect = new Rectangle(Vector2.Zero, Vector2.One);
				site = DockSite.Fill;
			} else if (topRect.Contains(pos)) {
				rect = topRect;
				site = DockSite.Top;
			} else if (bottomRect.Contains(pos)) {
				rect = bottomRect;
				site = DockSite.Bottom;
			} else if (leftRect.Contains(pos)) {
				rect = leftRect;
				site = DockSite.Left;
			} else if (rightRect.Contains(pos)) {
				rect = rightRect;
				site = DockSite.Right;
			}
			rect = new Rectangle(
				originRect.A + originRect.Size * rect.Value.A,
				originRect.A + originRect.Size * rect.Value.B
			);
		}
	}

	public class DragBehaviour
	{
		private readonly Widget inputWidget;
		private readonly Widget contentWidget;
		private readonly Placement placement;
		private Vector2 LocalMousePosition => inputWidget.Parent.AsWidget.LocalMousePosition();
		private const float DragThreshold = 30;
		private readonly WidgetInput input;

		public event Action<Vector2, Vector2> OnUndock;

		public DragBehaviour(Widget inputWidget, Widget contentWidget, Placement placement)
		{
			this.inputWidget = inputWidget;
			this.contentWidget = contentWidget;
			this.placement = placement;
			input = inputWidget.Input;
			inputWidget.Tasks.Add(MainTask());
		}

		private IEnumerator<object> MainTask()
		{
			while (true) {
				if (input.WasMousePressed()) {
					var pressedPosition = inputWidget.LocalMousePosition();
					var windowPlacement = (WindowPlacement)placement.Root;
					bool doUndock = windowPlacement != placement &&
						(placement.Parent != windowPlacement || windowPlacement.Placements.Count > 1);
					if (!doUndock) {
						var panelWindow = (WindowWidget)contentWidget.GetRoot();
						var window = panelWindow.Window;
						if (window.State == WindowState.Maximized) {
							var initialPosition = LocalMousePosition;
							while (input.IsMousePressed()) {
								var diff = Mathf.Abs(LocalMousePosition - initialPosition);
								if (diff.X >= DragThreshold || diff.Y >= DragThreshold) {
									window.State = WindowState.Normal;
									pressedPosition = new Vector2(window.ClientSize.X / 2, 10);
									WindowDragBehaviour.CreateFor(placement, pressedPosition);
									break;
								}
								yield return null;
							}
							yield return null;
							continue;
						}
						WindowDragBehaviour.CreateFor(placement, pressedPosition);
					} else {
						var size = inputWidget.Parent.AsWidget.Size;
						var initialPosition = LocalMousePosition;
						while (input.IsMousePressed() &&
							LocalMousePosition.X > -DragThreshold &&
							LocalMousePosition.X < size.X + DragThreshold &&
							Mathf.Abs(LocalMousePosition.Y - initialPosition.Y) < DragThreshold
						) {
							yield return null;
						}
						if (input.IsMousePressed()) {
							OnUndock?.Invoke(
								pressedPosition,
								Application.DesktopMousePosition - (input.MousePosition - contentWidget.GlobalPosition)
							);
						}
					}
				}
				yield return null;
			}
		}
	}
}
