using System;
using ProtoBuf;
using Yuzu;

namespace Lime
{
	[ProtoContract]
	public struct BoneArray
	{
		[ProtoContract]
		public struct Entry
		{
			/// <summary>
			/// ������� ���������� ������� �����
			/// </summary>
			[ProtoMember(1)]
			[YuzuMember]
			public float Rotation;

			/// <summary>
			/// ������� ����� �����
			/// </summary>
			[ProtoMember(2)]
			[YuzuMember]
			public float Length;

			/// <summary>
			/// ������� ���������� ������� ������ �����
			/// </summary>
			[ProtoMember(3)]
			[YuzuMember]
			public Vector2 Joint;

			/// <summary>
			/// ������� ���������� ������� ����� �����
			/// </summary>
			[ProtoMember(4)]
			[YuzuMember]
			public Vector2 Tip;

			/// <summary>
			/// Bone's transformation relative its reference position.
			/// </summary>
			[ProtoMember(5)]
			[YuzuMember]
			public Matrix32 RelativeTransform;
		}

		const int InitialSize = 10;

		void EnsureArraySize(int size)
		{
			if (items == null)
				items = new Entry[InitialSize];
			if (size > items.Length)
				Array.Resize<Entry>(ref items, size);
		}

		public Entry this[int index] {
			get {
				EnsureArraySize(index + 1);
				return items[index];
			}
			set {
				EnsureArraySize(index + 1);
				items[index] = value;
			}
		}

		void ApplyBone(BoneWeight bw, Vector2 sourceVector, ref Vector2 resultVector, ref float overallWeight)
		{
			if (items != null && bw.Index > 0 && bw.Index < items.Length) {
				BoneArray.Entry e = items[bw.Index];
				resultVector += bw.Weight * e.RelativeTransform.TransformVector(sourceVector);
				overallWeight += bw.Weight;
			}
		}

		public Vector2 ApplySkinningToVector(Vector2 vector, SkinningWeights weights)
		{
			Vector2 result = Vector2.Zero;
			float overallWeight = 0;
			ApplyBone(weights.Bone0, vector, ref result, ref overallWeight);
			ApplyBone(weights.Bone1, vector, ref result, ref overallWeight);
			ApplyBone(weights.Bone2, vector, ref result, ref overallWeight);
			ApplyBone(weights.Bone3, vector, ref result, ref overallWeight);
			if (overallWeight < 0)
				result = vector;
			else if (overallWeight >= 0 && overallWeight < 1)
				result += (1 - overallWeight) * vector;
			else {
				result.X /= overallWeight;
				result.Y /= overallWeight;
			}
			return result;
		}

		[ProtoMember(1)]
		[YuzuMember]
		public Entry[] items;
	}
}
