using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Lime;
#if PROFILER
using Lime.Profiler.Graphics;
#endif // PROFILER
using Tangerine.Common.FilesDropHandlers;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI.AnimeshEditor;
using Tangerine.UI.SceneView.Presenters;

namespace Tangerine.UI.SceneView
{
	public class SceneView : IDocumentView, ISceneView
	{
		private Vector2 mousePositionOnFilesDrop;

		// Given panel.
		public Widget Panel { get; }
		// Widget which is a direct child of the panel.
		public Widget Frame { get; }
		// Widget having the same size as panel, used for intercepting mouse events above the canvas.
		public Widget InputArea { get; }
		public WidgetInput Input => InputArea.Input;
		// Container for the document root node.
		public Widget Scene { get; }
		public readonly DropFilesGesture DropFilesGesture;
		public static readonly RulersWidget RulersWidget = new RulersWidget();
		public static readonly ZoomWidget ZoomWidget = new ZoomWidget();
		public static readonly ToolbarButton ShowNodeDecorationsPanelButton = new ToolbarButton {
			Tooltip = "Node decorations",
			Texture = IconPool.GetTexture("SceneView.ShowPanel"),
			MinMaxSize = new Vector2(24),
			LayoutCell = new LayoutCell(new Alignment { X = HAlignment.Left, Y = VAlignment.Bottom } )
		};
		public static readonly AnimeshContextualPanel AnimeshPanel = new AnimeshContextualPanel();
		public static Action<SceneView> OnCreate;

		/// <summary>
		/// Gets the mouse position in the scene coordinates.
		/// </summary>
		public Vector2 MousePosition => Scene.LocalMousePosition();

		public ComponentCollection<Component> Components = new ComponentCollection<Component>();

		public static SceneView Instance { get; private set; }

		public static void RegisterGlobalCommands()
		{
			ConnectCommand(
				SceneViewCommands.PreviewAnimation,
				new PreviewAnimationHandler(PreviewAnimationBehaviour.StopOnStartingFrame));
			ConnectCommand(
				SceneViewCommands.PreviewAnimationOnce,
				new PreviewAnimationHandler(PreviewAnimationBehaviour.StopOnCurrentFrame));
			ConnectCommand(SceneViewCommands.ResolutionChanger, new ResolutionChangerHandler());
			ConnectCommand(SceneViewCommands.ResolutionReverceChanger, new ResolutionChangerHandler(isReverse: true));
			ConnectCommand(SceneViewCommands.ResolutionOrientation, new ResolutionOrientationHandler());
			ConnectCommand(SceneViewCommands.Duplicate, DuplicateNodes);
			ConnectCommand(SceneViewCommands.TieWidgetsWithBones, TieWidgetsWithBones);
			ConnectCommand(SceneViewCommands.UntieWidgetsFromBones, UntieWidgetsFromBones);
			ConnectCommand(SceneViewCommands.ToggleOverdrawMode, ToggleOverdrawMode);
		}

		private static void ConnectCommand(ICommand command, DocumentCommandHandler handler)
		{
			CommandHandlerList.Global.Connect(command, handler);
		}

		private static void ConnectCommand(ICommand command, Action action, Func<bool> enableChecker = null)
		{
			CommandHandlerList.Global.Connect(command, new DocumentDelegateCommandHandler(action, enableChecker));
		}

		private static void DuplicateNodes()
		{
			var text = Clipboard.Text;
			try {
				Copy.CopyToClipboard();
				Paste.Perform(out _);
			} finally {
				Clipboard.Text = text;
			}
		}

		public Matrix32 CalcTransitionFromSceneSpace(Widget targetSpace)
		{
			return Scene.LocalToWorldTransform * targetSpace.LocalToWorldTransform.CalcInversed();
		}

		private static void ToggleOverdrawMode()
		{
#if PROFILER
			Overdraw.Enabled = !Overdraw.EnabledAtUpdateThread;
#endif // PROFILER
			Window.Current.Invalidate();
		}

		private static void TieWidgetsWithBones()
		{
			try {
				var bones = Document.Current.SelectedNodes().Editable().OfType<Bone>();
				var widgets = Document.Current.SelectedNodes().Editable().OfType<Widget>();
				Core.Operations.TieWidgetsWithBones.Perform(bones, widgets);
			} catch (TieWidgetsWithBonesException e) {
				Document.Current.History.RollbackTransaction();
				AlertDialog.Show($"Unable to tie bones with {e.Node.Id} node. There are no empty skinning slots.");
			}
		}

