using Lime;
using Tangerine.Core;

namespace Tangerine.UI.AnimeshEditor
{
	public class AnimeshContextualPanel
	{
		public Widget RootNode { get; }

		public AnimeshContextualPanel()
		{
			RootNode = new Widget {
				MinMaxSize = new Vector2(100f, 50f),
				Nodes = {
					CreateAnimeshStateButton(AnimeshTools.ModificationState.Modification, (Command)Tools.AnimeshTriangulate),
					CreateAnimeshStateButton(AnimeshTools.ModificationState.Creation, (Command)Tools.AnimeshCreate),
					CreateAnimeshStateButton(AnimeshTools.ModificationState.Removal, (Command)Tools.AnimeshRemove),
					CreateAnimeshStateButton(AnimeshTools.ModificationState.Animation, (Command)Tools.AnimeshAnimate),
				},
				Layout = new TableLayout { RowCount = 1, ColumnCount = 4, },
				LayoutCell = new LayoutCell(new Alignment { X = HAlignment.Center, Y = VAlignment.Bottom, }),
				CompoundPresenter = { new WidgetFlatFillPresenter(Theme.Colors.GrayBackground), new WidgetBoundsPresenter(Color4.Red)},
			};
		}

		private static Widget CreateAnimeshStateButton(AnimeshTools.ModificationState state, Command command)
		{
			var tb = new ToolbarButton(command.Icon.AsTexture) {
				Highlightable = true,
				Tooltip = command.Text,
			};
			tb.AddChangeWatcher(() => AnimeshTools.State, s => tb.Checked = s == state);
			tb.Clicked += () => {
				if (!tb.Checked) {
					command.Issue();
				}
			};
			return tb;
		}
	}
}
