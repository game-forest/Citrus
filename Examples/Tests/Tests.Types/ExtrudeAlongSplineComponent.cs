using Lime;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Yuzu;

namespace Tests.Types
{
	public enum VCompensationStrategy
	{
		Stretch,
		SplineLength,
	}

	[AllowedComponentOwnerTypes(typeof(Spline3D))]
	[TangerineRegisterComponent]
	public class ExtrudeAlongSplineComponent : NodeBehavior
	{
		public class ExtrudeShape
		{
			[YuzuMember]
			public List<Vector2> Vertices { get; } = new List<Vector2>();
			[YuzuMember]
			public List<int> Lines { get; } = new List<int>();
			[YuzuMember]
			public List<float> Us { get; } = new List<float>();
		}

		private static Queue<Mesh<Mesh3D.Vertex>> meshPool = new Queue<Mesh<Mesh3D.Vertex>>();

		private Presenter presenter;
		private Mesh<Mesh3D.Vertex> mesh;
		private long hash = 0;

		[YuzuMember]
		public ExtrudeShape Shape { get; set; }

		[YuzuMember]
		public int Step { get; set; } = 30;

		[YuzuMember]
		public ITexture Texture { get; set; }

		[YuzuMember]
		public VCompensationStrategy CompensationStrategy { get; set; } = VCompensationStrategy.Stretch;

		[YuzuMember]
		public Vector2 ShapeScale { get; set; } = Vector2.One;

		[YuzuMember]
		public float TileSize { get; set; } = 1f;

		[YuzuMember]
		public bool ConstantTiling { get; set; } = false;

		[YuzuMember]
		public int TileCount { get; set; } = 1;

		[YuzuMember]
		public CullMode CullMode { get; set; }

		private void Extrude()
		{
			var newHash = CalculateHash();
			if (newHash == hash) {
				return;
			}
			var oldMesh = mesh;
			mesh = AcquireMesh();
			if (oldMesh != null) {
				ReleaseMesh(mesh);
			}
			hash = newHash;
			var spline = (Spline3D)Owner;
			var savedTransform = spline.GlobalTransform;
			// If we calculate it in global space right away
			// we gonna have some unpleasant artifacts and we'll have to recalculate
			// mesh every time spline's global transform is changed.
			spline.SetGlobalTransform(Matrix44.Identity);
			var step = 1f / Step;
			var path = new List<(Matrix44 Transform, float V)>(Step);
			var splineLength = spline.CalcLengthRough();
			var accumulatedLength = 0f;
			var prevTransform = spline.CalcPointTransform(0f);
			for (float t = 0f; t <= 1f; t += step) {
				var transform = spline.CalcPointTransform(t * splineLength);
				accumulatedLength += (transform.Translation - prevTransform.Translation).Length;
				path.Add((transform, accumulatedLength));
				prevTransform = transform;
			}
			for (int i = 0; i < path.Count; i++) {
				var p = path[i];
				p.V /= accumulatedLength;
				path[i] = p;
			}
			ushort shapeVertexCount = (ushort)Shape.Vertices.Count;
			var segmentCount = path.Count - 1;
			var edgeLoopCount = path.Count;
			mesh.VertexCount = shapeVertexCount * edgeLoopCount;
			var triangleCount = Shape.Lines.Count * segmentCount;
			mesh.IndexCount = triangleCount * 3;
			var indicies = mesh.Indices == null || mesh.Indices.Length < mesh.IndexCount
				? new ushort[mesh.IndexCount] : mesh.Indices;
			var vertices = mesh.Vertices == null || mesh.Vertices.Length < mesh.VertexCount
				? new Mesh3D.Vertex[mesh.VertexCount] : mesh.Vertices;
			ushort offset = 0;
			var vScale = 1f;
			if (CompensationStrategy != VCompensationStrategy.Stretch) {
				vScale = ConstantTiling ? TileCount : accumulatedLength / TileSize;
			}
			for (int i = 0; i < path.Count; i++) {
				for (int j = 0; j < shapeVertexCount; j++) {
					var vertex = new Mesh3D.Vertex();
					vertex.Pos = path[i].Transform.TransformVector(new Vector3(Shape.Vertices[j] * ShapeScale, 0f));
					vertex.UV1 = new Vector2(Shape.Us[j], path[i].V * vScale);
					vertex.Color = Color4.White;
					vertices[offset++] = vertex;
				}
			}
			offset = 0;
			ushort index = 0;
			for (int i = 0; i < segmentCount; i++) {
				for (int j = 0; j < Shape.Lines.Count; j += 2) {
					ushort a = (ushort)(offset + Shape.Lines[j]);
					ushort b = (ushort)(offset + Shape.Lines[j] + shapeVertexCount);
					ushort c = (ushort)(offset + Shape.Lines[j + 1] + shapeVertexCount);
					ushort d = (ushort)(offset + Shape.Lines[j + 1]);
					indicies[index++] = a;
					indicies[index++] = b;
					indicies[index++] = c;
					indicies[index++] = c;
					indicies[index++] = d;
					indicies[index++] = a;
				}
				offset += shapeVertexCount;
			}
			mesh.Indices = indicies;
			mesh.Vertices = vertices;
			mesh.DirtyFlags = MeshDirtyFlags.VerticesIndices;
			spline.SetGlobalTransform(savedTransform);
		}

