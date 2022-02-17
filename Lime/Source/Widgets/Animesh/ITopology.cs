using System;
using System.Collections.Generic;
using Lime;
using SkinnedVertex = Lime.Animesh.SkinnedVertex;

namespace Lime
{
	public class TopologyHitTestResult
	{
		public ITopologyPrimitive Target;
		public object Info;
	}

	public interface ITopology
	{
		List<Animesh.SkinnedVertex> Vertices { get; }
		void ConstructFrom(
			List<Animesh.SkinnedVertex> vertices, List<TopologyEdge> constrainedEdges, List<TopologyFace> faces
		);
		IEnumerable<TopologyFace> Faces { get; }

		/// <summary>
		/// We need private information in Tangerine in order to show them to end user.
		/// In any other application there is no need to know inner information about topology.
		/// </summary>
		IEnumerable<(TopologyFace, TopologyFace.FaceInfo)> FacesWithInfo { get; }
		event Action<ITopology> OnTopologyChanged;
		int AddVertex(Animesh.SkinnedVertex vertex);
		void RemoveVertex(int index);
		bool TranslateVertex(int index, Vector2 positionDelta, Vector2 uvDelta, out List<int> deletedVertices);
		void InsertConstrainedEdge(int index0, int index1);
		void RemoveConstrainedEdge(int index0, int index1);
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
		float VertexHitTestRadius { get; set; }
		float EdgeHitTestDistance { get; set; }
		void Scale(Vector2 scale);
	}
}
