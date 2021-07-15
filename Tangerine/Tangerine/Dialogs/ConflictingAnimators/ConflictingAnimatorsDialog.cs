using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine
{
	public class ConflictingAnimatorsDialog
	{
		public static class ConflictingAnimatorsWindowWidgetProvider {
			private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
			private const string Title = @"Conflicting Animators";
			private const WindowStyle Style = WindowStyle.Regular;

			private static Vector2? position;
			private static WindowWidget instance;

			public static WindowWidget Get()
			{
				if (instance == null) {
					var display = DockManager.Instance.MainWindowWidget.Window.Display;
					var displayCenter = display.Position + display.Size / 2;

					var window = new Window(new WindowOptions {
						Title = Title,
						Style = Style,
						Icon = new System.Drawing.Icon(new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()),
					});
					window.DecoratedPosition = position ?? (displayCenter - window.DecoratedSize / 2f);
					window.Closing += OnClose;

					var scrollView = CreateScrollView();
					var controls = CreateControls(scrollView);
					instance = new ThemedInvalidableWindowWidget(window) {
						Layout = new VBoxLayout { Spacing = 8 },
						Padding = new Thickness(8),
						Nodes = {
							scrollView,
							controls,
						},
					};
					instance.FocusScope = new KeyboardFocusScope(instance);
				}
				return instance;
			}

			private static ThemedScrollView CreateScrollView()
			{
				var scrollView = new ThemedScrollView {
					Content = {
						Layout = new VBoxLayout { Spacing = 16 },
						Padding = new Thickness(8),
					},
				};
				scrollView.Content.CompoundPostPresenter.AddRange(new[] {
					new SyncDelegatePresenter<Widget>((w) => {
						w.PrepareRendererState();
						var rect = CalcRect(w);
						Renderer.DrawRect(rect.A, rect.B, Theme.Colors.GrayBackground.Transparentify(0.9f));
					}),
					new SyncDelegatePresenter<Widget>((w) => {
						w.PrepareRendererState();
						var rect = CalcRect(w);
						Renderer.DrawRectOutline(rect.A, rect.B, Theme.Colors.ControlBorder);
					}),
				});
				return scrollView;

				static Rectangle CalcRect(Widget w)
				{
					var wp = w.ParentWidget;
					var p = wp.Padding;
					return new Rectangle(
						-w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
						-w.Position + wp.Size + new Vector2(p.Right, p.Bottom)
					);
				}
			}

			private static Widget CreateControls(ThemedScrollView scrollView)
			{
				return new Widget {
					//Padding = new Thickness(8),
					Layout = new HBoxLayout { Spacing = 8 },
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Nodes = {
						new ThemedButton {
							Text = "Search",
							Clicked = () => {
								scrollView.Content.Nodes.Clear();
								foreach (var info in ConflictingAnimatorsInfoProvider.Get(Document.Current.Path)) {
									scrollView.Content.AddNode(new ConflictingAnimatorsItem(info));
								}
							},
						},
					},
				};
			}

			public static bool OnClose(CloseReason reason)
			{
				position = instance?.Window?.DecoratedPosition;
				instance = null;
				return true;
			}
		}

		public ConflictingAnimatorsDialog()
		{
			var windowWidget = ConflictingAnimatorsWindowWidgetProvider.Get();
			windowWidget.Window.Restore();
			windowWidget.SetFocus();
		}
	}
}
