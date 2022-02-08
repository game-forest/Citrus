using Yuzu;

namespace Lime
{
	[TangerineAllowedParentTypes(typeof(DistortionMesh))]
	public class DistortionMeshPoint : PointObject
	{
		private Vector2 offset;

		[YuzuMember]
		public Color4 Color { get; set; }

		[YuzuMember]
		public Vector2 UV { get; set; }

		[YuzuMember]
		public override Vector2 Offset
		{
			get => offset;
			set
			{
				if (offset != value) {
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					offset = value;
				}
			}
		}

		public DistortionMeshPoint()
		{
			Color = Color4.White;
		}

		public override void UpdateAncestorBoundingRect(Widget ancestor)
		{
			var p = Parent.AsWidget;
			if (p == ancestor) {
				p.ExpandBoundingRect(TransformedPosition, propagate: false);
			} else {
				var pos = CalcPositionInSpaceOf(ancestor);
				ancestor.ExpandBoundingRect(pos, propagate: false);
			}
		}
	}
}
