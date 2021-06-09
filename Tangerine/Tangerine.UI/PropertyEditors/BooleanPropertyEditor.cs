using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class BooleanPropertyEditor : CommonPropertyEditor<bool>
	{
		public BooleanPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			var checkBox = new ThemedCheckBox { LayoutCell = new LayoutCell(Alignment.LeftCenter) };
			EditorContainer.AddNode(checkBox);
			EditorContainer.AddNode(Spacer.HStretch());
			checkBox.Changed += args => {
				if (args.ChangedByUser) {
					SetProperty(args.Value);
				}
			};
			checkBox.AddLateChangeWatcher(CoalescedPropertyValue(), v => checkBox.State = v.IsDefined
				? v.Value ? CheckBoxState.Checked : CheckBoxState.Unchecked : CheckBoxState.Indeterminate);
		}
	}
}
