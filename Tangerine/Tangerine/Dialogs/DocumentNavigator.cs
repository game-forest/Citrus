using System;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Docking;
using Application = Lime.Application;
using Panel = Tangerine.UI.Docking.Panel;
using Shortcut = Lime.Shortcut;

namespace Tangerine
{
	public class DocumentNavigator
	{
		public static DocumentNavigator Instance { get; private set; }

		private readonly Window navigatorWindow;
		private readonly Widget windowWidget;
		private readonly NavigatorWidget navigatorWidget;
		private Shortcut nextDocumentShortcut;
		private Shortcut previousDocumentShortcut;
		
		public DocumentNavigator(int startFocusedDocumentOffset)
		{
			if (Instance != null) {
				throw new System.Exception();
			}
			Instance = this;
			Vector2? displayCenter = null;
			try {
				var display = CommonWindow.Current.Display;
				displayCenter = display.Position + display.Size / 2;
			} catch (System.ObjectDisposedException) {
				// Suppress
			}
			navigatorWidget = new NavigatorWidget(startFocusedDocumentOffset);
			navigatorWindow = new Window(new WindowOptions {
				Visible = false,
				FixedSize = true,
				Style = WindowStyle.Borderless,
				ClientSize = navigatorWidget.EffectiveMinSize
			});
			if (!displayCenter.HasValue) {
				var display = DockManager.Instance.MainWindowWidget.Window.Display;
				displayCenter = display.Position + display.Size / 2;
			}
			navigatorWindow.DecoratedPosition = displayCenter.Value - navigatorWindow.DecoratedSize / 2f;
			windowWidget = new ThemedInvalidableWindowWidget(navigatorWindow) {
				Nodes = { navigatorWidget },
			};
			windowWidget.Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Navigator.PanelBackground);
			if (
				GenericCommands.NextDocument.Shortcut != new Shortcut(Modifiers.Control, Key.Tab) ||
				GenericCommands.PreviousDocument.Shortcut != new Shortcut(Modifiers.Control | Modifiers.Shift, Key.Tab)
			) {
				throw new System.Exception("Unsupported shortcut");
			}
			var focusScope = new KeyboardFocusScope(windowWidget);
			focusScope.FocusNext.Clear();
			focusScope.FocusNext.Add(Key.MapShortcut(Modifiers.Control, Key.Tab));
			focusScope.FocusNext.Add(Key.MapShortcut(Modifiers.Control, Key.Down));
			focusScope.FocusPrevious.Clear();
			focusScope.FocusPrevious.Add(Key.MapShortcut(Modifiers.Control | Modifiers.Shift, Key.Tab));
			focusScope.FocusPrevious.Add(Key.MapShortcut(Modifiers.Control, Key.Up));
			windowWidget.FocusScope = focusScope;
			if (navigatorWidget.FocusedLabel == null) {
				focusScope.SetDefaultFocus();
			}
			InitializeTasks();
			navigatorWindow.ShowModal();
		}

		private void InitializeTasks()
		{
			// The navigator window is a window without a native menu. For this reason, when the navigator
			// window is open in modal mode, the commands from the native menu of the main window still work.
			// The navigator window is called by a shortcut (ctrl+tab) and inside the window we use the same
			// shortcut to switch the document, but it does not work because of the above. To get around this,
			// we temporarily remove commands from the native menu of the main window.
			ClearAndSaveConflictingShortcuts();
			int updateIndex = 0;
			windowWidget.Tasks.AddLoop(() => {
				if (updateIndex < 2) {
					++updateIndex;
					Application.Input.Simulator.SetKeyState(Key.Control, true);
				} else if (Application.Input.WasKeyReleased(Key.Control)) {
					CloseWindow(navigatorWidget.FocusedLabel);
				} else if (
					Application.Input.WasMouseReleased() &&
					navigatorWidget.HoveredLabel != null
				) {
					CloseWindow(navigatorWidget.HoveredLabel);
				}
			});
			navigatorWindow.Closed += () => navigatorWidget.UnlinkAndDispose();

			void CloseWindow(NavigatorWidget.LabelBase selectedLabel)
			{
				RestoreConflictingShortcuts();
				if (selectedLabel is NavigatorWidget.PanelLabel panelLabel) {
					panelLabel.Panel.PanelWidget.SetFocus();
				}
				if (selectedLabel is NavigatorWidget.DocumentLabel documentLabel) {
					documentLabel.Document.MakeCurrent();
				}
				Instance = null;
				navigatorWindow.Close();
			}
		}
		
