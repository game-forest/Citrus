using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Lime;
using Tangerine.Core.Components;
#if PROFILER
using Lime.Profiler;
#endif // PROFILER

namespace Tangerine.Core
{
	public interface IDocumentView
	{
		void Detach();
		void Attach();
		void SyncDocumentState()
		{ }
	}

	public enum DocumentFormat
	{
		Tan,
		T3D,
		Fbx,
	}

	public interface ISceneViewSnapshotProvider
	{
		void Generate(RenderTexture texture, Action callback);
	}

	public class PreservedDocumentState
	{
		public readonly ComponentCollection<Component> Components = new ComponentCollection<Component>();

		public readonly HashSet<string> SelectedItems = new HashSet<string>();

		public readonly HashSet<string> ExpandedItems = new HashSet<string>();

		public static readonly PreservedDocumentState Null = new PreservedDocumentState();

		public static string GetSceneItemIndexPath(SceneItem item)
		{
			var builder = new StringBuilder(item.Id);
			for (var i = item; i != null; i = i.Parent) {
				builder.Append('/');
				builder.Append(i.Parent == null ? -1 : i.Parent.SceneItems.IndexOf(i));
			}
			return builder.ToString();
		}

		public static string GetNodeIndexPath(Node node)
		{
			var builder = new StringBuilder(node.Id);
			for (var p = node; p != null; p = p.Parent) {
				builder.Append('/');
				builder.Append(p.CollectionIndex());
			}
			return builder.ToString();
		}
	}

	public sealed class Document
	{
		private class DocumentStateComponent : Component
		{
			public string AnimationId = null;
			public string AnimationOwnerNodePath = null;
			public string ContainerPath = null;
			public bool InspectRootNode = false;
		}

		public enum CloseAction
		{
			Cancel,
			SaveChanges,
			DiscardChanges,
		}

		private readonly string untitledPathFormat = ".untitled/{0:D2}/Untitled{0:D2}";
		private readonly Vector2 defaultSceneSize = new Vector2(1024, 768);
		private readonly ConditionalWeakTable<object, SceneItem> sceneItemCache =
			new ConditionalWeakTable<object, SceneItem>();
		private readonly MemoryStream preloadedSceneStream = null;
		private readonly AnimationFastForwarder animationFastForwarder;
		private static uint untitledCounter;

		public static readonly string[] AllowedFileTypes = { "tan", "t3d", "fbx" };
		public delegate bool PathSelectorDelegate(out string path);

		public DateTime LastWriteTime { get; private set; }
		public bool Loaded { get; private set; } = true;
		public bool SlowMotion { get; set; }
		public bool ShowAnimators { get; set; }

		public static event Action<Document> AttachingViews;
		public static event Action<Document, string> ShowingWarning;
		public static Func<Document, CloseAction> CloseConfirmation;
		public static PathSelectorDelegate PathSelector;

		public static Document Current { get; private set; }

		public readonly DocumentHistory History = new DocumentHistory();

		public readonly ComponentCollection<Component> DocumentViewStateComponents =
			new ComponentCollection<Component>();
		public bool IsModified => History.IsDocumentModified;

		/// <summary>
		/// The list of Tangerine node decorators.
		/// </summary>
		public static readonly NodeDecoratorList NodeDecorators = new NodeDecoratorList();

		/// <summary>
		/// Gets the path to the document relative to the project directory.
		/// </summary>
		public string Path { get; private set; }

		/// <summary>
		/// Gets absolute path to the document.
		/// </summary>
		public string FullPath => Project.Current.GetFullPath(Path, GetFileExtension(Format));

		/// <summary>
		/// Document name to be displayed.
		/// </summary>
		public string DisplayName => (IsModified ? "*" : string.Empty)
			+ System.IO.Path.GetFileName(Path ?? "Untitled");

		/// <summary>
		/// Gets or sets the file format the document should be saved to.
		/// </summary>
		public DocumentFormat Format { get; set; }

		public Node RootNodeUnwrapped { get; private set; }

		/// <summary>
		/// Gets the root node for the current document.
		/// </summary>
		public Node RootNode { get; private set; }

		public ISceneViewSnapshotProvider SceneViewSnapshotProvider { get; set; }

		private Node container;

		/// <summary>
		/// Gets or sets the current container widget.
		/// </summary>
		public Node Container
		{
			get => container;
			set
			{
				if (container != value) {
					container = value;
					BumpSceneTreeVersion();
				}
			}
		}

