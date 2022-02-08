using Tangerine.UI;
using Tangerine.UI.Inspector;

namespace Tangerine
{
	public class LookupAddComponentSection
	{
		static LookupAddComponentSection()
		{
			InspectorContent.CreateLookupForAddComponent += () => {
				new LookupDialog(sections => new[] {
					sections.Commands,
					new LookupCommandsSection(
						sections,
						menu: SceneViewCommands.AddComponentToSelection.Menu,
						breadcrumb: $"Edit\\{SceneViewCommands.AddComponentToSelection.Text}"
					),
				});
			};
		}
	}
}
