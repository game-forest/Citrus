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
			Animate,
			Deform,
			Create,
			Remove,
		}

		private List<Vertex> vertices;

		public static GeometryPrimitive[] Primitives =
			Enum.GetValues(typeof(GeometryPrimitive)) as GeometryPrimitive[];

		[YuzuMember]
		public override ITexture Texture { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Vertex> Vertices
		{
			get => vertices;
			set
			{
				vertices = value;
				if (Geometry != null && currentContext == Context.Animation) {
					Geometry.Vertices = vertices;
				}
			}
		}

		[YuzuMember]
		[TangerineIgnore]
		public List<int> IndexBuffer { get; set; }

#if TANGERINE
		public enum Context
		{
			Animation,
			Deformation
		}

		private State currentState = State.Animate;
		private Context currentContext = Context.Animation;

		[YuzuMember]
		[TangerineIgnore]
		public IGeometry Geometry { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public List<Vertex> DeformedVertices { get; set; }

		public State CurrentState
		{
			get => currentState;
			set
			{
				if (currentState != value) {
					switch (value) {
						case State.Animate:
							CurrentContext = Context.Animation;
							break;
						case State.Deform:
						case State.Create:
						case State.Remove:
							CurrentContext = Context.Deformation;
							break;
					}
					currentState = value;
				}
			}
		}

		public Context CurrentContext
		{
			get => currentContext;
			set
			{
				if (currentContext != value) {
					SwapContext();
					currentContext = value;
				}
			}
		}

		public void SwapContext()
		{
			switch (CurrentContext) {
				case Context.Animation:
					Geometry.Vertices = DeformedVertices;
					break;
				case Context.Deformation:
					Geometry.Vertices = Vertices;
					break;
			}
			Geometry.ResetCache();
		}

		public bool HitTest(Vector2 position, Matrix32 transform, out ITangerineGeometryPrimitive target, float scale = 1.0f)
		{
			target = null;
			foreach (var primitive in Primitives) {
				var minDistance = float.MaxValue;
				foreach (var obj in Geometry[primitive]) {
					if (obj.HitTest(
							position,
							transform,
							out var distance,
							radius: primitive == GeometryPrimitive.Vertex ? 16.0f : 8.0f,
							scale: scale
						)
					) {
						if (distance < minDistance) {
							minDistance = distance;
							target = obj;
							if (primitive == GeometryPrimitive.Face) {
								return true;
							}
						}
					}
				}
				if (target != null) {
					return true;
				}
			}
			return false;
		}
#endif

		public PolygonMesh()
		{
			Texture = new SerializableTexture();
			IndexBuffer = new List<int>();
			Vertices = new List<Vertex> {
				new Vertex() { Pos = Vector2.Zero, UV1 = Vector2.Zero, Color = GlobalColor },
				new Vertex() { Pos = new Vector2(Width, 0.0f), UV1 = new Vector2(1, 0), Color = GlobalColor },
				new Vertex() { Pos = new Vector2(0.0f, Height), UV1 = new Vector2(0, 1), Color = GlobalColor },
				new Vertex() { Pos = Size, UV1 = Vector2.One, Color = GlobalColor },
			};
			Geometry = new Geometry(Vertices, IndexBuffer, this);
#if TANGERINE
			DeformedVertices = new List<Vertex>();
			foreach (var v in Vertices) {
				DeformedVertices.Add(v);
			}
#endif
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			if (Geometry != null) {
				var verticeArrays = new[] {
#if TANGERINE
					DeformedVertices,
#endif
					Vertices
				};
				foreach (var verticeArray in verticeArrays) {
					for (var i = 0; i < verticeArray.Count; ++i) {
						var v = verticeArray[i];
						v.Pos *= Size / (Size - sizeDelta);
						verticeArray[i] = v;
					}
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
			var vertices =
				CurrentContext == Context.Animation ?
				Vertices :
				DeformedVertices;
			foreach (var v in vertices) {
				ro.Vertices.Add(v);
			}
			foreach (var i in IndexBuffer) {
				ro.IndexBuffer.Add(i);
			}
			return ro;
		}

		private class RenderObject : WidgetRenderObject
		{
			public static Vertex[] Polygon = new Vertex[3];

			public readonly List<Vertex> Vertices = new List<Vertex>();
			public readonly List<int> IndexBuffer = new List<int>();
			public ITexture Texture;

			protected override void OnRelease()
			{
				Texture = null;
				Vertices.Clear();
				IndexBuffer.Clear();
			}

			public override void Render()
			{
				PrepareRenderState();
				if (Texture != null && Vertices.Count > 0 && IndexBuffer.Count > 0) {
					for (var i = 0; i < IndexBuffer.Count; i += Polygon.Length) {
						for (int j = 0; j < Polygon.Length; ++j) {
							Polygon[j] = Vertices[IndexBuffer[i + j]];
						}
						Renderer.DrawTriangleFan(Texture, Polygon, Polygon.Length);
					}
				}
			}
		}
	}
}
