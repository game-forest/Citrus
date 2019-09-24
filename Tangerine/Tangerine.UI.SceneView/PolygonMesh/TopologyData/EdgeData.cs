using Lime;
using Lime.PolygonMesh.Topology;
using Lime.PolygonMesh.Utils;
using System;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public struct EdgeData : ITopologyData, IEquatable<EdgeData>
	{
		private readonly Lime.PolygonMesh.PolygonMesh.Edge edge;

		public int TopologicalIndex0 => edge[0];
		public int TopologicalIndex1 => edge[1];

		public TopologyDataType Type => TopologyDataType.Edge;
		public bool IsFraming { get; set; }
		public bool IsConstrained { get; set; }

		public EdgeData(int topologicalIndex0, int topologicalIndex1, bool isFraming, bool isConstrained)
		{
			edge = new Lime.PolygonMesh.PolygonMesh.Edge {
				Index0 = (ushort)topologicalIndex0,
				Index1 = (ushort)topologicalIndex1
			};
			IsFraming = isFraming;
			IsConstrained = isConstrained;
		}

		public bool HitTest(ITopology topology, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f)
		{
			var p0 = topology.Vertices[TopologicalIndex0].Pos * contextSize;
			var p1 = topology.Vertices[TopologicalIndex1].Pos * contextSize;
			return PolygonMeshUtils.PointLineIntersection(
				position, p0, p1, Theme.Metrics.PolygonMeshEdgeHitTestRadius / scale, out distance
			);
		}

		public void Render(ITopology topology, Matrix32 transform, Vector2 contextSize)
		{
			var foregroundColor = Theme.Colors.PolygonMeshInnerEdgeColor;
			var backgroundColor = Theme.Colors.PolygonMeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness
			);
			if (IsFraming) {
				foregroundColor = Theme.Colors.PolygonMeshFramingEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 1.7f);
			} else if (IsConstrained) {
				foregroundColor = Theme.Colors.PolygonMeshFixedEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			Utils.RenderLine(
				transform.TransformVector(topology.Vertices[TopologicalIndex0].Pos * contextSize),
				transform.TransformVector(topology.Vertices[TopologicalIndex1].Pos * contextSize),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
		}

		public void RenderHovered(ITopology topology, Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize)
		{
			var foregroundColor =
				state == PolygonMeshController.ModificationState.Removal ?
				Theme.Colors.PolygonMeshRemovalColor :
				Theme.Colors.PolygonMeshHoverColor;
			var backgroundColor = Theme.Colors.PolygonMeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness
			);
			if (IsFraming) {
				backgroundColor = Theme.Colors.PolygonMeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 1.7f);
			}
			else if (IsConstrained) {
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			Utils.RenderLine(
				transform.TransformVector(topology.Vertices[TopologicalIndex0].Pos * contextSize),
				transform.TransformVector(topology.Vertices[TopologicalIndex1].Pos * contextSize),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
			//if (state == PolygonMesh.ModificationState.Creation) {
			//	var v0 = topology.Vertices[TopologicalIndex0].Pos * contextSize;
			//	var v1 = topology.Vertices[TopologicalIndex1].Pos * contextSize;
			//	creationHintPosition = transform.TransformVector(
			//		PolygonMeshUtils.PointProjectionToLine(creationHintPosition, v0, v1, out var isInside)
			//	);
			//	PolygonMeshUtils.RenderVertex(
			//		creationHintPosition,
			//		Theme.Metrics.PolygonMeshBackgroundVertexRadius,
			//		Theme.Metrics.PolygonMeshVertexRadius,
			//		Color4.White.Transparentify(0.5f),
			//		Color4.Yellow.Lighten(0.5f).Transparentify(0.5f)
			//	);
			//}
		}

		public Vector2 InterpolateUV(ITopology topology, Vector2 position)
		{
			var v0 = topology.Vertices[TopologicalIndex0];
			var v1 = topology.Vertices[TopologicalIndex1];
			var weights = PolygonMeshUtils.CalcSegmentRelativeBarycentricCoordinates(position, v0.Pos, v1.Pos);
			return weights[0] * v0.UV1 + weights[1] * v1.UV1;
		}

		public bool Equals(EdgeData other) => edge.Equals(other.edge);

		public override int GetHashCode() => edge.GetHashCode();
	}
}
