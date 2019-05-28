using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime.PolygonMesh.Structure;
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

		public class Geometry
		{
			public enum StructureObjectsTypes
			{
				Edge,
				Vertex,
				Face,
			}

			public static StructureObjectsTypes[] StructureObjectsTypesArray = new[] {
				StructureObjectsTypes.Vertex,
				StructureObjectsTypes.Edge,
				StructureObjectsTypes.Face,
			};

			public PolygonMesh Owner { get; internal set; }

			public PolygonMeshVertex[] Vertices { get; set; }

			public Geometry(PolygonMesh owner)
			{
				Owner = owner;
			}

			public IPolygonMeshStructureObject[] this[StructureObjectsTypes type]
			{
				get
				{
					switch (type) {
						case StructureObjectsTypes.Vertex:
							return Vertices;
						case StructureObjectsTypes.Edge:
							var edges = new HashSet<IPolygonMeshStructureObject>();
							foreach (var vertex in Vertices) {
								foreach (var edge in vertex.AdjacentEdges) {
									edges.Add(edge);
								}
							}
							return edges.ToArray();
						case StructureObjectsTypes.Face:
							var faces = new HashSet<IPolygonMeshStructureObject>();
							foreach (var vertex in Vertices) {
								faces.Add(vertex.HalfEdge.Face ?? vertex.HalfEdge.Twin.Face);
							}
							return faces.ToArray();
						default:
							throw new MemberAccessException();
					}
				}
			}

			public void Build(Vector2 A, Vector2 B, Vector2 C, Vector2 D)
			{
				Vertices = new PolygonMeshVertex[4] {
					new PolygonMeshVertex(A) { UV = new Vector2(0, 0) },
					new PolygonMeshVertex(B) { UV = new Vector2(1, 0) },
					new PolygonMeshVertex(C) { UV = new Vector2(0, 1) },
					new PolygonMeshVertex(D) { UV = new Vector2(1, 1) }
				};
				new PolygonMeshFace(Vertices[0], Vertices[1], Vertices[2]) { Owner = Owner };
				new PolygonMeshFace(Vertices[0].HalfEdge.Face.GetOppositeEdge(Vertices[0]), Vertices[3]) { Owner = Owner };
			}

			internal Geometry ExtractSubset(List<PolygonMeshVertex> vertices)
			{
				throw new NotImplementedException();
			}

			internal void Triangulate(List<Vector2> points)
			{
				throw new NotImplementedException();
			}

			internal void Triangulate(params Vector2[] points)
			{
				Triangulate(points.ToList());
			}

			internal void Triangulate(List<PolygonMeshVertex> vertices)
			{
				Triangulate(vertices.Select(i => i.Position).ToList());
			}
		}

		public readonly Geometry Structure;

		[YuzuMember]
		public State CurrentState { get; set; }

		[YuzuMember]
		public override ITexture Texture { get; set; }

		public PolygonMesh()
		{
			var A = Vector2.Zero;
			var B = new Vector2(Width, 0.0f);
			var C = new Vector2(0.0f, Height);
			var D = Size;

			Structure = new Geometry(this);
			Structure.Build(A, B, C, D);
			Texture = new SerializableTexture();
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			if (Structure != null) {
				foreach (var v in Structure.Vertices) {
					v.Position *= Size / (Size - sizeDelta);
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
			foreach (var obj in Structure[Geometry.StructureObjectsTypes.Face]) {
				var face = obj as PolygonMeshFace;
				ro.Vertices.Add(new Vertex() { Pos = face.Root.Origin.Position, UV1 = face.Root.Origin.UV, Color = GlobalColor });
				ro.Vertices.Add(new Vertex() { Pos = face.Root.Next.Origin.Position, UV1 = face.Root.Next.Origin.UV, Color = GlobalColor });
				ro.Vertices.Add(new Vertex() { Pos = face.Root.Prev.Origin.Position, UV1 = face.Root.Prev.Origin.UV, Color = GlobalColor });
			}
			return ro;
		}

		private class RenderObject : WidgetRenderObject
		{
			public static Vertex[] Polygon = new Vertex[3];

			public readonly List<Vertex> Vertices = new List<Vertex>();
			public ITexture Texture;

			protected override void OnRelease()
			{
				Texture = null;
				Vertices.Clear();
			}

			public override void Render()
			{
				// TO DO: Reduce draw calls
				PrepareRenderState();
				if (Texture != null && Vertices != null) {
					for (var i = 0; i < Vertices.Count; i += Polygon.Length) {
						for (int j = 0; j < Polygon.Length; ++j) {
							Polygon[j] = Vertices[i + j];
						}
						Renderer.DrawTriangleFan(Texture, Polygon, Polygon.Length);
					}
				}
			}
		}
	}
}
