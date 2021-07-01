using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Lime;
using Tangerine.Core.Components;
using System.Runtime.CompilerServices;
#if PROFILER
using Lime.Profiler;
#endif // PROFILER

namespace Tangerine.Core
{
	public interface IDocumentView
	{
		void Detach();
		void Attach();
	}

	public enum DocumentFormat
	{
		Tan,
		T3D,
		Fbx
	}

	public interface ISceneViewThumbnailProvider
	{
		void Generate(int frame, Action<ITexture> callback);
	}

	public sealed class Document
	{
		public enum CloseAction
		{
			Cancel,
			SaveChanges,
			DiscardChanges
		}

		private readonly string untitledPathFormat = ".untitled/{0:D2}/Untitled{0:D2}";
		private readonly Vector2 defaultSceneSize = new Vector2(1024, 768);
		private readonly ConditionalWeakTable<object, Row> sceneItemCache = new ConditionalWeakTable<object, Row>();
		private readonly MemoryStream preloadedSceneStream = null;
		private readonly IAnimationPositioner animationPositioner = new AnimationPositioner();
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
		public string DisplayName => (IsModified ? "*" : string.Empty) + System.IO.Path.GetFileName(Path ?? "Untitled");

		/// <summary>
		/// Gets or sets the file format the document should be saved to.
		/// </summary>
		public DocumentFormat Format { get; set; }

		public Node RootNodeUnwrapped { get; private set; }

		/// <summary>
		/// Gets the root node for the current document.
		/// </summary>
		public Node RootNode { get; private set; }

		public ISceneViewThumbnailProvider SceneViewThumbnailProvider { get; set; }

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
		/// Gets or sets the scene we are navigated from. Need for getting back into the main scene from the external one.
		/// </summary>
		public string SceneNavigatedFrom { get; set; }

		/// <summary>
		/// The list of rows, currently displayed on the timeline.
		/// TODO: Rename to VisibleSceneItems
		/// </summary>
		public List<Row> Rows
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

				void TraverseAnimationTree(Row animationTree)
				{
					foreach (var i in animationTree.Rows) {
						if (i.TryGetAnimationTrack(out _)) {
							i.GetTimelineItemState().Index = cachedVisibleSceneItems.Count;
							cachedVisibleSceneItems.Add(i);
							TraverseAnimationTree(i);
						}
					}
				}

