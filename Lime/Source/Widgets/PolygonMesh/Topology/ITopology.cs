using System;
using System.Collections.Generic;
using Lime.Widgets.PolygonMesh.Topology;
using Vertex = Lime.Vertex;

namespace Lime.PolygonMesh.Topology
{
	public class TopologyHitTestResult
	{
		public ITopologyPrimitive Target;
		public object Info;
	}

	public interface ITopology
	{
		List<Vertex> Vertices { get; }

		void Sync(List<Vertex> vertices, List<Edge> constrainedEdges, List<Face> faces);
		IEnumerable<Face> Faces { get; }
#if TANGERINE
		/// <summary>
		/// We need private information in Tangerine in order to show them to end user.
		/// In any other application there is no need to know inner information about topology.
		/// </summary>
		IEnumerable<(Face, Face.FaceInfo)> FacesWithInfo { get; }
#endif

		event Action<ITopology> OnTopologyChanged;

		void AddVertex(Vertex vertex);
		void RemoveVertex(int index, bool keepConstrainedEdges = false);
		void TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta);
		void ConstrainEdge(int index0, int index1);
		void Concave(Vector2 position);
		IEnumerable<(int, int)> ConstrainedEdges { get; }

		/// <summary>
		/// Carries out a test to determine the most suitable
		/// topology primitive for a given position.
		/// </summary>
		/// <param name="position">Position in local space.</param>
		/// <param name="vertexHitRadius">Admissible radius for vertex testing.</param>
		/// <param name="edgeHitRadius">Admissible radius for edge testing.</param>
		/// <param name="result">Primitive and its appropriate info if test was successful, null otherwise.</param>
		/// <returns>Test success verdict.</returns>
		bool HitTest(Vector2 position, float vertexHitRadius, float edgeHitRadius, out TopologyHitTestResult result);
	}
}
