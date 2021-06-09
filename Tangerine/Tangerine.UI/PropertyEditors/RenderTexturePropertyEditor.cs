using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class RenderTexturePropertyEditor : CommonPropertyEditor<ITexture>
	{
		public RenderTexturePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			var editor = editorParams.EditBoxFactory();
			editor.IsReadOnly = true;
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			EditorContainer.AddNode(editor);
			var current = CoalescedPropertyValue();
			editor.AddLateChangeWatcher(current, v =>
				editor.Text = v.Value == null ?
					"RenderTexture (null)" :
					$"RenderTexture ({v.Value.ImageSize.Width}x{v.Value.ImageSize.Height})"
			);
			ManageManyValuesOnFocusChange(editor, current);
		}
	}
}
