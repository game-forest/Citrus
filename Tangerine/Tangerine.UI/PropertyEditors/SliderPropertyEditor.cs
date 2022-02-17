using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class SliderPropertyEditor : CommonPropertyEditor<float>
	{
		private readonly ThemedAreaSlider slider;
		private float previousValue;

		public SliderPropertyEditor(Vector2 range, IPropertyEditorParams editorParams) : base(editorParams)
		{
			slider = new ThemedAreaSlider(range);
			EditorContainer.AddNode(slider);
			var current = CoalescedPropertyValue();
			slider.Changed += () => SetProperty(slider.Value);
			slider.DragStarted += () => {
				EditorParams.History?.BeginTransaction();
				previousValue = slider.Value;
			};
			slider.DragEnded += () => {
				if (slider.Value != previousValue || (editorParams.Objects.Skip(1).Any() && SameValues())) {
					EditorParams.History?.CommitTransaction();
				}
				EditorParams.History?.EndTransaction();
			};
			slider.AddLateChangeWatcher(
				current,
				v => {
					slider.Value = v.IsDefined ? v.Value : slider.RangeMin;
					if (!v.IsDefined) {
						slider.LabelText = ManyValuesText;
					}
				});
			ManageManyValuesOnFocusChange(slider.Editor, current);
		}
	}
}
