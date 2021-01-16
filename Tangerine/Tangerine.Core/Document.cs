using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
		private readonly Dictionary<object, Row> sceneItemCache = new Dictionary<object, Row>();
		private readonly Dictionary<Node, Animation> selectedAnimationPerContainer = new Dictionary<Node, Animation>();
		private readonly MemoryStream preloadedSceneStream = null;
		private readonly IAnimationPositioner animationPositioner = new AnimationPositioner();
		private static uint untitledCounter;
		private DateTime dependenciesRefreshTimestamp = DateTime.MinValue;

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
			set {
				if (container != value) {
					var oldContainer = container;
					container = value;
					OnContainerChanged(oldContainer);
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
						TraverseAnimationTree(CompoundAnimationTree);
					} else {
						TraverseSceneTree(GetSceneItemForObject(Container), true);
					}
				}
				return cachedVisibleSceneItems;

				void TraverseAnimationTree(Row animationTree)
				{
					foreach (var i in animationTree.Rows) {
						cachedVisibleSceneItems.Add(i);
						TraverseAnimationTree(i);
					}
				}

				void TraverseSceneTree(Row sceneTree, bool addNodes)
				{
					var animation = Animation;
					sceneTree.Expandable = false;
					sceneTree.HasAnimators = false;
					var containerSceneItem = GetSceneItemForObject(Container);
					var expanded = sceneTree.Expanded || sceneTree == containerSceneItem;
					foreach (var i in sceneTree.Rows) {
						i.Index = cachedVisibleSceneItems.Count;
						if (i.TryGetAnimator(out var animator)) {
							if (
								!animator.IsZombie && animator.AnimationId == animation.Id &&
								(animator.Owner as Node)?.Parent == Container
							) {
								sceneTree.HasAnimators = true;
								sceneTree.Expandable |= (ShowAnimators || sceneTree.ShowAnimators);
								if (sceneTree.Expanded && (ShowAnimators || sceneTree.ShowAnimators)) {
									cachedVisibleSceneItems.Add(i);
								}
							}
						} else if (i.TryGetNode(out var node) || i.GetFolder() != null) {
							if (addNodes) {
								sceneTree.Expandable = true;
								if (expanded) {
									cachedVisibleSceneItems.Add(i);
									TraverseSceneTree(i, node is Bone || i.GetFolder() != null);
								}
							}
						} else {
							sceneTree.Expandable = true;
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

		public Row CompoundAnimationTree { get; private set; }

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

		public Animation Animation => SelectedAnimation ?? Container.DefaultAnimation;

		public Animation SelectedAnimation
		{
			get => selectedAnimation;
			set
			{
				if (selectedAnimation != value) {
					selectedAnimation = value;
					if (selectedAnimation != null) {
						animationPositioner.SetAnimationTime(selectedAnimation, value.Time, true);
					}
					RefreshCompoundAnimationTree();
				}
			}
		}

		private Animation selectedAnimation;

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
			manager.Processors.Add(new BoundingRectProcessor());
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PreLateUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(LateUpdateStage)));
			manager.Processors.Add(new BehaviorUpdateProcessor(typeof(PostLateUpdateStage)));
			return manager;
		}

		public static Func<NodeManager> ManagerFactory;

		private Document()
		{
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
			History.ProcessingOperation += DocumentProcessingOperation;
			RefreshSceneTree();
			RefreshCompoundAnimationTree();
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

		private void RefreshCompoundAnimationTree()
		{
			if (CompoundAnimationTree != null) {
				DisintegrateTree(CompoundAnimationTree);
			}
			CompoundAnimationTree = SceneTreeBuilder.BuildTreeForCompoundAnimation(Animation);
			BumpSceneTreeVersion();
		}

		private static void DisintegrateTree(Row tree)
		{
			foreach (var child in tree.Rows) {
				DisintegrateTree(child);
			}
			tree.Rows.Clear();
		}

		public void GetAnimations(List<Animation> animations)
		{
			GetAnimationsHelper(animations);
			animations.Sort(AnimationsComparer.Instance);
			animations.Insert(0, Container.DefaultAnimation);
		}

		private readonly HashSet<string> usedAnimations = new HashSet<string>();

		private void GetAnimationsHelper(List<Animation> animations)
		{
			var ancestor = Container;
			lock (usedAnimations) {
				usedAnimations.Clear();
				while (true) {
					foreach (var a in ancestor.Animations) {
						if (!a.IsLegacy && usedAnimations.Add(a.Id)) {
							animations.Add(a);
						}
					}
					if (ancestor == RootNode) {
						return;
					}
					ancestor = ancestor.Parent;
				}
			}
		}

		class AnimationsComparer : IComparer<Animation>
		{
			public static readonly AnimationsComparer Instance = new AnimationsComparer();

			public int Compare(Animation x, Animation y)
			{
				return x.Id.CompareTo(y.Id);
			}
		}

		private void Load()
		{
			try {
				if (preloadedSceneStream != null) {
					RootNodeUnwrapped = Node.Load(preloadedSceneStream, Path + $".{GetFileExtension(Format)}");
				} else {
					RootNodeUnwrapped = Node.Load(Path);
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
				// Initialize timestamp to prevent external scenes from reloading from the hdd.
				dependenciesRefreshTimestamp = DateTime.Now;
				// Take the external scenes from the currently opened documents.
				RefreshExternalScenes();
				RefreshSceneTree();
			} catch (System.Exception e) {
				throw new System.InvalidOperationException($"Can't open '{Path}': {e.Message}");
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
				Current?.ForceAnimationUpdate();
			}
		}

		public void RefreshExternalScenes()
		{
			var dependencyPaths = RootNodeUnwrapped.Descendants
				.Where(i => !string.IsNullOrEmpty(i.ContentsPath))
				.Select(i => i.ContentsPath).ToHashSet();
			// Get this document and its opened dependencies.
			var documents = Project.Current.Documents.Where(
				i => i == this || dependencyPaths.Contains(i.Path)).ToList();
			// Add modified dependencies from hdd.
			foreach (var scenePath in dependencyPaths.Except(documents.Select(i => i.Path))) {
				var scenePathInBundle = Node.ResolveScenePath(scenePath);
				if (
					AssetBundle.Current.FileExists(scenePathInBundle) &&
					AssetBundle.Current.GetFileLastWriteTime(scenePathInBundle) > dependenciesRefreshTimestamp
				) {
					documents.Add(new Document(scenePath));
				}
			}
			// Remove not loaded documents (these documents aren't changed for sure!)
			documents = documents.Where(i => i.Loaded).ToList();
			if (documents.Count > 0) {
				// Sort documents and check for cyclic dependencies.
				if (!TrySortDocumentsInDependencyOrder(documents, out var sortedDocuments)) {
					throw new CyclicDependencyException("Cyclic scenes dependency was detected");
				}
				// Integrate dependencies into the current document.
				foreach (var d in sortedDocuments.Except(new[] { this })) {
					ReplaceExternalContentWith(d);
				}
				RefreshSceneTree();
			}
			dependenciesRefreshTimestamp = DateTime.Now;
		}

		private static bool TrySortDocumentsInDependencyOrder(IEnumerable<Document> documents, out List<Document> sortedDocuments)
		{
			var dependencyGraph = new HashSet<(Document, Document)>();
			foreach (var m in documents) {
				var dependencies = m.RootNodeUnwrapped.Descendants
					.Where(i => !string.IsNullOrEmpty(i.ContentsPath))
					.Select(i => i.ContentsPath).ToHashSet();
				foreach (var d in documents.Where(i => dependencies.Contains(i.Path))) {
					dependencyGraph.Add((m, d));
				}
			}
			return TopologicalSort(documents.ToHashSet(), dependencyGraph, out sortedDocuments);
		}

		/// <summary>
		/// Topological Sorting (Kahn's algorithm)
		/// Source: https://gist.github.com/Sup3rc4l1fr4g1l1571c3xp14l1d0c10u5/3341dba6a53d7171fe3397d13d00ee3f
		/// </summary>
		private static bool TopologicalSort<T>(HashSet<T> nodes, HashSet<(T, T)> edges, out List<T> sortedNodes)
		{
			edges = edges.ToHashSet();
			sortedNodes = new List<T>();
			// Set of all nodes with no incoming edges
			nodes = nodes.Where(n => edges.All(e => !e.Item2.Equals(n))).ToHashSet();
			while (nodes.Any()) {
				var n = nodes.First();
				nodes.Remove(n);
				sortedNodes.Add(n);
				// For each node m with an edge e from n to m do
				foreach (var e in edges.Where(e => e.Item1.Equals(n)).ToList()) {
					var m = e.Item2;
					edges.Remove(e);
					if (edges.All(me => !me.Item2.Equals(m))) {
						nodes.Add(m);
					}
				}
			}
			return edges.Count == 0;
		}

		private void ReplaceExternalContentWith(Document document)
		{
			foreach (var node in RootNode.Descendants) {
				if (node.ContentsPath == document.Path) {
					var clone = document.RootNode.Clone();
					node.ReplaceContent(clone);
					foreach (var c in node.Descendants) {
						Decorate(c);
					}
					RestoreAnimationStates(document.RootNode, clone);
				}
			}
		}

		private static void RestoreAnimationStates(Node original, Node clone)
		{
			var clonedNodes = new Stack<Node>(new [] { clone });
			var originalNodes = new Stack<Node>(new [] { original });
			while (clonedNodes.Count != 0) {
				var c = clonedNodes.Pop();
				var o = originalNodes.Pop();
				int i = 0;
				foreach (var a in c.Animations) {
					a.Time = o.Animations[i++].Time;
				}
				i = 0;
				foreach (var n in c.Nodes) {
					if (string.IsNullOrEmpty(o.ContentsPath)) {
						clonedNodes.Push(n);
						originalNodes.Push(o.Nodes[i++]);
					}
				}
			}
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
			if (!SceneTree.SelfAndDescendants().Any(i => i.Selected)) {
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
			if (IsModified) {
				// Propagate the document unmodified content to the currently opened documents.
				Load();
				foreach (var d in Project.Current.Documents) {
					d.RefreshExternalScenes();
				}
			}
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
			foreach (var row in Rows) {
				if (row.Selected) {
					yield return row;
				}
			}
		}

		public Row RecentlySelectedSceneItem()
		{
			int c = 0;
			Row result = null;
			foreach (var i in Rows) {
				if (i.SelectionOrder > c) {
					c = i.SelectionOrder;
					result = i;
				}
			}
			return result;
		}

		public IEnumerable<Node> SelectedNodes()
		{
			if (InspectRootNode) {
				yield return RootNode;
				yield break;
			}
			Node prevNode = null;
			foreach (var item in Rows.Where(i => i.Selected).ToList()) {
				var nr = item.Components.Get<NodeRow>();
				if (nr != null) {
					yield return nr.Node;
					prevNode = nr.Node;
				}
				var pr = item.Components.Get<PropertyRow>();
				if (pr != null && pr.Node != prevNode) {
					yield return pr.Node;
					prevNode = pr.Node;
				}
			}
		}

		public IEnumerable<Row> TopLevelSelectedRows()
		{
			foreach (var row in Rows) {
				if (row.Selected) {
					var discardRow = false;
					for (var p = row.Parent; p != null; p = p.Parent) {
						discardRow |= p.Selected;
					}
					if (!discardRow) {
						yield return row;
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

		public void Decorate(Node node)
		{
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

		private void OnContainerChanged(Node oldContainer)
		{
			try {
				if (oldContainer != null) {
					selectedAnimationPerContainer[oldContainer] = SelectedAnimation;
				}
				var animations = new List<Animation>();
				GetAnimations(animations);
				if (Animation.IsLegacy) {
					SelectedAnimation = null;
					return;
				}
				if (animations.Contains(Animation)) {
					return;
				}
				if (selectedAnimationPerContainer.TryGetValue(Container, out var animation) &&
				    animations.Contains(animation)) {
					SelectedAnimation = animation;
					return;
				}
				SelectedAnimation = null;
			} finally {
				BumpSceneTreeVersion();
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

		public void SetCurrentAnimationFrame(Animation animation, int frameIndex, bool stopAnimations = true)
		{
			animationPositioner.SetAnimationFrame(animation, frameIndex, stopAnimations);
			// Bump scene tree version, since some animated properties may affect
			// the presentation of nodes on the timeline (e.g. a node label is grayed if Visible == false)
			BumpSceneTreeVersion();
		}

		public void TogglePreviewAnimation()
		{
			if (PreviewAnimation) {
				PreviewAnimation = false;
				PreviewScene = false;
				Animation.IsRunning = false;
				StopAnimationRecursive(PreviewAnimationContainer);
				if (!CoreUserPreferences.Instance.StopAnimationOnCurrentFrame) {
					SetCurrentAnimationFrame(Animation, PreviewAnimationBegin);
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
				SetCurrentAnimationFrame(Animation, AnimationFrame, stopAnimations: false);
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


		public void ForceAnimationUpdate()
		{
			SetCurrentAnimationFrame(Current.Animation, Current.AnimationFrame);
		}
	}
}
