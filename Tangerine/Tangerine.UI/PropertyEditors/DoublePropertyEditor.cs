using Lime;
using Tangerine.Core;
using Tangerine.Core.ExpressionParser;

namespace Tangerine.UI
{
	public class DoublePropertyEditor : CommonPropertyEditor<double>
	{
		public DoublePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			var editor = editorParams.NumericEditBoxFactory();
			EditorContainer.AddNode(editor);
			var current = CoalescedPropertyValue();
			editor.Submitted += text => {
				if (Parser.TryParse(text, out double newValue)) {
					SetProperty(newValue);
				} else {
					var currentValue = current.GetValue();
					editor.Text = currentValue.IsDefined ? currentValue.Value.ToString("0.###") : ManyValuesText;
				}
			};
			editor.AddChangeLateWatcher(current, v => editor.Text = v.IsDefined ? v.Value.ToString("0.###") : ManyValuesText);
			ManageManyValuesOnFocusChange(editor, current);
		}
	}
}