		private static void UntieWidgetsFromBones()
		{
			var bones = Document.Current.SelectedNodes().Editable().OfType<Bone>();
			var widgets = Document.Current.SelectedNodes().Editable().OfType<Widget>();
			Core.Operations.UntieWidgetsFromBones.Perform(bones, widgets);
		}

		private class ScenePresenter : IPresenter
		{
			private RenderChain renderChain = new RenderChain();
			private Node content;

			public ScenePresenter(Node content)
			{
				this.content = content;
			}

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var w = (Widget)node;
				var ro = RenderObjectPool<RenderObject>.Acquire();
				try {
					content.RenderChainBuilder?.AddToRenderChain(renderChain);
					renderChain.GetRenderObjects(ro.SceneObjects);
				} finally {
					renderChain.Clear();
				}
#if PROFILER
				ro.IsThumbnailOwner = SceneViewThumbnailProvider.IsGettingRenderObjects;
				ro.Frame = new RenderObject.FrameInfo {
					Transform = node.Parent.AsWidget.LocalToWorldTransform,
					Size = (Size)node.Parent.AsWidget.Size
				};
#endif // PROFILER
				ro.LocalToWorldTransform = w.LocalToWorldTransform;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args)
			{
				var w = (Widget)node;
				var p = args.Point;
				try {
					content.RenderChainBuilder?.AddToRenderChain(renderChain);
					args.Point = w.LocalToWorldTransform.CalcInversed().TransformVector(args.Point);
					return renderChain.HitTest(ref args);
				} finally {
					args.Point = p;
					renderChain.Clear();
				}
			}

			private class RenderObject : Lime.RenderObject
			{
#if PROFILER
				private static readonly RenderTargetQueue mainRenderTargetManager = new RenderTargetQueue();
				private static readonly RenderTargetQueue thumbnailRenderTargetManager = new RenderTargetQueue();
				private static Color4[] overdrawPixels = new Color4[1920 * 1080];

				public bool IsThumbnailOwner;
				public FrameInfo Frame;
#endif // PROFILER

				public Matrix32 LocalToWorldTransform;
				public RenderObjectList SceneObjects = new RenderObjectList();

				public override void Render()
				{
					Renderer.PushState(RenderState.All);
					// Hack: use the current state of Transform2 since it may be configured for generating SceneViewThumbnail.
					var sceneTransform2 = LocalToWorldTransform * Renderer.Transform2;
					Renderer.Transform2 = sceneTransform2;
#if PROFILER
					RenderTargetQueue renderTargetQueue = null;
					RenderTexture renderTexture = null;
					if (Overdraw.EnabledAtRenderThread) {
						Renderer.PushState(
							RenderState.ScissorState |
							RenderState.Viewport |
							RenderState.Projection |
							RenderState.Transform1 |
							RenderState.Transform2);
						var viewportSize = (Size)((Vector2)Frame.Size * Window.Current.PixelScale);
						renderTargetQueue = IsThumbnailOwner ? thumbnailRenderTargetManager : mainRenderTargetManager;
						renderTexture = renderTargetQueue.Acquire(viewportSize);
						renderTexture.SetAsRenderTarget();
						Renderer.ScissorState = ScissorState.ScissorDisabled;
						Renderer.Viewport = new Viewport(0, 0, viewportSize.Width, viewportSize.Height);
						Renderer.SetOrthogonalProjection(0, 0, Frame.Size.Width, Frame.Size.Height);
						Renderer.Transform1 = Matrix32.Identity;
						Renderer.Transform2 = LocalToWorldTransform * Frame.Transform.CalcInversed();
						Renderer.Clear(Color4.Zero);
						OverdrawMaterialScope.Enter();
					}
#endif // PROFILER
					SceneObjects.Render();
#if PROFILER
					if (Overdraw.EnabledAtRenderThread) {
						OverdrawMaterialScope.Leave();
						renderTexture.RestoreRenderTarget();
						if (Overdraw.MetricRequiredAtRenderThread && !IsThumbnailOwner) {
							OverdrawInterpreter.EnsureEnoughBufferSize(renderTexture, ref overdrawPixels);
							renderTexture.GetPixels(overdrawPixels);
							int pixelCount = renderTexture.PixelCount;
							float averageOverdraw = OverdrawInterpreter.GetAverageOverdraw(overdrawPixels, pixelCount);
							Overdraw.InvokeMetricCreated(averageOverdraw, pixelCount);
						}
						renderTargetQueue.Free(renderTexture);
						Renderer.PopState();
					}
#endif // PROFILER
					Renderer.PopState();
#if PROFILER
					if (Overdraw.EnabledAtRenderThread) {
						OverdrawInterpreter.DrawResults(renderTexture, Frame.Transform, Frame.Size);
						Renderer.PushState(RenderState.Transform2);
						Renderer.Transform2 = sceneTransform2;
						OverdrawForeground.Render();
						Renderer.PopState();
					}
#endif // PROFILER
				}