		private void ClearAndSaveConflictingShortcuts()
		{
			nextDocumentShortcut = GenericCommands.NextDocument.Shortcut;
			previousDocumentShortcut = GenericCommands.PreviousDocument.Shortcut;
			GenericCommands.NextDocument.Shortcut = new Shortcut();
			GenericCommands.PreviousDocument.Shortcut = new Shortcut();
		}
		
		private void RestoreConflictingShortcuts()
		{
			GenericCommands.NextDocument.Shortcut = nextDocumentShortcut;
			GenericCommands.PreviousDocument.Shortcut = previousDocumentShortcut;
		}
		
		private class NavigatorWidget : Widget
		{
			private const float WindowSpacing = 8;
			private const float LabelsListWidth = 168;
			private const float LabelsListSpacing = 0;
			private const int LabelsListLength = 16;
			private const string AccentTextId = "at";
			
			private readonly RichText pathCaption;
			private readonly Widget panelsView;
			private readonly ThemedScrollView filesView;
			private readonly Vector2 windowSize;

			private static float PathCaptionTextHeight => Theme.Metrics.TextHeight;
			
			private static Thickness PathCaptionPadding => 
				new Thickness(left: 16, right: 16, top: 8, bottom: 4);

			private static float PathCaptionHeight
			{
				get
				{
					var padding = PathCaptionPadding;
					return padding.Bottom + padding.Top + PathCaptionTextHeight;
				}
			}

			private static float LabelTextHeight => Theme.Metrics.TextHeight;
			
			private static Thickness LabelPadding => 
				new Thickness(horizontal: 2, vertical: 1);

			private static Vector2 LabelSize
			{
				get
				{
					var contentSize = new Vector2(
						x: LabelsListWidth,
						y: LabelTextHeight
					);
					return CalculateSize(LabelPadding, contentSize);
				}
			}
			
			private static Thickness LabelsListPadding => new Thickness();
			
			private static Vector2 LabelsListSize
			{
				get
				{
					var labelSize = LabelSize;
					var contentSize = new Vector2(
						x: labelSize.X,
						y: labelSize.Y * LabelsListLength + (LabelsListLength - 1) * LabelsListSpacing + 4
					);
					return CalculateSize(LabelsListPadding, contentSize);
				}
			}

			private static Thickness SeparationLinePadding => 
				new Thickness(left: 6, right: 6, top: 0, bottom: 4);

			private static float SeparationLineHeight
			{
				get
				{
					var padding = SeparationLinePadding;
					return padding.Bottom + padding.Top + 2;
				}
			}

			private static float PanelCaptionTextHeight => 1.30f * Theme.Metrics.TextHeight;
			
			private static Thickness PanelCaptionPadding => new Thickness(horizontal: 0, vertical: 4);
			
			private static float PanelCaptionHeight
			{
				get
				{
					var padding = PanelCaptionPadding;
					return padding.Bottom + padding.Top + PanelCaptionTextHeight;
				}
			}

			private static Thickness WindowPadding => new Thickness(left: 16, right: 16, top: 0, bottom: 12);
			
			private static Vector2 WindowSize
			{
				get
				{
					var listSize = LabelsListSize;
					var contentSize = new Vector2(
						x: 4 * listSize.X + WindowSpacing,
						y: 1 * listSize.Y + SeparationLineHeight + PanelCaptionHeight + PathCaptionHeight
					);
					return CalculateSize(WindowPadding, contentSize);
				}
			}

			public LabelBase FocusedLabel { get; private set; }
			
			public LabelBase HoveredLabel { get; private set; }
			