		private long CalculateHash()
		{
			var hasher = new Hasher();
			hasher.Begin();
			hasher.Write(Step);
			hasher.Write(CompensationStrategy);
			hasher.Write(ShapeScale);
			hasher.Write(TileSize);
			hasher.Write(ConstantTiling);
			hasher.Write(TileCount);
			foreach (var vertex in Shape.Vertices) {
				hasher.Write(vertex);
			}
			foreach (var u in Shape.Us) {
				hasher.Write(u);
			}
			foreach (var index in Shape.Lines) {
				hasher.Write(index);
			}
			foreach (var node in Owner.Nodes) {
				var splinePoint = (SplinePoint3D)node;
				hasher.Write(splinePoint.Interpolation);
				hasher.Write(splinePoint.Position);
				hasher.Write(splinePoint.TangentA);
				hasher.Write(splinePoint.TangentB);
			}
			return hasher.End();
		}

		private static Mesh<Mesh3D.Vertex> AcquireMesh()
		{
			Mesh<Mesh3D.Vertex> mesh;
			lock (meshPool) {
				if (meshPool.Count > 0) {
					mesh = meshPool.Dequeue();
				} else {
					mesh = new Mesh<Mesh3D.Vertex> {
						AttributeLocations = new[] {
							ShaderPrograms.Attributes.Pos1, ShaderPrograms.Attributes.Color1, ShaderPrograms.Attributes.UV1,
							ShaderPrograms.Attributes.BlendIndices, ShaderPrograms.Attributes.BlendWeights,
							ShaderPrograms.Attributes.Normal, ShaderPrograms.Attributes.Tangent,
						},
						Topology = PrimitiveTopology.TriangleList,
						DirtyFlags = MeshDirtyFlags.All,
					};
				}
			}
			mesh.VertexCount = 0;
			mesh.IndexCount = 0;
			return mesh;
		}

		private static void ReleaseMesh(Mesh<Mesh3D.Vertex> mesh)
		{
			Window.Current.InvokeOnRendering(() => {
				lock (meshPool) {
					meshPool.Enqueue(mesh);
				}
			});
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner != null) {
				oldOwner.Presenter = DefaultPresenter.Instance;
				oldOwner.RenderChainBuilder = null;
			}
			if (Owner != null) {
				Owner.Presenter = presenter ??= new Presenter();
				Owner.RenderChainBuilder = Owner;
			}
		}

		public override void LateUpdate(float delta)
		{
			if (
				Shape == null || Shape.Vertices.Count != Shape.Us.Count
				|| Shape.Vertices.Count < 2 || Shape.Lines.Count % 2 == 1
				|| Shape.Lines.Count < 2
			) {
				presenter.Mesh = null;
			} else {
				Extrude();
				presenter.Mesh = mesh;
			}
			presenter.World = Owner.AsNode3D.GlobalTransform;
			presenter.Texture = Texture;
			presenter.CullMode = CullMode;
			presenter.Material.DiffuseColor = Owner.AsNode3D.GlobalColor;
		}

		private class Presenter : IPresenter
		{
			public Matrix44 World;
			public Mesh<Mesh3D.Vertex> Mesh;
			public ITexture Texture;
			public CommonMaterial Material = new CommonMaterial();
			public CullMode CullMode;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				if (Mesh == null) {
					return null;
				}
				var ro = RenderObjectPool<RenderObject>.Acquire();
				ro.Mesh = Mesh;
				ro.World = World;
				ro.Texture = Texture;
				ro.Material = Material;
				ro.Opaque = true;
				ro.CullMode = CullMode;
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args)
			{
				return DefaultPresenter.Instance.PartialHitTest(node, ref args);
			}

			private class RenderObject : Lime.RenderObject3D
			{
				public Matrix44 World;
				public Mesh<Mesh3D.Vertex> Mesh;
				public ITexture Texture;
				public CommonMaterial Material;
				public CullMode CullMode;

				public override void Render()
				{
					Renderer.PushState(RenderState.World | RenderState.CullMode);
					Renderer.CullMode = CullMode;
					Renderer.World = World;
					Material.DiffuseTexture = Texture;
					Material.Apply(0);
					Mesh.DrawIndexed(0, Mesh.IndexCount);
					Renderer.PopState();
				}
			}
		}
	}
}
