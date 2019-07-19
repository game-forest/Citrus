using System;
using Lime;
using Lime.PolygonMesh;

namespace Tangerine.UI.SceneView.Utils.PolygonMesh
{
	public enum PrimitiveType
	{
		Vertex,
		Edge,
		Face
	}

	internal interface ITopologyPrimitive
	{
		PrimitiveType Type { get; }
		bool HitTest(Vector2 position, out float distance, float scale = 1.0f);
		void Translate(Vector2 positionDelta);
		void TranslateUV(Vector2 uvDelta);
		void Render(Matrix32 transform);
		void RenderHovered(Matrix32 transform, PolygonMeshManager.ModificationState state, Vector2 creationHintPosition);
		Vector2 InterpolateUV(Vector2 position);
	}

	internal struct Vertex : ITopologyPrimitive, IEquatable<Vertex>
	{
		public readonly int TopologicalIndex;
		public readonly ITopology Topology;

		public PrimitiveType Type => PrimitiveType.Vertex;

		public Vertex(ITopology topology, int topologicalIndex)
		{
			Topology = topology;
			TopologicalIndex = topologicalIndex;
		}

		public bool HitTest(Vector2 position, out float distance, float scale = 1.0f)
		{
			return PolygonMeshUtils.PointPointIntersection(
				Topology.Vertices[TopologicalIndex].Pos, position,
				Theme.Metrics.PolygonMeshVertexHitTestRadius / scale, out distance
			);
		}

		public void Translate(Vector2 positionDelta)
		{
			var v = Topology.Vertices[TopologicalIndex];
			v.Pos += positionDelta;
			Topology.Vertices[TopologicalIndex] = v;
		}

		public void TranslateUV(Vector2 uvDelta)
		{
			Topology.TranslateVertexUV(TopologicalIndex, uvDelta);
		}

		public void Render(Matrix32 transform)
		{
			PolygonMeshUtils.RenderVertex(
				transform.TransformVector(Topology.Vertices[TopologicalIndex].Pos),
				Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshVertexBackgroundColor,
				Theme.Colors.PolygonMeshVertexColor
			);
		}

		public void RenderHovered(Matrix32 transform, PolygonMeshManager.ModificationState state, Vector2 creationHintPosition)
		{
			PolygonMeshUtils.RenderVertex(
				transform.TransformVector(Topology.Vertices[TopologicalIndex].Pos),
				1.3f * Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				1.3f * Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshHoverColor.Darken(0.7f),
				state == PolygonMeshManager.ModificationState.Removal ? Theme.Colors.PolygonMeshRemovalColor : Theme.Colors.PolygonMeshHoverColor
			);
		}

		public Vector2 InterpolateUV(Vector2 position)
		{
			return position / Topology.Vertices[TopologicalIndex].Pos * Topology.Vertices[TopologicalIndex].UV1;
		}

		public bool Equals(Vertex other)
		{
			return
				TopologicalIndex == other.TopologicalIndex &&
				Topology.Equals(other.Topology);
		}
	}

	//===============================================================

	internal struct Edge : ITopologyPrimitive, IEquatable<Edge>
	{
		public readonly int TopologicalIndex0;
		public readonly int TopologicalIndex1;
		public readonly ITopology Topology;

		public PrimitiveType Type => PrimitiveType.Edge;
		public bool IsFraming { get; set; }
		public bool IsConstrained { get; set; }

		public Edge(ITopology topology, int topologicalIndex0, int topologicalIndex1, bool isFraming, bool isConstrained)
		{
			Topology = topology;
			TopologicalIndex0 = topologicalIndex0;
			TopologicalIndex1 = topologicalIndex1;
			IsFraming = isFraming;
			IsConstrained = isConstrained;
		}