		public NodeManager Manager { get; }

		/// <summary>
		/// Gets or sets the scene we are navigated from.
		/// Required for getting back into the main scene from the external one.
		/// </summary>
		public string SceneNavigatedFrom { get; set; }

		/// <summary>
		/// The list of scene items, currently displayed on the timeline.
		/// </summary>
		public List<SceneItem> VisibleSceneItems
		{
			get
			{
				if (cachedVisibleSceneItems.Count == 0) {
					if (Animation.IsCompound) {
						TraverseAnimationTree(GetSceneItemForObject(Animation));
					} else {
						TraverseSceneTree(GetSceneItemForObject(Container), true);
					}
				}
				return cachedVisibleSceneItems;

				void TraverseAnimationTree(SceneItem animationTree)
				{
					foreach (var i in animationTree.SceneItems) {
						if (i.TryGetAnimationTrack(out _)) {
							i.GetTimelineSceneItemState().Index = cachedVisibleSceneItems.Count;
							cachedVisibleSceneItems.Add(i);
							TraverseAnimationTree(i);
						}
					}
				}

				void TraverseSceneTree(SceneItem sceneTree, bool addNodes)
				{
					var timelineItemState = sceneTree.GetTimelineSceneItemState();
					timelineItemState.NodesExpandable = false;
					timelineItemState.AnimatorsExpandable = false;
					var containerSceneItem = GetSceneItemForObject(Container);
					var effectiveAnimatorsPerHost = animation.ValidatedEffectiveAnimatorsPerHost;
					foreach (var i in sceneTree.SceneItems) {
						i.GetTimelineSceneItemState().Index = cachedVisibleSceneItems.Count;
						if (i.TryGetAnimator(out var animator)) {
							if (
								sceneTree != containerSceneItem &&
								!animator.IsZombie &&
								effectiveAnimatorsPerHost.Contains(animator)
							) {
								timelineItemState.AnimatorsExpandable = true;
								if (ShowAnimators || timelineItemState.AnimatorsExpanded) {
									cachedVisibleSceneItems.Add(i);
								}
							}
						} else if (i.TryGetNode(out var node) || i.TryGetFolder(out _)) {
							if (addNodes) {
								timelineItemState.NodesExpandable = true;
								if (timelineItemState.NodesExpanded || sceneTree == containerSceneItem) {
									var hierarchyMode =
										!Animation.IsLegacy &&
										CoreUserPreferences.Instance.ExperimentalTimelineHierarchy;
									cachedVisibleSceneItems.Add(i);
									TraverseSceneTree(
										sceneTree: i,
										addNodes: (node is Bone || i.GetFolder() != null || hierarchyMode)
											&& (!i.TryGetNode(out var n) || string.IsNullOrEmpty(n.ContentsPath))
									);
								}
							}
						}
					}
				}
			}
		}

		private readonly List<SceneItem> cachedVisibleSceneItems = new List<SceneItem>();

		/// <summary>
		/// The root of the scene hierarchy.
		/// </summary>
		public SceneItem SceneTree { get; private set; }

		public SceneTreeBuilder SceneTreeBuilder { get; }

		/// <summary>
		/// The list of views (timeline, inspector, ...)
		/// </summary>
		public readonly List<IDocumentView> Views = new List<IDocumentView>();

		/// <summary>
		/// Base64 representation of Document preview in .png format
		/// </summary>
		public string Preview { get; set; }

		public int AnimationFrame
		{
			get => Animation.Frame;
			set => Animation.Frame = value;
		}

		/// <summary>
		/// Starts current animation from timeline cursor position
		/// </summary>
		public bool PreviewAnimation { get; set; }
		/// <summary>
		/// PreviewScene allow you to hide Tangerine specific presenters
		/// (e.g. FrameProgression or SelectedWidgets) in order to see
		/// how scene will look in the game
		/// </summary>
		public bool PreviewScene { get; set; }
		public int PreviewAnimationBegin { get; set; }
		public Node PreviewAnimationContainer { get; set; }
		public bool ExpositionMode { get; set; }
		public ResolutionPreview ResolutionPreview { get; set; } = new ResolutionPreview();
		public bool InspectRootNode { get; set; }

		public Animation Animation
		{
			get => animation;
			set
			{
				if (animation != value) {
					animation = value;
					if (animation != null) {
						animationFastForwarder.FastForwardSafe(animation, value.Frame, true);
					}
					BumpSceneTreeVersion();
				}
			}
		}

		private Animation animation;

