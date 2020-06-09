using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lime.PolygonMesh.Topology;
using SkinnedVertex = Lime.Widgets.PolygonMesh.PolygonMesh.SkinnedVertex;

namespace Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology
{
	public partial class HalfEdgeTopology : ITopology
	{
		private class HalfEdge
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

		private class VerticesIndexer
		{
			private readonly List<SkinnedVertex> boundingFigureVertices;
			private readonly List<SkinnedVertex> vertices;

			public VerticesIndexer(List<SkinnedVertex> boundingFigureVertices, List<SkinnedVertex> vertices)
			{
				this.boundingFigureVertices = boundingFigureVertices;
				this.vertices = vertices;
			}

			public SkinnedVertex this[int index] => index < 0 ? boundingFigureVertices[index * -1] : vertices[index];
		}

		private class Boundary : IEnumerable<int>
		{
			private readonly LinkedList<int> boundary = new LinkedList<int>();
			private readonly Dictionary<int, LinkedListNode<int>> vertexIndexToBoundaryIndex = new Dictionary<int, LinkedListNode<int>>();

			public int Count => boundary.Count;

			public void Add(int vertexIndex)
			{
				System.Diagnostics.Debug.Assert(vertexIndex >= 0);
				if (!vertexIndexToBoundaryIndex.ContainsKey(vertexIndex)) {
					boundary.AddLast(vertexIndex);
					vertexIndexToBoundaryIndex.Add(vertexIndex, boundary.Last);
				}
			}

			public void Remove(int boundaryVertexIndex)
			{
				if (vertexIndexToBoundaryIndex.TryGetValue(boundaryVertexIndex, out var n)) {
					boundary.Remove(n);
					vertexIndexToBoundaryIndex.Remove(boundaryVertexIndex);
				}
			}

			public void Insert(int boundaryVertexIndex, int vertexIndex)
			{
				System.Diagnostics.Debug.Assert(vertexIndex >= 0);
				if (vertexIndexToBoundaryIndex.ContainsKey(vertexIndex)) {
					throw new InvalidOperationException();
				}
				var n = vertexIndexToBoundaryIndex[boundaryVertexIndex];
				boundary.AddAfter(n, vertexIndex);
				vertexIndexToBoundaryIndex.Add(vertexIndex, n.Next);
			}

			public void Clear()
			{
				vertexIndexToBoundaryIndex.Clear();
				boundary.Clear();
			}

			public bool Contains(int vertexIndex) => vertexIndexToBoundaryIndex.ContainsKey(vertexIndex);

			public bool ContainsEdge(int index0, int index1) => Contains(index0) && Next(index0) == index1;

			public int Next(int boundaryVertexIndex) =>
				vertexIndexToBoundaryIndex.TryGetValue(boundaryVertexIndex, out var n) ? (n.Next ?? boundary.First).Value : -1;

			public int Prev(int boundaryVertexIndex) =>
				vertexIndexToBoundaryIndex.TryGetValue(boundaryVertexIndex, out var n) ? (n.Previous ?? boundary.Last).Value : -1;

			public void Remap(List<int> map)
			{
				var current = boundary.First;
				while (current != null) {
					vertexIndexToBoundaryIndex.Remove(current.Value);
					current.Value = map[current.Value];
					vertexIndexToBoundaryIndex.Add(current.Value, current);
					current = current.Next;
				}
			}

			public IEnumerator<int> GetEnumerator() => boundary.GetEnumerator();

			IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

			public IEnumerable<(int, int)> Edges()
			{
				var current = boundary.First;
				while (current != boundary.Last) {
					yield return (current.Value, current.Next.Value);
					current = current.Next;
				}
				yield return (current.Value, boundary.First.Value);
			}
		}

		private HalfEdge FaceToHalfEdge(Face face)
		{
			var e = new HalfEdge(face[0]) { Next = new HalfEdge(face[1]) { Next = new HalfEdge(face[2]) } };
			e.Prev.Next = e;
			return e;
		}

