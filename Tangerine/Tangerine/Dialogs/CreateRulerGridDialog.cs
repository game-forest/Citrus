using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.UI;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine
{
	public class CreateRulerGridDialog
	{
		[TangerineNumericEditBoxStep(1f)]
		[TangerineValidRange(0f, 50f)]
		public Vector2 GridSize { get; set; } = new Vector2(1, 1);

		[TangerineNumericEditBoxStep(1f)]
		[TangerineValidRange(0f, 50f)]
		public Vector2 GridSubdivision { get; set; } = new Vector2(0, 0);

		private List<IPropertyEditor> editors = new List<IPropertyEditor>();

		public CreateRulerGridDialog()
		{
			Button cancelButton;
			Button okButton;
			Widget pane;
			var window = new Window(new WindowOptions {
				Title = "Create Rulers Preset",
				Style = WindowStyle.Dialog,
				ClientSize = new Vector2(300, 128),
				Visible = false
			});
			WindowWidget rootWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
					(pane = new Widget {
						Layout = new VBoxLayout { Spacing = 4 }
					}),
					new Widget {
						Padding = new Thickness { Top = 10 },
						Layout = new HBoxLayout { Spacing = 8 },
						LayoutCell = new LayoutCell(Alignment.RightCenter),
						Nodes = {
							(okButton = new ThemedButton { Text = "Ok" }),
							(cancelButton = new ThemedButton { Text = "Cancel" }),
						}
					}
				}
			};
			editors.AddRange(new IPropertyEditor[] {
				new Vector2PropertyEditor(
					 new PropertyEditorParams(pane, this, nameof(GridSize), displayName: "Grid Size")),
				new Vector2PropertyEditor(
					new PropertyEditorParams(pane, this, nameof(GridSubdivision), displayName: "Grid Subdivision"))
			});
			okButton.Clicked += () => {
				editors.ForEach(i => i.Submit());
				if (editors.Any(i => (i is Vector2PropertyEditor e) && e.WarningsContainer.Nodes.Count > 0)) {
					AlertDialog.Show("Please, satisfy all requirements to generate preset.");
					return;
				}
				var container = Document.Current.Container.AsWidget;
				var origin = container.Position - container.Size * container.Pivot;
				var lines = new List<RulerLine> {
					new RulerLine(origin.X, RulerOrientation.Vertical),
					new RulerLine(origin.X + container.Width, RulerOrientation.Vertical),
					new RulerLine(origin.Y, RulerOrientation.Horizontal),
					new RulerLine(origin.Y + container.Height, RulerOrientation.Horizontal),
				};
				var cellCount = (GridSize + Vector2.One) * (GridSubdivision + Vector2.One);
				var cellSize = container.Size / cellCount;
				for (int i = 1; i < cellCount.X; i++) {
					lines.Add(new RulerLine(origin.X + cellSize.X * i, RulerOrientation.Vertical));
				}
				for (int i = 1; i < cellCount.Y; i++) {
					lines.Add(new RulerLine(origin.Y + cellSize.Y * i, RulerOrientation.Horizontal));
				}
				foreach (var line in lines) {
					CreateRuler.Perform(ProjectUserPreferences.Instance.ActiveRuler, line);
				}
				if (!ProjectUserPreferences.Instance.RulerVisible && lines.Count > 0) {
					(SceneViewCommands.ToggleDisplayRuler as Command)?.Issue();
				}
				UserPreferences.Instance.Save();
				Application.InvalidateWindows();
				window.Close();
			};
			cancelButton.Clicked += () => window.Close();
			rootWidget.FocusScope = new KeyboardFocusScope(rootWidget);
			rootWidget.LateTasks.AddLoop(() => {
				if (rootWidget.Input.ConsumeKeyPress(Key.Escape)) {
					window.Close();
					UserPreferences.Instance.Load();
				}
			});
			okButton.SetFocus();
			window.ShowModal();
		}
	}
}
