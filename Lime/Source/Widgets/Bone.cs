using System;
using System.Collections.Generic;
using System.Linq;
using Yuzu;

namespace Lime
{
	[YuzuCompact]
	public struct BoneWeight
	{
		[YuzuMember("0")]
		public int Index;

		[YuzuMember("1")]
		public float Weight;
	}

	[YuzuCompact]
	public class SkinningWeights
	{
		[YuzuMember("0")]
		public BoneWeight Bone0;

		[YuzuMember("1")]
		public BoneWeight Bone1;

		[YuzuMember("2")]
		public BoneWeight Bone2;

		[YuzuMember("3")]
		public BoneWeight Bone3;

		public bool IsEmpty()
		{
			return Bone0.Index == 0 && Bone1.Index == 0 && Bone2.Index == 0 && Bone3.Index == 0 &&
				   Bone0.Weight == 0 && Bone1.Weight == 0 && Bone2.Weight == 0 && Bone3.Weight == 0;
		}

		public BoneWeight this[int index]
		{
			get
			{
				if (index == 0) return Bone0;
				if (index == 1) return Bone1;
				if (index == 2) return Bone2;
				if (index == 3) return Bone3;
				throw new IndexOutOfRangeException();
			}
			set
			{
				switch (index) {
					case 0: Bone0 = value; break;
					case 1: Bone1 = value; break;
					case 2: Bone2 = value; break;
					case 3: Bone3 = value; break;
					default: throw new IndexOutOfRangeException();
				}
			}
		}

		public SkinningWeights Release(params int[] boneIndices)
		{
			if (boneIndices.Length == 0) {
				Bone0 = new BoneWeight();
				Bone1 = new BoneWeight();
				Bone2 = new BoneWeight();
				Bone3 = new BoneWeight();
				return this;
			}

			var weightToDistribute = 0.0f;
			foreach (var boneIndex in boneIndices) {
				if (boneIndex <= 0) {
					continue;
				}
				for (var j = 0; j < 4; ++j) {
					var sw = this[j];
					if (sw.Index == boneIndex) {
						weightToDistribute += sw.Weight;
						sw.Weight = sw.Index = 0;
						this[j] = sw;
					}
				}
			}
			var effectiveCount = 0;
			for (var i = 0; i < 4; ++i) {
				if (this[i].Index > 0) {
					++effectiveCount;
				}
			}
			if (effectiveCount > 0 && weightToDistribute > Mathf.ZeroTolerance) {
				weightToDistribute /= effectiveCount;
				for (var i = 0; i < 4; ++i) {
					var sw = this[i];
					if (sw.Index > 0) {
						sw.Weight += weightToDistribute;
						this[i] = sw;
					}
				}
			}
			return this;
		}

		public SkinningWeights Clone()
		{
			return new SkinningWeights {
				Bone0 = Bone0,
				Bone1 = Bone1,
				Bone2 = Bone2,
				Bone3 = Bone3,
			};
		}
	}

	[TangerineRegisterNode(Order = 5)]
	[TangerineVisualHintGroup("/All/Nodes/Bones")]
	public class Bone : Node
	{
		[YuzuMember]
		[TangerineKeyframeColor(10)]
		public Vector2 Position { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(1)]
		public float Rotation { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(2)]
		public float Length { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(7)]
		public bool IKStopper { get; set; }

		[YuzuMember]
		[TangerineReadOnly]
		public int Index { get; set; }

		[YuzuMember]
		[TangerineReadOnly]
		public int BaseIndex { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(8)]
		public float EffectiveRadius { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(9)]
		public float FadeoutZone { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(13)]
		public Vector2 RefPosition { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(11)]
		public float RefRotation { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(12)]
		public float RefLength { get; set; }

		public Matrix32 CalcLocalToParentWidgetTransform()
		{
				if (BaseIndex == 0) {
					return Matrix32.Identity;
				}
				BoneArray.Entry b = Parent.AsWidget.BoneArray[BaseIndex];
				var l = ClipAboutZero(b.Length);
				Vector2 u = b.Tip - b.Joint;
				Vector2 v = new Vector2(-u.Y / l, u.X / l);
				return new Matrix32(u, v, b.Tip);
		}

		public Bone()
		{
			RenderChainBuilder = null;
			Length = 100;
			EffectiveRadius = 100;
			FadeoutZone = 50;
			IKStopper = true;
			Components.Add(new BoneBehavior());
		}

		public virtual void OnUpdate(float delta)
		{
			if (Index > 0 && Parent != null) {
				BoneArray.Entry e;
				e.Joint = Position;
				e.Rotation = Rotation;
				e.Length = Length;
				if (BaseIndex > 0) {
					// Tie the bone to the parent bone.
					BoneArray.Entry b = Parent.AsWidget.BoneArray[BaseIndex];
					float l = ClipAboutZero(b.Length);
					Vector2 u = b.Tip - b.Joint;
					Vector2 v = new Vector2(-u.Y / l, u.X / l);
					e.Joint = b.Tip + u * Position.X + v * Position.Y;
					e.Rotation += b.Rotation;
				}
				// Get position of bone's tip.
				e.Tip = Vector2.RotateDegRough(new Vector2(e.Length, 0), e.Rotation) + e.Joint;
				if (RefLength != 0) {
					float relativeScaling = Length / ClipAboutZero(RefLength);
					// Calculating the matrix of relative transformation.
					Matrix32 m1, m2;
					m1 = Matrix32.TransformationRough(Vector2.Zero, Vector2.One, RefRotation * Mathf.DegToRad, RefPosition);
					m2 = Matrix32.TransformationRough(Vector2.Zero, new Vector2(relativeScaling, 1), e.Rotation * Mathf.DegToRad, e.Joint);
					e.RelativeTransform = m1.CalcInversed() * m2;
				} else
					e.RelativeTransform = Matrix32.Identity;
				Parent.AsWidget.BoneArray[Index] = e;
				Parent.PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				for (var child = Parent.FirstChild; child != null; child = child.NextSibling) {
					child.DirtyMask |= DirtyFlags.LocalTransform | DirtyFlags.ParentBoundingRect;
				}
			}
		}

