using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	public class PolygonMeshContextualPanel
	{
		private static PolygonMeshContextualPanel instance = null;
		public static PolygonMeshContextualPanel Instance => instance ?? (instance = new PolygonMeshContextualPanel());

		public Widget RootNode { get; }

		private PolygonMeshContextualPanel()
		{
			RootNode = new Widget {
				MinMaxSize = new Vector2(100f, 50f),
				Nodes = {
					CreatePolygonMeshStateButton(PolygonMeshTools.ModificationState.Triangulation, (Command)Tools.PolygonMeshTriangulate),
					CreatePolygonMeshStateButton(PolygonMeshTools.ModificationState.Creation, (Command)Tools.PolygonMeshCreate),
					CreatePolygonMeshStateButton(PolygonMeshTools.ModificationState.Animation, (Command)Tools.PolygonMeshAnimate),
					CreatePolygonMeshStateButton(PolygonMeshTools.ModificationState.Removal, (Command)Tools.PolygonMeshRemove),
				},
				Layout = new TableLayout { RowCount = 1, ColumnCount = 4, },
				LayoutCell = new LayoutCell(new Alignment { X = HAlignment.Center, Y = VAlignment.Bottom, }),
				CompoundPresenter = { new WidgetFlatFillPresenter(Theme.Colors.GrayBackground), new WidgetBoundsPresenter(Color4.Red)},
			};
		}

		private static Widget CreatePolygonMeshStateButton(PolygonMeshTools.ModificationState state, Command command)
		{
			var tb = new ToolbarButton(command.Icon.AsTexture) {
				Highlightable = true,
				Tooltip = command.Text,
			};
			tb.AddChangeWatcher(() => PolygonMeshTools.State, s => tb.Checked = s == state);
			tb.Clicked += () => {
				if (!tb.Checked) {
					command.Issue();
				}
			};
			return tb;
		}
	}
}
