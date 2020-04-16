using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine
{
	public class LookupDialog
	{
		// This implementation of Lookup Dialog is quick and dirty.
		// In case of adding features (async, file lookup, Widget lookup, Command lookup) consider revising the architecture from scratch.
		private static Window window;
		private static List<(Frame Frame, string Text)> items = new List<(Frame Frame, string Text)>();
		private static ThemedScrollView scrollView;
		private static WindowWidget windowWidget;
		private static ThemedEditBox editBox;
		private static int selectedIndex = 0;

		public LookupDialog()
		{
			if (window == null) {
				CreateWindow();
			} else {
				window.Activate();
				window.Visible = true;
			}
			windowWidget.FocusScope.SetDefaultFocus();
			FillItems();
		}

		private void CreateWindow()
		{
			window = new Window(new WindowOptions {
				Style = WindowStyle.Borderless,
			});
			windowWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
						new Widget {
							Layout = new VBoxLayout { Spacing = 8 },
							LayoutCell = new LayoutCell(Alignment.LeftCenter),
							Padding = new Thickness { Top = 5 },
							Nodes = {
								(editBox = new ThemedEditBox()),
								(scrollView = new ThemedScrollView()),
							}
						}
					}
			};
			editBox.AddChangeWatcher(() => editBox.Text, RefreshFilter);
			windowWidget.FocusScope = new KeyboardFocusScope(windowWidget);
			windowWidget.LateTasks.AddLoop(() => {
				var input = windowWidget.Input;
				if (input.ConsumeKeyPress(Key.Escape)) {
					window.Visible = false;
				} else if (input.ConsumeKeyPress(Key.Enter)) {
					scrollView.Content.Nodes[selectedIndex].AsWidget.Clicked();
				} else if (input.ConsumeKeyPress(Key.Up)) {
					selectedIndex = Math.Max(selectedIndex - 1, 0);
					Window.Current.Invalidate();
				} else if (input.ConsumeKeyPress(Key.Down)) {
					selectedIndex = Math.Min(selectedIndex + 1, scrollView.Content.Nodes.Count);
					Window.Current.Invalidate();
				}
			});
			//rootWidget.CompoundPostPresenter.Add(new LayoutDebugPresenter(Color4.Red, 2.0f));
			scrollView.Content.Layout = new VBoxLayout();
			//scrollView.Content.CompoundPostPresenter.Add(new LayoutDebugPresenter(Color4.Blue.Transparentify(0.5f), 2.0f));
		}

		private void RefreshFilter(string text)
		{
			var itemsTemp = new List<(Frame Frame, int Distance)>();
			var matches = new List<int>();
			if (!string.IsNullOrEmpty(text)) {
				foreach (var (f, t) in items) {
					var i = -1;
					var d = 0;
					foreach (var c in text) {
						if (i == t.Length - 1) {
							i = -1;
						}
						var ip = i;
						var lci = t.IndexOf(char.ToLowerInvariant(c), i + 1);
						var uci = t.IndexOf(char.ToUpperInvariant(c), i + 1);
						i = lci != -1 && uci != -1 ? Math.Min(lci, uci) :
							lci == -1 ? uci : lci;
						if (i == -1) {
							break;
						}
						matches.Add(i);
						if (ip != -1) {
							d += (i - ip) * (i - ip);
						}
					}
					var presenter = (f.Nodes[0].CompoundPresenter[1] as HighlightMatchesPresenter);
					if (i != -1) {
						itemsTemp.Add((f, d));
						presenter.Matches = matches.ToList();
					} else {
						presenter.Matches = null;
					}
					matches.Clear();
				}
				itemsTemp.Sort((a, b) => {
					return a.Distance.CompareTo(b.Distance);
				});
				scrollView.Content.Nodes.Clear();
				foreach (var (f, d) in itemsTemp) {
					scrollView.Content.Nodes.Add(f);
				}
			} else {
				scrollView.Content.Nodes.Clear();
				foreach (var (f, t) in items) {
					var presenter = (f.Nodes[0].CompoundPresenter[1] as HighlightMatchesPresenter);
					presenter.Matches = null;
					scrollView.Content.Nodes.Add(f);
				}
			}
		}

		private void FillItems()
		{
			foreach (var (f, _) in items) {
				f.UnlinkAndDispose();
			}
			items.Clear();
			if (Document.Current == null) {
				return;
			}
			foreach (var m in Document.Current.Animation.Markers) {
				var mClosed = m;
				var text = m.Id;
				text = m.Action.ToString() + " Marker '" + text + "' at Frame: " + m.Frame + " in " + Document.Current.Animation.Owner.Owner;
				scrollView.Content.AddNode(CreateItem(text, () => {
					Document.SetCurrentFrameToNode(Document.Current.Animation, mClosed.Frame, true);
					UI.Timeline.Operations.CenterTimelineOnCurrentColumn.Perform();
				}));
			}
		}

		private Widget CreateItem(string text, Action action)
		{
			ThemedSimpleText textWidget;
			var r = new Frame {
				Nodes = {
						(textWidget = new ThemedSimpleText {
							Text = text,
							ForceUncutText = false,
							Padding = new Thickness(left: 5.0f),
							MinHeight = 20.0f,
						})
					},
				Layout = new HBoxLayout(),
				Clicked = () => {
					window.Visible = false;
					editBox.Text = string.Empty;
					action();
				},
				HitTestTarget = true,
				Padding = new Thickness(10),
			};
			textWidget.CompoundPresenter.Add(new HighlightMatchesPresenter());
			r.CompoundPresenter.Add(new ItemPresenter());
			//r.CompoundPostPresenter.Add(new LayoutDebugPresenter(Color4.Green.Transparentify(0.5f), 2.0f));
			r.Tasks.Add(Lime.Theme.MouseHoverInvalidationTask(r));
			items.Add((r, text));
			return r;
		}

		private class HighlightMatchesPresenter : IPresenter
		{
			public List<int> Matches;
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				if (widget.GloballyEnabled) {
					var ro = RenderObjectPool<RenderObject>.Acquire();
					ro.CaptureRenderState(widget);
					ro.Matches = Matches;
					ro.TextWidget = (SimpleText)node;
					return ro;
				}
				return null;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public SimpleText TextWidget;
				public List<int> Matches;

				public override void Render()
				{
					if (Matches == null) {
						return;
					}
					PrepareRenderState();
					var text = TextWidget.Text;
					int i = 0;
					float dx = TextWidget.Padding.Left;
					foreach (var m in Matches) {
						var v0 = TextWidget.Font.MeasureTextLine(text, TextWidget.FontHeight, i, m - i, TextWidget.LetterSpacing);
						v0.Y = TextWidget.Padding.Top;
						var v1 = TextWidget.Font.MeasureTextLine(text, TextWidget.FontHeight, m, 1, TextWidget.LetterSpacing);
						v0.X += dx;
						v1.X += v0.X;
						v1.Y += v0.Y;
						dx = v1.X;
						Renderer.DrawRect(v0, v1, Theme.Colors.TextSelection);
						i = m + 1;
					}
				}
			}
		}


		private class ItemPresenter : IPresenter
		{
			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				if (widget.GloballyEnabled && (widget.IsMouseOverThisOrDescendant() || selectedIndex == scrollView.Content.Nodes.IndexOf(node))) {
					var ro = RenderObjectPool<RenderObject>.Acquire();
					ro.CaptureRenderState(widget);
					ro.Size = widget.Size;
					ro.Color = Theme.Colors.KeyboardFocusBorder;
					return ro;
				}
				return null;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public Vector2 Size;
				public Color4 Color;

				public override void Render()
				{
					PrepareRenderState();
					Renderer.DrawRect(Vector2.Zero, Size, Color);
				}
			}
		}
	}
}
