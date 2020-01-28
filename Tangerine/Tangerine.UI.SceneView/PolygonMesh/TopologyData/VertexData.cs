using System;
using System.Collections.Generic;
using Lime;
using Lime.PolygonMesh.Utils;

namespace Tangerine.UI.SceneView.PolygonMesh.TopologyData
{
	public struct VertexData : ITopologyData, IEquatable<VertexData>
	{
		public readonly int TopologicalIndex;

		public VertexData(int topologicalIndex)
		{
			TopologicalIndex = topologicalIndex;
		}

		public bool HitTest(IList<Vertex> vertices, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f)
		{
			return PolygonMeshUtils.PointPointIntersection(
				vertices[TopologicalIndex].Pos * contextSize, position,
				Theme.Metrics.PolygonMeshVertexHitTestRadius / scale, out distance
			);
		}

		public void Render(IList<Vertex> vertices, Matrix32 transform, Vector2 contextSize)
		{
			Utils.RenderVertex(
				transform.TransformVector(vertices[TopologicalIndex].Pos * contextSize),
				Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshVertexBackgroundColor,
				Theme.Colors.PolygonMeshVertexColor
			);
		}

		public void RenderHovered(IList<Vertex> vertices, Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize)
		{
			Utils.RenderVertex(
				transform.TransformVector(vertices[TopologicalIndex].Pos * contextSize),
				1.3f * Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				1.3f * Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshHoverColor.Darken(0.7f),
				state == PolygonMeshController.ModificationState.Removal ?
					Theme.Colors.PolygonMeshRemovalColor :
					Theme.Colors.PolygonMeshHoverColor
			);
		}

		public Vector2 InterpolateUV(IList<Vertex> vertices, Vector2 position)
		{
			return position / vertices[TopologicalIndex].Pos * vertices[TopologicalIndex].UV1;
		}

		public bool Equals(VertexData other) =>
			TopologicalIndex == other.TopologicalIndex;
	}
}