				protected override void OnRelease()
				{
					SceneObjects.Clear();
					base.OnRelease();
				}

				/// <summary>
				/// Describes <see cref="SceneView.Frame"/> positioning.
				/// </summary>
				public struct FrameInfo
				{
					public Matrix32 Transform;
					public Size Size;
				}
			}
		}

		[UpdateStage(typeof(PostLateUpdateStage))]
		[NodeComponentDontSerialize]
		private class SceneBehavior : BehaviorComponent
		{
			private bool documentChanged;

			public SceneBehavior()
			{
				Document.Current.History.DocumentChanged += () => documentChanged = true;
			}

			protected override void Update(float delta)
			{
				if (!Document.Current.PreviewAnimation) {
					delta = 0.0f;
				} else if (Document.Current.SlowMotion) {
					delta *= 0.1f;
				}
				Document.Current.Manager.Update(delta);
				if (documentChanged) {
					documentChanged = false;
					Document.Current.Animation.Frame = Document.Current.Animation.Frame;
					Document.Current.RootNode.Update(0);
				}
			}
		}

		public SceneView(Widget panelWidget)
		{
			this.Panel = panelWidget;
			InputArea = new Widget { HitTestTarget = true, Anchors = Anchors.LeftRightTopBottom };
			InputArea.FocusScope = new KeyboardFocusScope(InputArea);
			InputArea.Gestures.Add(DropFilesGesture = new DropFilesGesture());
			Scene = new Widget { Anchors = Anchors.LeftRightTopBottom };
			Scene.Components.Add(new SceneBehavior());
			Scene.PostPresenter = new ScenePresenter(Document.Current.RootNode);
			Frame = new Widget {
				Id = "SceneView",
				Nodes = { InputArea, Scene }
			};
			CreateComponents();
			CreateProcessors();
			CreatePresenters();
			CreateFilesDropHandlers();
			Frame.Awoke += CenterDocumentRoot;
			OnCreate?.Invoke(this);
		}

		private void CreateFilesDropHandlers()
		{
			DropFilesGesture.Recognized += new ImagesDropHandler(OnBeforeFilesDrop, FilesDropNodePostProcessor).Handle;
			DropFilesGesture.Recognized += new AudiosDropHandler().Handle;
			DropFilesGesture.Recognized += new ScenesDropHandler(OnBeforeFilesDrop, FilesDropNodePostProcessor).Handle;
			DropFilesGesture.Recognized += new Models3DDropHandler(OnBeforeFilesDrop, FilesDropNodePostProcessor).Handle;
		}

		private void CenterDocumentRoot(Node node)
		{
			// Before Frame awakens something is being changed on this frame enough to change Frame Size on LayoutManager.Layout()
			// which will come at the end of the frame. Force it now to get accurate Frame.Size;
			WidgetContext.Current.Root.LayoutManager.Layout();
			var rulerSize = RulersWidget.RulerHeight * (RulersWidget.Visible ? 1 : 0);
			var widget = Document.Current.RootNode.AsWidget;
			var frameWidth = Frame.Width - rulerSize;
			var frameHeight = Frame.Height - ZoomWidget.FrameHeight - rulerSize;
			var wantedZoom = Mathf.Clamp(Mathf.Min(frameWidth / (widget.Width * widget.Scale.X), frameHeight / (widget.Height * widget.Scale.Y)), 0.0f, 1.0f);
			var zoomIndex = ZoomWidget.FindNearest(wantedZoom, 0, ZoomWidget.zoomTable.Count);
			Scene.Scale = new Vector2(ZoomWidget.zoomTable[zoomIndex]);
			Scene.Position = -(widget.Position + widget.Size * widget.Scale * 0.5f) * Scene.Scale + new Vector2(frameWidth * 0.5f, frameHeight * 0.5f) + Vector2.One * rulerSize;
		}

