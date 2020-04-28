using System;
using System.Collections.Generic;
using Lime.Widgets.PolygonMesh.Topology;
using SkinnedVertex = Lime.Widgets.PolygonMesh.PolygonMesh.SkinnedVertex;

namespace Lime.PolygonMesh.Topology
{
	public class TopologyHitTestResult
	{
		public ITopologyPrimitive Target;
		public object Info;
	}

	public interface ITopology
	{
		List<SkinnedVertex> Vertices { get; }

		void Sync(List<SkinnedVertex> vertices, List<Edge> constrainedEdges, List<Face> faces);
		IEnumerable<Face> Faces { get; }
#if TANGERINE
		/// <summary>
		/// We need private information in Tangerine in order to show them to end user.
		/// In any other application there is no need to know inner information about topology.
		/// </summary>
		IEnumerable<(Face, Face.FaceInfo)> FacesWithInfo { get; }
#endif

		event Action<ITopology> OnTopologyChanged;

#if TANGERINE
		/// <summary>
		/// To switch between the animation and setup modes (<see cref="Widgets.PolygonMesh.PolygonMesh.ModificationMode"/>)
		/// while working with the mesh in Tangerine, we need to make sure it is
		/// possible for the topology controller to emplace current set of vertices with
		/// the other one, i.e. <see cref="Widgets.PolygonMesh.PolygonMesh.TransientVertices"/>.
		/// </summary>
		/// <param name="vertices"></param>
		void EmplaceVertices(List<SkinnedVertex> vertices);
#endif
		void AddVertex(SkinnedVertex vertex);
		void RemoveVertex(int index);
		void TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta);
		void ConstrainEdge(int index0, int index1);
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
