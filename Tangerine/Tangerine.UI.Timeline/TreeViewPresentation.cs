using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Timeline
{
	public class TreeViewItemPresentationOptions
	{
		public Func<string> SearchStringGetter;
		public bool Minimalistic;
	}

	public class TreeViewItemPresentationProcessor : ITreeViewItemPresentationProcessor
	{
		public void Process(ITreeViewItemPresentation presentation)
		{
			if (presentation is TreeViewItemPresentation p) {
				p.ExpandButton.Texture =
					p.Minimalistic && p.Item.Expanded ||
					!p.Minimalistic && p.SceneItem.GetTimelineItemState().NodesExpanded
					? IconPool.GetTexture("Timeline.Expanded")
					: IconPool.GetTexture("Timeline.Collapsed");
				p.ExpandButton.Visible = p.Item.CanExpand();
				p.IndentationSpacer.MinMaxWidth =
					TreeViewPresentation.CalcIndent(p.Item) * TreeViewPresentation.IndentWidth;
				p.Label.Text = p.Item.Label;
			}
		}
	}

	public class NodeTreeViewItemLabelProcessor : ITreeViewItemPresentationProcessor
	{
		public void Process(ITreeViewItemPresentation presentation)
		{
			if (presentation is NodeTreeViewItemPresentation p) {
				if (!string.IsNullOrEmpty(p.Node.ContentsPath)) {
					p.Label.Text = $"{p.Label.Text} [{p.Node.ContentsPath}]";
				}
				p.Label.Color = IsGrayedLabel(p.Node)
					? ColorTheme.Current.TimelineRoll.GrayedLabel
					: Theme.Colors.BlackText;
			}
		}

		private static bool IsGrayedLabel(Node node) =>
			node is Widget w && (!w.Visible || w.Color.A == 0) ||
			node is Frame frame && frame.ClipChildren == ClipMethod.NoRender;
	}

	public class TreeViewItemPresentation : ITreeViewItemPresentation
	{
		public readonly Widget ExpandButtonContainer;
		public readonly ToolbarButton ExpandButton;
		public readonly SimpleText Label;
		public readonly Widget IndentationSpacer;
		public readonly TreeViewItem Item;
		public readonly Row SceneItem;
		public readonly bool Minimalistic;
		public readonly TreeView TreeView;

		public Widget Widget { get; }

		public TreeViewItemPresentation(TreeView treeView, TreeViewItem item, Row sceneItem, TreeViewItemPresentationOptions options)
		{
			TreeView = treeView;
			Item = item;
			SceneItem = sceneItem;
			Minimalistic = options.Minimalistic;
			Widget = new Widget {
				MinMaxHeight = TimelineMetrics.DefaultRowHeight,
				Layout = new HBoxLayout {
					DefaultCell = new DefaultLayoutCell { VerticalAlignment = VAlignment.Center }
				},
				Padding = new Thickness { Right = 10 },	// Add padding for the scrollbar.
				Presenter = new SyncDelegatePresenter<Widget>(w => {
					w.PrepareRendererState();
					Renderer.DrawRect(Vector2.Zero, w.Size, ColorTheme.Current.Hierarchy.DefaultBackground);
					bool isSelected = Item.Selected;
					bool isHovered = Item == TreeView.HoveredItem;
					if (isSelected | isHovered) {
						Renderer.PushState(RenderState.Blending);
						Renderer.Blending = Blending.Add;
						if (isSelected) {
							var color = w.ParentWidget.IsFocused() ?
								ColorTheme.Current.Hierarchy.SelectedBackground :
								ColorTheme.Current.Hierarchy.SelectedInactiveBackground;
							Renderer.DrawRect(Vector2.Zero, w.Size, color);
						}
						if (isHovered) {
							Renderer.DrawRect(Vector2.Zero, w.Size, ColorTheme.Current.Hierarchy.HoveredBackground);
						}
						Renderer.PopState();
					}
					HighlightLabel(options.SearchStringGetter());
				})
			};
			Label = CreateLabel(Item);
			ExpandButton = CreateExpandButton();
			var nodeIcon = new Image(Item.Icon) {
				HitTestTarget = true,
				MinMaxSize = new Vector2(16),
			};
			nodeIcon.AddLateChangeWatcher(
				() => {
					var node = sceneItem.GetNode();
					if (node == null) {
						return 0;
					}
					var result = 17;
					unchecked {
						foreach (var component in node.Components) {
							result = result * 23 + component.GetHashCode();
						}
					}
					return result;
				},
				_ => nodeIcon.Texture = Item.Icon
			);
			IndentationSpacer = new Widget();
			Widget.Nodes.Add(IndentationSpacer);
			ExpandButtonContainer = new Widget {
				MinMaxSize = Theme.Metrics.DefaultToolbarButtonSize,
				Nodes = { ExpandButton }
			};
			Widget.Nodes.Add(ExpandButtonContainer);
			Widget.Nodes.Add(Spacer.HSpacer(3));
			Widget.Nodes.Add(nodeIcon);
			Widget.Nodes.Add(Spacer.HSpacer(3));
			Widget.Nodes.Add(Label);
		}

		private void HighlightLabel(string searchString)
		{
			if (string.IsNullOrEmpty(searchString)) {
				return;
			}
			int index;
			int previousIndex = 0;
			var pos = Label.CalcPositionInSpaceOf(Widget);
			while ((index = Label.Text.IndexOf(searchString, previousIndex, StringComparison.OrdinalIgnoreCase)) >= 0) {
				var skipSize = Label.Font.MeasureTextLine(
					Label.Text, Label.FontHeight, previousIndex, index - previousIndex, Label.LetterSpacing);
				var searchStringSize = Label.Font.MeasureTextLine(
					Label.Text, Label.FontHeight, index, searchString.Length, Label.LetterSpacing);
				pos.X += skipSize.X;
				Renderer.DrawRect(pos.X, 0, pos.X + searchStringSize.X, Widget.Height,
					ColorTheme.Current.Hierarchy.MatchColor);
				pos.X += searchStringSize.X;
				previousIndex = index + searchString.Length;
			}
		}

		private SimpleText CreateLabel(TreeViewItem item)
		{
			var label = new ThemedSimpleText {
				HitTestTarget = true,
				// To display ellipsis-minified text if the panel is too narrow.
				ForceUncutText = false,
				VAlignment = VAlignment.Center,
				OverflowMode = TextOverflowMode.Ellipsis,
				LayoutCell = new LayoutCell(Alignment.LeftCenter, float.MaxValue),
			};
			label.Gestures.Add(new DoubleClickGesture(() => {
				var labelExtent = label.MeasureUncutText();
				if (Item.CanRename() && label.LocalMousePosition().X < labelExtent.X) {
					Rename();
				} else {
					TreeView.RaiseActivated(item, TreeView.ActivationMethod.Mouse);
				}
			}));
			return label;
		}

		public void Rename()
		{
			if (!Label.Visible) {
				return;
			}
			((WindowWidget) Label.GetRoot()).Window.Activate();
			Label.Visible = false;
			var idx = Label.Parent.Nodes.IndexOf(Label);
			var editBoxContainer = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell(Alignment.LeftCenter, float.MaxValue)
			};
			Label.Parent.Nodes.Insert(idx, editBoxContainer);
			var editBox = new ThemedEditBox {
				LayoutCell = new LayoutCell(Alignment.Center),
				Text = Item.Label
			};
			editBoxContainer.AddNode(editBox);
			editBox.Tasks.Add(EditObjectIdTask(editBoxContainer, editBox, Label));
		}

		private IEnumerator<object> EditObjectIdTask(Widget container, EditBox editBox, SimpleText label)
		{
			// Skip one update since tabbedWidget gets the focus in the first place.
			yield return null;
			editBox.SetFocus();
			while (editBox.IsFocused()) {
				yield return null;
				if (!Item.Selected) {
					editBox.RevokeFocus();
				}
			}
			Item.Label = editBox.Text;
			container.Unlink();
			label.Visible = true;
		}

		private ToolbarButton CreateExpandButton()
		{
			var button = new ToolbarButton { Highlightable = false };
			if (Minimalistic) {
				button.Clicked += () => {
					Item.Expanded = !Item.Expanded;
				};
			} else {
				button.AddTransactionClickHandler(() => {
					DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
					SetProperty.Perform(
						obj: SceneItem.GetTimelineItemState(),
						propertyName: nameof(TimelineItemStateComponent.NodesExpanded),
						value: !SceneItem.GetTimelineItemState().NodesExpanded,
						isChangingDocument: false
					);
					DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
				});
			}
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}
	}

	public class FolderTreeViewItemPresentation : TreeViewItemPresentation
	{
		public FolderTreeViewItemPresentation(TreeView treeView, TreeViewItem item, Row sceneItem, TreeViewItemPresentationOptions options)
			: base(treeView, item, sceneItem, options)
		{
			if (!options.Minimalistic) {
				Widget.Nodes.Add(CreateEyeButton());
				Widget.Nodes.Add(CreateLockButton());
			}
			Widget.Gestures.Add(new ClickGesture(1, ShowContextMenu));
		}

		private void ShowContextMenu()
		{
			if (!SceneItem.GetTimelineItemState().Selected) {
				Document.Current.History.DoTransaction(() => {
					ClearRowSelection.Perform();
					SelectRow.Perform(SceneItem);
				});
			}
			var menu = new Menu {
				Command.Cut,
				Command.Copy,
				Command.Paste,
				Command.Delete,
				Command.MenuSeparator,
				new Command("Rename", Rename),
			};
			menu.Popup();
		}

		private ToolbarButton CreateEyeButton()
		{
			var button = new ToolbarButton {
				Highlightable = false,
				Texture = IconPool.GetTexture("Timeline.Eye")
			};
			button.AddTransactionClickHandler(
				() => {
					var visibility = NodeVisibility.Hidden;
					if (InnerNodes(SceneItem).All(i => i.EditorState().Visibility == NodeVisibility.Hidden)) {
						visibility = NodeVisibility.Shown;
					} else if (InnerNodes(SceneItem).All(i => i.EditorState().Visibility == NodeVisibility.Shown)) {
						visibility = NodeVisibility.Default;
					}
					foreach (var node in InnerNodes(SceneItem)) {
						SetProperty.Perform(node.EditorState(), nameof(NodeEditorState.Visibility), visibility);
					}
				}
			);
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		private ToolbarButton CreateLockButton()
		{
			var button = new ToolbarButton {
				Highlightable = false,
				Texture = IconPool.GetTexture("Timeline.Lock")
			};
			button.AddTransactionClickHandler(() => {
				var locked = InnerNodes(SceneItem).All(i => !i.EditorState().Locked);
				foreach (var node in InnerNodes(SceneItem)) {
					SetProperty.Perform(node.EditorState(), nameof(NodeEditorState.Locked), locked);
				}
			});
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		IEnumerable<Node> InnerNodes(Row item)
		{
			foreach (var i in item.Rows) {
				if (i.TryGetNode(out var node)) {
					yield return node;
				} else if (i.TryGetFolder(out var folder)) {
					foreach (var j in InnerNodes(i)) {
						yield return j;
					}
				}
			}
		}
	}

	public class NodeTreeViewItemPresentation : TreeViewItemPresentation
	{
		public readonly Node Node;
		public readonly LinkIndicatorButtonContainer LinkIndicatorButtonContainer;

		private static readonly Color4[] ColorMarks = {
			Color4.Transparent,
			ColorTheme.Current.TimelineRoll.RedMark,
			ColorTheme.Current.TimelineRoll.OrangeMark,
			ColorTheme.Current.TimelineRoll.YellowMark,
			ColorTheme.Current.TimelineRoll.BlueMark,
			ColorTheme.Current.TimelineRoll.GreenMark,
			ColorTheme.Current.TimelineRoll.VioletMark,
			ColorTheme.Current.TimelineRoll.GrayMark,
		};

		public NodeTreeViewItemPresentation(TreeView treeView, TreeViewItem item, Row sceneItem, Node node, TreeViewItemPresentationOptions options)
			: base(treeView, item, sceneItem, options)
		{
			Node = node;
			if (!options.Minimalistic) {
				var enterButton = NodeCompositionValidator.CanHaveChildren(node.GetType()) ? CreateEnterButton() : null;
				var showAnimatorsButton = CreateShowAnimatorsButton();
				var eyeButton = CreateEyeButton();
				var lockButton = CreateLockButton();
				var lockAnimationButton = CreateLockAnimationButton();
				LinkIndicatorButtonContainer = new LinkIndicatorButtonContainer();
				Widget.Nodes.Add(LinkIndicatorButtonContainer.Container);
				Widget.Nodes.Add((Widget) enterButton ?? Spacer.HSpacer(Theme.Metrics.DefaultToolbarButtonSize.X));
				Widget.Nodes.Add(showAnimatorsButton);
				Widget.Nodes.Add(lockAnimationButton);
				Widget.Nodes.Add(eyeButton);
				Widget.Nodes.Add(lockButton);
			}
			Widget.Gestures.Add(new ClickGesture(1, ShowContextMenu));
			ExpandButtonContainer.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>(widget => {
				if (node.EditorState().ColorIndex != 0) {
					widget.PrepareRendererState();
					var a = new Vector2(0, -4);
					var b = widget.Size + new Vector2(0, 3);
					int colorIndex = node.EditorState().ColorIndex;
					Renderer.DrawRect(a, b, ColorMarks[colorIndex]);
					if (colorIndex != 0) {
						Renderer.DrawRectOutline(a, b, ColorTheme.Current.TimelineRoll.Lines);
					}
				}
			}));
		}

		private void ShowContextMenu()
		{
			if (!SceneItem.GetTimelineItemState().Selected) {
				Document.Current.History.DoTransaction(() => {
					ClearRowSelection.Perform();
					SelectRow.Perform(SceneItem);
				});
			}
			var menu = new Menu {
				GenericCommands.ExportScene,
				InspectorCommands.CopyAssetPath,
				Command.MenuSeparator,
				Command.Cut,
				Command.Copy,
				Command.Paste,
				Command.Delete,
				Command.MenuSeparator,
				new Command("Rename", Rename),
				new Command("Color mark",
					new Menu {
						CreateSetColorMarkCommand("No Color", 0),
						CreateSetColorMarkCommand("Red", 1),
						CreateSetColorMarkCommand("Orange", 2),
						CreateSetColorMarkCommand("Yellow", 3),
						CreateSetColorMarkCommand("Blue", 4),
						CreateSetColorMarkCommand("Green", 5),
						CreateSetColorMarkCommand("Violet", 6),
						CreateSetColorMarkCommand("Gray", 7),
					}),
				GenericCommands.ConvertTo
			};
			if (NodeCompositionValidator.CanHaveChildren(Node.GetType())) {
				menu.Insert(8, new Command("Propagate Markers", () => {
					Document.Current.History.DoTransaction(() => {
						PropagateMarkers.Perform(Node);
					});
				}));
				menu.Insert(0, GenericCommands.InlineExternalScene);
			}
			menu.Popup();
		}

		private ICommand CreateSetColorMarkCommand(string title, int index)
		{
			return new Command(title,
				() => {
					Document.Current.History.DoTransaction(() => {
						foreach (var n in Document.Current.SelectedNodes()) {
							SetProperty.Perform(n.EditorState(), nameof(NodeEditorState.ColorIndex), index);
						}
					});
				}) { Checked = Node.EditorState().ColorIndex == index };
		}

		private ToolbarButton CreateEnterButton()
		{
			var button = new ToolbarButton {
				Texture = IconPool.GetTexture("Timeline.EnterContainer"),
				Highlightable = false
			};
			button.AddTransactionClickHandler(() => EnterNode.Perform(Node));
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		private ToolbarButton CreateShowAnimatorsButton()
		{
			var button = new ToolbarButton {
				Highlightable = false,
				Texture = IconPool.GetTexture("Timeline.Animator")
			};
			button.AddChangeWatcher(
				() => (HasAnimators: SceneItem.GetTimelineItemState().AnimatorsExpandable, Document.Current.ShowAnimators),
				i => button.Enabled = SceneItem.GetTimelineItemState().AnimatorsExpandable && !Document.Current.ShowAnimators
			);
			button.AddChangeWatcher(
				() => (ShowAnimators: SceneItem.GetTimelineItemState().AnimatorsExpanded, Document.Current.ShowAnimators),
				i => button.Checked = SceneItem.GetTimelineItemState().AnimatorsExpanded || Document.Current.ShowAnimators
			);
			button.AddTransactionClickHandler(
				() => {
					var nodes = Document.Current.SelectedRows()
						.Select(i => i.GetNode())
						.Where(i => i != null).ToList();
					var showAnimators = !SceneItem.GetTimelineItemState().AnimatorsExpanded;
					if (!nodes.Contains(Node)) {
						nodes.Clear();
						nodes.Add(Node);
					}
					foreach (var n in nodes) {
						DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
						SetProperty.Perform(
							obj: SceneItem.GetTimelineItemState(),
							propertyName: nameof(TimelineItemStateComponent.AnimatorsExpanded),
							value: showAnimators,
							isChangingDocument: false
						);
						DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
					}
				}
			);
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		private ToolbarButton CreateEyeButton()
		{
			var button = new ToolbarButton { Highlightable = false };
			button.AddChangeWatcher(() => Node.EditorState().Visibility, i => {
				var texture = "Timeline.Dot";
				if (i == NodeVisibility.Shown) {
					texture = "Timeline.Eye";
				} else if (i == NodeVisibility.Hidden) {
					texture = "Timeline.Cross";
				}
				button.Texture = IconPool.GetTexture(texture);
			});
			button.AddTransactionClickHandler(
				() => {
					var nodes = Document.Current.SelectedRows()
						.Select(i => i.GetNode())
						.Where(i => i != null).ToList();
					var visibility = (NodeVisibility)(((int)Node.EditorState().Visibility + 1) % 3);
					if (!nodes.Contains(Node)) {
						nodes.Clear();
						nodes.Add(Node);
					}
					foreach (var n in nodes) {
						SetProperty.Perform(n.EditorState(), nameof(NodeEditorState.Visibility), visibility);
					}
				}
			);
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		private ToolbarButton CreateLockButton()
		{
			var button = new ToolbarButton { Highlightable = false };
			button.AddChangeWatcher(
				() => Node.EditorState().Locked,
				i => button.Texture = IconPool.GetTexture(i ? "Timeline.Lock" : "Timeline.Dot")
			);
			button.AddTransactionClickHandler(
				() => {
					var nodes = Document.Current.SelectedRows()
						.Select(i => i.GetNode())
						.Where(i => i != null).ToList();
					var locked = !Node.EditorState().Locked;
					if (!nodes.Contains(Node)) {
						nodes.Clear();
						nodes.Add(Node);
					}
					foreach (var n in nodes) {
						SetProperty.Perform(n.EditorState(), nameof(NodeEditorState.Locked), locked);
					}
				}
			);
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		private ToolbarButton CreateLockAnimationButton()
		{
			var button = new ToolbarButton { Highlightable = false };
			button.AddChangeWatcher(
				() => GetAnimationState(Node),
				i => {
					var texture = "Timeline.Empty";
					if (i == AnimationState.Enabled) {
						texture = "Timeline.AnimationEnabled";
					} else if (i == AnimationState.PartiallyEnabled) {
						texture = "Timeline.AnimationPartiallyEnabled";
					} else if (i == AnimationState.Disabled) {
						texture = "Timeline.AnimationDisabled";
					}
					button.Texture = IconPool.GetTexture(texture);
				}
			);
			button.AddTransactionClickHandler(() => {
				var enabled = GetAnimationState(Node) == AnimationState.Enabled;
				foreach (var a in Node.Animators) {
					SetProperty.Perform(a, nameof(IAnimator.Enabled), !enabled);
				}
			});
			button.Components.Add(new DisableAncestralGesturesComponent());
			return button;
		}

		private enum AnimationState
		{
			None,
			Enabled,
			PartiallyEnabled,
			Disabled
		}

		private static AnimationState GetAnimationState(IAnimationHost animationHost)
		{
			var animators = animationHost.Animators;
			if (animators.Count == 0) {
				return AnimationState.None;
			}
			int enabled = 0;
			foreach (var a in animators) {
				if (a.Enabled && !a.IsZombie) {
					enabled++;
				}
			}
			if (enabled == 0) {
				return AnimationState.Disabled;
			} else if (enabled == animators.Count) {
				return AnimationState.Enabled;
			} else {
				return AnimationState.PartiallyEnabled;
			}
		}
	}

	public class AnimationTrackTreeViewItemPresentation : TreeViewItemPresentation
	{
		public AnimationTrackTreeViewItemPresentation(TreeView treeView, TreeViewItem item, Row sceneItem,
			TreeViewItemPresentationOptions options)
			: base(treeView, item, sceneItem, options)
		{
		}

		public static void AddAnimationTrack(int index = -1)
		{
			if (index < 0) {
				index = Document.Current.Animation.Tracks.Count;
			}
			Document.Current.History.DoTransaction(() => {
				var track = new AnimationTrack { Id = GenerateTrackId() };
				var item = LinkSceneItem.Perform(
					Document.Current.GetSceneItemForObject(Document.Current.Animation),
					index, track);
				ClearRowSelection.Perform();
				SelectRow.Perform(item);
			});

			string GenerateTrackId()
			{
				for (int i = 1; ; i++) {
					var id = "Track" + i;
					if (!Document.Current.Animation.Tracks.Any(t => t.Id == id)) {
						return id;
					}
				}
			}
		}
	}

	public class TreeViewPresentation : ITreeViewPresentation
	{
		public const float IndentWidth = 20;

		private readonly TreeViewItemPresentationOptions options;

		public IEnumerable<ITreeViewItemPresentationProcessor> Processors { get; }

		public TreeViewPresentation(TreeViewItemPresentationOptions options)
		{
			this.options = options;
			Processors = new List<ITreeViewItemPresentationProcessor> {
				new TreeViewItemPresentationProcessor(),
				new NodeTreeViewItemLabelProcessor(),
				new LinkIndicationCleanerProcessor(),
				new ImageCombinerLinkIndicationProcessor(),
				new BoneLinkIndicationProcessor(),
				new SplineGearLinkIndicationProcessor(),
				new SplineGear3DLinkIndicationProcessor()
			};
		}

		public ITreeViewItemPresentation CreateItemPresentation(TreeView treeView, TreeViewItem item)
		{
			var sceneItem = ((ISceneItemHolder)item).SceneItem;
			if (sceneItem.TryGetNode(out var node)) {
				return new NodeTreeViewItemPresentation(treeView, item, sceneItem, node, options);
			}
			if (sceneItem.TryGetFolder(out _)) {
				return new FolderTreeViewItemPresentation(treeView, item, sceneItem, options);
			}
			if (sceneItem.TryGetAnimationTrack(out _)) {
				return new AnimationTrackTreeViewItemPresentation(treeView, item, sceneItem, options);
			}
			return new TreeViewItemPresentation(treeView, item, sceneItem, options);
		}

		public void RenderDragCursor(Widget scrollWidget, TreeViewItem parent, int childIndex, bool dragInto)
		{
			if (dragInto) {
				var x = (CalcIndent(parent) + 1) * IndentWidth;
				var y = parent.Presentation.Widget.Top();
				Renderer.DrawRectOutline(x, y - 0.5f, scrollWidget.Width, y + TimelineMetrics.DefaultRowHeight + 0.5f, Theme.Colors.SeparatorDragColor, 2);
			} else {
				var x = (CalcIndent(parent) + 2) * IndentWidth;
				var y = CalcDragCursorY(parent, childIndex);
				Renderer.DrawRect(x, y - 0.5f, scrollWidget.Width, y + 1.5f, Theme.Colors.SeparatorDragColor);
			}
		}

		private static float CalcDragCursorY(TreeViewItem parent, int childIndex)
		{
			if (childIndex == 0) {
				if (parent.Parent == null) {
					return 0;
				}
				return parent.Presentation.Widget.Bottom();
			}
			var i = parent.Items[childIndex - 1];
			while (i.Expanded && i.Items.Count > 0) {
				i = i.Items.Last();
			}
			return i.Presentation.Widget.Bottom();
		}

		public static int CalcIndent(TreeViewItem item)
		{
			int indent = 0;
			for (var p = item; p.Parent != null; p = p.Parent) {
				indent++;
			}
			return indent - 1;
		}
	}
}
