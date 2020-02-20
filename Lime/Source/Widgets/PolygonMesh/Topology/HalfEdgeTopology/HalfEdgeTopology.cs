using System;
using System.Collections;
using System.Collections.Generic;
using Lime.PolygonMesh.Topology;

namespace Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology
{
	using Vertex = Lime.Vertex;
	public partial class HalfEdgeTopology : ITopology
	{

		public class HalfEdge
		{
			private bool constrained;
			public int Origin { get; internal set; }
			public HalfEdge Next { get; set; }
			public HalfEdge Prev => Next.Next;
			public HalfEdge Twin { get; private set; }

			public bool Constrained
			{
				get => constrained;
				set
				{
					constrained = value;
					if (Twin != null) {
						Twin.constrained = value;
					}
				}
			}

			public bool Detached => Next == null && Twin == null;
			public HalfEdge(int origin)
			{
				Origin = origin;
			}

			/// <summary>
			/// Removes edge from triangulation.
			/// </summary>
			public void Detach()
			{
				Next = null;
				if (Twin != null) {
					Twin.Twin = null;
				}
				Twin = null;
			}

			/// <summary>
			/// Twins with <paramref name="edge"/>
			/// </summary>
			/// <param name="edge">Edge to be twined with.</param>
			public void TwinWith(HalfEdge edge)
			{
				System.Diagnostics.Debug.Assert((edge.Next?.Origin ?? Origin) == Origin &&
												(Next?.Origin ?? edge.Origin) == edge.Origin);
				edge.Twin = this;
				Twin = edge;
				Constrained |= edge.Constrained;
			}

			public class HalfEdgesEnumerable : IEnumerable<HalfEdge>
			{
				private readonly HalfEdge root;

				/// <summary>
				/// Enumerates all HalfEdges that are reachable from specified <paramref name="root"/>
				/// </summary>
				/// <param name="root">Start point of enumeration.</param>
				public HalfEdgesEnumerable(HalfEdge root)
				{
					this.root = root;
				}

				public IEnumerator<HalfEdge> GetEnumerator()
				{
					return new Enumerator(root);
				}

				IEnumerator IEnumerable.GetEnumerator()
				{
					return GetEnumerator();
				}

				public class Enumerator : IEnumerator<HalfEdge>
				{
					private readonly HalfEdge root;
					private HalfEdge current;
					private Queue<HalfEdge> queue;
					private HashSet<HalfEdge> used;

					public Enumerator(HalfEdge root)
					{
						this.root = root;
						queue = new Queue<HalfEdge>();
						used = new HashSet<HalfEdge>();
					}

					public void Dispose()
					{
						current = null;
						queue.Clear();
					}

					// bfs
					public bool MoveNext()
					{
						if (current == null) {
							current = root;
							queue.Enqueue(root);
							queue.Enqueue(root.Next);
							queue.Enqueue(root.Prev);
						}
						while (queue.Count > 0) {
							current = queue.Dequeue();
							if (!used.Add(current)) {
								continue;
							}
							if (current.Twin != null) {
								var twin = current.Twin;
								var twinNext = twin.Next;
								queue.Enqueue(twin);
								queue.Enqueue(twinNext);
								queue.Enqueue(twinNext.Next);
							}
							return true;
						}
						return false;
					}

					public void Reset()
					{
						current = null;
					}

					public HalfEdge Current => current;
					object IEnumerator.Current => Current;
				}
			}
		}

		private HalfEdge FaceToHalfEdge(Face face)
		{
			var e = new HalfEdge(face[0]) { Next = new HalfEdge(face[1]) { Next = new HalfEdge(face[2]) } };
			e.Prev.Next = e;
			return e;
		}

		private HalfEdge Root { get; set; }

		public IEnumerable<(int, int)> ConstrainedEdges
		{
			get
			{
				foreach (var halfEdge in HalfEdges) {
					if (halfEdge.Constrained) {
						yield return (halfEdge.Origin, halfEdge.Next.Origin);
					}
				}
			}
		}

