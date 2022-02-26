using Lime;
using System.Collections.Generic;
using System;
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

		private static MeshPool<Mesh3D.Vertex> meshPool = new MeshPool<Mesh3D.Vertex>(
			() => new Mesh<Mesh3D.Vertex> {
				AttributeLocations = new int[] {
					ShaderPrograms.Attributes.Pos1, ShaderPrograms.Attributes.Color1, ShaderPrograms.Attributes.UV1,
					ShaderPrograms.Attributes.BlendIndices, ShaderPrograms.Attributes.BlendWeights,
					ShaderPrograms.Attributes.Normal, ShaderPrograms.Attributes.Tangent,
				},
				DirtyFlags = MeshDirtyFlags.All,
			}
		);

		private Presenter presenter;
		private PooledMesh<Mesh3D.Vertex> mesh;
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
			mesh?.Release();
			mesh = meshPool.Acquire();
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
			mesh.Mesh.VertexCount = shapeVertexCount * edgeLoopCount;
			var triangleCount = Shape.Lines.Count * segmentCount;
			mesh.Mesh.IndexCount = triangleCount * 3;
			var indicies = mesh.Mesh.Indices == null || mesh.Mesh.Indices.Length < mesh.Mesh.IndexCount
				? new ushort[mesh.Mesh.IndexCount] : mesh.Mesh.Indices;
			var vertices = mesh.Mesh.Vertices == null || mesh.Mesh.Vertices.Length < mesh.Mesh.VertexCount
				? new Mesh3D.Vertex[mesh.Mesh.VertexCount] : mesh.Mesh.Vertices;
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
			mesh.Mesh.Indices = indicies;
			mesh.Mesh.Vertices = vertices;
			mesh.Mesh.DirtyFlags = MeshDirtyFlags.VerticesIndices;
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

		private class MeshPool<TVertex> where TVertex : unmanaged
		{
			private Stack<PooledMesh<TVertex>> freeMeshes = new Stack<PooledMesh<TVertex>>();
			private Func<Mesh<TVertex>> meshFactory;

			public MeshPool(Func<Mesh<TVertex>> meshFactory)
			{
				this.meshFactory = meshFactory;
			}

			public PooledMesh<TVertex> Acquire()
			{
				var m = freeMeshes.Count > 0
					? freeMeshes.Pop()
					: new PooledMesh<TVertex>(this, meshFactory());
				m.AddRef();
				return m;
			}

			internal void ReleaseInternal(PooledMesh<TVertex> m)
			{
				freeMeshes.Push(m);
			}
		}

		private class PooledMesh<TVertex> where TVertex : unmanaged
		{
			private MeshPool<TVertex> pool;
			private int refCount;

			public Mesh<TVertex> Mesh { get; }

			internal PooledMesh(MeshPool<TVertex> pool, Mesh<TVertex> mesh)
			{
				this.pool = pool;
				Mesh = mesh;
			}

			public void AddRef()
			{
				refCount++;
			}

			public void Release()
			{
				refCount--;
				if (refCount == 0) {
					pool.ReleaseInternal(this);
				}
			}
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
			public PooledMesh<Mesh3D.Vertex> Mesh;
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
				ro.Mesh.AddRef();
				return ro;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args)
			{
				return DefaultPresenter.Instance.PartialHitTest(node, ref args);
			}

			private class RenderObject : Lime.RenderObject3D
			{
				public Matrix44 World;
				public PooledMesh<Mesh3D.Vertex> Mesh;
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
					Mesh.Mesh.DrawIndexed(0, Mesh.Mesh.IndexCount);
					Renderer.PopState();
				}

				protected override void OnRelease()
				{
					Mesh.Release();
				}
			}
		}
	}
}
