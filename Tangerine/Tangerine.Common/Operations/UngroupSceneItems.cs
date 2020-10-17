using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.Common.Operations
{
	public static class UngroupSceneItems
	{
		public static List<Row> Perform(IEnumerable<Frame> groups)
		{
			var history = Core.Document.Current.History;
			using (history.BeginTransaction()) {
				if (!groups.Any()) {
					throw new InvalidOperationException("Can't ungroup empty node list.");
				}
				var container = groups.First().Parent;
				foreach (var node in groups) {
					if (node.Parent != container) {
						throw new InvalidOperationException("When grouping all nodes must belong to a single parent.");
					}
				}
				UntieWidgetsFromBones.Perform(container.Nodes.OfType<Bone>(), groups);
				var containerItem = Document.Current.GetSceneItemForObject(container);
				if (containerItem.Parent == null && containerItem.Rows.Count == 0) {
					containerItem = Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(container);
				}
				int index = containerItem.Rows.IndexOf(Document.Current.GetSceneItemForObject(groups.First()));
				foreach (var group in groups) {
					UnlinkSceneItem.Perform(Document.Current.GetSceneItemForObject(group));
				}
				var result = new List<Row>();
				foreach (var group in groups) {
					var groupItems = Document.Current.GetSceneItemForObject(group).Rows.ToList();
					var localToParentTransform = group.CalcLocalToParentTransform();
					foreach (var i in groupItems) {
						UnlinkSceneItem.Perform(i);
						LinkSceneItem.Perform(containerItem, index++, i);
						var node = i.GetNode();
						result.Add(i);
						if (node is Widget) {
							GroupSceneItems.TransformPropertyAndKeyframes<Vector2>(node, nameof(Widget.Position), v => localToParentTransform * v);
							GroupSceneItems.TransformPropertyAndKeyframes<Vector2>(node, nameof(Widget.Scale), v => v * group.Scale);
							GroupSceneItems.TransformPropertyAndKeyframes<float>(node, nameof(Widget.Rotation),
								v => v * Mathf.Sign(group.Scale.X * group.Scale.Y) + group.Rotation);
							GroupSceneItems.TransformPropertyAndKeyframes<Color4>(node, nameof(Widget.Color), v => group.Color * v);
						} else if (node is Bone bone) {
							GroupSceneItems.TransformPropertyAndKeyframes<Vector2>(bone, nameof(Bone.Position), v => localToParentTransform * v);
							GroupSceneItems.TransformPropertyAndKeyframes<float>(bone, nameof(Bone.Rotation),
								v => (Matrix32.Rotation(v * Mathf.DegToRad) * localToParentTransform).ToTransform2().Rotation);
							GroupSceneItems.TransformPropertyAndKeyframes<Vector2>(bone, nameof(Bone.RefPosition), v => localToParentTransform * v);
							GroupSceneItems.TransformPropertyAndKeyframes<float>(bone, nameof(Bone.RefRotation),
								v => (Matrix32.Rotation(v * Mathf.DegToRad) * localToParentTransform).ToTransform2().Rotation);
						}
					}
				}
				history.CommitTransaction();
				return result;
			}
		}
	}
}
