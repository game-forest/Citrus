using Lime;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public static class ConflictingAnimatorsInfoProvider
	{
		private const string AssetType = ".tan";

		public static IEnumerable<ConflictingAnimatorsInfo> Get(string assetName)
		{
			if (Project.Current == null) yield break;

			assetName ??= string.Empty;
			var used = new HashSet<string>();
			var assets = Project.Current.AssetDatabase
				.Where(asset => asset.Value.Type == AssetType && asset.Value.Path.EndsWith(assetName))
				.Select(asset => asset.Value.Path);

			foreach (var asset in assets) {
				if (used.Contains(asset)) continue;
				used.Add(asset);

				var root = Node.Load(asset);
				var queue = new Queue<Node>(root.Nodes);
				while (queue.Count > 0) {
					var node = queue.Dequeue();
					var conflicts = node.Animators
						.GroupBy(i => i.TargetPropertyPath)
						.Where(i => i.Count() > 1)
						.Select(i => (property: i.Key, animations: new SortedSet<string>(i.Select(j => j.AnimationId ?? "Legacy"))));

					if (conflicts.Any()) {
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
						yield return new ConflictingAnimatorsInfo(
							node.GetType(),
							relativePath,
							documentPath,
							conflicts.Select(i => i.property).ToArray(),
							conflicts.Select(i => i.animations).ToArray(),
							indexForPathCollisions: node.Parent?.Nodes.IndexOf(node)
						);
					}

					var isExternal = !string.IsNullOrEmpty(node.ContentsPath);
					if (isExternal) {
						foreach (var child in Get(node.ContentsPath)) {
							yield return child;
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
