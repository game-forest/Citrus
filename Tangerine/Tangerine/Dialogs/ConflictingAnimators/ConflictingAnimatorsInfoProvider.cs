using Lime;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.UI;
using KeyframeColor = Tangerine.Core.PropertyAttributes<Lime.TangerineKeyframeColorAttribute>;

namespace Tangerine
{
	public static class ConflictingAnimatorsInfoProvider
	{
		private const string AssetType = ".tan";

		private static HashSet<string> visited;

		public static void Invalidate() => visited = null;

		public static IEnumerable<ConflictingAnimatorsInfo> Get(string assetName = null, bool shouldTraverseExternalScenes = true)
		{
			if (Project.Current == null) yield break;

			visited ??= new HashSet<string>();
			assetName ??= string.Empty;
			var assets = Project.Current.AssetDatabase
				.Where(asset => asset.Value.Type == AssetType && asset.Value.Path.EndsWith(assetName))
				.Select(asset => asset.Value.Path);

			foreach (var asset in assets) {
				if (visited.Contains(asset)) continue;
				visited.Add(asset);

				Node root;
				try {
					root = Node.Load(asset);
				} catch {
					continue;
				}

				var queue = new Queue<Node>(root.Nodes);
				while (queue.Count > 0) {
					var node = queue.Dequeue();
					var documentPath = asset;
					var relativePathStack = new Stack<string>();
					relativePathStack.Push(node.Id);
					foreach (var ancestor in node.Ancestors) {
						if (ancestor.Components.TryGet<Node.AssetBundlePathComponent>(out var pathComponent)) {
							documentPath = pathComponent.Path.Replace(AssetType, string.Empty);
							break;
						}
						relativePathStack.Push(ancestor.Id);
					}
					var relativePath = string.Join("/", relativePathStack);
					var tag = $"{relativePath} ({documentPath})";
					if (visited.Contains(tag)) continue;
					visited.Add(tag);

					var conflicts = node.Animators
						.GroupBy(i => i.TargetPropertyPath)
						.Where(i => i.Count() > 1)
						.Select(i => (
							property: i.Key,
							animable: i.First().Animable,
							animations: new SortedSet<string>(i.Select(j => j.AnimationId ?? "Legacy")
						)));

					if (conflicts.Any()) {
						var nodeType = node.GetType();
						var targetProperties = conflicts.Select(i => i.property).ToArray();
						var concurrentAnimations = conflicts.Select(i => i.animations).ToArray();
						var propertyKeyframeColorIndices = conflicts.Select(i =>
							KeyframeColor.Get(i.animable.GetType(), i.property)?.ColorIndex ?? 0
						).ToArray();
						yield return new ConflictingAnimatorsInfo(
							nodeType,
							relativePath,
							documentPath,
							targetProperties,
							concurrentAnimations,
							propertyKeyframeColorIndices,
							nodeIndexForPathCollisions: node.Parent?.Nodes.IndexOf(node)
						);
					}

					var isExternal = !string.IsNullOrEmpty(node.ContentsPath);
					if (isExternal) {
						if (shouldTraverseExternalScenes) {
							foreach (var child in Get(node.ContentsPath)) {
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
	}
}
