using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class StringPropertyEditor : CommonPropertyEditor<string>
	{
		private const int MaxLines = 5;
		private EditBox editor;

		public StringPropertyEditor(IPropertyEditorParams editorParams, bool multiline = false) : base(editorParams)
		{
			editor = editorParams.EditBoxFactory();
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			editor.Editor.EditorParams.MaxLines = multiline ? MaxLines : 1;
			editor.MinHeight += multiline ? editor.TextWidget.FontHeight * (MaxLines - 1) : 0;
			EditorContainer.AddNode(editor);
			editor.Submitted += text => SetProperty(text);
			var current = CoalescedPropertyValue();
			editor.AddLateChangeWatcher(current, v => editor.Text = v.IsDefined ? v.Value : ManyValuesText);
			ManageManyValuesOnFocusChange(editor, current);
		}

		public override void Submit()
		{
			SetProperty(editor.Text);
		}
	}
}