		public override void AddToRenderChain(RenderChain chain)
		{
		}

		static float ClipAboutZero(float value, float eps = 0.0001f)
		{
			if (value > -eps && value < eps)
				return eps < 0 ? -eps : eps;
			else
				return value;
		}

		public float CalcWeightForPoint(Vector2 point)
		{
			var entry = Parent.AsWidget.BoneArray[Index];
			var a = entry.Joint;
			var b = entry.Tip;
			var distance = (float)Mathf.CalcDistanceToSegment(a, b, point);
			if (distance < EffectiveRadius) {
				return Mathf.HermiteSpline(distance / EffectiveRadius, 100, 0, 1, -1);
			} else if (distance < EffectiveRadius + FadeoutZone) {
				return Mathf.HermiteSpline((distance - EffectiveRadius) / FadeoutZone, 1, -1, 0, 0);
			}
			return 0;
		}
	}

	public static class BoneUtils
	{
		public static int GenerateNewBoneIndex(Node hostNode)
		{
			var bones = hostNode.Nodes.OfType<Bone>().ToList();
			for (var i = 1;; i++) {
				if (bones.All(b => b.Index != i)) {
					return i;
				}
			}
		}

		/// <summary>
		/// Reorder bones with topological sort to maintain correct update
		/// order of transformations.
		/// </summary>
		public static void SortBones(IList<Bone> bones)
		{
			SortBones<Bone>(bones);
		}

		/// <summary>
		/// Reorder bones within mixed list of nodes with topological sort to maintain correct update
		/// order of transformations.
		/// </summary>
		public static void SortBones(IList<Node> nodes)
		{
			SortBones<Node>(nodes);
		}

		// This method utilizes a simple sort: O(n) complexity if bones are in the correct order,
		// O(n^2) in the worst case. This fits to our needs, since bones are already sorted in almost all cases.
		private static void SortBones<T>(IList<T> nodes) where T: Node
		{
			bool done;
			do {
				done = true;
				for (int i = 0; i < nodes.Count; i++) {
					if (nodes[i] is Bone bone && bone.BaseIndex > 0) {
						for (int j = 0; j < nodes.Count - 1; j++) {
							int k = i - 1 - j;
							if (k < 0) {
								k += nodes.Count;
							}
							if (nodes[k] is Bone baseBone && baseBone.Index == bone.BaseIndex) {
								if (k > i) {
									nodes.RemoveAt(i);
									nodes.Insert(k, bone as T);
									i--;
									done = false;
								}
								break;
							}
						}
					}
				}
			} while (!done);
		}

		public static Bone GetBone(this IEnumerable<Node> nodes, int index)
		{
			foreach (var node in nodes) {
				if (node is Bone && ((Bone)node).Index == index) {
					return node as Bone;
				}
			}
			return null;
		}

		public static Bone FindBoneRoot(Bone bone, IEnumerable<Node> nodes)
		{
			while (bone.BaseIndex != 0) {
				var root = nodes.GetBone(bone.BaseIndex);
				if (root == null) {
					return bone;
				}
				bone = root;
			}
			return bone;
		}

		public static bool CheckConsistency(IEnumerable<Bone> bones, params Widget[] widgets)
		{
			var container = bones.First().Parent.AsWidget;
			foreach (var bone in bones) {
				if (bone.Parent == null || bone.Parent != container) {
					return false;
				}
			}

			foreach (var widget in widgets) {
				if (widget.Parent == null || widget.Parent != container) {
					return false;
				}
			}

			return true;
		}

		public static SkinningWeights CalcSkinningWeight(SkinningWeights oldSkinningWeights, Vector2 position, List<Bone> bones)
		{
			var skinningWeights = new SkinningWeights();
			var i = 0;
			var overallWeight = 0f;
			while (oldSkinningWeights[i].Index != 0) {
				skinningWeights[i] = oldSkinningWeights[i];
				overallWeight += skinningWeights[i].Weight;
				i++;
			}
			var j = 0;
			while (j < bones.Count && i < 4) {
				var weight = bones[j].CalcWeightForPoint(position);
				if (Mathf.Abs(weight) > Mathf.ZeroTolerance) {
					skinningWeights[i] = new BoneWeight {
						Weight = weight,
						Index = bones[j].Index
					};
					overallWeight += skinningWeights[i].Weight;
					i++;
				}
				j++;
			}
			if (overallWeight != 0) {
				for (i = 0; i < 4; i++) {
					var bw = skinningWeights[i];
					bw.Weight /= overallWeight;
					skinningWeights[i] = bw;
				}
			}
			return skinningWeights;
		}
	}

	[NodeComponentDontSerialize]
	internal class BoneBehavior : BehaviorComponent
	{
		private Bone bone;
		private BoneArrayUpdaterBehavior boneArrayUpdater;

		protected internal override void Start()
		{
			Register();
		}

		public void Register()
		{
			if (bone == null) {
				bone = (Bone)Owner;
				boneArrayUpdater = Owner.Parent.Components.GetOrAdd<BoneArrayUpdaterBehavior>();
				boneArrayUpdater.AddBone(bone);
			}
		}

		protected internal override void Stop(Node owner)
		{
			boneArrayUpdater.RemoveBone(bone);
			bone = null;
		}
	}
}