		private void OnBeforeFilesDrop()
		{
			if (!Window.Current.Active) {
				Window.Current.Activate();
				InputArea.SetFocus();
			}
			mousePositionOnFilesDrop = MousePosition * Document.Current.Container.AsWidget.LocalToWorldTransform.CalcInversed();
		}

		private static void FilesDropNodePostProcessor(Node node)
		{
			if (node is Widget) {
				SetProperty.Perform(node, nameof(Widget.Position), Instance.mousePositionOnFilesDrop);
			}
		}

		public void Attach()
		{
			Instance = this;
			Document.Current.SceneViewThumbnailProvider = new SceneViewThumbnailProvider(Document.Current, Frame);
			Panel.AddNode(AnimeshPanel.RootNode);
			Panel.AddNode(ShowNodeDecorationsPanelButton);
			Panel.AddNode(ZoomWidget);
			Panel.AddNode(RulersWidget);
			Panel.AddNode(Frame);
		}

		public void Detach()
		{
			Instance = null;
			AnimeshPanel.RootNode.Unlink();
			Frame.Unlink();
			ShowNodeDecorationsPanelButton.Unlink();
			RulersWidget.Unlink();
			ZoomWidget.Unlink();
		}

		/// <summary>
		/// Checks whether the mouse is over a control point within a given radius.
		/// </summary>
		public bool HitTestControlPoint(Vector2 controlPoint, float radius = 10)
		{
			return (controlPoint - MousePosition).Length < radius / Scene.Scale.X;
		}

		/// <summary>
		/// Checks whether the mouse is over a control point within a specific for resize radius.
		/// </summary>
		public bool HitTestResizeControlPoint(Vector2 controlPoint)
		{
			return HitTestControlPoint(controlPoint, 6);
		}

		void CreateComponents() { }

		void CreateProcessors()
		{
			Frame.Tasks.Add(
				new ActivateOnMouseOverProcessor(),
				new CreateWidgetProcessor(),
				new CreateSplinePointProcessor(),
				new CreatePointObjectProcessor(),
				new CreateSplinePoint3DProcessor(),
				new CreateBoneProcessor(),
				new CreateNodeProcessor(),
				new ExpositionProcessor(),
				new MouseScrollProcessor(),
				new DragPivotProcessor(),
				new DragBoneProcessor(),
				new ChangeBoneRadiusProcessor(),
				new RotateBoneProcessor(),
				new DragPointObjectsProcessor(),
				new DragSplineTangentsProcessor(),
				new DragSplinePoint3DProcessor(),
				new DragAnimationPathPointProcessor(),
				new DragWidgetsProcessor(),
				new AnimeshProcessor(this),
				new ResizeWidgetsProcessor(),
				new RescalePointObjectSelectionProcessor(),
				new RotatePointObjectSelectionProcessor(),
				new RotateWidgetsProcessor(),
				new RulerProcessor(),
				new DragNineGridLineProcessor(),
				new DragPaddingLineProcessor(),
				new ShiftTimelineProcessor(),
				new MouseSelectionProcessor(),
				new ShiftClickProcessor(),
				new PreviewAnimationProcessor(),
				new ResolutionPreviewProcessor(),
				new FrameProgressionProcessor(),
				new AnimeshContextualPanelProcessor(AnimeshPanel)
			);
		}

		void CreatePresenters()
		{
			new Bone3DPresenter(this);
			new ContainerAreaPresenter(this);
			new SelectedWidgetsPresenter(this);
			new WidgetsPivotMarkPresenter(this);
			new PointObjectsPresenter(this);
			new SplinePointPresenter(this);
			new TranslationGizmoPresenter(this);
			new BonePresenter(this);
			new BoneAsistantPresenter(this);
			new DistortionMeshPresenter(this);
			new FrameBorderPresenter(this);
			new InspectRootNodePresenter(this);
			new NineGridLinePresenter(this);
			new PaddingLinePresenter(this);
			new Animation2DPathPresenter(this);
			new WavePivotPresenter(this);
			new AnimeshPresenter(this);
		}

		public void CreateNode(Type nodeType, ICommand command)
		{
			Components.Add(new CreateNodeRequestComponent { NodeType = nodeType, Command = command });
		}

