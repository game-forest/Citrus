using System;
using System.Collections.Generic;
using Yuzu;

namespace Lime.PolygonMesh
{
	[TangerineRegisterNode(Order = 32)]
	[TangerineVisualHintGroup("/All/Nodes/Images", "Polygon Mesh")]
	public class PolygonMesh : Widget
	{
		public enum State
		{
			Display,
			Modify,
			Create,
			Remove,
		}

		public static GeometryPrimitive[] Primitives =
			Enum.GetValues(typeof(GeometryPrimitive)) as GeometryPrimitive[];

		[YuzuMember]
		[TangerineIgnore]
		public IGeometry Geometry { get; set; }

		[YuzuMember]
		public State CurrentState { get; set; }

		[YuzuMember]
		public override ITexture Texture { get; set; }

		public PolygonMesh()
		{
			Texture = new SerializableTexture();
			var vertices = new List<Vertex> {
				new Vertex() { Pos = Vector2.Zero, UV1 = Vector2.Zero, Color = GlobalColor },
				new Vertex() { Pos = new Vector2(Width, 0.0f), UV1 = new Vector2(1, 0), Color = GlobalColor },
				new Vertex() { Pos = new Vector2(0.0f, Height), UV1 = new Vector2(0, 1), Color = GlobalColor },
				new Vertex() { Pos = Size, UV1 = Vector2.One, Color = GlobalColor },
			};
			Geometry = new Geometry(vertices);
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			if (Geometry != null) {
				for (var i = 0; i < Geometry.Vertices.Count; ++i) {
					var v = Geometry.Vertices[i];
					v.Pos *= Size / (Size - sizeDelta);
					Geometry.Vertices[i] = v;
				}
			}
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && ClipRegionTest(chain.ClipRegion)) {
				AddSelfToRenderChain(chain, Layer);
			}
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			var ro = RenderObjectPool<RenderObject>.Acquire();
			ro.CaptureRenderState(this);
			ro.Texture = Texture;
			foreach (var v in Geometry.Vertices) {
				ro.Vertices.Add(v);
			}
			foreach (var i in Geometry.Traverse()) {
				ro.Order.Add(i);
			}
			return ro;
		}

		private class RenderObject : WidgetRenderObject
		{
			public static Vertex[] Polygon = new Vertex[3];

			public readonly List<Vertex> Vertices = new List<Vertex>();
			public readonly List<int> Order = new List<int>();
			public ITexture Texture;

			protected override void OnRelease()
			{
				Texture = null;
				Vertices.Clear();
				Order.Clear();
			}

			public override void Render()
			{
				PrepareRenderState();
				if (Texture != null && Vertices.Count > 0 && Order.Count > 0) {
					for (var i = 0; i < Order.Count; i += Polygon.Length) {
						for (int j = 0; j < Polygon.Length; ++j) {
							Polygon[j] = Vertices[Order[i + j]];
						}
						Renderer.DrawTriangleFan(Texture, Polygon, Polygon.Length);
					}
				}
			}
		}
	}
}
