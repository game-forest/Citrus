using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Lime;
using Orange;
using Tangerine.Core;
using Tangerine.UI.Widgets.ConflictingAnimators;
using KeyframeColor = Tangerine.Core.PropertyAttributes<Lime.TangerineKeyframeColorAttribute>;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	internal sealed class ConflictFinder
	{
		private const string AssetType = ".tan";

		private readonly HashSet<string> visited = new HashSet<string>();

		private readonly Content content;
		private readonly AssetBundle assetBundle;
		private readonly WorkProgressSetter workProgress;
		private readonly CancellationToken cancellationToken;

		private ConflictFinder(Content content, CancellationToken cancellationToken)
		{
			this.content = content;
			this.cancellationToken = cancellationToken;
			workProgress = new WorkProgressSetter();
			assetBundle = new TangerineAssetBundle(The.Workspace.AssetsDirectory);
		}

		/// <summary>
		/// Performs enumeration of animation conflicts.
		/// </summary>
		/// <remarks>
		/// Modifying scenes during enumeration will cause a race condition and lead to undefined behavior!
		/// </remarks>
		public static (IEnumerable<ConflictInfo> Conflicts, WorkProgress Progress) Enumerate(
			Content content,
			CancellationToken cancellationToken
		) {
			var cf = new ConflictFinder(content, cancellationToken);
			return (cf.EnumerateInitializer(), cf.workProgress);
		}

		private IEnumerable<ConflictInfo> EnumerateInitializer() {
			if (Project.Current == null) {
				yield break;
			}
			bool isException = true;
			AssetBundle.Current = assetBundle;
			var root = content == Content.AssetDatabase ?
				string.Empty : Document.Current.Path;
			try {
				foreach (var info in Enumerate(root)) {
					yield return info;
				}
				isException = false;
			} finally {
				if (cancellationToken.IsCancellationRequested) {
					workProgress.MarkAsCancelled();
				} else if (isException) {
					workProgress.MarkAsException();
				} else {
					workProgress.MarkAsCompleted();
				}
			}
		}

		private IEnumerable<ConflictInfo> Enumerate(string name)
		{
			bool isLoadExternals = content == Content.CurrentDocument;
			var scenes = EnumerateScenes(e => e.Type == AssetType && e.Path.EndsWith(name));
			foreach (var scenePath in scenes) {
				cancellationToken.ThrowIfCancellationRequested();
				workProgress.SetCurrentFile(scenePath);
				if (visited.Contains(scenePath)) {
					continue;
				}
				visited.Add(scenePath);

				Node root;
				try {
					root = Node.Load(
						scenePath,
						instance: null,
						ignoreExternals: !isLoadExternals
					);
				} catch (System.Exception exception) {
					Console.WriteLine(exception);
					continue;
				}

				var queue = new Queue<Node>(root.Nodes);
				while (queue.Count > 0) {
					cancellationToken.ThrowIfCancellationRequested();
					var node = queue.Dequeue();
					var conflicts = GetConflicts(node);
					if (conflicts.Any()) {
						var count = conflicts.Count;
						var properties = new ConflictInfo.Property[count];
						var animations = new SortedSet<string>[count];
						for (var i = 0; i < count; ++i) {
							var (property, animable, sortedSet) = conflicts[i];
							animations[i] = sortedSet;
							properties[i] = new ConflictInfo.Property {
								Path = property,
								KeyframeColorIndex = KeyframeColor.Get(
									type: animable.GetType(),
									property: property
								)?.ColorIndex ?? 0,
							};
						}
						workProgress.IncrementConflictCount();
						yield return new ConflictInfo(
							nodeType: node.GetType(),
							documentPath: scenePath,
							relativeTextPath: GetRelativeTextPath(node, root),
							relativeIndexedPath: GetRelativeIndexedPath(node, root),
							affectedProperties: properties,
							concurrentAnimations: animations
						);
					}
					if (isLoadExternals && IsExternalScene(node)) {
						foreach (var child in Enumerate(node.ContentsPath)) {
							cancellationToken.ThrowIfCancellationRequested();
							yield return child;
						}
						workProgress.SetCurrentFile(scenePath);
					} else {
						foreach (var child in node.Nodes) {
							queue.Enqueue(child);
						}
					}
				}
			}

			bool IsExternalScene(Node node) => !string.IsNullOrEmpty(node.ContentsPath);
		}

		private IEnumerable<string> EnumerateScenes(Predicate<AssetDatabase.Entry> predicate) =>
			Project.Current.AssetDatabase
				   .Select(kv => kv.Value)
				   .Where(a => predicate(a))
				   .Select(a => a.Path);

		private ImmutableList<(
			string property,
			IAnimable animable,
			SortedSet<string> animations
		)> GetConflicts(Node node)
		{
			return node.Animators
				.GroupBy(i => i.TargetPropertyPath)
				.Where(i => i.Count(a => a.AnimationId != Animation.ZeroPoseId) > 1)
				.Select(i => (
					property: i.Key,
					animable: i.First().Animable,
					animations: new SortedSet<string>(i.Select(j => j.AnimationId ?? "Legacy")
				))).ToImmutableList();
		}

		private static string GetRelativeTextPath(Node node, Node root)
		{
			var relativePathStack = new Stack<string>();
			relativePathStack.Push(IdOrTypeName(node));
			foreach (var ancestor in node.Ancestors) {
				relativePathStack.Push(IdOrTypeName(ancestor));
				if (ancestor == root) {
					break;
				}
			}
			return string.Join("/", relativePathStack);

			string IdOrTypeName(Node n) => string.IsNullOrWhiteSpace(n.Id) ? n.GetType().Name : n.Id;
		}

		private static List<int> GetRelativeIndexedPath(Node node, Node root)
		{
			var indexedPath = new List<int>();
			indexedPath.Add(IndexInParent(node));
			foreach (var ancestor in node.Ancestors) {
				indexedPath.Add(IndexInParent(ancestor));
				if (ancestor == root) {
					break;
				}
			}
			indexedPath.RemoveAt(indexedPath.Count - 1);
			return indexedPath;

			int IndexInParent(Node n) => n.Parent?.Nodes.IndexOf(n) ?? -1;
		}

		public enum Content
		{
			AssetDatabase,
			CurrentDocument,
		}

		public class WorkProgress
		{
			protected volatile bool isCompleted;
			protected volatile bool isCancelled;
			protected volatile bool isException;
			protected volatile string currentFile;
			protected volatile int currentConflictCount;

			public bool IsCompleted => isCompleted;
			public bool IsCancelled => isCancelled;
			public bool IsException => isException;
			public string CurrentFile => currentFile;
			public int CurrentConflictCount => currentConflictCount;

			public static WorkProgress Done => new WorkProgress { isCompleted = true };

			protected WorkProgress()
			{ }
		}

		private class WorkProgressSetter : WorkProgress
		{
			public void MarkAsCompleted() => isCompleted = true;
			public void MarkAsCancelled() => isCompleted = isCancelled = true;
			public void MarkAsException() => isCompleted = isException = true;
			public void SetCurrentFile(string path) => currentFile = path;
			public void IncrementConflictCount() => Interlocked.Increment(ref currentConflictCount);
		}
	}
}
