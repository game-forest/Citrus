using Lime;

namespace Tangerine.Core
{
	public interface ISceneView
	{
		Widget Panel { get; }
		Widget Frame { get; }
		Widget InputArea { get; }
		Widget Scene { get; }
		Matrix32 CalcTransitionFromSceneSpace(Widget targetSpace);
		Vector2 MousePosition { get; }
		WidgetInput Input { get; }
	}
}
