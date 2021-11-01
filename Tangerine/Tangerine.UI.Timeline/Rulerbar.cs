using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class Rulerbar
	{
		public int MeasuredFrameDistance { get; set; }
		public Widget RootWidget { get; private set; }

		private Marker upperMarker;

		private static ITexture warningIcon;

		public Rulerbar()
		{
			RootWidget = new Widget {
				Id = nameof(Rulerbar),
				MinMaxHeight = Metrics.ToolbarHeight,
				HitTestTarget = true
			};
			RootWidget.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>(Render));
			RootWidget.Gestures.Add(
				new DoubleClickGesture(
					0,
					() => ShowMarkerDialog(Timeline.Instance.CurrentColumn)
				)
			);
			RootWidget.Gestures.Add(new ClickGesture(1, ShowContextMenu));
			RootWidget.AddChangeWatcher(() => Document.Current.AnimationFrame, (value) => {
				var markers = Document.Current.Animation.Markers;
				int i = markers.FindIndex(m => m.Frame == value);
				if (i >= 0) {
					upperMarker = markers[i];
				}
			});
			RootWidget.AddChangeWatcher(() => Document.Current.Container, (value) => {
				upperMarker = null;
			});
			warningIcon = IconPool.GetTexture("Inspector.Warning");

			void ShowContextMenu()
			{
				Document.Current.History.DoTransaction(() => SetCurrentColumn.Perform(CalcColumnUnderMouse()));
				new ContextMenu().Show();
			}
		}

		public int CalcColumnUnderMouse()
		{
			var mouseX = RootWidget.LocalMousePosition().X;
			return ((mouseX + Timeline.Instance.Offset.X) / TimelineMetrics.ColWidth).Floor().Max(0);
		}

		void Render(Widget widget)
		{
			widget.PrepareRendererState();
			Renderer.DrawRect(Vector2.Zero, RootWidget.Size, ColorTheme.Current.Toolbar.Background);
			Renderer.MultiplyTransform1(Matrix32.Translation(-Timeline.Instance.Offset.X.Round(), 0));
			RenderCursor();
			Timeline.Instance.GetVisibleColumnRange(out var minColumn, out var maxColumn);
			for (int i = minColumn; i <= maxColumn; i++) {
				if (i % 10 == 0) {
					var x = i * TimelineMetrics.ColWidth + 0.5f;
					float textHeight = Theme.Metrics.TextHeight;
					float y = (RootWidget.Height - textHeight) / 2;
					Renderer.DrawTextLine(
						new Vector2(x, y), i.ToString(),
						Theme.Metrics.TextHeight,
						Theme.Colors.BlackText,
						0.0f);
					if (!Document.Current.Animation.IsCompound) {
						Renderer.DrawLine(x, 0, x, RootWidget.Height, ColorTheme.Current.TimelineRuler.Notchings);
					}
				}
			}
			bool metUpperMarker = false;
			foreach (var m in Document.Current.Animation.Markers) {
				if (upperMarker != m) {
					RenderMarker(m);
				} else {
					metUpperMarker = true;
				}
			}
			if (!metUpperMarker && upperMarker != null) {
				upperMarker = null;
			}
			RenderUpperMarker();
		}

		void RenderCursor()
		{
			var r = GetRectangle(Timeline.Instance.CurrentColumnEased);
			Renderer.DrawRect(
				r.A, r.B,
				Document.Current.PreviewScene ?
					ColorTheme.Current.TimelineRuler.RunningCursor :
					ColorTheme.Current.TimelineRuler.Cursor);
		}

		void RenderMarker(Marker marker)
		{
			var r = GetRectangle(marker.Frame);
			r.AY = r.BY - 5;
			Renderer.DrawRect(r.A, r.B, GetMarkerColor(marker));
			Renderer.DrawRectOutline(r.A, r.B, ColorTheme.Current.TimelineRuler.MarkerBorder);
			if (
				marker.Action == MarkerAction.Jump &&
				(string.IsNullOrEmpty(marker.JumpTo) || (!string.IsNullOrEmpty(marker.Id) && (marker.Id == marker.JumpTo)))
			) {
				var size = new Vector2(RootWidget.Height, RootWidget.Height);
				var pos = new Vector2(r.Left - (size.X - r.Size.X) / 2.0f, r.Bottom - RootWidget.Height - 2);
				Renderer.DrawSprite(warningIcon, Color4.White, pos, size, Vector2.Zero, Vector2.One);
			} else if (!string.IsNullOrWhiteSpace(marker.Id)) {
				var h = Theme.Metrics.TextHeight;
				var padding = new Thickness { Left = 3.0f, Right = 5.0f, Top = 1.0f, Bottom = 1.0f };
				var extent = FontPool.Instance.DefaultFont.MeasureTextLine(marker.Id, h, 0.0f);
				var pos = new Vector2(r.A.X, r.A.Y - extent.Y - padding.Top - padding.Bottom - 1);
				Renderer.DrawRect(pos, pos + extent + padding.LeftTop + padding.RightBottom, Theme.Colors.WhiteBackground);
				Renderer.DrawRectOutline(pos, pos + extent + padding.LeftTop + padding.RightBottom, Theme.Colors.ControlBorder);
				Renderer.DrawTextLine(pos + padding.LeftTop, marker.Id, h, Theme.Colors.BlackText, 0.0f);
			}
		}

		void RenderUpperMarker()
		{
			if (upperMarker != null)
				RenderMarker(upperMarker);
		}

		Color4 GetMarkerColor(Marker marker)
		{
			switch (marker.Action) {
				case MarkerAction.Jump:
					return ColorTheme.Current.TimelineRuler.JumpMarker;
				case MarkerAction.Play:
					return ColorTheme.Current.TimelineRuler.PlayMarker;
				case MarkerAction.Stop:
					return ColorTheme.Current.TimelineRuler.StopMarker;
				default:
					return ColorTheme.Current.TimelineRuler.UnknownMarker;
			}
		}

		private Rectangle GetRectangle(float frame)
		{
			return new Rectangle {
				A = new Vector2(frame * TimelineMetrics.ColWidth + 0.5f, 0),
				B = new Vector2((frame + 1) * TimelineMetrics.ColWidth + 1.5f, RootWidget.Height)
			};
		}

		private static void PasteMarkers(bool atCursor, bool expandAnimation)
		{
			Document.Current.History.DoTransaction(() => {
				Common.Operations.CopyPasteMarkers.TryPasteMarkers(
					Document.Current.Animation,
					atCursor ? (int?)Document.Current.AnimationFrame : null,
					expandAnimation
				);
			});
		}

		private static void ShowMarkerDialog(int frameUnderMouse)
		{
			Document.Current.History.DoTransaction(() => {
				var marker = Document.Current.Animation.Markers.GetByFrame(frameUnderMouse);
				var newMarker = marker?.Clone() ?? new Marker { Frame = frameUnderMouse };
				var r = new MarkerPropertiesDialog().Show(newMarker, canDelete: marker != null);
				if (r == MarkerPropertiesDialog.Result.Ok) {
					Core.Operations.SetMarker.Perform(newMarker, true);
				} else if (r == MarkerPropertiesDialog.Result.Delete) {
					Core.Operations.DeleteMarker.Perform(marker, true);
				}
			});
		}

		private static void DeleteMarker(Marker marker)
		{
			Document.Current.History.DoTransaction(() => {
				Core.Operations.DeleteMarker.Perform(marker, true);
			});
		}

		public static void DeleteMarkers()
		{
			Document.Current.History.DoTransaction(() => {
				foreach (var marker in Document.Current.Animation.Markers.ToList()) {
					Core.Operations.DeleteMarker.Perform(marker, true);
				}
			});
		}

		private static void DeleteMarkersInRange()
		{
			using (Document.Current.History.BeginTransaction()) {
				if (!GridSelection.GetSelectionBoundaries(out var gs)) {
					new AlertDialog("Select a range on the timeline", "Ok").Show();
					return;
				}
				foreach (
					var marker in Document.Current.Animation.Markers.Where(m =>
						m.Frame >= gs.Left && m.Frame <= gs.Right).ToList()
				) {
					Core.Operations.DeleteMarker.Perform(marker, true);
				}
				Document.Current.History.CommitTransaction();
			}
		}

		class ContextMenu
		{
			public void Show()
			{
				var menu = new Menu();
				var frameUnderMouse = Timeline.Instance.Grid.CellUnderMouse().X;
				var marker = Document.Current.Animation.Markers.GetByFrame(frameUnderMouse);
				menu.Add(new Command(marker == null ? "Add Marker" : "Edit Marker", () => ShowMarkerDialog(frameUnderMouse)));
				menu.Add(new Command("Copy Marker", () => Common.Operations.CopyPasteMarkers.CopyMarkers(new [] { marker })) {
					Enabled = marker != null
				});
				menu.Add(new Command("Delete Marker", () => DeleteMarker(marker)) {
					Enabled = marker != null
				});
				menu.Add(Command.MenuSeparator);
				menu.Add(new Command("Copy All Markers", () => Common.Operations.CopyPasteMarkers.CopyMarkers(Document.Current.Animation.Markers)) {
					Enabled = Document.Current.Animation.Markers.Count > 0
				});
				menu.Add(new Command("Paste Markers", () => PasteMarkers(atCursor: true, expandAnimation: true)));
				menu.Add(new Command("Paste Markers At Original Positions", () => PasteMarkers(atCursor: false, expandAnimation: false)));
				menu.Add(new Command("Delete All Markers", DeleteMarkers) {
					Enabled = Document.Current.Animation.Markers.Count > 0
				});
				menu.Add(new Command("Delete Markers In Range", DeleteMarkersInRange) {
					Enabled = GridSelection.GetSelectionBoundaries(out _) && Document.Current.Animation.Markers.Count > 0
				});
				menu.Popup();
			}
		}
	}
}