			public NavigatorWidget(int startFocusedDocumentOffset)
			{
				windowSize = WindowSize;
				pathCaption = new RichText {
					Localizable = false,
					Color = Color4.White,
					HAlignment = HAlignment.Left,
					VAlignment = VAlignment.Top,
					OverflowMode = TextOverflowMode.Ignore,
					TrimWhitespaces = true,
					Nodes = {
						new TextStyle {
							Size = PathCaptionTextHeight,
							TextColor = Theme.Colors.BlackText,
						},
						new TextStyle {
							Id = AccentTextId,
							Size = PathCaptionTextHeight,
							TextColor = ColorTheme.Current.Navigator.AccentColor,
						},
					},
				};
				SetFilePathCaption(Document.Current.FullPath);
				var labelsListSize = LabelsListSize;
				panelsView = new Widget {
					Layout = new VBoxLayout(),
					MinMaxSize = labelsListSize
				};
				filesView = new ThemedScrollView(ScrollDirection.Horizontal) {
					MinMaxSize = new Vector2(3 * labelsListSize.X, labelsListSize.Y)
				};
				filesView.CompoundPresenter.Add(
					new WidgetFlatFillPresenter(ColorTheme.Current.Navigator.ListBackground)
				);
				var hoveredItemBackground = ColorTheme.Current.Navigator.HoveredItemBackground;
				filesView.Content.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
					if (FocusedLabel is DocumentLabel label) {
						label.PrepareRendererState();
						Renderer.DrawRect(Vector2.Zero, label.Size, hoveredItemBackground);
					}
				}));
				panelsView.Presenter = new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					Renderer.DrawRect(Vector2.Zero, w.Size, ColorTheme.Current.Navigator.ListBackground);
					if (FocusedLabel is PanelLabel label) {
						label.PrepareRendererState();
						Renderer.DrawRect(Vector2.Zero, label.Size, hoveredItemBackground);
					}
				});
				MinMaxSize = windowSize;
				Layout = new VBoxLayout();
				AddNode(new Widget {
					Layout = new HBoxLayout(),
					Padding = PathCaptionPadding,
					Nodes = { pathCaption },
					MinMaxHeight = PathCaptionHeight,
				});
				AddNode(new Widget {
					Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Navigator.AccentColor),
					Padding = SeparationLinePadding,
					MinMaxHeight = SeparationLineHeight,
					Anchors = Anchors.LeftRight,
				});
				var windowPadding = WindowPadding;
				AddNode(new Widget {
					Layout = new HBoxLayout {
						Spacing = WindowSpacing
					},
					MinMaxWidth = Width = windowSize.X,
					Padding = windowPadding,
					Nodes = {
						new Widget {
							Layout = new VBoxLayout(),
							Nodes = {
								new ThemedSimpleText("Active Panels") {
									FontHeight = PanelCaptionTextHeight,
									MinMaxHeight = PanelCaptionHeight
								},
								panelsView,
							}
						},
						new Widget {
							Layout = new VBoxLayout(),
							Nodes = {
								new ThemedSimpleText("Active Documents") {
									FontHeight = PanelCaptionTextHeight,
									MinMaxHeight = PanelCaptionHeight
								},
								filesView,
							}
						}
					}
				});
				var documents = Project.Current.Documents.ToArray();
				FillContents(documents);
				SelectCurrentDocument(documents, startFocusedDocumentOffset);
				LateTasks.AddLoop(HoverTask);
				LateTasks.AddLoop(FocusTask);
			}

			private void FillContents(Document[] documents)
			{
				var labelSize = LabelSize;
				var labelPadding = LabelPadding;
				var fontHeight = LabelTextHeight;
				var labelsListSize = LabelsListSize;
				foreach (var panel in DockHierarchy.Instance.Panels) {
					if (panel.PanelWidget != null && panel.PanelWidget.Visible) {
						panelsView.AddNode(new PanelLabel(panel) {
							FontHeight = fontHeight,
							Padding = labelPadding,
							MinMaxSize = labelSize
						});
					}
				}
				filesView.Content.Layout = new HBoxLayout();
				for (int i = 0; i < documents.Length; i += LabelsListLength) {
					int segmentLength = Math.Min(LabelsListLength, documents.Length - i);
					var segment = new ArraySegment<Document>(documents, offset: i, count: segmentLength);
					var labelList = CreateLabelList(segment);
					labelList.Position = new Vector2((i / LabelsListLength) * labelsListSize.X, 0);
					filesView.Content.AddNode(labelList);
				}
			}

			private void SelectCurrentDocument(Document[] documents, int startFocusedDocumentOffset)
			{
				if (Project.Current.Documents.Count > 0) {
					int currentIndex = Array.IndexOf(documents, Document.Current) + startFocusedDocumentOffset;
					var targetDocument = documents[currentIndex.Wrap(0, documents.Length - 1)];
					FocusedLabel = (LabelBase)filesView.Content.Nodes
						.SelectMany(n => n.Nodes)
						.First(n => ((DocumentLabel)n).Document == targetDocument);
					FocusedLabel.Color = ColorTheme.Current.Navigator.AccentColor;
					FocusedLabel.SetFocus();
				}
			}
			
			private void HoverTask()
			{
				var nodeUnderMouse = WidgetContext.Current?.NodeUnderMouse;
				var hoveredFileLabel = TryGetSelectedLabel(nodeUnderMouse, filesView.Content);
				var hoveredPanelLabel = TryGetSelectedLabel(nodeUnderMouse, panelsView.Parent);
				var hoveredLabel = hoveredFileLabel ?? hoveredPanelLabel;
				if (HoveredLabel != hoveredLabel) {
					if (HoveredLabel != null && HoveredLabel != FocusedLabel) {
						HoveredLabel.Color = Theme.Colors.BlackText;
					}
					HoveredLabel = hoveredLabel;
					if (HoveredLabel != null) {
						HoveredLabel.Color = ColorTheme.Current.Navigator.AccentColor;
					}
				}
			}

			private void FocusTask()
			{
				var focusedFileLabel = TryGetSelectedLabel(Widget.Focused, filesView.Content);
				var focusedPanelLabel = TryGetSelectedLabel(Widget.Focused, panelsView.Parent);
				var focusedLabel = focusedFileLabel ?? focusedPanelLabel;
				if (Application.Input.IsMousePressed() && HoveredLabel != null) {
					focusedLabel = HoveredLabel;
				}
				NavigateLeftOrRight(ref focusedLabel);
				if (FocusedLabel != focusedLabel) {
					if (FocusedLabel != null && FocusedLabel != HoveredLabel) {
						FocusedLabel.Color = Theme.Colors.BlackText;
					}
					FocusedLabel = focusedLabel;
					if (FocusedLabel != null) {
						FocusedLabel.SetFocus();
						FocusedLabel.Color = ColorTheme.Current.Navigator.AccentColor;
						if (focusedLabel is DocumentLabel documentLabel) {
							var labelsList = documentLabel.Parent.AsWidget;
							if (!filesView.Behaviour.IsItemFullyOnscreen(labelsList)) {
								var position = filesView.Behaviour.PositionToViewFully(labelsList);
								filesView.Behaviour.ScrollTo(position);
							}
							SetFilePathCaption(documentLabel.Document.FullPath);
						}
						if (focusedLabel is PanelLabel panelLabel) {
							SetPanelPathCaption(panelLabel.Panel);
						}
					}
				}
				
				void NavigateLeftOrRight(ref LabelBase focusedLabel)
				{
					if (focusedLabel == null) {
						focusedLabel = null;
						return;
					}
					bool havePanels = panelsView.Nodes.Count > 0;
					bool haveDocuments = filesView.Content.Nodes.Count > 0;
					var currentList = focusedLabel.Parent.Nodes;
					int index = currentList.IndexOf(focusedLabel);
					if (Input.ConsumeKeyRepeat(Key.Left)) {
						if (focusedLabel is PanelLabel && haveDocuments) {
							focusedLabel = GetDocumentLabel(filesView.Content.Nodes.Count - 1);
							return;
						}
						if (focusedLabel is DocumentLabel) {
							int currentListIndex = filesView.Content.Nodes.IndexOf(focusedLabel.Parent);
							if (currentListIndex == 0) {
								if (havePanels) {
									focusedLabel = GetPanelLabel();
									return;
								}
							} else {
								focusedLabel = GetDocumentLabel(currentListIndex - 1);
								return;
							}
						}
					}
					if (Input.ConsumeKeyRepeat(Key.Right)) {
						if (focusedLabel is PanelLabel && haveDocuments) {
							focusedLabel = GetDocumentLabel(0);
							return;
						}
						if (focusedLabel is DocumentLabel) {
							int currentListIndex = filesView.Content.Nodes.IndexOf(focusedLabel.Parent);
							if (currentListIndex == filesView.Content.Nodes.Count - 1) {
								if (havePanels) {
									focusedLabel = GetPanelLabel();
								}
							} else {
								focusedLabel = GetDocumentLabel(currentListIndex + 1);
							}
						}
					}

					LabelBase GetPanelLabel()
					{
						if (index < panelsView.Nodes.Count) {
							return (LabelBase)panelsView.Nodes[index];
						}
						return (LabelBase)panelsView.Nodes.Last();
					}
					LabelBase GetDocumentLabel(int documentListIndex)
					{
						var targetList = filesView.Content.Nodes[documentListIndex].Nodes;
						if (index < targetList.Count) {
							return (LabelBase)targetList[index];
						}
						return (LabelBase)targetList.Last();
					}
				}
			}

			private LabelBase TryGetSelectedLabel(Node labelDescendant, Node container)
			{
				var selected = labelDescendant;
				if (selected == null) {
					return null;
				}
				Node selectedChild = null;
				while (selected.Parent != null) {
					if (selected.Parent == container) {
						return selectedChild as LabelBase;
					}
					selectedChild = selected;
					selected = selected.Parent;
				}
				return null;
			}

			private void SetPanelPathCaption(Panel panel)
			{
				pathCaption.Text = $"Navigate to <{AccentTextId}>{panel.Title}</{AccentTextId}> panel";
			}
			
			private void SetFilePathCaption(string text)
			{
				string path = GetClampedPath(text);
				string file = Path.GetFileName(path);
				string directory = path.Substring(0, path.Length - file.Length - 1);
				pathCaption.Text = $"{directory}\\<{AccentTextId}>{file}</{AccentTextId}>";
			}
			
			private string GetClampedPath(string path)
			{
				var originalPath = path;
				var padding = WindowPadding;
				var font = FontPool.Instance.DefaultFont;
				float pathWidth = font.MeasureTextLine(path, PathCaptionTextHeight, 0).X;
				while (pathWidth > windowSize.X - padding.Left) {
					path = path.Substring(1);
					pathWidth = font.MeasureTextLine(path, PathCaptionTextHeight, 0).X;
				}
				if (originalPath.Length != path.Length) {
					path = "..." + path.Substring(3);
				}
				return path;
			}
			
			private Widget CreateLabelList(ArraySegment<Document> documents)
			{
				var labelList = new Widget {
					Layout = new VBoxLayout(),
					Padding = LabelsListPadding,
					MinMaxSize = LabelsListSize,
				};
				var labelSize = LabelSize;
				var labelPadding = LabelPadding;
				var fontHeight = LabelTextHeight;
				foreach (var document in documents) {
					labelList.AddNode(new DocumentLabel(document) {
						FontHeight = fontHeight,
						Padding = labelPadding,
						MinMaxSize = labelSize
					});
				}
				return labelList;
			}
			
			private static Vector2 CalculateSize(Thickness padding, Vector2 contentSize) => 
				contentSize + new Vector2(x: padding.Left + padding.Right, y: padding.Bottom + padding.Top);

			public class LabelBase : ThemedSimpleText
			{
				protected LabelBase(string text) : base(text) {}
			}
			
			public class PanelLabel : LabelBase
			{
				public readonly Panel Panel;
				
				public PanelLabel(Panel panel) : base(panel.Title)
				{
					TabTravesable = new TabTraversable();
					HitTestTarget = true;
					Panel = panel;
				}
			}
			
			public class DocumentLabel : LabelBase
			{
				public readonly Document Document;
				
				public DocumentLabel(Document document) : base(document.DisplayName)
				{
					TabTravesable = new TabTraversable();
					HitTestTarget = true;
					Document = document;
				}
			}
		}
	}
}
