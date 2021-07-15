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

			var used = new HashSet<string>();
			var assets = Project.Current.AssetDatabase
				.Where(asset => asset.Value.Type == AssetType && asset.Value.Path.EndsWith(assetName ?? string.Empty))
				.Select(asset => asset.Value.Path);
			foreach (var asset in assets) {
				if (used.Contains(asset)) continue;

				used.Add(asset);
				var root = Node.Load(asset);
				var queue = new Queue<(Node node, string document)>();
				foreach (var node in root.Nodes) {
					queue.Enqueue((node, asset));
				}
				while (queue.Count > 0) {
					var (node, document) = queue.Dequeue();
					var properties = new Dictionary<string, HashSet<string>>();
					foreach (var animator in node.Animators) {
						var id = animator.AnimationId;
						var property = animator.TargetPropertyPath;
						if (!properties.TryGetValue(animator.TargetPropertyPath, out var hash)) {
							properties[property] = new HashSet<string>();
						}
						properties[property].Add(id ?? "Legacy");
					}
					var conflictingProperties = new List<string>();
					var conflictingAnimations = new List<HashSet<string>>();
					foreach (var (property, animations) in properties) {
						if (animations.Count > 1) {
							conflictingProperties.Add(property);
							conflictingAnimations.Add(animations);
						}
					}
					if (conflictingProperties.Any()) {
						yield return new ConflictingAnimatorsInfo(
							node,
							document,
							conflictingProperties.ToArray(),
							conflictingAnimations.ToArray()
						);
					}
					used.Add(node.ContentsPath);
					var isExternal = !string.IsNullOrEmpty(node.ContentsPath);
					foreach (var child in node.Nodes) {
						queue.Enqueue((child, isExternal ? node.ContentsPath : document));
					}
				}
			}
		}
	}
}
