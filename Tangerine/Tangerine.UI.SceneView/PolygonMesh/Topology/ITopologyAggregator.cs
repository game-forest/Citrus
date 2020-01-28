using System.Collections.Generic;
using Tangerine.UI.SceneView.PolygonMesh.TopologyData;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public interface ITopologyAggregator
	{
		List<VertexData> VertexData { get; }
		List<EdgeData> EdgeData { get; }
		List<FaceData> FaceData { get; }
		Dictionary<int, List<(TopologyDataType Type, int Index)>> VertexAdjacency { get; }
		Dictionary<int, List<(TopologyDataType Type, int Index)>> EdgeAdjacency { get; }
		Dictionary<int, List<(TopologyDataType Type, int Index)>> FaceAdjacency { get; }

		List<ITopologyData> this[TopologyDataType type]
		{
			get;
		}

		List<(TopologyDataType Type, int Index)> this[TopologyDataType type, int index]
		{
			get;
		}

		void Invalidate();
	}
}
