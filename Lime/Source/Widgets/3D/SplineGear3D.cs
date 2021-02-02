using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 27)]
	[TangerineMenuPath("3D/Spline Gear 3D")]
	[TangerineAllowedParentTypes(typeof(Node3D), typeof(Viewport3D))]
	public class SplineGear3D : Node, IUpdatableNode
	{
		[YuzuMember]
		[TangerineKeyframeColor(2)]
		public NodeReference<Node3D> NodeRef { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(3)]
		public NodeReference<Spline3D> SplineRef { get; set; }

		public Node3D Node => NodeRef?.GetNode(Parent);
		public Spline3D Spline => SplineRef?.GetNode(Parent);

		[YuzuMember]
		[TangerineKeyframeColor(4)]
		[TangerineValidRange(0f, 1f)]
		public float SplineOffset { get; set; }

		public SplineGear3D()
		{
			RenderChainBuilder = null;
			Components.Add(new UpdatableNodeBehavior());
		}

		public virtual void OnUpdate(float delta)
		{
			if (Parent == null) {
				return;
			}
			var spline = Spline;
			var node = Node;
			if (spline != null && node != null) {
				var length = spline.CalcLengthRough();
				var transform = spline.CalcPointTransform(SplineOffset * length);
				node.Position = Vector3.Zero * transform;
				node.Rotation = Quaternion.CreateFromRotationMatrix(transform);
			}
		}

		public override void AddToRenderChain(RenderChain chain)
		{
		}
	}
}
