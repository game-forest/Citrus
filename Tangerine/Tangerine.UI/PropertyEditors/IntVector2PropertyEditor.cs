using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tangerine.Core;
using Tangerine.Core.ExpressionParser;

namespace Tangerine.UI
{
	public class IntVector2PropertyEditor : CommonPropertyEditor<IntVector2>
	{
		private NumericEditBox editorX;
		private NumericEditBox editorY;

		public IntVector2PropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			EditorContainer.AddNode(new Widget {
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4 },
				Nodes = {
					//new SimpleText { Text = "X" },
					(editorX = editorParams.NumericEditBoxFactory()),
					// new SimpleText { Text = "Y" },
					(editorY = editorParams.NumericEditBoxFactory()),
					Spacer.HStretch(),
				}
			});
			var currentX = CoalescedPropertyComponentValue(v => v.X);
			var currentY = CoalescedPropertyComponentValue(v => v.Y);
			editorX.Submitted += text => SetComponent(editorParams, 0, editorX, currentX.GetValue());
			editorY.Submitted += text => SetComponent(editorParams, 1, editorY, currentY.GetValue());
			editorX.AddLateChangeWatcher(currentX, v => editorX.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			editorY.AddLateChangeWatcher(currentY, v => editorY.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			ManageManyValuesOnFocusChange(editorX, currentX);
			ManageManyValuesOnFocusChange(editorY, currentY);
		}

		void SetComponent(IPropertyEditorParams editorParams, int component, CommonEditBox editor, CoalescedValue<int> currentValue)
		{
			if (Parser.TryParse(editor.Text, out double newValue)) {
				DoTransaction(() => {
					SetProperty<IntVector2>(current => {
						current[component] = (int)newValue;
						return current;
					});
				});
				editor.Text = ((int)newValue).ToString();
			} else {
				editor.Text = currentValue.IsDefined ? currentValue.Value.ToString() : ManyValuesText;
			}
		}

		public override void Submit()
		{
			var currentX = CoalescedPropertyComponentValue(v => v.X);
			var currentY = CoalescedPropertyComponentValue(v => v.Y);
			SetComponent(EditorParams, 0, editorX, currentX.GetValue());
			SetComponent(EditorParams, 1, editorY, currentY.GetValue());
		}
	}
}
