using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Orange
{
	internal class SyncScenes : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncScenes(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			return assetCooker.InputBundle.EnumerateFiles(null, ".tan")
				.Select(path => {
					var hash = assetCooker.InputBundle.ComputeCookingUnitHash(
						path, assetCooker.CookingRulesMap[path]
					);
					return (path, hash);
				});
		}

		public void Cook(string scenePath, SHA256 cookingUnitHash)
		{
			var node = InternalPersistence.Instance.ReadFromBundle<Node>(assetCooker.InputBundle, scenePath);
			DeleteNodesAndComponentsWithRemoveOnAssetCookAttribute(node);
			InternalPersistence.Instance.WriteToBundle(
				bundle: assetCooker.OutputBundle,
				path: scenePath,
				@object: node,
				format: Persistence.Format.Binary,
				cookingUnitHash: cookingUnitHash,
				attributes: AssetAttributes.None
			);
		}

		private static void DeleteNodesAndComponentsWithRemoveOnAssetCookAttribute(Node scene)
		{
			if (HasTheAttribute(scene)) {
				throw new InvalidOperationException("Can't remove root node.");
			}
			foreach (var n in scene.SelfAndDescendants.ToList()) {
				if (HasTheAttribute(n) && n.Parent != null) {
					n.UnlinkAndDispose();
				} else {
					foreach (var component in n.Components.ToList()) {
						if (HasTheAttribute(component) && component.Owner?.Parent != null) {
							n.Components.Remove(component);
							component.Dispose();
						}
					}
				}
			}
			static bool HasTheAttribute(object @object)
			{
				return @object.GetType().IsDefined(typeof(RemoveOnAssetCookAttribute), true);
			}
		}
	}
}