		private HalfEdge Root { get; set; }
		private List<SkinnedVertex> BoundingFigureVertices { get; set; }
		// Vertices + bounding figure vertices.
		private VerticesIndexer InnerVertices { get; set; }
		// A boundary of `true` triangulation.
		private Boundary InnerBoundary { get; set; }
		private Rectangle BoundingBox { get; set; }

		public IEnumerable<(int, int)> ConstrainedEdges
		{
			get
			{
				foreach (var halfEdge in HalfEdges) {
					if (halfEdge.Constrained && halfEdge.Origin >= 0 && halfEdge.Next.Origin >= 0) {
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
			result = null;
			switch (location) {
				case LocationResult.SameVertex:
					if (edge.Origin < 0) {
						return false;
					}
					result = new TopologyHitTestResult {
						Target = new Vertex { Index = (ushort)edge.Origin, },
					};
					break;
				case LocationResult.OnEdge:
					if (HitVertex(edge.Origin, out result) || HitVertex(edge.Next.Origin, out result)) {
						return true;
					}
					var belongsToInnerTriangle = IsPointInsideTrueTriangulation(Centroid(edge));
					var twinBelongsToInnerTriangle = !belongsToInnerTriangle &&
						edge.Twin != null && IsPointInsideTrueTriangulation(Centroid(edge.Twin));
					if (belongsToInnerTriangle || twinBelongsToInnerTriangle) {
						edge = belongsToInnerTriangle ? edge : edge.Twin;
						result = new TopologyHitTestResult {
							Target = new Edge((ushort)edge.Origin, (ushort)edge.Next.Origin),
							Info = new Edge.EdgeInfo {
								IsConstrained = edge.Constrained,
								IsFraming = edge.Twin == null || InnerBoundary.Contains(edge.Origin) &&
											InnerBoundary.Next(edge.Origin) == edge.Next.Origin,
							},
						};
					}
					break;
				case LocationResult.InsideTriangle:
				case LocationResult.OutsideTriangulation:
					if (
						HitVertex(edge.Origin, out result) || HitVertex(edge.Next.Origin, out result) ||
						HitVertex(edge.Prev.Origin, out result)
					) {
						return true;
					}
					var belongsToTriangulation = location != LocationResult.OutsideTriangulation &&
						IsPointInsideTrueTriangulation(position);
					var start = edge;
					do {
						var twinBelongsToTriangulation = belongsToTriangulation ||
							edge.Twin != null && IsPointInsideTrueTriangulation(Centroid(edge.Twin));
						if (belongsToTriangulation || twinBelongsToTriangulation) {
							var edgeToCheck = belongsToTriangulation ? edge : edge.Twin;
							var s1 = Vertices[edgeToCheck.Origin].Pos;
							var s2 = Vertices[edgeToCheck.Next.Origin].Pos;
							if (PointToSegmentSqrDistance(s1, s2, position) <= edgeHitRadius * edgeHitRadius) {
								result = new TopologyHitTestResult {
									Target = new Edge((ushort)edgeToCheck.Origin, (ushort)edgeToCheck.Next.Origin),
									Info = new Edge.EdgeInfo {
										IsConstrained = edgeToCheck.Constrained,
										IsFraming = edgeToCheck.Twin == null || InnerBoundary.Contains(edgeToCheck.Origin) &&
													InnerBoundary.Next(edgeToCheck.Origin) == edgeToCheck.Next.Origin,
									},
								};
								return true;
							}
						}
						edge = edge.Next;
					} while (edge != start);
					if (belongsToTriangulation) {
						result = new TopologyHitTestResult {
							Target = new Face((ushort)edge.Origin, (ushort)edge.Next.Origin, (ushort)edge.Prev.Origin),
							Info = CreateFaceInfo(edge),
						};
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
			bool HitVertex(int index, out TopologyHitTestResult r)
			{
				var didHit = index >= 0 && (Vertices[index].Pos - position).SqrLength <= vertexHitRadius * vertexHitRadius;
				r = didHit ? new TopologyHitTestResult { Target = new Vertex { Index = (ushort)index, }, } : null;
				return didHit;
			}
			return result != null;
		}

		private Vector2 Centroid(HalfEdge triangle)
		{
			var v1 = InnerVertices[triangle.Origin].Pos;
			var v2 = InnerVertices[triangle.Next.Origin].Pos;
			var v3 = InnerVertices[triangle.Prev.Origin].Pos;
			return (v1 + v2 + v3) / 3;
		}

		private IEnumerable<HalfEdge> HalfEdges => new HalfEdge.HalfEdgesEnumerable(Root);

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

		private IEnumerable<(HalfEdge, HalfEdge, HalfEdge)> InnerTriangles()
		{
			HalfEdge start = null;
			foreach (var (e1, e2, e3) in Triangles()) {
				if (e1.Origin >= 0 && e2.Origin >= 0 && e3.Origin >= 0) {
					var v1 = Vertices[e1.Origin].Pos;
					var v2 = Vertices[e2.Origin].Pos;
					var v3 = Vertices[e3.Origin].Pos;
					var centroid = (v1 + v2 + v3) / 3;
					if (IsPointInsideTrueTriangulation(centroid)) {
						start = e1;
						break;
					}
				}
			}
			if (start == null) {
				yield break;
			}
			var queue = new Queue<HalfEdge>();
			var used = new HashSet<HalfEdge>();
			queue.Enqueue(start);
			queue.Enqueue(start.Next);
			queue.Enqueue(start.Prev);
			while (queue.Count > 0) {
				var e1 = queue.Dequeue();
				var e2 = queue.Dequeue();
				var e3 = queue.Dequeue();
				if (!used.Add(e1) || !used.Add(e2) || !used.Add(e3)) {
					continue;
				}
				yield return (e1, e2, e3);
				UpdateQueue(e1);
				UpdateQueue(e2);
				UpdateQueue(e3);
			}

			void UpdateQueue(HalfEdge e)
			{
				if (
					e.Twin != null &&
					!(InnerBoundary.Contains(e.Origin) && InnerBoundary.Next(e.Origin) == e.Next.Origin)
				) {
					var twin = e.Twin;
					queue.Enqueue(twin);
					queue.Enqueue(twin.Next);
					queue.Enqueue(twin.Prev);
				}
			}
		}

		public List<SkinnedVertex> Vertices { get; private set; }
		public float VertexHitTestRadius { get; set; }
		public float EdgeHitTestDistance { get; set; }

		public HalfEdgeTopology()
		{
			Vertices = new List<SkinnedVertex>();
		}

		public HalfEdgeTopology(List<SkinnedVertex> vertices)
		{
			// TODO: fix this constructor after reimplementation (if it'll happen).
			Vertices = vertices;
			var e1 = new HalfEdge(0) {
				Next = new HalfEdge(1) { Next = new HalfEdge(2), },
				Constrained = true,
			};
			e1.Prev.Constrained = true;
			e1.Prev.Next = e1;
			var e2 = new HalfEdge(2) {
				Next = new HalfEdge(1) {
					Next = new HalfEdge(3),
					Constrained = true,
				},
			};
			e2.Prev.Constrained = true;
			e2.Prev.Next = e2;
			e2.TwinWith(e1.Next);
			BoundingFigureVertices = new List<SkinnedVertex>(Vertices.Take(4));
			// 0 doesn't have a sign, so this is a hack to mark bounding figure's vertices
			// with negative indices.
			BoundingFigureVertices.Insert(0, new SkinnedVertex { Pos = new Vector2(float.NegativeInfinity), });
			InnerVertices = new VerticesIndexer(BoundingFigureVertices, Vertices);
			InnerBoundary = new Boundary { 0, 1, 3, 2, };
			Root = e1;
			BoundingBox = new Rectangle(0f, 0f, 1f, 1f);
		}

		public void Sync(List<SkinnedVertex> vertices, List<Edge> constrainedEdges, List<Face> faces)
		{
			Vertices = vertices;
			// (vertex, vertex) -> HalfEdge
			// Used to restore connection between half edges.
			// Simple bfs doesn't work because there exist multiple paths
			// to reach some triangle
			HalfEdge[,] table = new HalfEdge[vertices.Count, vertices.Count];
			var usedBoundingFigureVertices = new bool[BoundingFigureVertices.Count];
			foreach (var face in faces) {
				var current = Root = FaceToHalfEdge(face);
				for (int i = 0; i < 3; i++) {
					if (BelongsToBoundingFigure(face[i], out var bfvi)) {
						usedBoundingFigureVertices[bfvi] = true;
					}
				}
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
			HalfEdge start = null;
			foreach (var edge in HalfEdges) {
				if (edge.Twin == null) {
					start = edge;
					break;
				}
			}
			InnerBoundary.Clear();
			var c = start;
			do {
				InnerBoundary.Add(c.Origin);
				c = NextBorderEdge(c);
			} while (c != start);
			for (int i = 1; i < usedBoundingFigureVertices.Length; i++) {
				if (!usedBoundingFigureVertices[i]) {
					AddVertex(-i);
				}
			}
			BoundingBox = Rectangle.Empty;
			foreach (var vertex in Vertices) {
				BoundingBox = BoundingBox.IncludingPoint(vertex.Pos);
			}
			ToConvexHull();
		}

		public IEnumerable<Face> Faces
		{
			get
			{
				foreach (var (e1, e2, e3) in InnerTriangles()) {
					yield return new Face { Index0 = (ushort) e1.Origin, Index1 = (ushort) e2.Origin, Index2 = (ushort) e3.Origin, };
				}
			}
		}

#if TANGERINE
		public IEnumerable<(Face, Face.FaceInfo)> FacesWithInfo
		{
			get
			{
				foreach (var (e1, e2, e3) in InnerTriangles()) {
					yield return (
						new Face {
							Index0 = (ushort)e1.Origin,
							Index1 = (ushort)e2.Origin,
							Index2 = (ushort)e3.Origin,
						},
						CreateFaceInfo(e1, e2, e3)
					);
				}
			}
		}
#endif

		private Face.FaceInfo CreateFaceInfo(HalfEdge triangle) =>
			CreateFaceInfo(triangle, triangle.Next, triangle.Prev);

		private Face.FaceInfo CreateFaceInfo(HalfEdge e1, HalfEdge e2, HalfEdge e3) =>
			new Face.FaceInfo {
				IsConstrained0 = e1.Constrained,
				IsConstrained1 = e2.Constrained,
				IsConstrained2 = e3.Constrained,
				IsFraming0 = e1.Twin == null || InnerBoundary.Contains(e1.Origin) && InnerBoundary.Next(e1.Origin) == e2.Origin,
				IsFraming1 = e2.Twin == null || InnerBoundary.Contains(e2.Origin) && InnerBoundary.Next(e2.Origin) == e3.Origin,
				IsFraming2 = e3.Twin == null || InnerBoundary.Contains(e3.Origin) && InnerBoundary.Next(e3.Origin) == e1.Origin,
			};

		public event Action<ITopology> TopologyChanged;

		public event Action<ITopology> OnTopologyChanged
		{
			add
			{
				TopologyChanged += value;
				value(this);
			}
			remove => TopologyChanged -= value;
		}

		public void AddVertex(SkinnedVertex vertex)
		{
			Vertices.Add(vertex);
			if (AddVertex(Vertices.Count - 1)) {
				TopologyChanged?.Invoke(this);
			} else {
				Vertices.RemoveAt(Vertices.Count - 1);
			}

		}

		public void RemoveVertex(int index)
		{
			var pos = Vertices[index].Pos;
			var bfvi = BoundingFigureVertices.FindIndex(vertex => vertex.Pos == pos);
			if (bfvi >= 0) {
				LocateClosestTriangle(index, out var e);
				// if vertex is belongs to bounding figure than it is definitely belongs to InnerBoundary.
				RemoveVertexFromBoundary(index, e);
				foreach (var incidentEdge in IncidentEdges(e)) {
					incidentEdge.Origin = -bfvi;
				}
				FixupTopologyAfterVerticesRemoval(new List<int> { index, });
			} else {
				if (InnerBoundary.Contains(index)) {
					RemoveVertexFromBoundary(index);
				}
				var isolatedVertices = InnerRemoveVertex(index);
				FixupTopologyAfterVerticesRemoval(isolatedVertices);
			}

			TopologyChanged?.Invoke(this);
		}

		private void RemoveVertexFromBoundary(int vertexIndex, HalfEdge incidentEdge = null)
		{
			if (incidentEdge == null) {
				LocateClosestTriangle(vertexIndex, out incidentEdge);
			}
			var prev = InnerBoundary.Prev(vertexIndex);
			var prevBorderVertex = InnerVertices[prev].Pos;
			var next = InnerBoundary.Next(vertexIndex);
			var last = next;
			var areCwOrdered = AreClockwiseOrdered(InnerVertices[prev].Pos, InnerVertices[vertexIndex].Pos, InnerVertices[next].Pos);
			if (AreClockwiseOrdered(InnerVertices[prev].Pos, InnerVertices[vertexIndex].Pos, InnerVertices[next].Pos)) {
				// Ensure that `incidentEdge` connects with next.
				foreach (var incident in IncidentEdges(incidentEdge)) {
					if (incident.Next.Origin == next) {
						incidentEdge = incident;
						break;
					}
				}
				// Then we iterate from next to prev cw (that will ensure that only vertices inside `true` triangulation will be chosen)
				foreach (var incident in IncidentEdges(incidentEdge)) {
					if (incident.Next.Origin == prev) {
						break;
					}
					if (
						areCwOrdered && incident.Next.Origin != next && incident.Origin != prev &&
						AreClockwiseOrdered(prevBorderVertex, InnerVertices[incident.Next.Origin].Pos, InnerVertices[last].Pos)
					) {
						InnerBoundary.Insert(vertexIndex, incident.Next.Origin);
						last = incident.Next.Origin;
					}
				}
			}
			InnerBoundary.Remove(vertexIndex);
			var current = next;
			while (current != prev) {
				var p = InnerBoundary.Prev(current);
				InsertConstrainEdge(current, p);
				current = p;
			}
		}

		private void FixupTopologyAfterVerticesRemoval(List<int> removedVertices)
		{
			if (removedVertices.Count == 0) {
				return;
			}
			removedVertices.Sort((lhs, rhs) => rhs - lhs);
			var map = new List<int>(Vertices.Count);
			for (int i = 0; i < Vertices.Count; i++) {
				map.Add(i);
			}
			foreach (var i in removedVertices) {
				Toolbox.Swap(Vertices, i, Vertices.Count - 1);
				Toolbox.Swap(map, i, Vertices.Count - 1);
				// Shorten a path to the length of 1
				map[map[i]] = i;
				Vertices.RemoveAt(Vertices.Count - 1);
			}
			foreach (var he in HalfEdges) {
				if (he.Origin >= 0) {
					he.Origin = map[he.Origin];
				}
			}
			InnerBoundary.Remap(map);
		}

		public bool TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta)
		{
			LocateClosestTriangle(index, out var he);
			var original = Vertices[index];
			var originalPos = original.Pos;
			var translated = original;
			translated.Pos += positionDelta;
			translated.UV1 += uvDelta;
			var translatedPos = translated.Pos;
			// Check if translated vertex is contained inside bounding figure.
			if (translatedPos.Y < 0f || translatedPos.Y > 1f || translatedPos.X < 0f || translatedPos.X > 1f) {
				return false;
			}
			List<(int, int)> constrainedEdges = null;
			var isBoundaryVertex = InnerBoundary.Contains(index);
			int prevIndex = -1;
			if (isBoundaryVertex) {
				// Check that new border edges don't intersect boundary.
				prevIndex = InnerBoundary.Prev(index);
				var nextIndex = InnerBoundary.Next(index);
				var prev = Vertices[prevIndex].Pos;
				var next = Vertices[nextIndex].Pos;
				foreach (var (s, e) in InnerBoundary.Edges()) {
					var sp = Vertices[s].Pos;
					var ep = Vertices[e].Pos;
					if (
							!(s == prevIndex && e == index || s == index && e == nextIndex) &&
							(s != nextIndex && RobustSegmentSegmentIntersection(translatedPos, next, sp, ep) ||
							 e != prevIndex && RobustSegmentSegmentIntersection(prev, translatedPos, sp, ep))
					) {
						return false;
					}
				}
				var isLeftDegenerated = Orient2D(prev,	 originalPos, translatedPos) == 0;
				var isRightDegenerated = Orient2D(next,  translatedPos, originalPos) == 0;
				var isTriangleDegenerated = Orient2D(prev,	 translatedPos, next) == 0;
				var isAngleConvex = AreClockwiseOrdered(prev, originalPos, next);
				var mustBeRemoved = new List<int>();
				for (int i = 0; i < Vertices.Count; i++) {
					var v = Vertices[i].Pos;
					if (
							i != index && i != nextIndex && i != prevIndex &&
									(isAngleConvex && (!isLeftDegenerated && VertexInsideTriangle(v, prev, originalPos, translatedPos) ||
											!isRightDegenerated && VertexInsideTriangle(v, originalPos, next, translatedPos)) ||
									!isAngleConvex && !isTriangleDegenerated && VertexInsideTriangle(v, prev, translatedPos, next))
					) {
						mustBeRemoved.Add(i);
					}
				}
				foreach (var vertex in mustBeRemoved) {
					// Removing inside-triangulation vertex doesn't make any other vertex isolated.
					InnerRemoveVertex(vertex);
				}
				constrainedEdges = new List<(int, int)> { (prevIndex, index), };
				FixupTopologyAfterVerticesRemoval(mustBeRemoved);
				LocateClosestTriangle(originalPos, out he);
				index = he.Origin;
			} else if (!IsPointInsideTrueTriangulation(translatedPos)) {
				return false;
			}
			// Otherwise just delete original and add translated.
			// Don't forget to save constrained edges.
			var bfvi = BoundingFigureVertices.FindIndex(vertex => vertex.Pos == originalPos);
			constrainedEdges = constrainedEdges ?? new List<(int, int)>();
			foreach (var incident in IncidentEdges(he)) {
				if (incident.Constrained) {
					constrainedEdges.Add((incident.Origin, incident.Next.Origin));
				}
				if (bfvi >= 0) {
					incident.Origin = -bfvi;
				}
			}
			if (bfvi < 0) {
				InnerRemoveVertex(index);
			}
			Vertices[index] = translated;
			// TODO Check translation on another vertex.
			var wasAdded = AddVertex(index);
			if (!wasAdded) {
				var savedVertexHitTestRadius = VertexHitTestRadius;
				var savedEdgeHitTestDistance = EdgeHitTestDistance;
				VertexHitTestRadius = EdgeHitTestDistance = 0f;
				Vertices[index] = original;
				var returnedOriginal = AddVertex(index);
				System.Diagnostics.Debug.Assert(returnedOriginal);
				VertexHitTestRadius = savedVertexHitTestRadius;
				EdgeHitTestDistance = savedEdgeHitTestDistance;
			}
			foreach (var edge in constrainedEdges) {
				InsertConstrainEdge(edge.Item1, edge.Item2);
			}
			if (isBoundaryVertex && !InnerBoundary.Contains(index)) {
				InnerBoundary.Insert(prevIndex, index);
			}
			TopologyChanged?.Invoke(this);
			return wasAdded;
		}

		public void ConstrainEdge(int index0, int index1)
		{
			if (index0 == index1) {
				return;
			}
			InsertConstrainEdge(index0, index1);
			TopologyChanged?.Invoke(this);
		}

		public void DeconstrainEdge(int index0, int index1)
		{
			if (index0 == index1 || InnerBoundary.ContainsEdge(index0, index1)) {
				return;
			}
			var location = LocateClosestTriangle(index0, out var start);
			System.Diagnostics.Debug.Assert(location == LocationResult.SameVertex);
			System.Diagnostics.Debug.Assert(start.Origin == index0);
			var a = InnerVertices[index0].Pos;
			var b = InnerVertices[index1].Pos;
			var ab = b - a;
			var signab = new IntVector2(Mathf.Sign(ab.X), Mathf.Sign(ab.Y));
			var en = IncidentEdges(start).GetEnumerator();
			// Needs to resolve degenerated case when there is some vertex v
			// that lies on the line between a and b vertices
			// (edges (a, v) and (a, v) becomes constrained see InsertConstrainEdge).
			var path = new List<HalfEdge>();
			while (en.MoveNext()) {
				var incidentEdge = en.Current;
				var next = incidentEdge.Next;
				var prev = next.Next;
				if (next.Origin == index1 || prev.Origin == index1) {
					var edge = next.Origin == index1 ? incidentEdge : prev;
					if (!edge.Constrained) {
						return;
					}
					foreach (var halfEdge in path) {
						halfEdge.Constrained = false;
					}
					edge.Constrained = false;
					path.Add(edge);
					RestoreDelaunayProperty(path);
					TopologyChanged?.Invoke(this);
					break;
				}
				var c = InnerVertices[next.Origin].Pos;
				var d = InnerVertices[prev.Origin].Pos;
				var e = InnerVertices[incidentEdge.Origin].Pos;
				if (IsVertexOnLine(a, e, c) && IsVertexOnLine(b, e, c)) {
					var ec = c - e;
					if (Mathf.Sign(ec.X) == signab.X && Mathf.Sign(ec.Y) == signab.Y) {
						if (!incidentEdge.Constrained) {
							return;
						}
						path.Add(incidentEdge);
						en.Dispose();
						en = IncidentEdges(next).GetEnumerator();
					}

				} else if (IsVertexOnLine(a, e, d) && IsVertexOnLine(b, e, d)) {
					var ed = d - e;
					if (Mathf.Sign(ed.X) == signab.X && Mathf.Sign(ed.Y) == signab.Y) {
						if (!prev.Constrained) {
							return;
						}
						path.Add(prev);
						en.Dispose();
						en = IncidentEdges(prev).GetEnumerator();
					}
				}
			}
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

		private bool BelongsToBoundingFigure(Vector2 vertex, out int boundingFigureVertexIndex)
		{
			for (int i = 1; i < BoundingFigureVertices.Count; i++) {
				if (BoundingFigureVertices[i].Pos == vertex) {
					boundingFigureVertexIndex = i;
					return true;
				}
			}
			boundingFigureVertexIndex = -1;
			return false;
		}

		private bool BelongsToBoundingFigure(int vertexIndex, out int boundingFigureVertexIndex) =>
			BelongsToBoundingFigure(InnerVertices[vertexIndex].Pos, out boundingFigureVertexIndex);

		private bool IsPointInsideTrueTriangulation(Vector2 point)
		{
			bool inside = false;
			var rayStart = new Vector2(BoundingBox.Left, point.Y);
			foreach (var i in InnerBoundary) {
				var current = Vertices[i].Pos;
				var next = Vertices[InnerBoundary.Next(i)].Pos;
				if (RobustSegmentSegmentIntersection(rayStart, point, current, next)) {
					if (current.Y == point.Y && point.Y > next.Y) {
						inside = !inside;
					} else if (next.Y == point.Y && point.Y > current.Y) {
						inside = !inside;
					} else if (current.Y != point.Y && next.Y != point.Y) {
						inside = !inside;
					}
				}
			}
			return inside;
		}
	}
}
