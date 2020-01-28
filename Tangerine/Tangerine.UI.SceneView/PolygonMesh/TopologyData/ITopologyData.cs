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
		bool HitTest(IList<Vertex> vertices, Vector2 position, out float distance, Vector2 contextSize, float scale = 1.0f);
		void Render(IList<Vertex> vertices, Matrix32 transform, Vector2 contextSize);
		void RenderHovered(IList<Vertex> vertices, Matrix32 transform, PolygonMeshController.ModificationState state, Vector2 contextSize);
		Vector2 InterpolateUV(IList<Vertex> vertices, Vector2 position);
	}

	public static class TopologyDataListExtenstions
	{
		public static List<ITopologyData> AsBulkData<T>(this List<T> list) where T : ITopologyData =>
			list.Cast<ITopologyData>().ToList();
	}
}
