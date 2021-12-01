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
		public static void Perform(SceneItem sceneItem, Type destType, Type commonParent)
		{
			var node = sceneItem.Components.Get<NodeSceneItem>()?.Node;
			if (!string.IsNullOrEmpty(node.ContentsPath)) {
				Console.WriteLine(
					$"[Warning] Skipping conversion: converting nodes with non empty contents path is not supported."
				);
				return;
			}
			if (!NodeCompositionValidator.Validate(node.Parent.GetType(), destType)) {
				Console.WriteLine(
					$"[Warning] Skipping conversion: parent node of type `{node.Parent.GetType().FullName}` "
					+ $"can't contain node of type `{destType.FullName}`"
				);
				return;
			}
			DelegateOperation.Perform(null,Document.Current.RefreshSceneTree, false);
			Validate(node, destType, commonParent);
			var result = CreateNode.Perform(
				sceneItem.Parent,
				new SceneTreeIndex(sceneItem.Parent.SceneItems.IndexOf(sceneItem)),
				destType
			);
			var assetBundlePathComponent = node.Components.Get<Node.AssetBundlePathComponent>();
			if (assetBundlePathComponent != null) {
				result.Components.Add(Cloner.Clone(assetBundlePathComponent));
			} else {
				int j = 0;
				var resultSceneItem = Document.Current.GetSceneItemForObject(result);
				foreach (var i in sceneItem.SceneItems.ToList()) {
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
						LinkSceneItem.Perform(resultSceneItem, new SceneTreeIndex(j), i);
						j = resultSceneItem.SceneItems.IndexOf(i) + 1;
					}
				}
			}
			CopyProperties(node, result);
			UnlinkSceneItem.Perform(sceneItem);
			DelegateOperation.Perform(Document.Current.RefreshSceneTree, null, false);
		}

		private static void CopyProperties(Node from, Node to)
		{
			var excludedProperties = new HashSet<string> {
				nameof(Node.Parent),
				nameof(Node.Nodes),
				nameof(Node.Animations),
				nameof(Node.Animators)
			};
			if (!(from is Animesh | from is DistortionMesh) && (to is Animesh | to is DistortionMesh)) {
				excludedProperties.Add(nameof(Widget.SkinningWeights));
			}
			var sourceProperties = from
				.GetType()
				.GetProperties()
				.Where(prop =>
					prop.IsDefined(typeof(Yuzu.YuzuMember), true) &&
					prop.CanWrite &&
					!excludedProperties.Contains(prop.Name)
				);
			var destinationProperties = to
				.GetType()
				.GetProperties()
				.Where(prop => prop.IsDefined(typeof(Yuzu.YuzuMember), true) && prop.CanRead);
			var pairs = sourceProperties
				.Join(
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
			if (excludedProperties.Contains(nameof(Widget.SkinningWeights))) {
				var sw = from.AsWidget.SkinningWeights;
				var bones = Enumerable.Range(0, 4)
					.Where(i => sw[i].Index > 0)
					.Select(i => from.Parent.Nodes.GetBone(sw[i].Index));
				var widgets = new [] { to.AsWidget };
				UntieWidgetsFromBones.Perform(bones, widgets);
				TieWidgetsWithBones.Perform(bones, widgets);
			}
			var destType = to.GetType();
			foreach (var component in from.Components) {
				if (!NodeCompositionValidator.ValidateComponentType(destType, component.GetType())) {
					Console.WriteLine($"[Warning] Node {from} has component {component} " +
						$"that will be incompatible with {destType}, skipping.");
					continue;
				}
				if (component.GetType().GetCustomAttribute<NodeComponentDontSerializeAttribute>(true) != null) {
					continue;
				}
				to.Components.Add(Cloner.Clone(component));
			}
			foreach (var animator in from.Animators) {
				var (propertyDataOfSourceNode, animableOfSourceNode, _) =
					AnimationUtils.GetPropertyByPath(from, animator.TargetPropertyPath);
				var (propertyDataOfTargetNode, animableOfTargetNode, _) =
					AnimationUtils.GetPropertyByPath(to, animator.TargetPropertyPath);
				if (
					animableOfTargetNode == null ||
					propertyDataOfTargetNode.Info.PropertyType != propertyDataOfSourceNode.Info.PropertyType
				) {
					Console.WriteLine($"[Warning] Node {from} has animator on property " +
						$"{animator.TargetPropertyPath}, which doesn't exist in {destType}, skipping.");
					continue;
				}
				to.Animators.Add(Cloner.Clone(animator));
			}
			if (NodeCompositionValidator.CanHaveChildren(destType)) {
				foreach (var animation in from.Animations) {
					if (string.IsNullOrEmpty(animation.Id)) {
						if (!to.DefaultAnimation.Markers.Any()) {
							foreach (var m in animation.Markers) {
								to.DefaultAnimation.Markers.Add(m.Clone());
							}
						} else {
							foreach (var m in animation.Markers) {
								Console.WriteLine(
									$"[Warning] Removing marker '{m.Id}', frame: {m.Frame}, type: {m.Action}"
								);
							}
						}
						continue;
					}
					to.Animations.Add(Cloner.Clone(animation));
				}
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