		public bool HitTest(
			Vector2 position,
			float vertexHitRadius,
			float edgeHitRadius,
			out TopologyHitTestResult result
		) {
			var location = LocateClosestTriangle(position, out var edge);
			var sqrVertexHitRadius = vertexHitRadius * vertexHitRadius;
			var sqrEdgeHitRadius = edgeHitRadius * edgeHitRadius;
			switch (location) {
				case LocationResult.SameVertex:
					result = new TopologyHitTestResult {
						Target = new Topology.Vertex { Index = (ushort)edge.Origin, },
					};
					return true;
				case LocationResult.OnEdge:
					result = new TopologyHitTestResult {
						Target = new Edge { Index0 = (ushort)edge.Origin, Index1 = (ushort)edge.Next.Origin, },
						Info = new Edge.EdgeInfo() { IsConstrained = edge.Constrained, IsFraming = edge.Twin == null, },
					};
					var v1 = Vertices[result.Target[0]].Pos;
					var v2 = Vertices[result.Target[1]].Pos;
					var i = ((v1 - position).SqrLength <= (v2 - position).SqrLength) ? 0 : 1;
					if (((i == 0 ? v1 : v2) - position).SqrLength <= sqrVertexHitRadius) {
						result = new TopologyHitTestResult {
							Target = new Topology.Vertex { Index = result.Target[i], },
						};
					}
					return true;
				default:
					var next = edge.Next;
					var prev = next.Next;
					result = new TopologyHitTestResult {
						Target = new Face {
							Index0 = (ushort)edge.Origin,
							Index1 = (ushort)next.Origin,
							Index2 = (ushort)prev.Origin,
						},
						Info = new Face.FaceInfo {
							IsConstrained0 = edge.Constrained,
							IsConstrained1 = next.Constrained,
							IsConstrained2 = prev.Constrained,
							IsFraming0 = edge.Twin == null,
							IsFraming1 = next.Twin == null,
							IsFraming2 = prev.Twin == null,
						}
					};
					break;
			}
			// Since using exact testing, we should check whether given position
			// lies in the closest triangle's vertex/edge proximity in order to prioritize
			// inbound primitives or correctly test position that is slightly outside of triangulation.

			/// TODO place inside <see cref="LocateClosestTriangle(Vector2, out HalfEdge)"/>
			var inbound = location == LocationResult.InsideTriangle;
			var faceInfo = result.Info as Face.FaceInfo;
			var target = result.Target;
			for (var i = 0; i < 3; ++i) {
				var v1 = Vertices[target[i]].Pos;
				var v2 = Vertices[target[(i + 1) % 3]].Pos;
				if ((v1 - position).SqrLength <= sqrVertexHitRadius) {
					inbound = true;
					result = new TopologyHitTestResult {
						Target = new Topology.Vertex { Index = target[i], },
					};
					break;
				}
				if (PointToSegmentSqrDistance(v1, v2, position) <= sqrEdgeHitRadius) {
					inbound = true;
					var edgeInfo = faceInfo?[i];
					result = new TopologyHitTestResult {
						Target = new Edge(target[i], target[(i + 1) % 3]),
						Info = new Edge.EdgeInfo {
							IsFraming = edgeInfo?.IsFraming ?? false,
							IsConstrained = edgeInfo?.IsConstrained ?? false,
						}
					};
				}
			}
			if (!inbound) {
				result = null;
			}
			return inbound;
		}

		// Stays public until we get LocateClosestTriangle work O(logN) (cause otherwise rendering will
		// be laggy).
		public IEnumerable<HalfEdge> HalfEdges => new HalfEdge.HalfEdgesEnumerable(Root);

		private IEnumerable<(HalfEdge, HalfEdge, HalfEdge)> Triangles()
		{
			var enumerator = HalfEdges.GetEnumerator();
			while (enumerator.MoveNext()) {
				var e1 = enumerator.Current;
				enumerator.MoveNext();
				var e2 = enumerator.Current;
				enumerator.MoveNext();
				var e3 = enumerator.Current;
				yield return (e1, e2, e3);
			}
			enumerator.Dispose();
		}

		public List<Vertex> Vertices { get; private set; }

		public HalfEdgeTopology()
		{
			Vertices = new List<Vertex>();
		}

		public HalfEdgeTopology(List<Vertex> vertices)
		{

			Vertices = vertices;
			var e1 = new HalfEdge(0) { Next = new HalfEdge(1) { Next = new HalfEdge(2) } };
			e1.Prev.Next = e1;
			var e2 = new HalfEdge(2) { Next = new HalfEdge(1) { Next = new HalfEdge(3) } };
			e2.Prev.Next = e2;
			e2.TwinWith(e1.Next);
			Root = e1;
		}

		public void Sync(List<Vertex> vertices, List<Edge> constrainedEdges, List<Face> faces)
		{
			Vertices = vertices;
			// (vertex, vertex) -> HalfEdge
			// Used to restore connection between half edges.
			// Simple bfs doesn't work because there exist multiple paths
			// to reach some triangle
			HalfEdge[,] table = new HalfEdge[vertices.Count, vertices.Count];
			foreach (var face in faces) {
				var current = Root = FaceToHalfEdge(face);
				do {
					table[current.Origin, current.Next.Origin] = current;
					var possibleTwin = table[current.Next.Origin, current.Origin];
					possibleTwin?.TwinWith(current);
					current = current.Next;
				} while (current != Root);
			}

			foreach (var edge in constrainedEdges) {
				var he = table[edge.Index0, edge.Index1];
				if (he != null) {
					he.Constrained = true;
				}
			}
		}

