using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lime;
using Tangerine.Core.Components;

namespace Tangerine.Core.Operations
{
	public static class NodeTypeConvert
	{
		public static Node Perform(Row sceneItem, Type destType, Type commonParent)
		{
			var node = sceneItem.Components.Get<NodeRow>()?.Node;
			DelegateOperation.Perform(null,Document.Current.RefreshSceneTree, false);
			Validate(node, destType, commonParent);
			var result = CreateNode.Perform(sceneItem.Parent, sceneItem.Parent.Rows.IndexOf(sceneItem), destType);
			CopyProperties(node, result);
			var assetBundlePathComponent = node.Components.Get<Node.AssetBundlePathComponent>();
			if (assetBundlePathComponent != null) {
				result.Components.Add(Cloner.Clone(assetBundlePathComponent));
			} else {
				int j = 0;
				var resultSceneItem = Document.Current.GetSceneItemForObject(result);
				foreach (var i in sceneItem.Rows.ToList()) {
					var isNode = i.TryGetNode(out var childNode);
					if (isNode || i.TryGetFolder(out _)) {
						UnlinkSceneItem.Perform(i);
						if (isNode) {
							if (!NodeCompositionValidator.Validate(destType, childNode.GetType())) {
								Console.WriteLine($"[Warning] Skipping child `{childNode}` of Node `{node}` " +
									$"because it is incompatible with `{destType}`."
								);
								continue;
							}
						}
						LinkSceneItem.Perform(resultSceneItem, j, i);
						j = resultSceneItem.Rows.IndexOf(i) + 1;
					}
				}
			}
			UnlinkSceneItem.Perform(sceneItem);
			DelegateOperation.Perform(Document.Current.RefreshSceneTree, null, false);
			return result;
		}

		private static void CopyProperties(Node from, Node to)
		{
			var excludedProperties = new HashSet<string> {
				nameof(Node.Parent),
				nameof(Node.Nodes),
				nameof(Node.Animations),
				nameof(Node.Animators)
			};
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
			var destType = to.GetType();
			foreach (var animator in from.Animators) {
				var prop = destType.GetProperty(animator.TargetPropertyPath);
				if (
					prop == null ||
					prop.PropertyType != from.GetType().GetProperty(animator.TargetPropertyPath).PropertyType
				) {
					Console.WriteLine($"[Warning] Node {from} has animator on property " +
						$"{animator.TargetPropertyPath}, which doesn't exist in {destType}, skipping.");
					continue;
				}
				to.Animators.Add(Cloner.Clone(animator));
			}
			foreach (var component in from.Components) {
				if  (!NodeCompositionValidator.ValidateComponentType(destType, component.GetType())) {
					Console.WriteLine($"[Warning] Node {from} has component {component} " +
						$"that will be incompatible with {destType}, skipping.");
					continue;
				}
				if (component.GetType().GetCustomAttribute<NodeComponentDontSerializeAttribute>(true) != null) {
					continue;
				}
				to.Components.Add(Cloner.Clone(component));
			}
			foreach (var animation in from.Animations) {
				if (string.IsNullOrEmpty(animation.Id)) {
					if (!to.DefaultAnimation.Markers.Any()) {
						foreach (var m in animation.Markers) {
							to.DefaultAnimation.Markers.Add(m.Clone());
						}
					} else {
						foreach (var m in animation.Markers) {
							Console.WriteLine($"[Warning] Removing marker '{m.Id}', frame: {m.Frame}, type: {m.Action}");
						}
					}
					continue;
				}
				to.Animations.Add(Cloner.Clone(animation));
			}
		}

		private static void Validate(Node source, Type destType, Type commonParent)
		{
			if (!(source.GetType().IsSubclassOf(commonParent) && destType.IsSubclassOf(commonParent))) {
				throw new InvalidOperationException(
					$"Node {source} type or/and destination {destType} type are not subclasses of {commonParent}"
				);
			}
		}
	}
}
