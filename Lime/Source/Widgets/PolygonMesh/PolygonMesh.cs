using System;
using System.Collections.Generic;
using Yuzu;

namespace Lime.PolygonMesh
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Images", "Polygon Mesh")]
	public class PolygonMesh : Widget
	{
		public enum ModificationContext
		{
			Animation,
			Setup
		}

		[YuzuMember]
		public override ITexture Texture { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Vertex> TriangulationVertices { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<int> IndexBuffer { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<(int, int)> ConstrainedPairs { get; set; }

		public ModificationContext CurrentModificationContext { get; set; }

		public List<Vertex> AnimatorVertices { get; set; }

		public PolygonMesh()
		{
			Texture = new SerializableTexture();
			TriangulationVertices = new List<Vertex>();
			IndexBuffer = new List<int>();
			ConstrainedPairs = new List<(int, int)>();
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && ClipRegionTest(chain.ClipRegion)) {
				AddSelfToRenderChain(chain, Layer);
			}
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			return null;
		}

		private class RenderObject : WidgetRenderObject
		{
			protected override void OnRelease()
			{

			}

			public override void Render()
			{
				PrepareRenderState();
			}
		}
	}
}
