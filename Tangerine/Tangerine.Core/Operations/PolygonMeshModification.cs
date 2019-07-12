using Lime;
using Lime.PolygonMesh;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tangerine.Core.Operations
{
	public static class PolygonMeshModification
	{
		public class Deform : Operation
		{
			public override bool IsChangingDocument => true;

			private PolygonMesh mesh;
			private Vector2 positionDelta;
			private Vector2 uvDelta;
			private int vertexIndex;

			private Deform(PolygonMesh mesh, Vector2 positionDelta, Vector2 uvDelta, int vertexIndex)
			{
				this.mesh = mesh;
				this.positionDelta = positionDelta;
				this.vertexIndex = vertexIndex;
				this.uvDelta = uvDelta;
			}

			public static void Perform(PolygonMesh mesh, Vector2 positionDelta, Vector2 uvDelta, int vertexIndex)
			{
				Document.Current.History.Perform(new Deform(mesh, positionDelta, uvDelta, vertexIndex));
			}

			public class Processor : OperationProcessor<Deform>
			{
				protected override void InternalRedo(Deform op)
				{
					var wasContextSwapped = false;
					if (op.mesh.CurrentContext == PolygonMesh.Context.Animation) {
						op.mesh.SwapContext();
						wasContextSwapped = true;
					}
					op.mesh.Geometry.MoveVertex(op.vertexIndex, op.positionDelta);
					op.mesh.Geometry.MoveVertexUv(op.vertexIndex, op.uvDelta);
					var v = op.mesh.Vertices[op.vertexIndex];
					v.UV1 = op.mesh.Geometry.Vertices[op.vertexIndex].UV1;
					op.mesh.Vertices[op.vertexIndex] = v;
					if (op.mesh.Animators.TryFind($"{nameof(mesh.Vertices)}", out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							v = (newKey.Value as List<Vertex>)[op.vertexIndex];
							v.UV1 = op.mesh.Geometry.Vertices[op.vertexIndex].UV1;
							(newKey.Value as List<Vertex>)[op.vertexIndex] = v;
							animator.Keys.AddOrdered(newKey);
							animator.ResetCache();
						}
						op.mesh.Animators.Invalidate();
					}
					if (wasContextSwapped) {
						op.mesh.SwapContext();
					}
				}

				protected override void InternalUndo(Deform op)
				{
					var wasContextSwapped = false;
					if (op.mesh.CurrentContext == PolygonMesh.Context.Animation) {
						op.mesh.SwapContext();
						wasContextSwapped = true;
					}
					op.mesh.Geometry.MoveVertex(op.vertexIndex, -op.positionDelta);
					op.mesh.Geometry.MoveVertexUv(op.vertexIndex, -op.uvDelta);
					var v = op.mesh.Vertices[op.vertexIndex];
					v.UV1 = op.mesh.Geometry.Vertices[op.vertexIndex].UV1;
					op.mesh.Vertices[op.vertexIndex] = v;
					if (op.mesh.Animators.TryFind($"{nameof(mesh.Vertices)}", out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							v = (newKey.Value as List<Vertex>)[op.vertexIndex];
							v.UV1 = op.mesh.Geometry.Vertices[op.vertexIndex].UV1;
							(newKey.Value as List<Vertex>)[op.vertexIndex] = v;
							animator.Keys.AddOrdered(newKey);
							animator.ResetCache();
						}
						op.mesh.Animators.Invalidate();
					}
					if (wasContextSwapped) {
						op.mesh.SwapContext();
					}
				}
			}
		}

		public class Create : Operation
		{
			public override bool IsChangingDocument => true;

			private PolygonMesh mesh;
			private Vertex animatedVertex;
			private Vertex deformedVertex;

			private Create(PolygonMesh mesh, Vertex animatedVertex, Vertex deformedVertex)
			{
				this.mesh = mesh;
				this.animatedVertex = animatedVertex;
				this.deformedVertex = deformedVertex;
			} 

			public static void Perform(PolygonMesh mesh, Vertex animatedVertex, Vertex deformedVertex)
			{
				Document.Current.History.Perform(new Create(mesh, animatedVertex, deformedVertex));
			}

			public class Processor : OperationProcessor<Create>
			{
				protected override void InternalRedo(Create op)
				{
					var wasContextSwapped = false;
					if (op.mesh.CurrentContext == PolygonMesh.Context.Animation) {
						op.mesh.SwapContext();
						wasContextSwapped = true;
					}
					op.mesh.Geometry.AddVertex(op.deformedVertex);
					if (op.mesh.Animators.TryFind($"{nameof(mesh.Vertices)}", out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							(newKey.Value as List<Vertex>).Add(op.animatedVertex);
							animator.Keys.AddOrdered(newKey);
							animator.ResetCache();
						}
						op.mesh.Animators.Invalidate();
					} else {
						op.mesh.Vertices.Add(op.animatedVertex);
					}
					if (wasContextSwapped) {
						op.mesh.SwapContext();
					}
				}

				protected override void InternalUndo(Create op)
				{
					var wasContextSwapped = false;
					if (op.mesh.CurrentContext == PolygonMesh.Context.Animation) {
						op.mesh.SwapContext();
						wasContextSwapped = true;
					}
					var i = op.mesh.Geometry.Vertices.Count - 1;
					op.mesh.Geometry.RemoveVertex(i);
					if (op.mesh.Animators.TryFind($"{nameof(mesh.Vertices)}", out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							(newKey.Value as List<Vertex>).RemoveAt(i);
							animator.Keys.AddOrdered(newKey);
							animator.ResetCache();
						}
						op.mesh.Animators.Invalidate();
					} else {
						op.mesh.Vertices.RemoveAt(i);
					}
					if (wasContextSwapped) {
						op.mesh.SwapContext();
					}
				}
			}
		}

		public class Constrain : Operation
		{
			public override bool IsChangingDocument => true;

			private PolygonMesh mesh;
			private int startIndex;
			private int endIndex;
			private bool value;

			private Constrain(PolygonMesh mesh, int startIndex, int endIndex, bool value)
			{
				this.mesh = mesh;
				this.startIndex = startIndex;
				this.endIndex = endIndex;
				this.value = value;
			}

			public static void Perform(PolygonMesh mesh, int startIndex, int endIndex, bool value = true)
			{
				Document.Current.History.Perform(new Constrain(mesh, startIndex, endIndex, value));
			}

			public class Processor : OperationProcessor<Constrain>
			{
				protected override void InternalRedo(Constrain op)
				{
					if (op.startIndex != op.endIndex && op.value) {
						((Geometry)op.mesh.Geometry).InsertConstrainedEdge(op.startIndex, op.endIndex);
					} else if (op.startIndex != op.endIndex) {
						var he = ((Geometry) op.mesh.Geometry).HalfEdges.First(i =>
								(i.Origin == op.startIndex &&
								 ((Geometry) op.mesh.Geometry).Next(i).Origin == op.endIndex))
							.Index;
						op.mesh.Geometry.SetConstrain(he, constrained: false);
						op.mesh.Geometry.MoveVertex(op.startIndex, Vector2.Zero);
					}
				}

				protected override void InternalUndo(Constrain op)
				{
					if (op.startIndex != op.endIndex && op.value) {
						var he = ((Geometry)op.mesh.Geometry).HalfEdges.First(i =>
								(i.Origin == op.startIndex &&
								 ((Geometry)op.mesh.Geometry).Next(i).Origin == op.endIndex) ||
								(i.Origin == op.endIndex &&
								 ((Geometry)op.mesh.Geometry).Next(i).Origin == op.startIndex))
							.Index;
						System.Diagnostics.Debug.Assert(((Geometry)op.mesh.Geometry).HalfEdges[he].Constrained);
						op.mesh.Geometry.SetConstrain(he, constrained: false);
						op.mesh.Geometry.MoveVertex(op.startIndex, Vector2.Zero);

					} else if (op.startIndex != op.endIndex) {
						((Geometry)op.mesh.Geometry).InsertConstrainedEdge(op.startIndex, op.endIndex);
					}
				}
			}
		}

		public class Remove : Operation
		{
			public override bool IsChangingDocument => true;

			private PolygonMesh mesh;
			private Vertex animatedVertex;
			private Vertex deformedVertex;
			private int vertexIndex;

			private Remove(PolygonMesh mesh, Vertex animatedVertex, Vertex deformedVertex, int vertexIndex)
			{
				this.mesh = mesh;
				this.animatedVertex = animatedVertex;
				this.deformedVertex = deformedVertex;
				this.vertexIndex = vertexIndex;
			}

			public static void Perform(PolygonMesh mesh, Vertex animatedVertex, Vertex deformedVertex, int vertexIndex)
			{
				Document.Current.History.Perform(new Remove(mesh, animatedVertex, deformedVertex, vertexIndex));
			}

			public class Processor : OperationProcessor<Remove>
			{
				protected override void InternalRedo(Remove op)
				{
					var wasContextSwapped = false;
					if (op.mesh.CurrentContext == PolygonMesh.Context.Animation) {
						op.mesh.SwapContext();
						wasContextSwapped = true;
					}
					var i = op.mesh.Vertices.Count - 1;
					op.mesh.Geometry.RemoveVertex(op.vertexIndex);
					op.mesh.Vertices[op.vertexIndex] = op.mesh.Vertices[i];
					op.mesh.IndexBuffer = op.mesh.Geometry.IndexBuffer;
					if (op.mesh.Animators.TryFind($"{nameof(mesh.Vertices)}", out var animator)) {
						using (Document.Current.History.BeginTransaction()) {
							foreach (var key in animator.Keys.ToList()) {
								var newKey = key.Clone();
								(newKey.Value as List<Vertex>).RemoveAt(i);
								animator.Keys.AddOrdered(newKey);
								animator.ResetCache();
							}
							op.mesh.Animators.Invalidate();
						}
					} else {
						op.mesh.Vertices.RemoveAt(i);
					}
					if (wasContextSwapped) {
						op.mesh.SwapContext();
					}
				}

				protected override void InternalUndo(Remove op)
				{
					var wasContextSwapped = false;
					if (op.mesh.CurrentContext == PolygonMesh.Context.Animation) {
						op.mesh.SwapContext();
						wasContextSwapped = true;
					}
					var i = op.mesh.Vertices.Count - 1;
					if (i == op.vertexIndex - 1) {
						op.mesh.Geometry.AddVertex(op.deformedVertex);
					} else {
						var v = op.mesh.Geometry.Vertices[op.vertexIndex];
						op.mesh.Geometry.MoveVertex(op.vertexIndex, op.deformedVertex.Pos - v.Pos);
						op.mesh.Geometry.AddVertex(v);
						op.mesh.Geometry.Vertices[op.vertexIndex] = op.deformedVertex;
						(op.mesh.Geometry as Geometry).Invalidate();
					}
					if (op.mesh.Animators.TryFind($"{nameof(mesh.Vertices)}", out var animator)) {
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var list = (newKey.Value as List<Vertex>);
							if (i == op.vertexIndex - 1) {
								list.Add(op.animatedVertex);
							} else {
								list.Add(list[op.vertexIndex]);
								list[op.vertexIndex] = op.animatedVertex;
							}
							animator.Keys.AddOrdered(newKey);
							animator.ResetCache();
						}
						op.mesh.Animators.Invalidate();
					} else {
						if (i == op.vertexIndex - 1) {
							op.mesh.Vertices.Add(op.animatedVertex);
						} else {
							op.mesh.Vertices.Add(op.mesh.Vertices[op.vertexIndex]);
							op.mesh.Vertices[op.vertexIndex] = op.animatedVertex;
						}
					}
					if (wasContextSwapped) {
						op.mesh.SwapContext();
					}
				}
			}
		}
	}
}