		public bool HitTest(Vector2 position, out float distance, float scale = 1.0f)
		{
			var p0 = Topology.Vertices[TopologicalIndex0].Pos;
			var p1 = Topology.Vertices[TopologicalIndex1].Pos;
			return PolygonMeshUtils.PointLineIntersection(
				position, p0, p1, Theme.Metrics.PolygonMeshEdgeHitTestRadius / scale, out distance
			);
		}

		public void Translate(Vector2 positionDelta)
		{
			foreach (var i in new[] { TopologicalIndex0, TopologicalIndex1 }) {
				var v = Topology.Vertices[i];
				v.Pos += positionDelta;
				Topology.Vertices[i] = v;
			}
		}

		public void TranslateUV(Vector2 uvDelta)
		{
			Topology.TranslateVertexUV(TopologicalIndex0, uvDelta);
			Topology.TranslateVertexUV(TopologicalIndex1, uvDelta);
		}

		public void Render(Matrix32 transform)
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
			}
			else if (IsConstrained) {
				foregroundColor = Theme.Colors.PolygonMeshFixedEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			PolygonMeshUtils.RenderLine(
				transform.TransformVector(Topology.Vertices[TopologicalIndex0].Pos),
				transform.TransformVector(Topology.Vertices[TopologicalIndex1].Pos),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
		}

		public void RenderHovered(Matrix32 transform, PolygonMeshManager.ModificationState state, Vector2 creationHintPosition)
		{
			var foregroundColor =
				state == PolygonMeshManager.ModificationState.Removal ?
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
			PolygonMeshUtils.RenderLine(
				transform.TransformVector(Topology.Vertices[TopologicalIndex0].Pos),
				transform.TransformVector(Topology.Vertices[TopologicalIndex1].Pos),
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(IsFraming || IsConstrained)
			);
			if (state == PolygonMeshManager.ModificationState.Creation) {
				var v0 = Topology.Vertices[TopologicalIndex0];
				var v1 = Topology.Vertices[TopologicalIndex1];
				creationHintPosition = transform.TransformVector(
					PolygonMeshUtils.PointProjectionToLine(creationHintPosition, v0.Pos, v1.Pos, out var isInside)
				);
				PolygonMeshUtils.RenderVertex(
					creationHintPosition,
					Theme.Metrics.PolygonMeshBackgroundVertexRadius,
					Theme.Metrics.PolygonMeshVertexRadius,
					Color4.White.Transparentify(0.5f),
					Color4.Yellow.Lighten(0.5f).Transparentify(0.5f)
				);
			}
		}

		public Vector2 InterpolateUV(Vector2 position)
		{
			var v0 = Topology.Vertices[TopologicalIndex0];
			var v1 = Topology.Vertices[TopologicalIndex1];
			var weights = PolygonMeshUtils.CalcSegmentRelativeBarycentricCoordinates(position, v0.Pos, v1.Pos);
			return weights[0] * v0.UV1 + weights[1] * v1.UV1;
		}

		public bool Equals(Edge other)
		{
			return
				(
					TopologicalIndex0 == other.TopologicalIndex0 && TopologicalIndex1 == other.TopologicalIndex1 ||
					TopologicalIndex0 == other.TopologicalIndex1 && TopologicalIndex1 == other.TopologicalIndex0
				) &&
				Topology.Equals(other.Topology);
		}
	}

	//===============================================================

	internal struct Face : ITopologyPrimitive, IEquatable<Face>
	{
		public readonly int TopologicalIndex0;
		public readonly int TopologicalIndex1;
		public readonly int TopologicalIndex2;
		public readonly ITopology Topology;

		public PrimitiveType Type => PrimitiveType.Face;

		public Face(ITopology topology, int topologicalIndex0, int topologicalIndex1, int topologicalIndex2)
		{
			Topology = topology;
			TopologicalIndex0 = topologicalIndex0;
			TopologicalIndex1 = topologicalIndex1;
			TopologicalIndex2 = topologicalIndex2;
		}

