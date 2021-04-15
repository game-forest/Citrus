using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core.Operations;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine.Common.Operations
{
	public static class GroupSceneItems
	{
		private const string DefaultAnimationId = "<DefaultAnimationId>";

		/// <exception cref="InvalidOperationException">Thrown when sceneItems contains no elements.</exception>
		public static Node Perform(IEnumerable<Row> sceneItems)
		{
			if (!sceneItems.Any()) {
				throw new InvalidOperationException();
			}
			var topSceneItems = SceneTreeUtils.EnumerateTopSceneItems(sceneItems).ToList();
			var nodes = EnumerateNodes(topSceneItems).ToList();
			var history = Document.Current.History;
			using (history.BeginTransaction()) {
				var containerSceneTree = SceneTreeUtils.GetOwnerNodeSceneItem(topSceneItems[0].Parent);
				var container = containerSceneTree.GetNode();
				if (topSceneItems.Any(i => SceneTreeUtils.GetOwnerNodeSceneItem(i.Parent).GetNode() != container)) {
					throw new InvalidOperationException("When grouping all nodes must belong to a single parent.");
				}
				if (Utils.CalcHullAndPivot(nodes, out var hull, out _)) {
					hull = hull.Transform(container.AsWidget.LocalToWorldTransform.CalcInversed());
				}
				var aabb = hull.ToAABB();
				var selectedBones = nodes.OfType<Bone>().ToList();
				var group = CreateGroupFrame(topSceneItems[0]);
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
					} else {
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
				foreach (var item in topSceneItems) {
					UnlinkSceneItem.Perform(item);
				}
				int index = 0;
				foreach (var item in topSceneItems) {
					LinkSceneItem.Perform(Document.Current.GetSceneItemForObject(group), index++, item);
				}
				foreach (var node in nodes) {
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

		private static Frame CreateGroupFrame(Row item)
		{
			var i = item;
			while (i.Parent.TryGetNode(out var node) && node is Bone) {
				i = i.Parent;
			}
			var group = (Frame)CreateNode.Perform(
				i.Parent,
				i.Parent.Rows.IndexOf(i),
				typeof(Frame));
			group.Id = item.Id + "Group";
			return group;
		}

		public static IEnumerable<Node> EnumerateNodes(IEnumerable<Row> items)
		{
			foreach (var i in items) {
				if (i.TryGetNode(out var node)) {
					if (node is Bone) {
						yield return node;
						foreach (var n in EnumerateNodes(i.Rows)) {
							yield return n;
						}
					} else {
						yield return node;
					}
				} else if (i.TryGetFolder(out _)) {
					foreach (var n in EnumerateNodes(i.Rows)) {
						yield return n;
					}
				}
			}
		}

		private static void SetKeyframes(Dictionary<Node, BoneAnimationData> keyframeDictionary)
		{
			foreach (var (node, animationData) in keyframeDictionary) {
				if (animationData.NoParentKeyframes) {
					TransformPropertyAndKeyframes(node, nameof(Bone.Position), animationData.PositionTransformer);
				} else {
					SetProperty.Perform(node, nameof(Bone.Position), animationData.CurrentPosition);
					SetProperty.Perform(node, nameof(Bone.Rotation), animationData.CurrentRotation);
					foreach (var keyframe in animationData.PositionKeyframes) {
						SetKeyframe.Perform(node, nameof(Bone.Position), Document.Current.Animation, keyframe.Value);
					}
					foreach (var keyframe in animationData.RotationKeyframes) {
						SetKeyframe.Perform(node, nameof(Bone.Rotation), Document.Current.Animation, keyframe.Value);
					}
					SetAnimableProperty.Perform(node, nameof(Bone.BaseIndex), 0);
				}
			}
		}

		public static void TransformPropertyAndKeyframes<T>(Node node, string propertyId, Func<T, T> transformer)
		{
			var value = new Property<T>(node, propertyId).Value;
			SetProperty.Perform(node, propertyId, transformer(value));
			foreach (var animator in node.Animators) {
				if (animator.TargetPropertyPath == propertyId) {
					foreach (var keyframe in animator.Keys.ToList()) {
						var newKeyframe = keyframe.Clone();
						newKeyframe.Value = transformer((T)newKeyframe.Value);
						SetKeyframe.Perform(
							node,
							animator.TargetPropertyPath,
							FindAnimationById(node, animator.AnimationId),
							newKeyframe);
					}
				}
			}
		}

		private static Animation FindAnimationById(Node node, string animationId)
		{
			while (true) {
				if (node.Animations.TryFind(animationId, out var a)) {
					return a;
				}
				node = node.Parent;
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
