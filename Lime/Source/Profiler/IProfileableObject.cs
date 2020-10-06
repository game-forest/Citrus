#if PROFILER
namespace Lime.Profiler
{
	public interface IProfileableObject
	{
		bool IsPartOfScene { get; }
		bool IsOverdrawForeground { get; }
	}
}
#endif // PROFILER
