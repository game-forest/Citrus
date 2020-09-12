#if PROFILER
namespace Lime.Profiler.Graphics
{
	/// <summary>
	/// Used to accumulate profiling information from render calls to various objects.
	/// </summary>
	/// <remarks>
	/// Since batch render is deferred, it is necessary to save information from various
	/// areas (for example, OverdrawMaterialsScope) in an instance of this class.
	/// </remarks>
	public struct RenderBatchProfilingInfo
	{
		public bool IsPartOfScene { get; private set; }
		public bool IsInsideOverdrawMaterialsScope { get; private set; }

		public void Initialize()
		{
			IsPartOfScene = false;
			IsInsideOverdrawMaterialsScope = false;
		}

		public void ProcessNode(IProfileableObject node)
		{
			IsPartOfScene |= node == null || node.IsPartOfScene;
			IsInsideOverdrawMaterialsScope |= OverdrawMaterialsScope.IsInside;
		}
	}
}
#endif // PROFILER