		public bool HitTest(Vector2 position, out float distance, float scale = 1.0f)
		{
			var p0 = Topology.Vertices[TopologicalIndex0].Pos;
			var p1 = Topology.Vertices[TopologicalIndex1].Pos;
			var p2 = Topology.Vertices[TopologicalIndex2].Pos;

			p0 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p1 - p0).Normalized + (p2 - p0).Normalized);
			p1 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p0 - p1).Normalized + (p2 - p1).Normalized);
			p2 += Theme.Metrics.PolygonMeshEdgeHitTestRadius / (scale * 2.0f) * ((p1 - p2).Normalized + (p0 - p2).Normalized);

			distance = 0.0f;
			return PolygonMeshUtils.PointTriangleIntersection(position, p0, p1, p2);
		}

		public void Translate(Vector2 positionDelta)
		{
			foreach (var i in new[] { TopologicalIndex0, TopologicalIndex1, TopologicalIndex2 }) {
				var v = Topology.Vertices[i];
				v.Pos += positionDelta;
				Topology.Vertices[i] = v;
			}
		}

		public void TranslateUV(Vector2 uvDelta)
		{
			Topology.TranslateVertexUV(TopologicalIndex0, uvDelta);
			Topology.TranslateVertexUV(TopologicalIndex1, uvDelta);
			Topology.TranslateVertexUV(TopologicalIndex2, uvDelta);
		}

		public void Render(Matrix32 transform)
		{
		}

		public void RenderHovered(Matrix32 transform, PolygonMeshManager.ModificationState state, Vector2 creationHintPosition)
		{
			PolygonMeshUtils.RenderTriangle(
				transform.TransformVector(Topology.Vertices[TopologicalIndex0].Pos),
				transform.TransformVector(Topology.Vertices[TopologicalIndex1].Pos),
				transform.TransformVector(Topology.Vertices[TopologicalIndex2].Pos),
				state == PolygonMeshManager.ModificationState.Removal ? Theme.Colors.PolygonMeshRemovalColor : Theme.Colors.PolygonMeshHoverColor
			);
			if (state == PolygonMeshManager.ModificationState.Creation) {
				PolygonMeshUtils.RenderVertex(
					transform.TransformVector(creationHintPosition),
					Theme.Metrics.PolygonMeshBackgroundVertexRadius,
					Theme.Metrics.PolygonMeshVertexRadius,
					Color4.White.Transparentify(0.5f),
					Color4.Yellow.Lighten(0.5f).Transparentify(0.5f)
				);
			}
		}

		public Vector2 InterpolateUV(Vector2 position)
		{
			var v0 = Topology.Vertices[TopologicalIndex0];
			var v1 = Topology.Vertices[TopologicalIndex1];
			var v2 = Topology.Vertices[TopologicalIndex2];
			var weights = PolygonMeshUtils.CalcTriangleRelativeBarycentricCoordinates(position, v0.Pos, v1.Pos, v2.Pos);
			return weights[0] * v0.UV1 + weights[1] * v1.UV1 + weights[2] * v2.UV1;
		}

		public bool Equals(Face other)
		{
			return
				(
					TopologicalIndex0 == other.TopologicalIndex0 &&
					TopologicalIndex1 == other.TopologicalIndex1 &&
					TopologicalIndex2 == other.TopologicalIndex2 ||
				
					TopologicalIndex0 == other.TopologicalIndex0 && 
					TopologicalIndex1 == other.TopologicalIndex2 &&
					TopologicalIndex2 == other.TopologicalIndex1 ||

					TopologicalIndex1 == other.TopologicalIndex1 &&
					TopologicalIndex0 == other.TopologicalIndex2 &&
					TopologicalIndex2 == other.TopologicalIndex0 ||

					TopologicalIndex2 == other.TopologicalIndex2 && 
					TopologicalIndex0 == other.TopologicalIndex1 &&
					TopologicalIndex1 == other.TopologicalIndex0
				) &&
				Topology.Equals(other.Topology);
		}
	}
}
