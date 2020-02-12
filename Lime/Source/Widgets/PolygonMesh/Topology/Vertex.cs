namespace Lime.Widgets.PolygonMesh.Topology
{
	public struct Vertex : ITopologyPrimitive
	{
		public ushort Index;
		public ushort this[int index] => Index;
		public int Count => 1;
	}
}