		public string AnimationId => Animation.Id;

		public static NodeManager CreateDefaultManager()
		{
			var services = new ServiceRegistry();
			services.Add(new BehaviorSystem());
			services.Add(new LayoutManager());

			var manager = new NodeManager(services);
			manager.Processors.Add(new BehaviorSetupProcessor());
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PreEarlyUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(EarlyUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PostEarlyUpdateStage)));
			manager.Processors.Add(new AnimationProcessor());
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(AfterAnimationStage)));
			manager.Processors.Add(new LayoutProcessor());
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PreLateUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(LateUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PostLateUpdateStage)));
			return manager;
		}

		public static Func<NodeManager> ManagerFactory;

		private Document()
		{
			History.ExceptionHandler += e => {
				if (e is Operations.AttemptToSetKeyFrameOutOfAnimationScopeException) {
					ShowWarning("Attempt to set keframe out of the animation scope");
					return true;
				}
				return false;
			};
			History.DocumentChanged += () => fullHierarchyChangeTime = DateTime.Now;
			Manager = ManagerFactory?.Invoke() ?? CreateDefaultManager();
			animationFastForwarder = new AnimationFastForwarder();
			SceneTreeBuilder = new SceneTreeBuilder(o => {
				var item = GetSceneItemForObject(o);
				if (item.Parent != null || item.SceneItems.Count > 0) {
					throw new InvalidOperationException("Attempt to allocate a SceneItem built into hierarchy");
				}
				return item;
			});
		}

		public Document(DocumentFormat format = DocumentFormat.Tan, Type rootType = null) : this()
		{
			Format = format;
			Path = string.Format(untitledPathFormat, untitledCounter++);
			if (rootType == null) {
				Container = RootNodeUnwrapped = RootNode = new Frame { Size = defaultSceneSize };
			} else {
				var constructor = rootType.GetConstructor(Type.EmptyTypes);
				Container = RootNodeUnwrapped = RootNode = (Node)constructor.Invoke(new object[] { });
				if (RootNode is Widget widget) {
					widget.Size = defaultSceneSize;
				}
			}
			RootNode = RootNodeUnwrapped;
			if (RootNode is Node3D) {
				RootNode = WrapNodeWithViewport3D(RootNode);
			}
			Manager.RootNodes.Clear();
			Manager.RootNodes.Add(RootNode);
			if (RootNode is Widget w) {
				w.LayoutManager = Manager.ServiceProvider.GetService<LayoutManager>();
			}
			Decorate(RootNode);
			Container = RootNode;
			Animation = Container.DefaultAnimation;
			History.ProcessingOperation += DocumentProcessingOperation;
			RefreshSceneTree();
		}

		public Document(string path, bool delayLoad = false) : this()
		{
			Path = path;
			Loaded = false;
			Format = ResolveFormat(Path);
			LastWriteTime = File.GetLastWriteTime(FullPath);
			if (delayLoad) {
				preloadedSceneStream = new MemoryStream();
				var fullPath = Node.ResolveScenePath(path);
				using (var stream = AssetBundle.Current.OpenFileLocalized(fullPath)) {
					stream.CopyTo(preloadedSceneStream);
					preloadedSceneStream.Seek(0, SeekOrigin.Begin);
				}
			} else {
				Load();
			}
		}

		private int sceneTreeVersion;

		public int SceneTreeVersion => sceneTreeVersion;

		public void BumpSceneTreeVersion()
		{
			sceneTreeVersion++;
			cachedVisibleSceneItems.Clear();
		}

		public void RefreshSceneTree()
		{
			if (SceneTree != null) {
				DisintegrateTree(SceneTree);
			}
			SceneTree = SceneTreeBuilder.BuildSceneTreeForNode(RootNode);
			BumpSceneTreeVersion();
		}

		private static void DisintegrateTree(SceneItem tree)
		{
			foreach (var child in tree.SceneItems) {
				DisintegrateTree(child);
			}
			tree.SceneItems.Clear();
		}

		private void Load() => Load(new HashSet<string>());

		private void Load(HashSet<string> documentsBeingLoaded)
		{
			try {
				// Load the scene without externals since they will be loaded further in RefreshExternalScenes().
				if (preloadedSceneStream != null) {
					RootNodeUnwrapped = Node.Load(
						preloadedSceneStream,
						Path + $".{GetFileExtension(Format)}",
						ignoreExternals: true
					);
				} else {
					RootNodeUnwrapped = Node.Load(Path, ignoreExternals: true);
				}
				if (Format == DocumentFormat.Fbx) {
					Path = string.Format(untitledPathFormat, untitledCounter++);
				}
				RootNode = RootNodeUnwrapped;
				if (RootNode is Node3D) {
					RootNode = WrapNodeWithViewport3D(RootNode);
				}
				Manager.RootNodes.Clear();
				Manager.RootNodes.Add(RootNode);
				if (RootNode is Widget w) {
					w.LayoutManager = Manager.ServiceProvider.GetService<LayoutManager>();
				}
				Decorate(RootNode);
				Container = RootNode;
				Animation = Container.DefaultAnimation;
				if (Format == DocumentFormat.Tan) {
					if (preloadedSceneStream != null) {
						preloadedSceneStream.Seek(0, SeekOrigin.Begin);
						Preview = DocumentPreview.ReadAsBase64(preloadedSceneStream);
					} else {
						Preview = DocumentPreview.ReadAsBase64(FullPath);
					}
				}
				History.ProcessingOperation += DocumentProcessingOperation;
				// Protect us from legacy scenes where bones order isn't determined.
				SortBones(RootNode);
				// Take the external scenes from the currently opened documents.
				RefreshExternalScenes(documentsBeingLoaded);
				RootNode.NotifyOnBuilt();
				RefreshSceneTree();
			} catch (System.Exception e) {
				throw new System.InvalidOperationException($"Can't open '{Path}':\n{e.Message}\n{e.StackTrace}\n---");
			}
			Loaded = true;
			OnLocaleChanged();

			void SortBones(Node node)
			{
				foreach (var n in node.Nodes) {
					SortBones(n);
				}
				BoneUtils.SortBones(node.Nodes);
			}
		}

		private void DocumentProcessingOperation(IOperation operation)
		{
			if (PreviewAnimation) {
				TogglePreviewAnimation();
			}
			if (operation.IsChangingDocument) {
				BumpSceneTreeVersion();
			}
			Application.InvalidateWindows();
		}

		private static Viewport3D WrapNodeWithViewport3D(Node node)
		{
			var vp = new Viewport3D { Width = 1024, Height = 768 };
			vp.AddNode(node);
			var camera = node.Descendants.FirstOrDefault(n => n is Camera3D);
			if (camera == null) {
				camera = new Camera3D {
					Id = "DefaultCamera",
					Position = new Vector3(0, 0, 10),
					FarClipPlane = 1000,
					NearClipPlane = 0.01f,
					FieldOfView = 1.0f,
					AspectRatio = 1.3f,
					OrthographicSize = 1.0f,
				};
				vp.AddNode(camera);
			}
			vp.CameraRef = new NodeReference<Camera3D>(camera.Id);
			return vp;
		}

		public static DocumentFormat ResolveFormat(string path)
		{
			if (AssetExists(path, "tan")) {
				return DocumentFormat.Tan;
			}
			if (AssetExists(path, "fbx")) {
				return DocumentFormat.Fbx;
			}
			if (AssetExists(path, "t3d")) {
				return DocumentFormat.T3D;
			}
			throw new FileNotFoundException(path);
		}

		public static string GetFileExtension(DocumentFormat format)
		{
			switch (format) {
				case DocumentFormat.Tan:
					return "tan";
				case DocumentFormat.T3D:
				case DocumentFormat.Fbx:
					return "t3d";
				default: throw new InvalidOperationException();
			}
		}

		public string GetFileExtension() => GetFileExtension(Format);

		private static bool AssetExists(string path, string ext) => AssetBundle.Current.FileExists(path + $".{ext}");

		public void MakeCurrent()
		{
			SetCurrent(this);
		}

		public static void SetCurrent(Document doc)
		{
			if (doc != null && !doc.Loaded) {
				if (Project.Current.GetFullPath(doc.Path, out _) || doc.preloadedSceneStream != null) {
					doc.Load();
				}
			}
			if (Current != doc) {
				DetachViews();
				Current = doc;
#if PROFILER
				SceneProfilingInfo.NodeManager = doc?.Manager;
#endif // PROFILER
				doc?.AttachViews();
				if (doc != null) {
					ProjectUserPreferences.Instance.CurrentDocument = doc.Path;
				}
			}
		}

		public void RefreshExternalScenes()
		{
			RefreshExternalScenes(new HashSet<string>());
			RefreshSceneTree();
		}

		private void RefreshExternalScenes(HashSet<string> scenesBeingRefreshed)
		{
			if (!Loaded) {
				return;
			}
			if (scenesBeingRefreshed.Contains(Path)) {
				throw new CyclicDependencyException($"Cyclic scenes dependency was detected: {Path}");
			}
			scenesBeingRefreshed.Add(Path);
			try {
				RefreshExternalScenesHelper(scenesBeingRefreshed);
			} finally {
				scenesBeingRefreshed.Remove(Path);
			}
		}

		/// <summary>
		/// Contains all the external scenes even those which are not currently opened in the tangerine.
		/// </summary>
		private static readonly Dictionary<string, (Document, SHA256)> externalScenesCache =
			new Dictionary<string, (Document, SHA256)>();

		/// <summary>
		/// Denotes the timestamp when hierarchy (including content of external scenes) was changed.
		/// </summary>
		private DateTime fullHierarchyChangeTime;

		private void RefreshExternalScenesHelper(HashSet<string> scenesBeingRefreshed)
		{
			var nodesGroupedByContentsPath =
				RootNodeUnwrapped.SelfAndDescendants.Where(
					i => !string.IsNullOrEmpty(i.ContentsPath) &&
					i.Ancestors.All(i => string.IsNullOrEmpty(i.ContentsPath) // Only the top-level external scenes.
				)).GroupBy(i => i.ContentsPath).ToList();
			var someContentReplaced = false;
			foreach (var nodesWithSameContentPath in nodesGroupedByContentsPath) {
				var contentsPath = nodesWithSameContentPath.Key;
				var externalScene = GetExternalSceneDocument(
					scenesBeingRefreshed,
					contentsPath,
					nodesWithSameContentPath.First().GetType()
				);
				if (
					fullHierarchyChangeTime == DateTime.MinValue ||
					externalScene.fullHierarchyChangeTime == DateTime.MinValue ||
					externalScene.fullHierarchyChangeTime > fullHierarchyChangeTime
				) {
					someContentReplaced = true;
					foreach (var node in nodesWithSameContentPath) {
						var clone = InternalPersistence.Instance.Clone(externalScene.RootNodeUnwrapped);
						node.ReplaceContent(clone);
						foreach (var n in node.Descendants.ToList()) {
							Decorate(n);
						}
					}
				}
				foreach (var node in nodesWithSameContentPath) {
					SynchronizeAnimations(externalScene.RootNodeUnwrapped, node);
				}
			}
			if (someContentReplaced) {
				fullHierarchyChangeTime = DateTime.Now;
			}
			// Apply the current animation to synchronize external scenes.
			ForceAnimationUpdate();
		}

		private Document GetExternalSceneDocument(
			HashSet<string> scenesBeingRefreshed, string path, Type ownerNodeType
		) {
			var document = Project.Current.Documents.FirstOrDefault(i => i.Path == path);
			var contentHash = new SHA256();
			if (document != null) {
				if (!document.Loaded) {
					document.Load(scenesBeingRefreshed);
				}
			} else {
				var assetPath = Node.ResolveScenePath(path);
				if (assetPath != null) {
					contentHash = AssetBundle.Current.GetFileContentsHash(assetPath);
					if (assetPath.EndsWith(".t3d")) {
						var attachmentPath =
							System.IO.Path.ChangeExtension(assetPath, Model3DAttachment.FileExtension);
						if (AssetBundle.Current.FileExists(attachmentPath)) {
							contentHash = SHA256.Compute(
								contentHash,
								AssetBundle.Current.GetFileContentsHash(attachmentPath)
							);
						}
					}
					document =
						externalScenesCache.TryGetValue(path, out (Document Document, SHA256 ContentHash) i) &&
						i.ContentHash == contentHash
							? i.Document
							: new Document(path);
				} else {
					document = new Document {
						Path = path,
						RootNodeUnwrapped = (Node)Activator.CreateInstance(ownerNodeType),
					};
					document.Animation = document.RootNodeUnwrapped.DefaultAnimation;
				}
			}
			externalScenesCache[path] = (document, contentHash);
			document.RefreshExternalScenes(scenesBeingRefreshed);
			return document;
		}

		private static void SynchronizeAnimations(Node origin, Node target)
		{
			// Heuristic: it is necessary to check the number of nodes and animations,
			// because the user code can change the node hierarchy.
			if (origin.Nodes.Count == target.Nodes.Count) {
				int i = 0;
				foreach (var o in origin.Nodes) {
					SynchronizeAnimations(o, target.Nodes[i++]);
				}
			}
			if (origin.Animations.Count == target.Animations.Count) {
				int i = 0;
				foreach (var a1 in origin.Animations) {
					var a2 = target.Animations[i++];
					if (a2.Frame != a1.Frame) {
						a2.Frame = a1.Frame;
					}
				}
			}
		}

		public void RestoreState(PreservedDocumentState preservedState)
		{
			DocumentViewStateComponents.Clear();
			foreach (var component in preservedState.Components) {
				DocumentViewStateComponents.Add(component);
			}
			var docState = preservedState.Components.GetOrAdd<DocumentStateComponent>();
			InspectRootNode = docState.InspectRootNode;
			if (preservedState.SelectedItems.Count > 0 ||
				preservedState.ExpandedItems.Count > 0 ||
				docState.AnimationOwnerNodePath != null ||
				docState.ContainerPath != null
			) {
				if (!Loaded && (Project.Current.GetFullPath(Path, out _) || preloadedSceneStream != null)) {
					Load();
				}
				bool containerWasUpdated = false;
				var states = new List<TimelineSceneItemStateComponent>();
				foreach (var item in SceneTree.SelfAndDescendants()) {
					var state = item.GetTimelineSceneItemState();
					string itemPath = PreservedDocumentState.GetSceneItemIndexPath(item);
					state.Selected = preservedState.SelectedItems.Contains(itemPath);
					state.NodesExpanded = preservedState.ExpandedItems.Contains(itemPath);
					if (state.Selected) {
						states.Add(state);
					}
					var node = item.GetNode();
					if (node == null) {
						continue;
					}
					string nodePath = PreservedDocumentState.GetNodeIndexPath(node);
					if (nodePath == docState.ContainerPath) {
						Container = node;
						containerWasUpdated = true;
					}
					if (nodePath == docState.AnimationOwnerNodePath) {
						if (node.Animations.TryFind(docState.AnimationId, out var animation)) {
							Animation = animation;
						} else {
							Animation = node.DefaultAnimation;
						}
					}
				}
				if (states.Count != preservedState.SelectedItems.Count || !containerWasUpdated) {
					foreach (var state in states) {
						state.Selected = false;
					}
				}
			}
		}

		public PreservedDocumentState PreserveState()
		{
			if (!Loaded && (Project.Current.GetFullPath(Path, out _) || preloadedSceneStream != null)) {
				return PreservedDocumentState.Null;
			}
			var ds = DocumentViewStateComponents.GetOrAdd<DocumentStateComponent>();
			ds.ContainerPath = PreservedDocumentState.GetNodeIndexPath(container);
			ds.InspectRootNode = InspectRootNode;
			if (animation != null) {
				ds.AnimationId = animation.Id;
				ds.AnimationOwnerNodePath = PreservedDocumentState.GetNodeIndexPath(animation.OwnerNode);
			} else {
				ds.AnimationId = null;
				ds.AnimationOwnerNodePath = null;
			}
			foreach (var view in Views) {
				view.SyncDocumentState();
			}
			var state = new PreservedDocumentState();
			foreach (var component in DocumentViewStateComponents) {
				state.Components.Add(component);
			}
			foreach (var item in SceneTree.SelfAndDescendants()) {
				string itemPath = PreservedDocumentState.GetSceneItemIndexPath(item);
				if (item.GetTimelineSceneItemState().Selected) {
					state.SelectedItems.Add(itemPath);
				}
				if (item.GetTimelineSceneItemState().NodesExpanded) {
					state.ExpandedItems.Add(itemPath);
				}
			}
			return state;
		}

		private void AttachViews()
		{
			RefreshExternalScenes();
			AttachingViews?.Invoke(this);
			foreach (var i in Current.Views) {
				i.Attach();
			}
			SelectFirstSceneItemIfNoneSelected();
		}

		private void SelectFirstSceneItemIfNoneSelected()
		{
			if (!SceneTree.SelfAndDescendants().Any(i => i.GetTimelineSceneItemState().Selected)) {
				using (History.BeginTransaction()) {
					Operations.Dummy.Perform(Current.History);
					if (VisibleSceneItems.Count > 0) {
						Operations.SelectSceneItem.Perform(VisibleSceneItems[0]);
					}
					History.CommitTransaction();
				}
			}
		}

		private static void DetachViews()
		{
			if (Current == null) {
				return;
			}
			foreach (var i in Current.Views) {
				i.Detach();
			}
		}

		public void ShowWarning(string message)
		{
			ShowingWarning?.Invoke(this, message);
		}

		public bool Close(bool force)
		{
			if (!force && IsModified) {
				if (CloseConfirmation != null) {
					switch (CloseConfirmation(this)) {
						case CloseAction.Cancel:
							return false;
						case CloseAction.SaveChanges:
							Save();
							break;
					}
				} else {
					Save();
				}
			}
			externalScenesCache.Remove(Path);
			return true;
		}

		public void Save()
		{
			if (Project.IsDocumentUntitled(Path)) {
				if (PathSelector(out var path)) {
					var directoryInfo = new DirectoryInfo(System.IO.Path.GetDirectoryName(FullPath));
					SaveAs(path);
					// Delete Untitled directory and it's content
					directoryInfo.Delete(true);
				}
			} else {
				SaveAs(Path);
			}
		}

		public void SaveAs(string path)
		{
			if (System.IO.Path.IsPathRooted(path)) {
				throw new InvalidOperationException("The path must be project relative");
			}
			if (!Loaded && IsModified) {
				Load();
			}
			Project.RaiseDocumentSaving(this);
			History.AddSavePoint();
			Path = path;
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FullPath));
			ExportNodeToFile(FullPath, Path, RootNodeUnwrapped);
			if (Format == DocumentFormat.Tan) {
				DocumentPreview.AppendToFile(FullPath, Preview);
			}
			LastWriteTime = File.GetLastWriteTime(FullPath);
			Project.Current.AddRecentDocument(Path);
			Project.RaiseDocumentSaved(this);
		}

		public void ExportToFile(string filePath, string assetPath, FileAttributes attributes = 0)
		{
			ExportNodeToFile(filePath, assetPath, RootNodeUnwrapped, attributes);
		}

		public static void ExportNodeToFile(
			string filePath,
			string assetPath,
			Node node,
			FileAttributes attributes = 0
		) {
			// Save the document into memory at first to avoid a torn file in the case of a serialization error.
			var ms = new MemoryStream();
			// Dispose cloned object to preserve keyframes identity in the original node. See Animator.Dispose().
			using (node = CreateCloneForSerialization(node)) {
				// Removing dangling animators is a context dependent action, so performing it inside
				// CreateCloneForSerializetion will lead to bugs (e.g. disappearing animators on paste)
				var removedDanglingAnimatorCount = Orange.NodeExtensions.RemoveDanglingAnimators(node);
				if (removedDanglingAnimatorCount != 0) {
					if (removedDanglingAnimatorCount == 1) {
						Console.WriteLine("Removed 1 dangling animator.");
					} else {
						Console.WriteLine($"Removed {removedDanglingAnimatorCount} dangling animators.");
					}
				}
				InternalPersistence.Instance.WriteToStream(assetPath, ms, node, Persistence.Format.Json);
			}
			var fileModeForHiddenFile = File.Exists(filePath) ? FileMode.Truncate : FileMode.Create;
			using (var fs = new FileStream(filePath, fileModeForHiddenFile)) {
				var a = ms.ToArray();
				fs.Write(a, 0, a.Length);
			}
			var fileInfo = new System.IO.FileInfo(filePath);
			fileInfo.Attributes |= attributes;
		}

		public static Node CreateCloneForSerialization(Node node)
		{
			return Orange.Toolbox.CreateCloneForSerialization(node);
		}

		public IEnumerable<Node> Nodes()
		{
			foreach (var i in VisibleSceneItems) {
				var ni = i.Components.Get<NodeSceneItem>();
				if (ni != null) {
					yield return ni.Node;
				}
			}
		}

		public IEnumerable<SceneItem> SelectedSceneItems()
		{
			foreach (var i in VisibleSceneItems) {
				if (i.GetTimelineSceneItemState().Selected) {
					yield return i;
				}
			}
		}

		public SceneItem RecentlySelectedSceneItem()
		{
			int c = 0;
			SceneItem result = null;
			foreach (var i in VisibleSceneItems) {
				var order = i.GetTimelineSceneItemState().SelectionOrder;
				if (order > c) {
					c = order;
					result = i;
				}
			}
			return result;
		}

		private IEnumerable<Node> SelectedNodes(bool onlyTopLevel)
		{
			if (InspectRootNode) {
				yield return RootNode;
				yield break;
			}
			Node prevNode = null;
			foreach (var item in VisibleSceneItems.Where(i => i.GetTimelineSceneItemState().Selected).ToList()) {
				Node node = null;
				var nr = item.Components.Get<NodeSceneItem>();
				if (nr != null) {
					node = nr.Node;
					prevNode = nr.Node;
				}
				var pr = item.Components.Get<AnimatorSceneItem>();
				if (pr != null && pr.Node != prevNode) {
					node = pr.Node;
					prevNode = pr.Node;
				}
				if (node != null) {
					if (onlyTopLevel) {
						for (var i = item.Parent; i != null; i = i.Parent) {
							if (i.GetTimelineSceneItemState().Selected && i.Components.Contains<NodeSceneItem>()) {
								node = null;
								break;
							}
						}
					}
					if (node != null) {
						yield return node;
					}
				}
			}
		}

		public IEnumerable<Node> SelectedNodes() => SelectedNodes(onlyTopLevel: false);
		public IEnumerable<Node> TopLevelSelectedNodes() => SelectedNodes(onlyTopLevel: true);

		public IEnumerable<Node> ContainerChildNodes() => Container.Nodes;

		public IEnumerable<SceneItem> TopLevelSelectedSceneItems()
		{
			foreach (var item in VisibleSceneItems) {
				if (item.GetTimelineSceneItemState().Selected) {
					var discardItem = false;
					for (var p = item.Parent; p != null; p = p.Parent) {
						discardItem |= p.GetTimelineSceneItemState().Selected;
					}
					if (!discardItem) {
						yield return item;
					}
				}
			}
		}

		public SceneItem GetSceneItemForObject(object obj)
		{
			if (!sceneItemCache.TryGetValue(obj, out var i)) {
				i = new SceneItem();
				sceneItemCache.Add(obj, i);
			}
			return i;
		}

		public static bool HasCurrent() => Current != null;

		public static void Decorate(Node node)
		{
			// Make sure the legacy animation is exists.
			if (NodeCompositionValidator.CanHaveChildren(node.GetType())) {
				_ = node.DefaultAnimation;
			}
			foreach (var decorator in NodeDecorators) {
				decorator(node);
			}
			foreach (var child in node.Nodes) {
				Decorate(child);
			}
		}

		public void OnLocaleChanged()
		{
			if (!Loaded) {
				return;
			}
			foreach (var text in RootNode.Descendants.OfType<IText>()) {
				text.Invalidate();
			}
		}

		public class NodeDecoratorList : List<Action<Node>>
		{
			public void AddFor<T>(Action<Node> action)
				where T : Node
			{
				Add(node => {
					if (node is T) {
						action(node);
					}
				});
			}
		}

		public void SetAnimationFrame(Animation animation, int frameIndex, bool stopAnimations = true)
		{
			animationFastForwarder.FastForwardSafe(animation, frameIndex, stopAnimations);
		}

		public void TogglePreviewAnimation()
		{
			if (PreviewAnimation) {
				PreviewAnimation = false;
				PreviewScene = false;
				Animation.IsRunning = false;
				StopAnimationRecursive(PreviewAnimationContainer);
				if (!CoreUserPreferences.Instance.StopAnimationOnCurrentFrame) {
					SetAnimationFrame(Animation, PreviewAnimationBegin);
				}
				AudioSystem.StopAll();
				ForceAnimationUpdate();
				ClearParticlesRecursive(Animation.OwnerNode);
			} else {
				foreach (var node in RootNode.Descendants) {
					if (node is ITangerinePreviewAnimationListener t) {
						t.OnStart();
					}
				}
				int savedAnimationFrame = AnimationFrame;
				SetAnimationFrame(Animation, AnimationFrame, stopAnimations: false);
				PreviewScene = true;
				PreviewAnimation = true;
				Animation.IsRunning = PreviewAnimation;
				PreviewAnimationBegin = savedAnimationFrame;
				PreviewAnimationContainer = Container;
			}
			Application.InvalidateWindows();

			void StopAnimationRecursive(Node node)
			{
				void StopAnimation(Node n)
				{
					foreach (var animation in n.Animations) {
						animation.IsRunning = false;
					}
				}
				foreach (var descendant in node.SelfAndDescendants) {
					StopAnimation(descendant);
				}
			}

			void ClearParticlesRecursive(Node node)
			{
				if (node is ParticleEmitter emitter) {
					emitter.ClearParticles();
				}
				foreach (var child in node.Nodes) {
					ClearParticlesRecursive(child);
				}
			}
		}

		public void ForceAnimationUpdate() => SetAnimationFrame(Animation, AnimationFrame);
	}
}
