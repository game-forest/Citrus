namespace Lime.PolygonMesh.Topology
{
	public sealed class HalfEdgeTopologyModificator : ITopologyModificator
	{
		private readonly HalfEdgeTopology topology;

		public HalfEdgeTopologyModificator(HalfEdgeTopology topology)
		{
			this.topology = topology;
		}

		public void AddVertex(Vertex vertex)
		{
			topology.Vertices.Add(vertex);
			topology.Triangulator.AddVertex(topology.Vertices.Count - 1);
			topology.Invalidate();
			topology.Traverse();
		}

		public void RemoveVertex(int index, bool keepConstrainedEdges = false)
		{
			topology.Triangulator.RemoveVertex(index);
			topology.Vertices[index] = topology.Vertices[topology.Vertices.Count - 1];
			topology.Vertices.RemoveAt(topology.Vertices.Count - 1);
			if (!keepConstrainedEdges) {
				topology.Triangulator.DoNotKeepConstrainedEdges();
			}
			topology.Invalidate(index);
			System.Diagnostics.Debug.Assert(topology.HalfEdges.Count % 3 == 0);
			System.Diagnostics.Debug.Assert(topology.Triangulator.FullCheck());
		}

		public void TranslateVertex(int index, Vector2 positionDelta, bool modifyStructure)
		{
			if (modifyStructure) {
				topology.Triangulator.RemoveVertex(index);
			}

			var v = topology.Vertices[index];
			v.Pos += positionDelta;
			topology.Vertices[index] = v;

			if (modifyStructure) {
				topology.Triangulator.AddVertex(index);
				topology.Invalidate();
				topology.Traverse();
			}
		}

		public void TranslateVertexUV(int index, Vector2 uvDelta)
		{
			var v = topology.Vertices[index];
			v.UV1 += uvDelta;
			topology.Vertices[index] = v;
		}

		public void ConstrainEdge(int index0, int index1)
		{
			topology.InsertConstrainedEdge(index0, index1);
		}

		public void Concave(Vector2 position)
		{
			topology.Triangulator.TryConcave(position);
			topology.Invalidate();
		}
	}
}
