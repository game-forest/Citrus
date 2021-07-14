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
		private readonly Dictionary<Animation, Queue<string>> groupSelection;
		private readonly Widget rootWidget;
		private readonly HashSet<string> selected;
		private ThemedScrollView scrollView;
		private ThemedEditBox filter;

		private static bool showAnimationPreviews = true;

		public TriggerSelectionDialog(Node scene, HashSet<string> selected, Action<string> onSave)
		{
			this.scene = scene;
			this.onSave = onSave;
			this.selected = selected;
			groupSelection = new Dictionary<Animation, Queue<string>>();
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
			ToolbarButton togglePreview;
			var toolbar = new Widget {
				Padding = new Thickness(2, 10, 0, 0),
				MinMaxHeight = Metrics.ToolbarHeight,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Toolbar.Background),
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 2 },
				Nodes = {
					(togglePreview = new ToolbarButton {
						Texture = IconPool.GetTexture("TriggerSelectionDialog.ShowPreview"),
						Tooltip = "Toggle Animation Previews",
					}),
					(filter = new ThemedEditBox())
				}
			};
			togglePreview.AddTransactionClickHandler(() => {
				showAnimationPreviews = !showAnimationPreviews;
				var i = rootWidget.Nodes.IndexOf(scrollView);
				scrollView.Unlink();
				scrollView = CreateScrollView();
				rootWidget.Nodes.Insert(i, scrollView);
			});
			togglePreview.AddChangeWatcher(() => showAnimationPreviews, v => togglePreview.Checked = v);
			filter.AddChangeWatcher(
				() => filter.Text,
				_ => ApplyFilter(_)
			);
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
				Renderer.DrawRect(w.ContentPosition, w.ContentSize + w.ContentPosition, Theme.Colors.GrayBackground.Transparentify(0.9f));
			}));
			scrollView.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				Renderer.DrawRectOutline(w.ContentPosition, w.ContentSize + w.ContentPosition, Theme.Colors.ControlBorder);
			}));

			int animationIndex = 0;
			var previews = new List<AnimationPreview>();
			var visiblePreviews = new HashSet<AnimationPreview>();
			var	scenePool = new ScenePool(scene);
			var incrementalFF = new IncrementalFastForwarder();
			if (showAnimationPreviews) {
				scrollView.Content.LateTasks.Add(AttachPreviewsTask(scrollView.Behaviour, previews, visiblePreviews));
			}
			foreach (var animation in scene.Animations) {
				groupSelection[animation] = new Queue<string>();
				checkBoxes[animation] = new Dictionary<string, ThemedCheckBox>();
				var expandButton = new ThemedExpandButton {
					MinMaxSize = Vector2.One * 20f,
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Expanded = true
				};
				var groupLabel = new ThemedSimpleText {
					Text = animation.Id ?? "Legacy",
					VAlignment = VAlignment.Center,
					LayoutCell = new LayoutCell(Alignment.Center),
					ForceUncutText = false,
					HitTestTarget = true
				};
				var header = new Widget {
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.Center),
					Padding = new Thickness(4),
					Nodes = {
						expandButton,
						groupLabel
					}
				};
				var wrapper = new Frame {
					Padding = new Thickness(4),
					Layout = new VBoxLayout(),
					Visible = true
				};
				expandButton.Clicked += () => {
					wrapper.Visible = !wrapper.Visible;
				};
				groupLabel.Clicked += () => {
					wrapper.Visible = !wrapper.Visible;
					expandButton.Expanded = !expandButton.Expanded;
				};
				foreach (var trigger in animation.Markers.Select(i => i.Id).Distinct().Where(i => !string.IsNullOrWhiteSpace(i))) {
					wrapper.AddNode(CreateTriggerSelectionWidget(animation, trigger, out var previewContainer));
					if (previewContainer != null) {
						var preview = new AnimationPreview(incrementalFF, scenePool, previewContainer, animationIndex, trigger);
						previews.Add(preview);
						previewContainer.AddLateChangeWatcher(() => IsWidgetOnscreen(scrollView, previewContainer), onScreen => {
							if (onScreen) {
								visiblePreviews.Add(preview);
							} else {
								visiblePreviews.Remove(preview);
							}
 						});
					}
				}
				animationIndex++;
				scrollView.Content.AddNode(new Widget {
					Layout = new VBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.Center),
					Padding = new Thickness(4),
					Nodes = {
						header,
						wrapper
					}
				});
			}

			bool IsWidgetOnscreen(ThemedScrollView scrollView, Widget widget)
			{
				var widgetTop = widget.CalcPositionInSpaceOf(scrollView).Y;
				var widgetBottom = widgetTop + widget.Height;
				return widgetBottom >= 0 && widgetTop < scrollView.Height;
			}

			return scrollView;
		}

		private IEnumerator<object> AttachPreviewsTask(ScrollView scrollView, List<AnimationPreview> previews, HashSet<AnimationPreview> visiblePreviews)
		{
			while (true) {
				if (!scrollView.IsScrolling()) {
					foreach (var p in previews) {
						if (visiblePreviews.Contains(p) != p.IsAttached) {
							if (!p.IsAttached) {
								p.Attach();
							} else {
								p.Detach();
							}
						}
					}
				}
				yield return null;
			}
		}

		private Widget CreateTriggerSelectionWidget(Animation animation, string trigger, out Widget previewContainer)
		{
			previewContainer = null;
			var isChecked = selected.Contains(trigger);
			var checkBox = new ThemedCheckBox {
				Checked = isChecked
			};
			if (isChecked) {
				groupSelection[animation].Enqueue(trigger);
			}
			checkBoxes[animation].Add(trigger, checkBox);
			Frame previewFrame;
			var widget = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell(Alignment.Center),
				Padding = new Thickness(left: 8, right: 8, top: 4, bottom: 4),
				Nodes = {
					new ThemedSimpleText {
						Text = trigger,
						LayoutCell = new LayoutCell(Alignment.Center),
						Anchors = Anchors.LeftRight,
						ForceUncutText = false
					},
					Spacer.HSpacer(5),
					checkBox
				},
				HitTestTarget = true
			};
			if (showAnimationPreviews) {
				previewContainer = new Frame {
					Size = new Vector2(64),
				};
				previewFrame = new Frame {
					ClipChildren = ClipMethod.ScissorTest,
					MinMaxSize = previewContainer.Size,
					Nodes = {
						previewContainer,
					}
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
				var value = "";
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
					cancelButton
				},
			};
		}

		private void ToggleTriggerCheckbox(string trigger, Animation animation)
		{
			var currentBoxes = checkBoxes[animation];
			var checkBox = currentBoxes[trigger];
			if (checkBox.Checked) {
				selected.Add(trigger);
			} else {
				selected.Remove(trigger);
			}
			var currentGroup = groupSelection[animation];
			if (currentGroup.Count > 0) {
				var last = currentGroup.Peek();
				if (last == trigger) {
					currentGroup.Dequeue();
				} else {
					currentBoxes[last].Toggle();
					currentGroup.Enqueue(trigger);
				}
			} else {
				currentGroup.Enqueue(trigger);
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

		private class IncrementalFastForwarder
		{
			private class AnimationSnapshot
			{
				public int AnimationIndex;
				public int Frame;
				public List<ReferencelessAnimationState> States;
			}

			private struct ReferencelessAnimationState
			{
				// Denotes a path to the node from the root node. The path consists of NodeList indexes packed as bitfields.
				// The number of bits allocated to a particular index depends on the NodeList.Count value.
				// If it turns out that 64 bit are not enough, I suggest to use BitArray instead.
				public long NodeIndex;
				public int AnimationIndex;
				public int Frame;
				public int FrameCount;
				public bool IsRunning;
			}

			private readonly List<AnimationSnapshot> snapshots = new List<AnimationSnapshot>();
			private readonly AnimationFastForwarder animationFF = new AnimationFastForwarder();

			public void FastForward(Node node, int animationIndex, int frame)
			{
				AnimationSnapshot closestSnapshot = null;
				foreach (var i in snapshots) {
					if (
						i.AnimationIndex == animationIndex &&
						i.Frame <= frame &&
						(closestSnapshot == null || closestSnapshot.Frame < i.Frame)
					) {
						closestSnapshot = i;
					}
				}
				AnimationSnapshot effectiveSnapshot = null;
				var animation = node.Animations[animationIndex];
				if (closestSnapshot != null && frame == closestSnapshot.Frame) {
					effectiveSnapshot = closestSnapshot;
				} else {
					effectiveSnapshot = new AnimationSnapshot {
						AnimationIndex = animationIndex,
						Frame = frame,
						States = new List<ReferencelessAnimationState>()
					};
					var states = new List<AnimationFastForwarder.AnimationState>();
					if (closestSnapshot != null) {
						foreach (var reflessState in closestSnapshot.States) {
							var state = FromReflessAnimationState(reflessState, node);
							if (state.IsRunning) {
								if (state.Animation != animation) {
									animationFF.BuildAnimationStates(
										states,
										state.Animation,
										closestSnapshot.Frame,
										frame - closestSnapshot.Frame,
										processMarkers: true
									);
								}
							} else {
								state.FrameCount += frame - closestSnapshot.Frame;
								states.Add(state);
							}
						}
						animationFF.BuildAnimationStates(states, animation, closestSnapshot.Frame, frame - closestSnapshot.Frame);
					} else {
						animationFF.BuildAnimationStates(states, animation, 0, frame);
					}
					effectiveSnapshot.States.AddRange(states.Select(i => ToReflessAnimationState(i, node)));
					snapshots.Add(effectiveSnapshot);
				}
				var effectiveStates = effectiveSnapshot.States.Select(i => FromReflessAnimationState(i, node)).ToList();
				animationFF.ApplyAnimationStates(effectiveStates, animation, stopAnimations: false);
			}

			private static ReferencelessAnimationState ToReflessAnimationState(AnimationFastForwarder.AnimationState state, Node root)
			{
				var node = state.Animation.OwnerNode;
				return new ReferencelessAnimationState {
					NodeIndex = GetNodeIndex(node, root),
					AnimationIndex = node.Animations.IndexOf(state.Animation),
					Frame = state.Frame,
					FrameCount = state.FrameCount,
					IsRunning = state.IsRunning
				};
			}

			private static long GetNodeIndex(Node node, Node root)
			{
				long result = 0;
				for (; node != root; node = node.Parent) {
					result *= GetNearestPowerOf2(node.Parent.Nodes.Count + 1);
					result += node.Parent.Nodes.IndexOf(node) + 1;
				}
				return result;
			}

			private static Node GetNode(long index, Node root)
			{
				if (index == 0) {
					return root;
				}
				var range = GetNearestPowerOf2(root.Nodes.Count + 1);
				root = root.Nodes[(int)(index & (range - 1)) - 1];
				index /= range;
				return index > 0 ? GetNode(index, root) : root;
			}

			private static int GetNearestPowerOf2(int value)
			{
				if (!IsPowerOf2(value)) {
					int i = 1;
					while (i < value) {
						i *= 2;
					}
					return i;
				}
				return value;
			}

			private static bool IsPowerOf2(int value)
			{
				return value == 1 || (value & (value - 1)) == 0;
			}

			private static AnimationFastForwarder.AnimationState FromReflessAnimationState(ReferencelessAnimationState state, Node root)
			{
				// var node = root.SelfAndDescendants.ElementAt(state.NodeIndex);
				var node = GetNode(state.NodeIndex, root);
				return new AnimationFastForwarder.AnimationState {
					Animation = node.Animations[state.AnimationIndex],
					Frame = state.Frame,
					FrameCount = state.FrameCount,
					IsRunning = state.IsRunning
				};
			}
		}

		class AnimationPreview
		{
			private readonly IncrementalFastForwarder incrementalFF;
			private readonly ScenePool scenePool;
			private readonly Widget previewContainer;
			private readonly int animationIndex;
			private readonly string trigger;
			private Node clone;
			private int frame;
			private AnimationProcessor animationProcessor;

			public bool IsAttached { get; private set; }

			public AnimationPreview(
				IncrementalFastForwarder incrementalFF,
				ScenePool scenePool,
				Widget previewContainer,
				int animationIndex,
				string trigger
			) {
				this.incrementalFF = incrementalFF;
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
				previewContainer.PostPresenter = new AnimationPreviewPresenter((Widget)clone);
				var animation = clone.Animations[animationIndex];
				frame = animation.Markers.First(i => i.Id == trigger).Frame;
				animationProcessor = clone.Manager.Processors.OfType<AnimationProcessor>().First();
				animationProcessor.Reset();
				animationProcessor.AllAnimationsStopped += RestartAnimation;
				incrementalFF.FastForward(clone, animationIndex, frame);
				animation.IsRunning = true;
				IsAttached = true;
			}

			private void RestartAnimation() => previewContainer.Tasks.Add(RestartAnimationOnNextFrame());

			private IEnumerator<object> RestartAnimationOnNextFrame()
			{
				incrementalFF.FastForward(clone, animationIndex, frame);
				yield break;
			}

			public void Detach()
			{
				if (!IsAttached) {
					throw new InvalidOperationException();
				}
				animationProcessor.AllAnimationsStopped -= RestartAnimation;
				incrementalFF.FastForward(clone, animationIndex, 0); // Restore animation to default state.
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
				if (pool.Count == 0) {
					var clone = originNode.Clone();
					foreach (var n in clone.SelfAndDescendants) {
						var _ = n.DefaultAnimation; // Force create the default animation.
					}
					var manager = Document.CreateDefaultManager();
					manager.Processors.Add(new AnimationProcessor());
					manager.RootNodes.Add(clone);
					return clone;
				}
				return pool.Pop();
			}

			public void Release(Node node) => pool.Push(node);
		}

		public class AnimationProcessor : NodeComponentProcessor<AnimationComponent>
		{
			public event Action AllAnimationsStopped;
			private int activeAnimationsCount;

			public void Reset() => activeAnimationsCount = 0;

			protected override void Add(AnimationComponent component, Node owner)
			{
				component.AnimationRun += OnAnimationRun;
				component.AnimationStopped += OnAnimationStopped;
			}

			protected override void Remove(AnimationComponent component, Node owner)
			{
				component.AnimationRun -= OnAnimationRun;
				component.AnimationStopped -= OnAnimationStopped;
			}

			internal void OnAnimationRun(AnimationComponent component, Animation animation)
			{
				activeAnimationsCount++;
			}

			internal void OnAnimationStopped(AnimationComponent component, Animation animation)
			{
				activeAnimationsCount--;
				if (activeAnimationsCount <= 0) {
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

			public AnimationPreviewPresenter(Widget content)
			{
				this.content = content;
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
				ro.LocalToWorldTransform = content.AsWidget.LocalToWorldTransform.CalcInversed() *
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
