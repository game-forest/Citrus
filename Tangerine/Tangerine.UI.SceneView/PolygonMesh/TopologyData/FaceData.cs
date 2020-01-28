using System;
using System.Collections.Generic;
using Lime;
using Lime.PolygonMesh.Utils;
using Lime.Widgets.PolygonMesh.Topology;

namespace Tangerine.UI.SceneView.PolygonMesh.TopologyData
{
	public struct FaceData : ITopologyData, IEquatable<FaceData>
	{
		private readonly Face face;

		public int TopologicalIndex0 => face[0];
		public int TopologicalIndex1 => face[1];
		public int TopologicalIndex2 => face[2];

		public TopologyDataType Type => TopologyDataType.Face;

		public FaceData(int topologicalIndex0, int topologicalIndex1, int topologicalIndex2)
		{
			face = new Face {
				Index0 = (ushort)topologicalIndex0,
				Index1 = (ushort)topologicalIndex1,
				Index2 = (ushort)topologicalIndex2
			};
		}

		public bool HitTest(IList<Vertex> vertices, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f)
		{
			var p0 = vertices[TopologicalIndex0].Pos * contextSize;
			var p1 = vertices[TopologicalIndex1].Pos * contextSize;
			var p2 = vertices[TopologicalIndex2].Pos * contextSize;

			p0 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p1 - p0).Normalized + (p2 - p0).Normalized);
			p1 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p0 - p1).Normalized + (p2 - p1).Normalized);
			p2 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p1 - p2).Normalized + (p0 - p2).Normalized);

			distance = 0.0f;
			return PolygonMeshUtils.PointTriangleIntersection(position, p0, p1, p2);
		}

		public void Render(IList<Vertex> vertices, Matrix32 transform, Vector2 contextSize)
		{
		}

		public void RenderHovered(IList<Vertex> vertices,  Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize)
		{
			Utils.RenderTriangle(
				transform.TransformVector(vertices[TopologicalIndex0].Pos * contextSize),
				transform.TransformVector(vertices[TopologicalIndex1].Pos * contextSize),
				transform.TransformVector(vertices[TopologicalIndex2].Pos * contextSize),
				state == PolygonMeshController.ModificationState.Removal ?
					Theme.Colors.PolygonMeshRemovalColor :
					Theme.Colors.PolygonMeshHoverColor
			);
		}

		public Vector2 InterpolateUV(IList<Vertex> vertices, Vector2 position)
		{
			var v0 = vertices[TopologicalIndex0];
			var v1 = vertices[TopologicalIndex1];
			var v2 = vertices[TopologicalIndex2];
			var weights = PolygonMeshUtils.CalcTriangleRelativeBarycentricCoordinates(position, v0.Pos, v1.Pos, v2.Pos);
			return weights[0] * v0.UV1 + weights[1] * v1.UV1 + weights[2] * v2.UV1;
		}

		public bool Equals(FaceData other) => face.Equals(other.face);

		public override int GetHashCode() => face.GetHashCode();
	}
}
