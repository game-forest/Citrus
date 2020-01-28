using System;
using System.Collections.Generic;
using Lime;
using Lime.PolygonMesh.Utils;
using Lime.Widgets.PolygonMesh.Topology;

namespace Tangerine.UI.SceneView.PolygonMesh.TopologyData
{
	public struct EdgeData : ITopologyData, IEquatable<EdgeData>
	{
		private readonly Edge edge;

		public int TopologicalIndex0 => edge[0];
		public int TopologicalIndex1 => edge[1];

		public TopologyDataType Type => TopologyDataType.Edge;
		public bool IsFraming { get; set; }
		public bool IsConstrained { get; set; }

		public EdgeData(int topologicalIndex0, int topologicalIndex1, bool isFraming, bool isConstrained)
		{
			edge = new Edge {
				Index0 = (ushort)topologicalIndex0,
				Index1 = (ushort)topologicalIndex1,
			};
			IsFraming = isFraming;
			IsConstrained = isConstrained;
		}

		public bool HitTest(IList<Vertex> vertices, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f)
		{
			var p0 = vertices[TopologicalIndex0].Pos * contextSize;
			var p1 = vertices[TopologicalIndex1].Pos * contextSize;
			return PolygonMeshUtils.PointLineIntersection(
				position, p0, p1, Theme.Metrics.PolygonMeshEdgeHitTestRadius / scale, out distance
			);
		}

		public void Render(IList<Vertex> vertices, Matrix32 transform, Vector2 contextSize)
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
				transform.TransformVector(vertices[TopologicalIndex0].Pos * contextSize),
				transform.TransformVector(vertices[TopologicalIndex1].Pos * contextSize),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
		}

		public void RenderHovered(IList<Vertex> vertices, Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize)
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
				transform.TransformVector(vertices[TopologicalIndex0].Pos * contextSize),
				transform.TransformVector(vertices[TopologicalIndex1].Pos * contextSize),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
		}

		public Vector2 InterpolateUV(IList<Vertex> vertices, Vector2 position)
		{
			var v0 = vertices[TopologicalIndex0];
			var v1 = vertices[TopologicalIndex1];
			var weights = PolygonMeshUtils.CalcSegmentRelativeBarycentricCoordinates(position, v0.Pos, v1.Pos);
			return weights[0] * v0.UV1 + weights[1] * v1.UV1;
		}

		public bool Equals(EdgeData other) => edge.Equals(other.edge);

		public override int GetHashCode() => edge.GetHashCode();
	}
}
