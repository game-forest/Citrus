using Lime;
using Lime.PolygonMesh.Topology;
using System.Collections.Generic;
using System.Linq;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public enum TopologyDataType
	{
		None,
		Vertex,
		Edge,
		Face
	}

	public interface ITopologyData
	{
		bool HitTest(ITopology topology, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f);
		void Render(ITopology topology, Matrix32 transform, Vector2 contextSize);
		void RenderHovered(ITopology topology, Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize);
		Vector2 InterpolateUV(ITopology topology, Vector2 position);
	}

	public static class TopologyDataListExtenstions
	{
		public static List<ITopologyData> AsBulkData<T>(this List<T> list) where T : ITopologyData =>
			list.Cast<ITopologyData>().ToList();
	}
}
