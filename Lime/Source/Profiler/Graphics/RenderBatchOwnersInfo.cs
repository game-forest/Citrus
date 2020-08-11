#if PROFILER
namespace Lime.Profiler.Graphics
{
	public struct RenderBatchOwnersInfo
	{
		public bool IsPartOfScene { get; private set; }

		public void Initialize()
		{
			IsPartOfScene = false;
		}

		public void ProcessNode(IProfileableObject node)
		{
			IsPartOfScene |= node == null || node.IsPartOfScene;
		}
	}
}
#endif // PROFILER
