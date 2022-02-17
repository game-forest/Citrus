using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class NodeIdPropertyEditor : CommonPropertyEditor<string>
	{
		private EditBox editor;

		public NodeIdPropertyEditor(IPropertyEditorParams editorParams, bool multiline = false) : base(editorParams)
		{
			editor = editorParams.EditBoxFactory();
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			editor.Editor.EditorParams.MaxLines = 1;
			EditorContainer.AddNode(editor);
			editor.Submitted += SetValue;
			editor.AddLateChangeWatcher(
				CoalescedPropertyValue(), v => editor.Text = v.IsDefined ? v.Value : ManyValuesText
			);
			ManageManyValuesOnFocusChange(editor, CoalescedPropertyValue());
		}

		private void SetValue(string value)
		{
			SetProperty(editor.Text);
			editor.Text = SameValues() ? PropertyValue(EditorParams.Objects.First()).GetValue() : ManyValuesText;
		}
	}
}
