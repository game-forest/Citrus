using Lime;
using Lime.PolygonMesh.Topology;
using Lime.PolygonMesh.Utils;
using System;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public struct VertexData : ITopologyData, IEquatable<VertexData>
	{
		public readonly int TopologicalIndex;

		public VertexData(int topologicalIndex)
		{
			TopologicalIndex = topologicalIndex;
		}

		public bool HitTest(ITopology topology, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f)
		{
			return PolygonMeshUtils.PointPointIntersection(
				topology.Vertices[TopologicalIndex].Pos * contextSize, position,
				Theme.Metrics.PolygonMeshVertexHitTestRadius / scale, out distance
			);
		}

		public void Render(ITopology topology, Matrix32 transform, Vector2 contextSize)
		{
			Utils.RenderVertex(
				transform.TransformVector(topology.Vertices[TopologicalIndex].Pos * contextSize),
				Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshVertexBackgroundColor,
				Theme.Colors.PolygonMeshVertexColor
			);
		}

		public void RenderHovered(ITopology topology, Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize)
		{
			Utils.RenderVertex(
				transform.TransformVector(topology.Vertices[TopologicalIndex].Pos * contextSize),
				1.3f * Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				1.3f * Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshHoverColor.Darken(0.7f),
				state == PolygonMeshController.ModificationState.Removal ?
					Theme.Colors.PolygonMeshRemovalColor :
					Theme.Colors.PolygonMeshHoverColor
			);
		}

		public Vector2 InterpolateUV(ITopology topology, Vector2 position)
		{
			return position / topology.Vertices[TopologicalIndex].Pos * topology.Vertices[TopologicalIndex].UV1;
		}

		public bool Equals(VertexData other) =>
			TopologicalIndex == other.TopologicalIndex;
	}
}
