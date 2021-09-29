using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine.UI.Timeline
{
	public class Toolbar
	{
		public Widget RootWidget { get; private set; }

		public Toolbar()
		{
			RootWidget = new Widget {
				Padding = new Thickness(2, 10, 0, 0),
				MinMaxHeight = Metrics.ToolbarHeight,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Toolbar.Background),
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center) },
				Nodes = {
					CreateAutoKeyframesButton(),
					CreateFolderButton(),
					CreateCurveEditorButton(),
					CreateTimelineCursorLockButton(),
					CreateAnimationStretchButton(),
					CreateSlowMotionButton(),
					CreateFrameProgressionButton(),
					CreateApplyZeroPoseButton(),
					CreateCenterTimelineButton(),
					CreateAnimationIndicator(),
					new Widget(),
					CreatePlaybackButton(),
					CreateStopButton(),
					CreateExitButton(),
					CreateShowAnimatorsButton(),
					CreateLockAnimationButton(),
					CreateEyeButton(),
					CreateLockButton(),
				}
			};
		}

		TimelineUserPreferences UserPreferences => TimelineUserPreferences.Instance;
		CoreUserPreferences CoreUserPreferences => Core.CoreUserPreferences.Instance;

		ToolbarButton CreateCurveEditorButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.Curve")) { Tooltip = "Edit curves" };
			button.AddChangeWatcher(() => UserPreferences.EditCurves, i => button.Checked = i);
			button.Clicked += () => UserPreferences.EditCurves = !UserPreferences.EditCurves;
			button.Components.Add(new DocumentationComponent("EditCurves"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		Widget CreateAnimationIndicator()
		{
			var t = new ThemedSimpleText { Padding = new Thickness(4, 0), MinWidth = 100 };
			Action f = () => {
				var distance = Timeline.Instance.Ruler.MeasuredFrameDistance;
				t.Text = (distance == 0) ?
					$"Col : {Document.Current.AnimationFrame}" :
					$"Col : {Document.Current.AnimationFrame} {Timeline.Instance.Ruler.MeasuredFrameDistance:+#;-#;0}";
			};
			t.AddChangeWatcher(() => Document.Current.AnimationFrame, _ => f());
			t.AddChangeWatcher(() => Timeline.Instance.Ruler.MeasuredFrameDistance, _ => f());
			return t;
		}

		ToolbarButton CreateAutoKeyframesButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.Key")) { Tooltip = "Automatic keyframes" };
			button.AddChangeWatcher(() => CoreUserPreferences.AutoKeyframes, i => button.Checked = i);
			button.Clicked += () => CoreUserPreferences.AutoKeyframes = !CoreUserPreferences.AutoKeyframes;
			button.Components.Add(new DocumentationComponent("AutomaticKeyframes"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreateCenterTimelineButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("AnimationsPanel.ShowCurrent")) { Tooltip = "Center Timeline on Current Column" };
			button.Clicked += () => (TimelineCommands.CenterTimelineOnCurrentColumn as Command)?.Issue();
			button.Components.Add(new DocumentationComponent("CenterTimeline"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreatePlaybackButton()
		{
			var imgPlay = IconPool.GetTexture("Timeline.Play");
			var imgPause = IconPool.GetTexture("Timeline.Pause");
			var button = new ToolbarButton(imgPlay) { Tooltip = "Play Animation" };
			button.AddChangeWatcher(() => Document.Current.PreviewAnimation, i => {
				button.Texture = i ? imgPause : imgPlay;
				button.Tooltip = i ? "Pause Animation" : "Play Animation";
			});
			button.Clicked += () => (SceneViewCommands.PreviewOrPauseAnimation as Command)?.Issue();
			button.Components.Add(new DocumentationComponent("PlaybackAnimation"));
			return button;
		}

		ToolbarButton CreateStopButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.Stop")) { Tooltip = "Stop Animation" };
			button.Clicked += () => {
				if (Document.Current.PreviewAnimation) {
					(SceneViewCommands.PreviewOrStopAnimation as Command)?.Issue();
				}
			};
			button.Components.Add(new DocumentationComponent("StopAnimation"));
			return button;
		}

		ToolbarButton CreateFolderButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Tools.NewFolder")) { Tooltip = "Create folder" };
			button.AddTransactionClickHandler(() => {
				var folder = new Folder.Descriptor { Id = "Folder" };
				SceneTreeUtils.GetSceneItemLinkLocation(out var parent, out var i, typeof(Folder.Descriptor));
				if (!LinkSceneItem.CanLink(parent, folder)) {
					return;
				}
				LinkSceneItem.Perform(parent, i, folder);
				ClearRowSelection.Perform();
				SelectRow.Perform(parent.Rows[i]);
			});
			button.Components.Add(new DocumentationComponent("CreateFolder"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreateShowAnimatorsButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.Animator")) { Tooltip = "Show Animators" };
			button.AddTransactionClickHandler(() => {
				SetProperty.Perform(Document.Current, nameof(Document.ShowAnimators), !Document.Current.ShowAnimators);
			});
			button.AddChangeWatcher(() => Document.Current.ShowAnimators, v => button.Checked = v);
			button.Components.Add(new DocumentationComponent("ShowAnimators"));
			return button;
		}

		ToolbarButton CreateExitButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.ExitContainer")) { Tooltip = "Exit current container (backspace)" };
			button.AddTransactionClickHandler(Core.Operations.LeaveNode.Perform);
			button.Updating += _ => button.Enabled = Core.Operations.LeaveNode.IsAllowed();
			button.Components.Add(new DocumentationComponent("ExitContainer"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreateEyeButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.Eye")) { Tooltip = "Show widgets" };
			button.AddTransactionClickHandler(() => {
				var nodes = !RootWidget.Input.IsKeyPressed(Key.Shift) ? Document.Current.Container.Nodes.ToList() : Document.Current.SelectedNodes().ToList();
				var visibility = NodeVisibility.Hidden;
				if (nodes.All(i => i.EditorState().Visibility == NodeVisibility.Hidden)) {
					visibility = NodeVisibility.Shown;
				} else if (nodes.All(i => i.EditorState().Visibility == NodeVisibility.Shown)) {
					visibility = NodeVisibility.Default;
				}
				foreach (var node in nodes) {
					Core.Operations.SetProperty.Perform(node.EditorState(), nameof(NodeEditorState.Visibility), visibility);
				}
			});
			button.Components.Add(new DocumentationComponent("ShowWidgets"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreateLockButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.Lock")) { Tooltip = "Lock widgets" };
			button.AddTransactionClickHandler(() => {
				var nodes = !RootWidget.Input.IsKeyPressed(Key.Shift) ? Document.Current.Container.Nodes.ToList() : Document.Current.SelectedNodes().ToList();
				var locked = nodes.All(i => !i.EditorState().Locked);
				foreach (var node in nodes) {
					Core.Operations.SetProperty.Perform(node.EditorState(), nameof(NodeEditorState.Locked), locked);
				}
			});
			button.Components.Add(new DocumentationComponent("LockWidgets"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreateLockAnimationButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.AnimationEnabled")) { Tooltip = "Lock animation" };
			button.AddTransactionClickHandler(() => {
				var nodes = !RootWidget.Input.IsKeyPressed(Key.Shift) ? Document.Current.Container.Nodes.ToList() : Document.Current.SelectedNodes().ToList();
				var enable = !nodes.All(IsAnimationEnabled);
				foreach (var node in nodes) {
					foreach (var animator in node.Animators) {
						Core.Operations.SetProperty.Perform(animator, nameof(IAnimator.Enabled), enable);
					}
				}
			});
			button.Components.Add(new DocumentationComponent("LockAnimation"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		ToolbarButton CreateApplyZeroPoseButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.ApplyZeroPose")) { Tooltip = "Apply zero pose" };
			button.AddTransactionClickHandler(() => {
				Core.Operations.SetProperty.Perform(Document.Current.Animation, nameof(Animation.ApplyZeroPose), !Document.Current.Animation.ApplyZeroPose);
			});
			button.AddChangeWatcher(() => Document.Current.Animation.ApplyZeroPose, i => button.Checked = i);
			button.Components.Add(new DocumentationComponent("ApplyZeroPose"));
			button.AddChangeWatcher(() => !Document.Current.Animation.IsLegacy && !Document.Current.Animation.IsCompound, v => button.Visible = v);
			return button;
		}

		ToolbarButton CreateAnimationStretchButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.AnimationStretch")) { Tooltip = "Animation stretch mode" };
			button.AddChangeWatcher(() => UserPreferences.AnimationStretchMode, i => button.Checked = i);
			button.Clicked += () => UserPreferences.AnimationStretchMode = !UserPreferences.AnimationStretchMode;
			button.Components.Add(new DocumentationComponent("AnimationStretch.md"));
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		private ToolbarButton CreateSlowMotionButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.SlowMotionMode")) { Tooltip = "Slow motion mode (~)" };
			button.AddChangeWatcher(() => UserPreferences.SlowMotionMode, i => button.Checked = i);
			button.Clicked += () => UserPreferences.SlowMotionMode = !UserPreferences.SlowMotionMode;
			button.Components.Add(new DocumentationComponent("SlowMotionMode.md"));
			return button;
		}

		private ToolbarButton CreateFrameProgressionButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.FrameProgression")) { Tooltip = "Frame progression mode" };
			button.AddChangeWatcher(() => CoreUserPreferences.ShowFrameProgression, i => button.Checked = i);
			button.Clicked += () => CoreUserPreferences.ShowFrameProgression = !CoreUserPreferences.ShowFrameProgression;
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		private ToolbarButton CreateTimelineCursorLockButton()
		{
			var button = new ToolbarButton(IconPool.GetTexture("Timeline.TimelineCursorLock")) { Tooltip = "Lock timeline cursor" };
			button.AddChangeWatcher(() => CoreUserPreferences.LockTimelineCursor, i => button.Checked = i);
			button.Clicked += () => CoreUserPreferences.LockTimelineCursor = !CoreUserPreferences.LockTimelineCursor;
			button.AddChangeWatcher(() => Document.Current.Animation.IsCompound, v => button.Visible = !v);
			return button;
		}

		static bool IsAnimationEnabled(IAnimationHost animationHost)
		{
			foreach (var a in animationHost.Animators) {
				if (!a.Enabled)
					return false;
			}
			return true;
		}
	}
}

