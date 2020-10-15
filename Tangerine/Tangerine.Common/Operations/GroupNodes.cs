using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine.Common.Operations
{
	public static class GroupNodes
	{
		private const string DefaultAnimationId = "<DefaultAnimationId>";

		public static Node Perform(List<Node> nodes)
		{
			var history = Core.Document.Current.History;
			using (history.BeginTransaction()) {
				if (!nodes.Any()) {
					throw new InvalidOperationException("Can't group empty node list.");
				}
				var container = nodes[0].Parent;
				foreach (var node in nodes) {
					if (node.Parent != container) {
						throw new InvalidOperationException("When grouping all nodes must belong to a single parent.");
					}
				}
				if (!Utils.CalcHullAndPivot(nodes, out var hull, out _)) {
					throw new InvalidOperationException("Can't calc hull and pivot when grouping nodes.");
				}
				hull = hull.Transform(container.AsWidget.LocalToWorldTransform.CalcInversed());
				var aabb = hull.ToAABB();
				var containerSceneTree = Document.Current.GetSceneItemForObject(container);
				if (containerSceneTree.Parent == null && containerSceneTree.Rows.Count == 0) {
					Document.Current.SceneTreeBuilder.BuildSceneTreeForNode(container);
				}
				foreach (var sceneItem in containerSceneTree.Rows.Where(si => si.TryGetNode(out var n) && nodes.Contains(n))) {
					var br = sceneItem.Components.Get<BoneRow>();
					if (br != null) {
						if (!sceneItem.Expanded) {
							nodes.AddRange(BoneUtils.FindBoneDescendats(br.Bone, container.Nodes.OfType<Bone>()));
						}
					}
				}
				var selectedBones = nodes.OfType<Bone>().ToList();
				var firstItem = Document.Current.GetSceneItemForObject(nodes[0]);
				var group = (Frame)Core.Operations.CreateNode.Perform(
					firstItem.Parent,
					firstItem.Parent.Rows.IndexOf(firstItem),
					typeof(Frame));
				group.Id = nodes[0].Id + "Group";
				group.Pivot = Vector2.Half;
				group.Position = aabb.Center;
				group.Size = aabb.Size;
				var bonesExceptSelected = container.Nodes.Except(nodes).OfType<Bone>().ToList();
				UntieWidgetsFromBones.Perform(bonesExceptSelected, nodes.OfType<Widget>());
				UntieWidgetsFromBones.Perform(selectedBones, container.Nodes.Except(nodes).OfType<Widget>());
				var nodeKeyframesDict = new Dictionary<Node, BoneAnimationData>();
				var localRoots = new List<Bone>();
				var sortedBones = container.Nodes.OfType<Bone>().ToList();
				BoneUtils.SortBones(sortedBones);
				foreach (var bone in sortedBones) {
					Bone localRoot;
					var delta = Vector2.Zero;
					var isSelectedBone = selectedBones.Contains(bone);
					if (isSelectedBone) {
						localRoot = BoneUtils.FindBoneRoot(bone, nodes);
						delta = -aabb.A;
					}
					else {
						localRoot = BoneUtils.FindBoneRoot(bone, bonesExceptSelected);
					}
					if (!localRoots.Contains(localRoot)) {
						if (!isSelectedBone && localRoot.BaseIndex == 0) {
							localRoots.Add(localRoot);
							continue;
						}
						nodeKeyframesDict.Add(localRoot, EvaluateBoneAnimationUsingParent(localRoot, v => v + delta));
						localRoots.Add(localRoot);
					}
				}
				SetKeyframes(nodeKeyframesDict);
				foreach (var node in nodes) {
					if (node.Parent != null) {
						UnlinkSceneItem.Perform(Document.Current.GetSceneItemForObject(node));
					}
				}
				int i = 0;
				foreach (var node in nodes) {
					if (node.Parent != null) {
						continue;
					}
					LinkSceneItem.Perform(
						Document.Current.GetSceneItemForObject(group), i++, Document.Current.GetSceneItemForObject(node));
					if (node is Widget) {
						TransformPropertyAndKeyframes<Vector2>(node, nameof(Widget.Position), v => v - aabb.A);
					}
					if (node is Bone) {
						TransformPropertyAndKeyframes<Vector2>(node, nameof(Bone.RefPosition), v => v - aabb.A);
					}
				}
				group.DefaultAnimation.Frame = container.DefaultAnimation.Frame;
				history.CommitTransaction();
				return group;
			}

		}

		private static void SetKeyframes(Dictionary<Node, BoneAnimationData> keyframeDictionary)
		{
			foreach (var pair in keyframeDictionary) {
				if (pair.Value.NoParentKeyframes) {
					TransformPropertyAndKeyframes(pair.Key, nameof(Bone.Position), pair.Value.PositionTransformer);
				} else {
					SetProperty.Perform(pair.Key, nameof(Bone.Position), pair.Value.CurrentPosition);
					SetProperty.Perform(pair.Key, nameof(Bone.Rotation), pair.Value.CurrentRotation);
					foreach (var keyframe in pair.Value.PositionKeyframes) {
						SetKeyframe.Perform(pair.Key, nameof(Bone.Position), Document.Current.AnimationId, keyframe.Value);
					}
					foreach (var keyframe in pair.Value.RotationKeyframes) {
						SetKeyframe.Perform(pair.Key, nameof(Bone.Rotation), Document.Current.AnimationId, keyframe.Value);
					}
					SetAnimableProperty.Perform(pair.Key, nameof(Bone.BaseIndex), 0);
				}
			}
		}

		public static void TransformPropertyAndKeyframes<T>(Node node, string propertyId, Func<T, T> transformer)
		{
			var value = new Property<T>(node, propertyId).Value;
			SetProperty.Perform(node, propertyId, transformer(value));
			foreach (var animation in node.Animators) {
				if (animation.TargetPropertyPath == propertyId) {
					foreach (var keyframe in animation.Keys.ToList()) {
						var newKeyframe = keyframe.Clone();
						newKeyframe.Value = transformer((T)newKeyframe.Value);
						SetKeyframe.Perform(node, animation.TargetPropertyPath, animation.AnimationId, newKeyframe);
					}
				}
			}
		}

		private static BoneAnimationData EvaluateBoneAnimationUsingParent(Bone node, Func<Vector2, Vector2> positionTransformer)
		{
			var boneChain = new List<Bone>();
			var parentNode = node.Parent.AsWidget;
			var parentBone = node;
			while (parentBone != null) {
				parentBone = parentNode.Nodes.GetBone(parentBone.BaseIndex);
				if (parentBone != null) {
					boneChain.Insert(0, parentBone);
				}
			};
			var data = new BoneAnimationData();
			var framesDict = new Dictionary<string, SortedSet<int>>();
			foreach (var bone in boneChain) {
				foreach (var a in bone.Animators) {
					if (a.TargetPropertyPath == nameof(Bone.Position) ||
						a.TargetPropertyPath == nameof(Bone.Length) ||
						a.TargetPropertyPath == nameof(Bone.Rotation)
					) {
						var id = a.AnimationId ?? DefaultAnimationId;
						if (!framesDict.ContainsKey(id)) {
							framesDict[id] = new SortedSet<int>();
						}
						foreach (var k in a.Keys.ToList()) {
							framesDict[id].Add(k.Frame);
						}
					}
				}
			}
			data.CurrentPosition = positionTransformer(GetBonePositionInSpaceOfParent(node));
			data.CurrentRotation = GetBoneRotationInSpaceOfParent(node);

			if (node.BaseIndex == 0 && (boneChain.Count == 0 || framesDict.Count == 0)) {
				data.NoParentKeyframes = true;
				data.PositionTransformer = positionTransformer;
				return data;
			}

			var curFrame = parentNode.DefaultAnimation.Frame;
			boneChain.Add(node);
			foreach (var pair in framesDict) {
				foreach (var frame in pair.Value) {
					ApplyAnimationAtFrame(pair.Key, frame, boneChain);
					data.PositionKeyframes.Add(frame, new Keyframe<Vector2> {
						Frame = frame,
						Function = KeyFunction.Spline,
						Value = positionTransformer(GetBonePositionInSpaceOfParent(node))
					});
					data.RotationKeyframes.Add(frame, new Keyframe<float> {
						Frame = frame,
						Function = KeyFunction.Spline,
						Value = GetBoneRotationInSpaceOfParent(node)
					});
				}
			}

			foreach (var a in node.Animators) {
				foreach (var key in a.Keys) {
					ApplyAnimationAtFrame(a.AnimationId, key.Frame, boneChain);
					switch (a.TargetPropertyPath) {
						case nameof(Bone.Position):
							if (!data.PositionKeyframes.ContainsKey(key.Frame)) {
								data.PositionKeyframes[key.Frame] = new Keyframe<Vector2>();
							}
							data.PositionKeyframes[key.Frame].Frame = key.Frame;
							data.PositionKeyframes[key.Frame].Function = key.Function;
							data.PositionKeyframes[key.Frame].Value = positionTransformer(GetBonePositionInSpaceOfParent(node));
							break;
						case nameof(Bone.Rotation):
							if (!data.RotationKeyframes.ContainsKey(key.Frame)) {
								data.RotationKeyframes[key.Frame] = new Keyframe<float>();
							}
							data.RotationKeyframes[key.Frame].Frame = key.Frame;
							data.RotationKeyframes[key.Frame].Function = key.Function;
							data.RotationKeyframes[key.Frame].Value = GetBoneRotationInSpaceOfParent(node);
							break;
					}
				}
			}
			ApplyAnimationAtFrame(DefaultAnimationId, curFrame, boneChain);
			return data;
		}

		private static float GetBoneRotationInSpaceOfParent(Bone bone)
		{
			var parentEntry = bone.Parent.AsWidget.BoneArray[bone.BaseIndex];
			return bone.Rotation + (parentEntry.Tip - parentEntry.Joint).Atan2Deg;
		}

		private static Vector2 GetBonePositionInSpaceOfParent(Bone node)
		{
			return node.Position * node.CalcLocalToParentWidgetTransform();
		}

		private static void ApplyAnimationAtFrame(string animationId, int frame, IEnumerable<Bone> bones)
		{
			if (animationId != DefaultAnimationId) {
				throw new NotImplementedException();
			}
			foreach (var node in bones) {
				node.DefaultAnimation.Frame = frame;
				node.Animators.Apply(node.DefaultAnimation.Time);
				node.Update(0);
			}
		}

		public static bool IsValidNode(Node node) => (node is Widget) || (node is Bone) || (node is Audio) || (node is ImageCombiner);

		private class BoneAnimationData
		{
			public readonly Dictionary<int, Keyframe<Vector2>> PositionKeyframes = new Dictionary<int, Keyframe<Vector2>>();
			public readonly Dictionary<int, Keyframe<float>> RotationKeyframes = new Dictionary<int, Keyframe<float>>();
			public Vector2 CurrentPosition { get; set; }
			public float CurrentRotation { get; set; }
			public bool NoParentKeyframes { get; set; }
			public Func<Vector2, Vector2> PositionTransformer { get; set; }
		}
	}
}
