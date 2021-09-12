using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Widgets.ConflictingAnimators;
using KeyframeColor = Tangerine.Core.PropertyAttributes<Lime.TangerineKeyframeColorAttribute>;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public sealed class ConflictInfoProvider : IDisposable
	{
		[Flags]
		public enum SearchFlags
		{
			None = 0,

			Global   = 1 << 0,
			External = 1 << 1,

			All = ~None,
		}

		private const string AssetType = ".tan";

		private readonly HashSet<string> visited = new HashSet<string>();
		private SearchFlags searchFlags;
		private CancellationToken? cancellationToken;

		public void Invalidate()
		{
			visited.Clear();
			searchFlags = SearchFlags.None;
			cancellationToken = null;
		}

		public IEnumerable<ConflictInfo> Enumerate(SearchFlags searchFlags, CancellationToken? cancellationToken = null)
		{
			if (Project.Current == null) {
				yield break;
			}

			this.searchFlags = searchFlags;
			this.cancellationToken = cancellationToken;
			var root = this.searchFlags.Contains(SearchFlags.Global) ? string.Empty : Document.Current.Path;

			foreach (var info in Enumerate(root)) {
				yield return info;
			}
		}

		private IEnumerable<ConflictInfo> Enumerate(string name)
		{
			var scenes = EnumerateScenes(e => e.Type == AssetType && e.Path.EndsWith(name));
			foreach (var scene in scenes) {
				cancellationToken?.ThrowIfCancellationRequested();
				if (visited.Contains(scene)) {
					continue;
				}
				visited.Add(scene);

				Node root;
				try {
					root = Node.Load(scene);
				} catch (System.Exception exception) {
					Console.WriteLine(exception);
					continue;
				}

				var queue = new Queue<Node>(root.Nodes);
				while (queue.Count > 0) {
					cancellationToken?.ThrowIfCancellationRequested();
					var node = queue.Dequeue();
					var documentPath = scene;
					var relativePath = GetRelativePath(node, ref documentPath);
					var tag = $"{relativePath} ({documentPath})";
					if (visited.Contains(tag)) continue;
					visited.Add(tag);

					var conflicts = GetConflicts(node);
					if (conflicts.Any()) {
						var count = conflicts.Count;
						var props = new string[count];
						var anims = new SortedSet<string>[count];
						var indices = new int[count];
						for (var i = 0; i < count; ++i) {
							props[i] = conflicts[i].property;
							anims[i] = conflicts[i].animations;
							indices[i] = KeyframeColor.Get(conflicts[i].animable.GetType(), props[i])?.ColorIndex ?? 0;
						}
						yield return new ConflictInfo(
							node.GetType(),
							relativePath,
							documentPath,
							props,
							anims,
							indices,
							node.Parent?.Nodes.IndexOf(node)
						);
					}

					if (!string.IsNullOrEmpty(node.ContentsPath)) {
						if (searchFlags.Contains(SearchFlags.External)) {
							foreach (var child in Enumerate(node.ContentsPath)) {
								cancellationToken?.ThrowIfCancellationRequested();
								yield return child;
							}
						}
					} else {
						foreach (var child in node.Nodes) {
							queue.Enqueue(child);
						}
					}
				}
			}
		}

		private IEnumerable<string> EnumerateScenes(Predicate<AssetDatabase.Entry> predicate) =>
			Project.Current.AssetDatabase
				   .Select(kv => kv.Value)
				   .Where(a => predicate(a))
				   .Select(a => a.Path);

		private ImmutableList<(string property, IAnimable animable, SortedSet<string> animations)> GetConflicts(Node node) =>
			node.Animators
			    .GroupBy(i => i.TargetPropertyPath)
			    .Where(i => i.Count() > 1)
			    .Select(i => (
		            property: i.Key,
		            animable: i.First().Animable,
		            animations: new SortedSet<string>(i.Select(j => j.AnimationId ?? "Legacy")
		        ))).ToImmutableList();

		private string GetRelativePath(Node node, ref string documentPath)
		{
			var relativePathStack = new Stack<string>();
			relativePathStack.Push(node.Id);
			foreach (var ancestor in node.Ancestors) {
				if (ancestor.Components.TryGet<Node.AssetBundlePathComponent>(out var pathComponent)) {
					documentPath = pathComponent.Path.Replace(AssetType, string.Empty);
					break;
				}
				relativePathStack.Push(ancestor.Id);
			}
			return string.Join("/", relativePathStack);
		}

		public void Dispose() => Invalidate();
	}
}
