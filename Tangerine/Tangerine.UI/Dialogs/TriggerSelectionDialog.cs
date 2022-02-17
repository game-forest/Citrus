using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TriggerSelectionDialog
	{
		private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
		private readonly Window window;
		private readonly Node scene;
		private readonly Action<string> onSave;
		private readonly Dictionary<Animation, Dictionary<string, ThemedCheckBox>> checkBoxes;
		private readonly Widget rootWidget;
		private readonly HashSet<string> selected;
		private ThemedScrollView scrollView;
		private ThemedEditBox filter;

		private static bool showAnimationPreviews = true;
		private static bool showPlayMarkers = true;
		private static bool showJumpMarkers = true;
		private static bool showStopMarkers = true;

		public TriggerSelectionDialog(Node scene, HashSet<string> selected, Action<string> onSave)
		{
			this.scene = scene;
			this.onSave = onSave;
			this.selected = selected;
			checkBoxes = new Dictionary<Animation, Dictionary<string, ThemedCheckBox>>();
			window = new Window(new WindowOptions {
				Title = "Trigger Selection",
				ClientSize = new Vector2(300, 400),
				MinimumDecoratedSize = new Vector2(300, 400),
				FixedSize = false,
				Visible = false,
#if WIN
				Icon = new System.Drawing.Icon(new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()),
#endif // WIN
			});
			rootWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8), Layout = new VBoxLayout(),
			};
			rootWidget.Tasks.Add(new TooltipProcessor());
			scrollView = CreateScrollView();
			rootWidget.Nodes.AddRange(
				CreateToolbar(),
				Spacer.VSpacer(5),
				scrollView,
				CreateButtonsPanel()
			);
			rootWidget.FocusScope = new KeyboardFocusScope(rootWidget);
			rootWidget.LateTasks.AddLoop(() => {
				if (rootWidget.Input.ConsumeKeyPress(Key.Escape)) {
					window.Close();
				}
			});
			// Don't play sounds during animations preview.
			var wasAudioEnabled = Audio.GloballyEnable;
			Audio.GloballyEnable = false;
			window.Closed += () => {
				Audio.GloballyEnable = wasAudioEnabled;
			};
			window.ShowModal();
		}

		private Widget CreateToolbar()
		{
			ToolbarButton togglePreviewButton;
			ToolbarButton toggleShowPlayMarkersButton;
			ToolbarButton toggleShowJumpMarkersButton;
			ToolbarButton toggleShowStopMarkersButton;
			var toolbar = new Widget {
				Padding = new Thickness(2, 10, 0, 0),
				MinMaxHeight = Metrics.ToolbarHeight,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Toolbar.Background),
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 2 },
				Nodes = {
					(togglePreviewButton = new ToolbarButton {
						Texture = IconPool.GetTexture("TriggerSelectionDialog.ShowPreview"),
						Tooltip = "Toggle Animation Previews",
					}),
					(toggleShowPlayMarkersButton = new ToolbarButton {
						Texture = IconPool.GetTexture("Lookup.MarkerPlayAction"),
						Tooltip = "Show Play Markers",
					}),
					(toggleShowJumpMarkersButton = new ToolbarButton {
						Texture = IconPool.GetTexture("Lookup.MarkerJumpAction"),
						Tooltip = "Show Jump Markers",
					}),
					(toggleShowStopMarkersButton = new ToolbarButton {
						Texture = IconPool.GetTexture("Lookup.MarkerStopAction"),
						Tooltip = "Show Stop Markers",
					}),
					(filter = new ThemedEditBox()),
				},
			};
			togglePreviewButton.AddTransactionClickHandler(() => {
				showAnimationPreviews = !showAnimationPreviews;
				RecreateScrollView();
			});
			toggleShowPlayMarkersButton.AddTransactionClickHandler(() => {
				showPlayMarkers = !showPlayMarkers;
				RecreateScrollView();
			});
			toggleShowJumpMarkersButton.AddTransactionClickHandler(() => {
				showJumpMarkers = !showJumpMarkers;
				RecreateScrollView();
			});
			toggleShowStopMarkersButton.AddTransactionClickHandler(() => {
				showStopMarkers = !showStopMarkers;
				RecreateScrollView();
			});
			togglePreviewButton.AddChangeWatcher(() => showAnimationPreviews, v => togglePreviewButton.Checked = v);
			toggleShowPlayMarkersButton.AddChangeWatcher(
				() => showPlayMarkers, v => toggleShowPlayMarkersButton.Checked = v
			);
			toggleShowJumpMarkersButton.AddChangeWatcher(
				() => showJumpMarkers, v => toggleShowJumpMarkersButton.Checked = v
			);
			toggleShowStopMarkersButton.AddChangeWatcher(
				() => showStopMarkers, v => toggleShowStopMarkersButton.Checked = v
			);

			filter.AddChangeWatcher(
				() => filter.Text,
				_ => ApplyFilter(_)
			);

			void RecreateScrollView()
			{
				var i = rootWidget.Nodes.IndexOf(scrollView);
				scrollView.Unlink();
				scrollView = CreateScrollView();
				rootWidget.Nodes.Insert(i, scrollView);
			}
			return toolbar;
		}

		private ThemedScrollView CreateScrollView()
		{
			var scrollView = new ThemedScrollView();
			scrollView.Behaviour.Content.Padding = new Thickness(4);
			scrollView.Behaviour.Content.Layout = new VBoxLayout();
			scrollView.Behaviour.Content.LayoutCell = new LayoutCell(Alignment.Center);
			scrollView.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				Renderer.DrawRect(
					a: w.ContentPosition,
					b: w.ContentSize + w.ContentPosition,
					color: Theme.Colors.GrayBackground.Transparentify(0.9f)
				);
			}));
			scrollView.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				Renderer.DrawRectOutline(
					w.ContentPosition, w.ContentSize + w.ContentPosition, Theme.Colors.ControlBorder
				);
			}));

			int animationIndex = 0;
			var previews = new List<AnimationPreview>();
			var visiblePreviews = new HashSet<AnimationPreview>();
			var scenePool = new ScenePool(scene);
			if (showAnimationPreviews) {
				scrollView.Content.LateTasks.Add(AttachPreviewsTask(previews, visiblePreviews));
			}
			foreach (var animation in scene.Animations) {
				checkBoxes[animation] = new Dictionary<string, ThemedCheckBox>();
				var expandButton = new ThemedExpandButton {
					MinMaxSize = Vector2.One * 20f,
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Expanded = true,
				};
				var groupLabel = new ThemedSimpleText {
					Text = animation.Id ?? "Legacy",
					VAlignment = VAlignment.Center,
					LayoutCell = new LayoutCell(Alignment.Center),
					ForceUncutText = false,
					HitTestTarget = true,
				};
				var header = new Widget {
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.Center),
					Padding = new Thickness(4),
					Nodes = {
						expandButton,
						groupLabel,
					},
				};
				var wrapper = new Frame {
					Padding = new Thickness(4),
					Layout = new VBoxLayout(),
					Visible = true,
				};
				expandButton.Clicked += () => {
					wrapper.Visible = !wrapper.Visible;
				};
				groupLabel.Clicked += () => {
					wrapper.Visible = !wrapper.Visible;
					expandButton.Expanded = !expandButton.Expanded;
				};
				var triggers = animation.Markers.
					Where(
						i =>
							(i.Action == MarkerAction.Play && showPlayMarkers ||
							i.Action == MarkerAction.Jump && showJumpMarkers ||
							i.Action == MarkerAction.Stop && showStopMarkers)
							&& !string.IsNullOrWhiteSpace(i.Id)
					).
					Select(i => i.Id).Distinct();
				foreach (var trigger in triggers) {
					wrapper.AddNode(CreateTriggerSelectionWidget(animation, trigger, out var previewContainer));
					if (previewContainer != null) {
						var preview = new AnimationPreview(scenePool, previewContainer, animationIndex, trigger);
						previews.Add(preview);
						previewContainer.AddLateChangeWatcher(
							() => IsWidgetOnscreen(scrollView, previewContainer),
							onScreen => {
								if (onScreen) {
									visiblePreviews.Add(preview);
								} else {
									visiblePreviews.Remove(preview);
								}
							}
						);
					}
				}
				animationIndex++;
				scrollView.Content.AddNode(new Widget {
					Layout = new VBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.Center),
					Padding = new Thickness(4),
					Nodes = {
						header,
						wrapper,
					},
				});
			}

			static bool IsWidgetOnscreen(ThemedScrollView scrollView, Widget widget)
			{
				var widgetTop = widget.CalcPositionInSpaceOf(scrollView).Y;
				var widgetBottom = widgetTop + widget.Height;
				return widgetBottom >= 0 && widgetTop < scrollView.Height;
			}

			return scrollView;
		}

		private static IEnumerator<object> AttachPreviewsTask(
			List<AnimationPreview> previews, HashSet<AnimationPreview> visiblePreviews
		) {
			while (true) {
				foreach (var p in previews) {
					if (visiblePreviews.Contains(p) != p.IsAttached) {
						if (!p.IsAttached) {
							p.Attach();
						} else {
							p.Detach();
						}
					}
				}
				yield return null;
			}
		}

		private Widget CreateTriggerSelectionWidget(Animation animation, string trigger, out Widget previewContainer)
		{
			previewContainer = null;
			var checkBox = new ThemedCheckBox {
				Checked = selected.Contains(animation.IsLegacy ? trigger : $"{trigger}@{animation.Id}"),
			};
			checkBoxes[animation].Add(trigger, checkBox);
			var widget = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell(Alignment.Center),
				Padding = new Thickness(left: 8, right: 8, top: 4, bottom: 4),
				Nodes = {
					new ThemedSimpleText {
						Text = trigger,
						LayoutCell = new LayoutCell(Alignment.Center),
						Anchors = Anchors.LeftRight,
						ForceUncutText = false,
					},
					Spacer.HSpacer(5),
					checkBox,
				},
				HitTestTarget = true,
			};
			if (showAnimationPreviews) {
				previewContainer = new Frame {
					MinMaxSize = new Vector2(128),
					Size = new Vector2(128),
				};
				var previewFrame = new Frame {
					ClipChildren = ClipMethod.ScissorTest,
					MinMaxSize = previewContainer.Size,
					Size = previewContainer.Size,
					Nodes = {
						previewContainer,
					},
				};
				previewFrame.CompoundPostPresenter.Add(new WidgetBoundsPresenter(Theme.Colors.ControlBorder));
				widget.Nodes.Insert(1, Spacer.HSpacer(5));
				widget.Nodes.Insert(2, previewFrame);
			}
			widget.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>(w => {
				if (w.IsMouseOverThisOrDescendant()) {
					w.PrepareRendererState();
					Renderer.DrawRect(Vector2.Zero, w.Size, Theme.Colors.SelectedBackground);
				}
			}));
			widget.LateTasks.Add(Theme.MouseHoverInvalidationTask(widget));
			widget.Clicked += checkBox.Toggle;
			checkBox.Changed += e => ToggleTriggerCheckbox(trigger, animation);
			return widget;
		}

		private Widget CreateButtonsPanel()
		{
			var okButton = new ThemedButton { Text = "Ok" };
			var cancelButton = new ThemedButton { Text = "Cancel" };
			okButton.Clicked += () => {
				var value = string.Empty;
				foreach (var s in selected) {
					value += $"{s},";
				}
				if (!string.IsNullOrEmpty(value)) {
					value = value.Trim(',');
				}
				onSave.Invoke(value);
				window.Close();
			};
			cancelButton.Clicked += () => {
				window.Close();
			};
			return new Widget {
				Layout = new HBoxLayout { Spacing = 8 },
				LayoutCell = new LayoutCell { StretchY = 0 },
				Padding = new Thickness { Top = 5 },
				Nodes = {
					new Widget { MinMaxHeight = 0 },
					okButton,
					cancelButton,
				},
			};
		}

		private void ToggleTriggerCheckbox(string trigger, Animation animation)
		{
			var currentBoxes = checkBoxes[animation];
			var checkBox = currentBoxes[trigger];
			if (!animation.IsLegacy) {
				trigger = $"{trigger}@{animation.Id}";
			}
			if (checkBox.Checked) {
				// Uncheck other triggers for the given animation.
				foreach (var s in selected.ToList()) {
					var t = s.Contains('@') ? s.Split('@') : new[] { s, null };
					var otherTrigger = t[0];
					var animationId = t[1];
					if (animationId == animation.Id) {
						selected.Remove(otherTrigger);
						currentBoxes[otherTrigger].Checked = false;
					}
				}
				selected.Add(trigger);
			} else {
				selected.Remove(trigger);
			}
		}

		private void ApplyFilter(string filter)
		{
			filter = filter.ToLower();
			scrollView.ScrollPosition = 0;
			var isShowingAll = string.IsNullOrEmpty(filter);
			foreach (var group in scrollView.Content.Nodes) {
				var header = group.Nodes[0];
				var expandButton = header.Nodes[0] as ThemedExpandButton;
				var wrapper = group.Nodes[1];
				var expanded = false;
				foreach (var node in wrapper.Nodes) {
					var trigger = (node.Nodes[0] as ThemedSimpleText).Text.ToLower();
					node.AsWidget.Visible = trigger.Contains(filter) || isShowingAll;
					if (!isShowingAll && node.AsWidget.Visible && !expanded) {
						expanded = true;
						expandButton.Expanded = true;
						wrapper.AsWidget.Visible = true;
					}
				}
			}
		}

		private class AnimationPreview
		{
			private readonly ScenePool scenePool;
			private readonly Widget previewContainer;
			private readonly int animationIndex;
			private readonly string trigger;
			private Node clone;
			private int frame;
			private AnimationProcessor animationProcessor;

			public bool IsAttached { get; private set; }

			public AnimationPreview(
				ScenePool scenePool,
				Widget previewContainer,
				int animationIndex,
				string trigger
			) {
				this.scenePool = scenePool;
				this.previewContainer = previewContainer;
				this.animationIndex = animationIndex;
				this.trigger = trigger;
			}

			public void Attach()
			{
				if (IsAttached) {
					throw new InvalidOperationException();
				}
				clone = scenePool.Acquire();
				previewContainer.Components.Add(new AnimationPreviewBehavior(clone.Manager));
				previewContainer.PostPresenter = new AnimationPreviewPresenter(clone);
				var animation = clone.Animations[animationIndex];
				frame = animation.Markers.First(i => i.Id == trigger).Frame;
				animationProcessor = clone.Manager.Processors.OfType<AnimationProcessor>().First();
				animationProcessor.Reset();
				animationProcessor.AllAnimationsStopped += AddRestartAnimationOnNextFrameTask;
				RestartAnimation();
				IsAttached = true;
			}

			private void RestartAnimation()
			{
				foreach (var n in clone.SelfAndDescendants) {
					foreach (var a in n.Animations) {
						a.IsRunning = false;
						a.Frame = 0;
					}
				}
				clone.Animations[animationIndex].Frame = frame;
				clone.Animations[animationIndex].IsRunning = true;
			}

			private void AddRestartAnimationOnNextFrameTask()
			{
				previewContainer.Tasks.Add(RestartAnimationOnNextFrame());
			}

			private IEnumerator<object> RestartAnimationOnNextFrame()
			{
				yield return null;
				RestartAnimation();
			}

			public void Detach()
			{
				if (!IsAttached) {
					throw new InvalidOperationException();
				}
				animationProcessor.AllAnimationsStopped -= AddRestartAnimationOnNextFrameTask;
				scenePool.Release(clone);
				previewContainer.Components.Remove<AnimationPreviewBehavior>();
				previewContainer.PostPresenter = null;
				IsAttached = false;
			}
		}

		private class ScenePool
		{
			private readonly Node originNode;
			private Stack<Node> pool = new Stack<Node>();

			public ScenePool(Node originNode)
			{
				this.originNode = originNode;
			}

			public Node Acquire()
			{
				Node clonedModel;
				if (pool.Count == 0) {
					clonedModel = originNode.Clone();
					foreach (var n in clonedModel.SelfAndDescendants) {
						_ = n.DefaultAnimation; // Force create the default animation.
					}
					var manager = Document.CreateDefaultManager();
					manager.Processors.Add(new AnimationProcessor());
					if (clonedModel is Node3D) {
						var viewport3D = new Viewport3D();
						var camera = new Camera3D {
							Id = "DefaultCamera",
							Position = new Vector3(0, 0, 10),
							FarClipPlane = 10000,
							NearClipPlane = 0.001f,
							FieldOfView = 1.0f,
							AspectRatio = 1.3f,
							OrthographicSize = 1.0f,
						};
						viewport3D.Nodes.Add(camera);
						viewport3D.CameraRef = new NodeReference<Camera3D>(camera.Id);
						viewport3D.AddNode(clonedModel);
						manager.RootNodes.Add(viewport3D);
					} else {
						manager.RootNodes.Add(clonedModel);
					}
					manager.Update(0f);
				} else {
					clonedModel = pool.Pop();
				}
				foreach (var n in clonedModel.SelfAndDescendants) {
					foreach (var a in n.Animations) {
						a.IsRunning = false;
						a.Frame = 0;
					}
				}
				return clonedModel;
			}

			public void Release(Node node) => pool.Push(node);
		}

		private class AnimationProcessor : NodeComponentProcessor<AnimationComponent>
		{
			public event Action AllAnimationsStopped;
			private readonly HashSet<Animation> activeAnimations = new HashSet<Animation>();

			public void Reset() => activeAnimations.Clear();

			protected override void Add(AnimationComponent component, Node owner)
			{
				component.AnimationRun += OnAnimationRun;
				component.AnimationStopped += OnAnimationStopped;
			}

			protected override void Remove(AnimationComponent component, Node owner)
			{
				foreach (var animation in component.Animations) {
					if (animation.IsRunning) {
						activeAnimations.Remove(animation);
					}
				}
				component.AnimationRun -= OnAnimationRun;
				component.AnimationStopped -= OnAnimationStopped;
			}

			internal void OnAnimationRun(AnimationComponent component, Animation animation)
			{
				activeAnimations.Add(animation);
			}

			internal void OnAnimationStopped(AnimationComponent component, Animation animation)
			{
				activeAnimations.Remove(animation);
				if (activeAnimations.Count == 0) {
					AllAnimationsStopped?.Invoke();
				}
			}
		}

		[UpdateStage(typeof(PostLateUpdateStage))]
		[NodeComponentDontSerialize]
		private class AnimationPreviewBehavior : BehaviorComponent
		{
			private readonly NodeManager manager;

			public AnimationPreviewBehavior(NodeManager manager)
			{
				this.manager = manager;
			}

			protected internal override void Update(float delta)
			{
				manager.Update(delta);
			}
		}

		private class AnimationPreviewPresenter : IPresenter
		{
			private readonly RenderChain renderChain = new RenderChain();
			private readonly Widget content;

			public AnimationPreviewPresenter(Node content)
			{
				if (content is Node3D node3D) {
					this.content = node3D.Viewport;
				} else {
					this.content = content.AsWidget;
				}
			}

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var previewContainer = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				try {
					content.RenderChainBuilder?.AddToRenderChain(renderChain);
					renderChain.GetRenderObjects(ro.SceneObjects);
				} finally {
					renderChain.Clear();
				}
				ro.LocalToWorldTransform =
					content.LocalToWorldTransform.CalcInversed() *
					Matrix32.Scaling(previewContainer.Size / content.Size) *
					previewContainer.LocalToWorldTransform;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : Lime.RenderObject
			{
				public Matrix32 LocalToWorldTransform;
				public RenderObjectList SceneObjects = new RenderObjectList();

				public override void Render()
				{
					Renderer.PushState(RenderState.Transform2);
					Renderer.Transform2 = LocalToWorldTransform;
					SceneObjects.Render();
					Renderer.PopState();
				}

				protected override void OnRelease()
				{
					SceneObjects.Clear();
					base.OnRelease();
				}
			}
		}
	}
}
