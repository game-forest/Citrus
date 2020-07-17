using Lime;
using Lime.Widgets.Animesh;
using Tangerine.Core;
using Tangerine.UI.AnimeshEditor.Topology.HalfEdgeTopology;

namespace Tangerine.UI.AnimeshEditor
{
	public static class Extensions
	{
		public static TopologyController Controller(this Animesh mesh) =>
			mesh.Components.Get<TopologyController<HalfEdgeTopology>>();

		public static TopologyController Controller(this Animesh mesh, ISceneView sv)
		{
			var controller = mesh.Components.Get<TopologyController<HalfEdgeTopology>>();
			if (controller == null) {
				mesh.Components.Add(controller = new TopologyController<HalfEdgeTopology>(sv));
			}
			return controller;
		}

		public static Vector2 ApplySkinning(this Animesh mesh, Vector2 vector, SkinningWeights weights) =>
			mesh.ParentWidget.BoneArray.ApplySkinningToVector(vector, weights);

		public static Vector2 CalcVertexPositionInCurrentSpace(this Animesh mesh, int index) =>
			mesh.Controller().Vertices[index].Pos;

		public static Vector2 CalcVertexPositionInParentSpace(this Animesh mesh, int index) =>
			mesh.CalcLocalToParentTransform().TransformVector(mesh.CalcVertexPositionInCurrentSpace(index));

		public static Vector2 TransformedVertexPosition(this Animesh mesh, int index) =>
			mesh.ApplySkinning(mesh.CalcVertexPositionInParentSpace(index), mesh.Controller().Vertices[index].SkinningWeights);

	}
}
