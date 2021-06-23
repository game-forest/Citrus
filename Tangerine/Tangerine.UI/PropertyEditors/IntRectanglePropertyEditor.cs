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
	public class IntRectanglePropertyEditor : CommonPropertyEditor<IntRectangle>
	{
		private NumericEditBox editorAX;
		private NumericEditBox editorAY;
		private NumericEditBox editorBX;
		private NumericEditBox editorBY;

		public IntRectanglePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			EditorContainer.AddNode(new Widget {
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4 },
				Nodes = {
					//new SimpleText { Text = "AX" },
					(editorAX = editorParams.NumericEditBoxFactory()),
					// new SimpleText { Text = "AY" },
					(editorAY = editorParams.NumericEditBoxFactory()),
					//new SimpleText { Text = "BX" },
					(editorBX = editorParams.NumericEditBoxFactory()),
					// new SimpleText { Text = "BY" },
					(editorBY = editorParams.NumericEditBoxFactory()),
					Spacer.HStretch(),
				}
			});
			var currentAX = CoalescedPropertyComponentValue(v => v.A.X);
			var currentAY = CoalescedPropertyComponentValue(v => v.A.Y);
			var currentBX = CoalescedPropertyComponentValue(v => v.B.X);
			var currentBY = CoalescedPropertyComponentValue(v => v.B.Y);
			editorAX.Submitted += text => SetComponent(editorParams, 0, editorAX, currentAX.GetValue());
			editorAY.Submitted += text => SetComponent(editorParams, 1, editorAY, currentAY.GetValue());
			editorBX.Submitted += text => SetComponent(editorParams, 2, editorBX, currentBX.GetValue());
			editorBY.Submitted += text => SetComponent(editorParams, 3, editorBY, currentBY.GetValue());
			editorAX.AddLateChangeWatcher(currentAX, v => editorAX.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			editorAY.AddLateChangeWatcher(currentAY, v => editorAY.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			editorBX.AddLateChangeWatcher(currentBX, v => editorBX.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			editorBY.AddLateChangeWatcher(currentBY, v => editorBY.Text = v.IsDefined ? v.Value.ToString() : ManyValuesText);
			ManageManyValuesOnFocusChange(editorAX, currentAX);
			ManageManyValuesOnFocusChange(editorAY, currentAY);
			ManageManyValuesOnFocusChange(editorBX, currentBX);
			ManageManyValuesOnFocusChange(editorBY, currentBY);
		}

		void SetComponent(IPropertyEditorParams editorParams, int component, CommonEditBox editor, CoalescedValue<int> currentValue)
		{
			if (Parser.TryParse(editor.Text, out double newValue)) {
				DoTransaction(() => {
					SetProperty<IntRectangle>(current => {
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
			var currentAX = CoalescedPropertyComponentValue(v => v.A.X);
			var currentAY = CoalescedPropertyComponentValue(v => v.A.Y);
			var currentBX = CoalescedPropertyComponentValue(v => v.B.X);
			var currentBY = CoalescedPropertyComponentValue(v => v.B.Y);
			SetComponent(EditorParams, 0, editorAX, currentAX.GetValue());
			SetComponent(EditorParams, 1, editorAY, currentAY.GetValue());
			SetComponent(EditorParams, 2, editorBX, currentBX.GetValue());
			SetComponent(EditorParams, 3, editorBY, currentBY.GetValue());
		}
	}
}
