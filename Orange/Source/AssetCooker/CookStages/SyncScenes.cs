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
			var node = InternalPersistence.Instance.ReadObjectFromBundle<Node>(assetCooker.InputBundle, scenePath);
			DeleteNodesAndComponentsWithRemoveOnAssetCookAttribute(node);
			InternalPersistence.Instance.WriteObjectToBundle(
				bundle: assetCooker.OutputBundle,
				path: scenePath,
				instance: node,
				format: Persistence.Format.Binary,
				cookingUnitHash: cookingUnitHash,
				attributes: AssetAttributes.None
			);
		}

		private static void DeleteNodesAndComponentsWithRemoveOnAssetCookAttribute(Node scene)
		{
			foreach (var component in scene.Components.ToList()) {
				if (HasTheAttribute(component)) {
					scene.Components.Remove(component);
				}
			}
			foreach (var node in scene.Nodes.ToList()) {
				if (HasTheAttribute(node)) {
					scene.Nodes.Remove(node);
				} else {
					DeleteNodesAndComponentsWithRemoveOnAssetCookAttribute(node);
				}
			}
			static bool HasTheAttribute(object @object) =>
				@object.GetType().IsDefined(typeof(RemoveOnAssetCookAttribute), true);
		}
	}
}
