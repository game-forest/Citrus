namespace Lime.Widgets.PolygonMesh.Topology
{
	public interface ITopologyPrimitive
	{
		ushort this[int index] { get; }
		int Count { get; }
	}
}
