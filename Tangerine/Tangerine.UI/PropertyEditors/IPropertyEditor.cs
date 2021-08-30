using System.Collections.Generic;
using Lime;

namespace Tangerine.UI
{
	public interface IPropertyEditor
	{
		IPropertyEditorParams EditorParams { get; }
		Widget ContainerWidget { get; }
		Widget LabelContainer { get; }
		Widget EditorContainer { get; }
		SimpleText PropertyLabel { get; }
		void DropFiles(IEnumerable<string> files);
		bool Enabled { get; set; }
		void Submit();
	}
}