		public void DuplicateSelectedNodes()
		{
			DuplicateNodes();
		}
	}

	public class CreateNodeRequestComponent : Component
	{
		public Type NodeType { get; set; }
		public ICommand Command { get; set; }

		public static bool Consume<T>(ComponentCollection<Component> components, out Type nodeType, out ICommand command) where T : Node
		{
			var c = components.Get<CreateNodeRequestComponent>();
			if (c != null && (c.NodeType.IsSubclassOf(typeof(T)) || c.NodeType == typeof(T))) {
				components.Remove<CreateNodeRequestComponent>();
				nodeType = c.NodeType;
				command = c.Command;
				return true;
			}
			nodeType = null;
			command = null;
			return false;
		}

		public static bool Consume<T>(ComponentCollection<Component> components) where T : Node
		{
			Type type;
			ICommand command;
			return Consume<T>(components, out type, out command);
		}

		public static bool Consume<T>(ComponentCollection<Component> components, out ICommand command) where T : Node
		{
			Type type;
			return Consume<T>(components, out type, out command);
		}
	}

	public class SceneViewThumbnailProvider : ISceneViewThumbnailProvider
	{
		private readonly RenderChain renderChain = new RenderChain();
		private readonly RenderObjectList renderList = new RenderObjectList();
		private readonly Document document;
		private readonly Widget sceneViewFrame;
		private RenderTexture texture;

#if PROFILER
		/// <summary>
		/// Hack: use the static field to determine when SceneView.RenderObject belongs to SceneViewThumbnail.
		/// </summary>
		internal static bool IsGettingRenderObjects { get; private set; }
#endif // PROFILER

		public SceneViewThumbnailProvider(Document document, Widget sceneViewFrame)
		{
			this.document = document;
			this.sceneViewFrame = sceneViewFrame;
		}

		public void Generate(int frame, Action<ITexture> callback)
		{
			var sceneSize = sceneViewFrame.Size;
			var thumbSize = new Vector2(200);
			if (sceneSize.X > sceneSize.Y) {
				thumbSize.Y *= sceneSize.Y / sceneSize.X;
			} else {
				thumbSize.X *= sceneSize.X / sceneSize.Y;
			}
			var ap = new AnimationPositioner(Document.Current.Manager);
			var savedTime = document.Animation.Time;
			var savedIsRunning = Document.Current.Animation.IsRunning;
			renderChain.Clear();
			ap.SetAnimationFrame(document.Animation, frame, stopAnimations: true);
			sceneViewFrame.RenderChainBuilder?.AddToRenderChain(renderChain);
			renderList.Clear();
#if PROFILER
			IsGettingRenderObjects = true;
#endif // PROFILER
			renderChain.GetRenderObjects(renderList);
#if PROFILER
			IsGettingRenderObjects = false;
#endif // PROFILER
			ap.SetAnimationTime(document.Animation, savedTime, stopAnimations: true);
			Document.Current.Animation.IsRunning = savedIsRunning;
			Window.Current.InvokeOnRendering(() => RenderThumbnail(callback));
		}

		private void RenderThumbnail(Action<ITexture> callback)
		{
			var pixelScale = Window.Current.PixelScale;
			var scaledWidth = (int)(sceneViewFrame.Width * pixelScale);
			var scaledHeight = (int)(sceneViewFrame.Height * pixelScale);
			if (texture == null || texture.ImageSize != new Size(scaledWidth, scaledHeight)) {
				texture?.Dispose();
				texture = new RenderTexture(scaledWidth, scaledHeight);
			}
			if (sceneViewFrame.Width > 0 && sceneViewFrame.Height > 0) {
				texture.SetAsRenderTarget();
				Renderer.PushState(
					RenderState.ScissorState |
					RenderState.View |
					RenderState.World |
					RenderState.View |
					RenderState.Projection |
					RenderState.DepthState |
					RenderState.CullMode |
					RenderState.Transform2);
				Renderer.ScissorState = ScissorState.ScissorDisabled;
				Renderer.Viewport = new Viewport(0, 0, texture.ImageSize.Width, texture.ImageSize.Height);
				Renderer.Clear(Color4.Zero);
				Renderer.World = Matrix44.Identity;
				Renderer.View = Matrix44.Identity;
				Renderer.SetOrthogonalProjection(0, 0, sceneViewFrame.Width, sceneViewFrame.Height);
				Renderer.DepthState = DepthState.DepthDisabled;
				Renderer.CullMode = CullMode.None;
				Renderer.Transform2 = sceneViewFrame.Parent.AsWidget.LocalToWorldTransform.CalcInversed();
				renderList.Render();
				Renderer.PopState();
				texture.RestoreRenderTarget();
				callback?.Invoke(texture);
			}
		}
	}
}
