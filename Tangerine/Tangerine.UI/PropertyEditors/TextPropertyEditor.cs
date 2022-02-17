using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TextPropertyEditor : CommonPropertyEditor<string>
	{
		private const int MaxLines = 5;
		private EditBox editor;

		public TextPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			ThemedButton button;
			EditorContainer.AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					(editor = editorParams.EditBoxFactory()),
					Spacer.HSpacer(4),
					(button = new ThemedButton {
						Text = "...",
						MinMaxWidth = 20,
						LayoutCell = new LayoutCell(Alignment.Center),
					}),
				},
			});
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			editor.Editor.EditorParams.MaxLines = MaxLines;
			editor.MinHeight += editor.TextWidget.FontHeight * (MaxLines - 1);
			var first = true;
			var submitted = false;
			var current = CoalescedPropertyValue();
			editor.AddLateChangeWatcher(current, v => editor.Text = v.IsDefined ? v.Value : ManyValuesText);
			button.Clicked += () => {
				var window = new TextEditorDialog(
					title: editorParams.DisplayName ?? editorParams.PropertyName,
					text: editor.Text,
					onSave: (s) => {
						SetProperty(s);
					}
				);
			};
			editor.Submitted += text => Submit();
			editor.AddLateChangeWatcher(() => editor.Text, text => {
				if (first) {
					first = false;
					return;
				}
				if (!editor.IsFocused()) {
					return;
				}
				if (submitted) {
					Document.Current.History.Undo();
				}
				submitted = true;
				Submit();
			});
			editor.AddLateChangeWatcher(() => editor.IsFocused(), focused => {
				if (submitted) {
					Document.Current.History.Undo();
				}
				if (!focused) {
					submitted = false;
				}
			});
			ManageManyValuesOnFocusChange(editor, current);
		}

		public override void Submit()
		{
			SetProperty(editor.Text);
		}
	}
}
