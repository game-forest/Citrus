using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Common.FilesDropHandlers;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.UI.Docking;
using Tangerine.UI.Timeline.Components;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class Timeline : IDocumentView
	{
		private class TimelineStateComponent : Component
		{
			public Vector2 Offset = Vector2.Zero;
			public int CurrentColumn;
		}

		public static Timeline Instance { get; private set; }
		public static Action<Timeline> OnCreate;

		public readonly Toolbar Toolbar;
		public readonly Rulerbar Ruler;
		public readonly OverviewPane Overview;
		public readonly GridPane Grid;
		public readonly CurveEditorPane CurveEditor;
		public readonly RollPane Roll;
		public readonly Widget PanelWidget;
		public readonly Panel Panel;
		public readonly Widget RootWidget;
		public readonly WaveformCache WaveformCache;
		public readonly DropFilesGesture DropFilesGesture;

		private float offsetX;
		public Vector2 Offset
		{
			get => new Vector2(offsetX, Roll.ScrollView.ScrollPosition);
			set
			{
				if (value != Offset) {
					offsetX = value.X;
					Roll.ScrollView.ScrollPosition = value.Y;
				}
			}
		}

		private int columnCount;

		public int ColumnCount
		{
			get
			{
				if (columnCount == 0) {
					columnCount = CalcColumnCount();
				}
				var maxVisibleColumn = ((OffsetX + Ruler.RootWidget.Width) / TimelineMetrics.ColWidth).Ceiling();
				var result = Math.Max(CurrentColumn, Math.Max(columnCount + 1, maxVisibleColumn));
				return result;
			}
		}

		public void ClampAndSetOffset(Vector2 offset)
		{
			var maxOffset = new Vector2(
				float.MaxValue,
				Math.Max(0, Roll.ScrollView.Content.Height - Roll.RootWidget.Height)
			);
			Offset = Vector2.Clamp(offset, Vector2.Zero, maxOffset);
		}

		public float OffsetX { get => offsetX; set => Offset = new Vector2(value, Offset.Y); }
		public float OffsetY { get => Offset.Y; set => Offset = new Vector2(offsetX, value); }

		public int CurrentColumn => Document.Current.AnimationFrame;

		public float CurrentColumnEased
		{
			get {
				if (Document.Current.PreviewScene) {
					var time = Document.Current.Animation.Time;
					time = Document.Current.Animation.BezierEasingCalculator.EaseTime(time);
					return (float)(time * AnimationUtils.FramesPerSecond);
				} else {
					return Document.Current.AnimationFrame;
				}
			}
		}

		public readonly ComponentCollection<Component> Globals = new ComponentCollection<Component>();

		/// <summary>
		/// Called before Attach code execution.
		/// </summary>
		public event Action Attaching;
		/// <summary>
		/// Called before Detach code execution.
		/// </summary>
		public event Action Detaching;
		/// <summary>
		/// Called after Attach code execution.
		/// </summary>
		public event Action Attached;
		/// <summary>
		/// Called after Detach code execution
		/// </summary>
		public event Action Detached;

		public static IEnumerable<Type> GetOperationProcessorTypes() => new[] {
			typeof(EnsureSceneItemVisibleIfSelected),
		};

		private bool skipNextTimelineCentrify = false;

		public Timeline(Panel panel)
		{
			RootWidget = new Widget();
			Panel = panel;
			PanelWidget = panel.ContentWidget;
			Toolbar = new Toolbar();
			Ruler = new Rulerbar();
			Overview = new OverviewPane();
			Grid = new GridPane(this);
			CurveEditor = new CurveEditorPane(this);
			Roll = new RollPane();
			CreateProcessors();
			InitializeWidgets();
			WaveformCache = new WaveformCache(Project.Current.FileSystemWatcher);
			RootWidget.AddChangeWatcher(() => Document.Current.Container, container => {
				container.Components.GetOrAdd<TimelineOffset>().Offset = Offset;
			});
			Document.Current.SceneTreeBuilder.SceneItemCreated += SceneItemDecorator.Decorate;
			DecorateSceneTree(Document.Current.SceneTree);
			RootWidget.AddChangeWatcher(() => Offset, (value) => {
				var offset = Document.Current.Container.Components.Get<TimelineOffset>();
				if (offset != null) {
					offset.Offset = value;
				}
			});
			RootWidget.Gestures.Add(DropFilesGesture = new DropFilesGesture());
			CreateFilesDropHandlers();
			RootWidget.AddChangeWatcher(() => Document.Current?.Animation, _ => {
				columnCount = 0;
				if (skipNextTimelineCentrify) {
					skipNextTimelineCentrify = false;
				} else {
					CenterTimelineOnCurrentColumn.Perform();
				}
			});
			Document.Current.History.DocumentChanged += () => columnCount = 0;
			RootWidget.Awoke += RestoreStateFromDocument;
			OnCreate?.Invoke(this);
		}

		private void RestoreStateFromDocument(Node node)
		{
			var state = Document.Current.DocumentViewStateComponents.Get<TimelineStateComponent>();
			if (state != null) {
				skipNextTimelineCentrify = true;
				Offset = state.Offset;
				Document.Current.AnimationFrame = state.CurrentColumn;
			}
		}

		private void DecorateSceneTree(SceneItem sceneTree)
		{
			SceneItemDecorator.Decorate(sceneTree);
			foreach (var i in sceneTree.SceneItems) {
				DecorateSceneTree(i);
			}
		}

		private void CreateFilesDropHandlers()
		{
			DropFilesGesture.Recognized += new ImagesDropHandler().Handle;
			DropFilesGesture.Recognized += new AudiosDropHandler().Handle;
			DropFilesGesture.Recognized += new ScenesDropHandler().Handle;
			DropFilesGesture.Recognized += new Models3DDropHandler().Handle;
		}

		public void Attach()
		{
			Attaching?.Invoke();
			Instance = this;
			PanelWidget.PushNode(RootWidget);
			RootWidget.SetFocus();
			UpdateTitle();
			Attached?.Invoke();
		}

		public void Detach()
		{
			Detaching?.Invoke();
			Instance = null;
			RootWidget.Unlink();
			Detached?.Invoke();
		}

		private void InitializeWidgets()
		{
			RootWidget.Layout = new StackLayout();
			RootWidget.AddNode(new ThemedVSplitter {
				Stretches = Splitter.GetStretchesList(
					TimelineUserPreferences.Instance.TimelineVSplitterStretches, 0.5f, 1
				),
				Nodes = {
					Overview.RootWidget,
					new ThemedHSplitter {
						Stretches = Splitter.GetStretchesList(
							TimelineUserPreferences.Instance.TimelineHSplitterStretches, 0.3f, 1
						),
						Nodes = {
							new Widget {
								Layout = new VBoxLayout(),
								Nodes = {
									Toolbar.RootWidget,
									Roll.RootWidget,
								},
							},
							new Widget {
								Layout = new HBoxLayout(),
								Nodes = {
									new Widget { MinMaxWidth = 0 },
									new Frame {
										ClipChildren = ClipMethod.ScissorTest,
										Layout = new VBoxLayout(),
										Nodes = {
											Ruler.RootWidget,
											Grid.RootWidget,
											CurveEditor.RootWidget,
										},
									},
								},
							},
						},
					},
				},
			});
		}

		private void CreateProcessors()
		{
			RootWidget.LateTasks.Add(
				new SlowMotionProcessor(),
				new AnimationStretchProcessor(),
				new OverviewScrollProcessor(),
				new MouseWheelProcessor(this),
				new SelectAndDragKeyframesProcessor(),
				new CompoundAnimations.CreateAnimationTrackWeightRampProcessor(),
				new CompoundAnimations.SelectAndDragAnimationClipsProcessor(0),
				new CompoundAnimations.SelectAndDragAnimationClipsProcessor(1),
				new HasKeyframeRespondentProcessor(),
				new DragKeyframesRespondentProcessor(),
				new GridMouseScrollProcessor(),
				new RulerbarMouseScrollProcessor(),
				new GridContextMenuProcessor(),
				new CompoundAnimations.GridContextMenuProcessor()
			);
			RootWidget.Components.GetOrAdd<LateConsumeBehavior>().Add(ShowCurveEditorTask());
			RootWidget.Components.GetOrAdd<LateConsumeBehavior>().Add(PanelTitleUpdater());
		}

		private int CalcColumnCount()
		{
			const int ExtraFramesCount = 100;
			int maxColumn = 0;
			foreach (var item in Document.Current.VisibleSceneItems) {
				if (item.TryGetNode(out var node)) {
					maxColumn = Math.Max(maxColumn, node.Animators.GetOverallDuration());
				}
			}
			var markers = Document.Current.Animation.Markers;
			if (markers.Count > 0) {
				int maxMarkerColumn = Math.Max(maxColumn, markers[markers.Count - 1].Frame);
				maxColumn = Math.Max(maxMarkerColumn, maxColumn);
			}
			maxColumn += ExtraFramesCount;
			return maxColumn;
		}

		private void UpdateTitle()
		{
			Panel.Title = "Timeline";
			var t = string.Empty;
			for (var n = Document.Current.Container; n != Document.Current.RootNode; n = n.Parent) {
				var id = string.IsNullOrEmpty(n.Id) ? "?" : n.Id;
				t = id + ((t != string.Empty) ? ": " + t : t);
			}
			if (t != string.Empty) {
				Panel.Title += " - '" + t + "'";
			}
		}

		private IConsumer PanelTitleUpdater()
		{
			return new DelegateDataflowProvider<Node>(
				() => Document.Current.Container).WhenChanged(_ => UpdateTitle()
			);
		}

		private IConsumer ShowCurveEditorTask()
		{
			return new DelegateDataflowProvider<(SceneItem SceneItem, bool)>(() =>
				(FirstSelectedSceneItem(), TimelineUserPreferences.Instance.EditCurves)
			).WhenChanged(t => {
				var i = t.SceneItem;
				var showCurves = TimelineUserPreferences.Instance.EditCurves
					&& i != null
					&& CurveEditorPane.CanEditSceneItem(i);
				CurveEditor.RootWidget.Visible = showCurves;
				Grid.RootWidget.Visible = !showCurves;
				if (showCurves) {
					CurveEditor.EditSceneItem(i);
				}
			});
		}

		private static SceneItem FirstSelectedSceneItem() => Document.Current.SelectedSceneItems().FirstOrDefault();

		public void EnsureColumnVisible(int column)
		{
			if ((column + 1) * TimelineMetrics.ColWidth - Offset.X >= Ruler.RootWidget.Width) {
				OffsetX = (column + 1) * TimelineMetrics.ColWidth - Ruler.RootWidget.Width;
			}
			if (column * TimelineMetrics.ColWidth < Offset.X) {
				OffsetX = Math.Max(0, column * TimelineMetrics.ColWidth);
			}
		}

		public void GetVisibleColumnRange(out int min, out int max)
		{
			min = Math.Max(0, (Offset.X / TimelineMetrics.ColWidth).Round() - 1);
			max = Math.Min(
				ColumnCount - 1,
				((Offset.X + Ruler.RootWidget.Width) / TimelineMetrics.ColWidth).Round() + 1
			);
		}

		public bool IsColumnVisible(int col)
		{
			var pos = col * TimelineMetrics.ColWidth - Offset.X;
			return pos >= 0 && pos < Ruler.RootWidget.Width;
		}

		public void EnsureSceneItemVisible(SceneItem sceneItem)
		{
			// Make sure any SceneItem.Index is in order.
			_ = Document.Current.VisibleSceneItems;
			var heightWithSpacing = TimelineMetrics.DefaultRowHeight + TimelineMetrics.RowSpacing;
			var top = sceneItem.GetTimelineSceneItemState().Index * heightWithSpacing;
			var bottom = top + TimelineMetrics.DefaultRowHeight;
			if (bottom > Offset.Y + Roll.RootWidget.ContentHeight) {
				OffsetY = bottom - Roll.RootWidget.ContentHeight;
			}
			if (top < Offset.Y) {
				OffsetY = Math.Max(0, top);
			}
		}

		public void SyncDocumentState()
		{
			var c = Document.Current.DocumentViewStateComponents.GetOrAdd<TimelineStateComponent>();
			c.CurrentColumn = CurrentColumn;
			c.Offset = Offset;
		}
	}

	public static class SceneItemExtensions
	{
		public static Widget GridWidget(this SceneItem sceneItem)
		{
			return sceneItem.Components.Get<RowView>()?.GridRowView.GridWidget;
		}
	}
}