				void TraverseSceneTree(Row sceneTree, bool addNodes)
				{
					var currentAnimation = Animation;
					var timelineItemState = sceneTree.GetTimelineItemState();
					timelineItemState.Expandable = false;
					timelineItemState.HasAnimators = false;
					var containerSceneItem = GetSceneItemForObject(Container);
					var expanded = timelineItemState.Expanded || sceneTree == containerSceneItem;
					foreach (var i in sceneTree.Rows) {
						i.GetTimelineItemState().Index = cachedVisibleSceneItems.Count;
						if (i.TryGetAnimator(out var animator)) {
							if (!animator.IsZombie && currentAnimation.ValidatedEffectiveAnimatorsSet.Contains(animator)) {
								timelineItemState.HasAnimators = true;
								timelineItemState.Expandable |= (ShowAnimators || timelineItemState.ShowAnimators);
								if (timelineItemState.Expanded && (ShowAnimators || timelineItemState.ShowAnimators)) {
									cachedVisibleSceneItems.Add(i);
								}
							}
						} else if (i.TryGetNode(out var node) || i.GetFolder() != null) {
							if (addNodes) {
								timelineItemState.Expandable = true;
								if (expanded) {
									var hierarchyMode =
										!Animation.IsLegacy &&
									    CoreUserPreferences.Instance.ExperimentalTimelineHierarchy;
									cachedVisibleSceneItems.Add(i);
									TraverseSceneTree(i,
										(node is Bone || i.GetFolder() != null || hierarchyMode)
										&& (!i.TryGetNode(out var n) || string.IsNullOrEmpty(n.ContentsPath)));
								}
							}
						} else if (i.TryGetAnimation(out _)) {
						} else {
							timelineItemState.Expandable = true;
							if (expanded) {
								cachedVisibleSceneItems.Add(i);
								TraverseSceneTree(i, addNodes);
							}
						}
					}
				}
			}
		}

		private readonly List<Row> cachedVisibleSceneItems = new List<Row>();

		/// <summary>
		/// The root of the scene hierarchy.
		/// </summary>
		public Row SceneTree { get; private set; }

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
						animationPositioner.SetAnimationTime(animation, value.Time, true);
					}
					BumpSceneTreeVersion();
				}
			}
		}

		private Animation animation;

		public string AnimationId => Animation.Id;

		private static NodeManager CreateDefaultManager()
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
				};
				return false;
			};
			Manager = ManagerFactory?.Invoke() ?? CreateDefaultManager();
			SceneTreeBuilder = new SceneTreeBuilder(o => {
				var item = GetSceneItemForObject(o);
				if (item.Parent != null || item.Rows.Count > 0) {
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

		private static void DisintegrateTree(Row tree)
		{
			foreach (var child in tree.Rows) {
				DisintegrateTree(child);
			}
			tree.Rows.Clear();
		}

		private void Load() => Load(new HashSet<string>());

		private void Load(HashSet<string> documentsBeingLoaded)
		{
			try {
				// Load the scene without externals since they will be loaded further in RefreshExternalScenes().
				if (preloadedSceneStream != null) {
					RootNodeUnwrapped = Node.Load(preloadedSceneStream,
						Path + $".{GetFileExtension(Format)}", ignoreExternals: true);
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
					OrthographicSize = 1.0f
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

		static bool AssetExists(string path, string ext) => AssetBundle.Current.FileExists(path + $".{ext}");

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

		private static readonly Dictionary<string, Document> documentCache = new Dictionary<string, Document>();
		private static readonly Dictionary<string, SHA256> documentHashes = new Dictionary<string, SHA256>();

		public void RefreshExternalScenes() => RefreshExternalScenes(new HashSet<string>());

		private void RefreshExternalScenes(HashSet<string> documentsBeingLoaded)
		{
			if (!Loaded) {
				return;
			}
			if (documentsBeingLoaded.Contains(Path)) {
				throw new CyclicDependencyException($"Cyclic scenes dependency was detected: {Path}");
			}
			documentsBeingLoaded.Add(Path);
			try {
				RefreshExternalContentHelper(documentsBeingLoaded);
				RefreshSceneTree();
			} finally {
				documentsBeingLoaded.Remove(Path);
			}
		}

		private void RefreshExternalContentHelper(HashSet<string> documentsBeingLoaded)
		{
			var nodesWithContentsPath = RootNodeUnwrapped.SelfAndDescendants
				.Where(
					i => !string.IsNullOrEmpty(i.ContentsPath) &&
					i.Ancestors.All(i => string.IsNullOrEmpty(i.ContentsPath) // Only the top-level external scenes.
				)).ToList();
			var processedNodes = new List<Node>();
			foreach (var node in nodesWithContentsPath) {
				var document = Project.Current.Documents.FirstOrDefault(i => i.Path == node.ContentsPath);
				if (document != null) {
					if (!document.Loaded) {
						document.Load(documentsBeingLoaded);
					}
				} else {
					var assetPath = Node.ResolveScenePath(node.ContentsPath);
					if (assetPath != null) {
						var hash = AssetBundle.Current.GetFileContentsHash(assetPath);
						if (assetPath.EndsWith(".t3d")) {
							var attachmentPath = System.IO.Path.ChangeExtension(assetPath, Model3DAttachment.FileExtension);
							if (AssetBundle.Current.FileExists(attachmentPath)) {
								hash = SHA256.Compute(hash, AssetBundle.Current.GetFileContentsHash(attachmentPath));
							}
						}
						if (
							!documentCache.TryGetValue(node.ContentsPath, out document) ||
							hash != documentHashes[node.ContentsPath]
						) {
							documentCache[node.ContentsPath] = document = new Document(node.ContentsPath);
							documentHashes[node.ContentsPath] = hash;
						}
					}
				}
				if (document == null) {
					node.ReplaceContent((Node)Activator.CreateInstance(node.GetType()));
				} else {
					// optimization: don't refresh external scenes twice.
					if (!processedNodes.Any(i => i.ContentsPath == document.Path)) {
						document.RefreshExternalScenes(documentsBeingLoaded);
					}
					var clone = InternalPersistence.Instance.Clone(document.RootNodeUnwrapped);
					node.ReplaceContent(clone);
					foreach (var n in node.Descendants.ToList()) {
						Decorate(n);
					}
				}
				processedNodes.Add(node);
			}
			// Force animation update to synchronize reloaded external scenes.
			ForceAnimationUpdate();
		}
		private void AttachViews()
		{
			RefreshExternalScenes();
			AttachingViews?.Invoke(this);
			foreach (var i in Current.Views) {
				i.Attach();
			}
			SelectFirstRowIfNoneSelected();
		}

		private void SelectFirstRowIfNoneSelected()
		{
			if (!SceneTree.SelfAndDescendants().Any(i => i.GetTimelineItemState().Selected)) {
				using (History.BeginTransaction()) {
					Operations.Dummy.Perform(Current.History);
					if (Rows.Count > 0) {
						Operations.SelectRow.Perform(Rows[0]);
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
			documentCache.Remove(Path);
			documentHashes.Remove(Path);
			return true;
		}

		public void Save()
		{
			if (Project.Current.IsDocumentUntitled(Path)) {
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
			ExportNodeToFile(FullPath, Path, Format, RootNodeUnwrapped);
			if (Format == DocumentFormat.Tan) {
				DocumentPreview.AppendToFile(FullPath, Preview);
			}
			LastWriteTime = File.GetLastWriteTime(FullPath);
			Project.Current.AddRecentDocument(Path);
			Project.RaiseDocumentSaved(this);
		}

		public void ExportToFile(string filePath, string assetPath, FileAttributes attributes = 0)
		{
			ExportNodeToFile(filePath, assetPath, Format, RootNodeUnwrapped, attributes);
		}

		public static void ExportNodeToFile(string filePath, string assetPath, DocumentFormat format, Node node, FileAttributes attributes = 0)
		{
			// Save the document into memory at first to avoid a torn file in the case of a serialization error.
			var ms = new MemoryStream();
			// Dispose cloned object to preserve keyframes identity in the original node. See Animator.Dispose().
			using (node = CreateCloneForSerialization(node)) {
				InternalPersistence.Instance.WriteObject(assetPath, ms, node, Persistence.Format.Json);
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
			foreach (var row in Rows) {
				var nr = row.Components.Get<NodeRow>();
				if (nr != null) {
					yield return nr.Node;
				}
			}
		}

		public IEnumerable<Row> SelectedRows()
		{
			foreach (var i in Rows) {
				if (i.GetTimelineItemState().Selected) {
					yield return i;
				}
			}
		}

		public Row RecentlySelectedSceneItem()
		{
			int c = 0;
			Row result = null;
			foreach (var i in Rows) {
				var order = i.GetTimelineItemState().SelectionOrder;
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
			foreach (var item in Rows.Where(i => i.GetTimelineItemState().Selected).ToList()) {
				Node node = null;
				var nr = item.Components.Get<NodeRow>();
				if (nr != null) {
					node = nr.Node;
					prevNode = nr.Node;
				}
				var pr = item.Components.Get<PropertyRow>();
				if (pr != null && pr.Node != prevNode) {
					node = pr.Node;
					prevNode = pr.Node;
				}
				if (node != null) {
					if (onlyTopLevel) {
						for (var i = item.Parent; i != null; i = i.Parent) {
							if (i.GetTimelineItemState().Selected && i.Components.Contains<NodeRow>()) {
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

		public IEnumerable<Row> TopLevelSelectedRows()
		{
			foreach (var item in Rows) {
				if (item.GetTimelineItemState().Selected) {
					var discardItem = false;
					for (var p = item.Parent; p != null; p = p.Parent) {
						discardItem |= p.GetTimelineItemState().Selected;
					}
					if (!discardItem) {
						yield return item;
					}
				}
			}
		}

		public Row GetSceneItemForObject(object obj)
		{
			if (!sceneItemCache.TryGetValue(obj, out var i)) {
				i = new Row();
				sceneItemCache.Add(obj, i);
			}
			return i;
		}

		public static bool HasCurrent() => Current != null;

		public static void Decorate(Node node)
		{
			// Make sure the legacy animation is exists.
			if (NodeCompositionValidator.CanHaveChildren(node.GetType())) {
				var _ = node.DefaultAnimation;
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
			public void AddFor<T>(Action<Node> action) where T: Node
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
			animationPositioner.SetAnimationFrame(animation, frameIndex, stopAnimations);
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
