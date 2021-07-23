using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core.Operations
{
	public static class NodeTypeConvert
	{
		public static Node Perform(Row sceneItem, Type destType, Type commonParent, ICollection<string> excludedProperties)
		{
			var node = sceneItem.Components.Get<NodeRow>()?.Node;
			DelegateOperation.Perform(null,Document.Current.RefreshSceneTree, false);
			Validate(node, destType, commonParent);
			var result = CreateNode.Perform(sceneItem.Parent, sceneItem.Parent.Rows.IndexOf(sceneItem), destType);
			CopyProperties(node, result, excludedProperties);
			var assetBundlePathComponent = node.Components.Get<Node.AssetBundlePathComponent>();
			if (assetBundlePathComponent != null) {
				result.Components.Add(Cloner.Clone(assetBundlePathComponent));
			} else {
				int j = 0;
				var resultSceneItem = Document.Current.GetSceneItemForObject(result);
				foreach (var i in sceneItem.Rows.ToList()) {
					if (i.TryGetNode(out _) || i.TryGetFolder(out _)) {
						UnlinkSceneItem.Perform(i);
						LinkSceneItem.Perform(resultSceneItem, j, i);
						j = resultSceneItem.Rows.IndexOf(i) + 1;
					}
				}
			}
			UnlinkSceneItem.Perform(sceneItem);
			DelegateOperation.Perform(Document.Current.RefreshSceneTree, null, false);
			return result;
		}

		private static void CopyProperties(Node from, Node to, ICollection<string> excludedProperties)
		{
			var sourceProperties =
				from.GetType().GetProperties()
				.Where(prop =>
					prop.IsDefined(typeof(Yuzu.YuzuMember), true) &&
					prop.CanWrite &&
					!excludedProperties.Contains(prop.Name)
				);
			var destinationProperties =
				to.GetType().GetProperties()
				.Where(prop => prop.IsDefined(typeof(Yuzu.YuzuMember), true) && prop.CanRead);
			var pairs =
				sourceProperties.
				Join(
					destinationProperties,
					prop => prop.Name,
					prop => prop.Name,
					(sourceProp, destProp) => new { SourceProp = sourceProp, DestProp = destProp }
				);
			foreach (var pair in pairs) {
				if (pair.SourceProp.PropertyType == pair.DestProp.PropertyType) {
					pair.DestProp.SetValue(to, pair.SourceProp.GetValue(from));
				}
			}
			foreach (var animator in from.Animators) {
				to.Animators.Add(Cloner.Clone(animator));
			}
		}

		private static void Validate(Node source, Type destType, Type commonParent)
		{
			foreach (var child in source.Nodes) {
				if (!NodeCompositionValidator.Validate(destType, child.GetType())) {
					throw new InvalidOperationException(
						$"Node {source} has child {child} that will be incompatible with {destType}"
					);
				}
			}
			foreach (var component in source.Components) {
				if (!NodeCompositionValidator.ValidateComponentType(destType, component.GetType())) {
					throw new InvalidOperationException(
						$"Node {source} has component {component} that will be incompatible with {destType}"
					);
				}
			}
			if (!(source.GetType().IsSubclassOf(commonParent) && destType.IsSubclassOf(commonParent))) {
				throw new InvalidOperationException(
					$"Node {source} type or/and destination {destType} type are not subclasses of {commonParent}"
				);
			}
			foreach (var animator in source.Animators) {
				var prop = destType.GetProperty(animator.TargetPropertyPath);
				if (
					prop == null ||
					prop.PropertyType != source.GetType().GetProperty(animator.TargetPropertyPath).PropertyType
				) {
					throw new InvalidOperationException(
						$"Node {source} has animator on property {animator.TargetPropertyPath}, which doesn't exist in {destType}"
					);
				}
			}
		}
	}
}
