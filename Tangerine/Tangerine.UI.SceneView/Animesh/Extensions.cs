using Lime;
using Lime.Widgets.Animesh.Topology.HalfEdgeTopology;
using System.Collections.Generic;
using SkinnedVertex = Lime.Widgets.Animesh.Animesh.SkinnedVertex;

namespace Tangerine.UI.SceneView.Animesh
{
	public static class Extensions
	{
		public static TopologyController Controller(this Lime.Widgets.Animesh.Animesh mesh) =>
			mesh.Components.GetOrAdd<TopologyController<HalfEdgeTopology>>();

		public static Vector2 ApplySkinning(this Lime.Widgets.Animesh.Animesh mesh, Vector2 vector, SkinningWeights weights) =>
			mesh.ParentWidget.BoneArray.ApplySkinningToVector(vector, weights);

		public static Vector2 CalcVertexPositionInCurrentSpace(this Lime.Widgets.Animesh.Animesh mesh, int index) =>
			mesh.Controller().Vertices[index].Pos * mesh.Size;

		public static Vector2 CalcVertexPositionInParentSpace(this Lime.Widgets.Animesh.Animesh mesh, int index) =>
			mesh.CalcLocalToParentTransform().TransformVector(mesh.CalcVertexPositionInCurrentSpace(index));

		public static Vector2 TransformedVertexPosition(this Lime.Widgets.Animesh.Animesh mesh, int index) =>
			mesh.ApplySkinning(mesh.CalcVertexPositionInParentSpace(index), mesh.Controller().Vertices[index].SkinningWeights);

	}
}
