using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;
using Tangerine.Core.ExpressionParser;

namespace Tangerine.UI
{
	public class RectanglePropertyEditor : CommonPropertyEditor<Rectangle>
	{
		private NumericEditBox editorAX;
		private NumericEditBox editorAY;
		private NumericEditBox editorBX;
		private NumericEditBox editorBY;

		public RectanglePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			EditorContainer.AddNode(new Widget {
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4 },
				Nodes = {
					// new SimpleText { Text = "AX" },
					(editorAX = editorParams.NumericEditBoxFactory()),
					// new SimpleText { Text = "AY" },
					(editorAY = editorParams.NumericEditBoxFactory()),
					// new SimpleText { Text = "BX" },
					(editorBX = editorParams.NumericEditBoxFactory()),
					// new SimpleText { Text = "BY" },
					(editorBY = editorParams.NumericEditBoxFactory()),
					Spacer.HStretch(),
				},
			});
			var currentAX = CoalescedPropertyComponentValue(v => v.AX);
			var currentAY = CoalescedPropertyComponentValue(v => v.AY);
			var currentBX = CoalescedPropertyComponentValue(v => v.BX);
			var currentBY = CoalescedPropertyComponentValue(v => v.BY);
			editorAX.Submitted += text => SetComponent(editorParams, 0, editorAX, currentAX.GetValue());
			editorAY.Submitted += text => SetComponent(editorParams, 1, editorAY, currentAY.GetValue());
			editorBX.Submitted += text => SetComponent(editorParams, 2, editorBX, currentBX.GetValue());
			editorBY.Submitted += text => SetComponent(editorParams, 3, editorBY, currentBY.GetValue());
			editorAX.AddLateChangeWatcher(
				currentAX, v => editorAX.Text = v.IsDefined ? v.Value.ToString("0.###") : ManyValuesText
			);
			editorAY.AddLateChangeWatcher(
				currentAY, v => editorAY.Text = v.IsDefined ? v.Value.ToString("0.###") : ManyValuesText
			);
			editorBX.AddLateChangeWatcher(
				currentBX, v => editorBX.Text = v.IsDefined ? v.Value.ToString("0.###") : ManyValuesText
			);
			editorBY.AddLateChangeWatcher(
				currentBY, v => editorBY.Text = v.IsDefined ? v.Value.ToString("0.###") : ManyValuesText
			);
			ManageManyValuesOnFocusChange(editorAX, currentAX);
			ManageManyValuesOnFocusChange(editorAY, currentAY);
			ManageManyValuesOnFocusChange(editorBX, currentBX);
			ManageManyValuesOnFocusChange(editorBY, currentBY);
		}

		private void SetComponent(
			IPropertyEditorParams editorParams, int component, CommonEditBox editor, CoalescedValue<float> currentValue
		) {
			if (Parser.TryParse(editor.Text, out double newValue)) {
				DoTransaction(() => {
					SetProperty<Rectangle>(current => {
						current[component] = (float)newValue;
						return current;
					});
				});
				editor.Text = newValue.ToString("0.###");
			} else {
				editor.Text = currentValue.IsDefined ? currentValue.Value.ToString("0.###") : ManyValuesText;
			}
		}

		public override void Submit()
		{
			var currentAX = CoalescedPropertyComponentValue(v => v.AX);
			var currentAY = CoalescedPropertyComponentValue(v => v.AY);
			var currentBX = CoalescedPropertyComponentValue(v => v.BX);
			var currentBY = CoalescedPropertyComponentValue(v => v.BY);
			SetComponent(EditorParams, 0, editorAX, currentAX.GetValue());
			SetComponent(EditorParams, 1, editorAY, currentAY.GetValue());
			SetComponent(EditorParams, 2, editorBX, currentBX.GetValue());
			SetComponent(EditorParams, 3, editorBY, currentBY.GetValue());
		}
	}
}
