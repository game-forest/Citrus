using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TriggerPropertyEditor : CommonPropertyEditor<string>
	{
		private readonly EditBox editBox;
		private readonly Node node;

		public TriggerPropertyEditor(IPropertyEditorParams editorParams, bool multiline = false) : base(editorParams)
		{
			if (EditorParams.Objects.Skip(1).Any()) {
				EditorContainer.AddNode(CreateWarning("Edit of triggers isn't supported for multiple selection."));
				return;
			}
			node = (Node)editorParams.Objects.First();
			var button = new ThemedButton {
				Text = "...",
				MinMaxWidth = 20,
				LayoutCell = new LayoutCell(Alignment.Center),
			};
			EditorContainer.AddNode(editBox = editorParams.EditBoxFactory());
			EditorContainer.AddNode(Spacer.HSpacer(4));
			EditorContainer.AddNode(button);
			EditorContainer.AddNode(Spacer.HStretch());
			editBox.Submitted += text => {
				var newValue = FilterTriggers(text);
				editBox.Text = newValue;
				SetProperty(newValue);
			};
			button.Clicked += () => {
				var value = CoalescedPropertyValue().GetValue().Value;
				var currentTriggers = string.IsNullOrEmpty(value) ?
					new HashSet<string>() :
					value.Split(',').Select(el => el.Trim()).ToHashSet();
				var window = new TriggerSelectionDialog(
					node,
					currentTriggers,
					s => {
						s = FilterTriggers(s);
						SetProperty(s);
						editBox.Text = s;
					}
				);
			};
			editBox.AddLateChangeWatcher(CoalescedPropertyValue(), v => editBox.Text = v.Value);
			Invalidate();
		}

		public void Invalidate()
		{
			var value = CoalescedPropertyValue().GetValue().Value;
			if (node != null && editBox != null) {
				editBox.Text = FilterTriggers(value);
			}
		}

		private Dictionary<string, HashSet<string>> GetAvailableTriggers()
		{
			var triggers = new Dictionary<string, HashSet<string>>();
			foreach (var a in node.Animations) {
				foreach (var m in a.Markers.Where(i => !string.IsNullOrEmpty(i.Id))) {
					var id = a.Id != null ? m.Id + '@' + a.Id : m.Id;
					var key = a.Id ?? string.Empty;
					if (!triggers.Keys.Contains(key)) {
						triggers[key] = new HashSet<string>();
					}
					if (!triggers[key].Contains(id)) {
						triggers[key].Add(id);
					}
				}
			}
			return triggers;
		}

		private Widget CreateWarning(string message)
		{
			return new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new ThemedSimpleText {
						Text = message,
						Padding = Theme.Metrics.ControlsPadding,
						LayoutCell = new LayoutCell(Alignment.Center),
						VAlignment = VAlignment.Center,
						ForceUncutText = false,
					},
				},
				Presenter = new WidgetFlatFillPresenter(Theme.Colors.WarningBackground),
			};
		}

		private string FilterTriggers(string text)
		{
			var newValue = string.Empty;
			if (!string.IsNullOrEmpty(text)) {
				var triggersToSet = text.Split(',').ToList();
				var triggers = GetAvailableTriggers();
				foreach (var key in triggers.Keys) {
					foreach (var trigger in triggersToSet) {
						if (triggers[key].Contains(trigger.Trim(' '))) {
							newValue += trigger.Trim(' ') + ',';
							break;
						}
					}
				}
				if (!string.IsNullOrEmpty(newValue)) {
					newValue = newValue.Trim(',');
				}
			}
			return newValue;
		}
	}
}