		public IEnumerable<Face> Faces
		{
			get
			{
				foreach (var (e1, e2, e3) in Triangles()) {
					yield return new Face { Index0 = (ushort)e1.Origin, Index1 = (ushort)e2.Origin, Index2 = (ushort)e3.Origin, };
				}
			}
		}

#if TANGERINE
		public IEnumerable<(Face, Face.FaceInfo)> FacesWithInfo
		{
			get
			{
				foreach (var (e1, e2, e3) in Triangles()) {
					yield return (
						new Face {
							Index0 = (ushort)e1.Origin,
							Index1 = (ushort)e2.Origin,
							Index2 = (ushort)e3.Origin,
						},
						new Face.FaceInfo {
							IsConstrained0 = e1.Constrained,
							IsConstrained1 = e2.Constrained,
							IsConstrained2 = e3.Constrained,
							IsFraming0 = e1.Twin == null,
							IsFraming1 = e2.Twin == null,
							IsFraming2 = e3.Twin == null,
						});
				}
			}
		}
#endif

		public event Action<ITopology> OnTopologyChanged;

#if TANGERINE
		public void EmplaceVertices(List<Vertex> vertices) => Vertices = vertices;
#endif

		public void AddVertex(Vertex vertex)
		{
			Vertices.Add(vertex);
			AddVertex(Vertices.Count - 1);
			OnTopologyChanged?.Invoke(this);
		}

		public void RemoveVertex(int index, bool keepConstrainedEdges = false)
		{
			var isolatedVertices = RemoveVertex(index);
			isolatedVertices.Sort((lhs, rhs) => rhs - lhs);
			var map = new List<int>(Vertices.Count);
			for (int i = 0; i < Vertices.Count; i++) {
				map.Add(i);
			}
			foreach (var i in isolatedVertices) {
				Toolbox.Swap(Vertices, i, Vertices.Count - 1);
				Toolbox.Swap(map, i, Vertices.Count - 1);
				// Shorten a path to the length of 1
				map[map[i]] = i;
				Vertices.RemoveAt(Vertices.Count - 1);
			}
			foreach (var he in HalfEdges) {
				he.Origin = map[he.Origin];
			}
			OnTopologyChanged?.Invoke(this);
		}

		public void TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta)
		{
			LocateClosestTriangle(index, out var he);
			var original = Vertices[index];
			var translated = original;
			translated.Pos += positionDelta;
			translated.UV1 += uvDelta;
			if (
				TryFindBorderEdge(he, out var borderEdge) &&
				(borderEdge.Origin == index || borderEdge.Next.Origin == index)
			) {
				// If it's boundary vertex than triangle bases determine
				// vertex translation constrains
				var p = GetBoundingPolygon(index, borderEdge);
				var intersectsAny = false;
				for (int i = 1; i < p.Count - 1; i++) {
					var edge = p[i];
					var s1 = Vertices[edge.Origin].Pos;
					var s2 = Vertices[edge.Next.Origin].Pos;
					if (ArePointsOnOppositeSidesOfSegment(s1, s2, translated.Pos, original.Pos)) {
						intersectsAny = true;
						break;
					}
				}
				if (!intersectsAny) {
					Vertices[index] = translated;
					OnTopologyChanged?.Invoke(this);
				}
			} else {
				// Otherwise just delete original and add translated.
				// Don't forget to save constrained edges.
				var constrainedEdges = new List<(int, int)>();
				foreach (var adjacent in AdjacentEdges(he)) {
					if (adjacent.Constrained) {
						constrainedEdges.Add((adjacent.Origin, adjacent.Next.Origin));
					}
				}
				RemoveVertex(index);
				Vertices[index] = translated;
				AddVertex(index);
				foreach (var edge in constrainedEdges) {
					InsertConstrainEdge(edge.Item1, edge.Item2);
				}
				OnTopologyChanged?.Invoke(this);
			}
		}

		public void ConstrainEdge(int index0, int index1)
		{
			InsertConstrainEdge(index0, index1);
			OnTopologyChanged?.Invoke(this);
		}

		public void Concave(Vector2 position)
		{
			throw new NotImplementedException();
		}

		private bool SelfCheck()
		{
			foreach (var (e1, e2, e3) in Triangles()) {
				if (!IsDelaunay(e1) || !IsDelaunay(e2) || !IsDelaunay(e3)) {
					return false;
				}
			}
			return true;
		}
	}
}
