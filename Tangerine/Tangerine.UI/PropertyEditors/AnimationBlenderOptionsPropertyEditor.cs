using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI
{
	public class AnimationBlenderOptionsPropertyEditor : CommonPropertyEditor<Dictionary<string, AnimationBlending>>
	{
		private const float Spacing = 4;
		private Widget rows;
		private Dictionary<string, AnimationBlending> dictionary;

		private class KeyValuePair
		{
			public string Key;
			public double Value;
		}

		public AnimationBlenderOptionsPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			if (EditorParams.Objects.Skip(1).Any()) {
				EditorContainer.AddNode(new Widget() {
					Layout = new HBoxLayout(),
					Nodes = { new ThemedSimpleText {
						Text = "Edit of dictionary properties isn't supported for multiple selection.",
						ForceUncutText = false
					} },
					Presenter = new WidgetFlatFillPresenter(Theme.Colors.WarningBackground)
				});
				return;
			}
			dictionary = PropertyValue(EditorParams.Objects.First()).GetValue();
			EditorContainer.AddNode(
				new Widget {
					Layout = new VBoxLayout(),
					Nodes = {
						new Widget {
							Layout = new HBoxLayout {
								DefaultCell = new DefaultLayoutCell(Alignment.Center),
								Spacing = Spacing
							},
							Nodes = {
								new ThemedSimpleText("Animation Id") {
									HAlignment = HAlignment.Center,
									ForceUncutText = false,
									MinHeight = Theme.Metrics.TextHeight,
								},
								new ThemedSimpleText("Duration (Frames)") {
									ForceUncutText = false,
									HAlignment = HAlignment.Center,
									Padding = new Thickness(0f, Theme.Metrics.CloseButtonSize.X + Spacing, 0f, 0f),
									MinHeight = Theme.Metrics.TextHeight,
								},
								Spacer.HStretch(),
							},
						},
						(rows = new Widget { Layout = new VBoxLayout(), }),
						new Widget {
							Layout = new HBoxLayout(),
							Nodes = {
								new ThemedAddButton { Clicked = AddDefaultRecord, },
								new ThemedSimpleText("Add new"),
							},
						},
					},
				}
			);
			EditorContainer.AddChangeWatcher(() => dictionary.Count, (c) => Rebuild());
		}

		private void Rebuild()
		{
			rows.Nodes.Clear();
			foreach (var kv in dictionary) {
				CreateWidgetsForKeyValuePair(kv);
			}
		}

		private void CreateWidgetsForKeyValuePair(KeyValuePair<string, AnimationBlending> kv)
		{
			var keyEditor = EditorParams.EditBoxFactory();
			var valueEditor = EditorParams.NumericEditBoxFactory();
			valueEditor.MaxWidth = float.PositiveInfinity;
			keyEditor.Text = kv.Key;
			valueEditor.Value = (float)(kv.Value?.Option?.Frames ?? 0f);
			var keyValue = new KeyValuePair { Key = keyEditor.Text, Value = valueEditor.Value, };
			var deleteButton = new ThemedDeleteButton { Clicked = () => DeleteRecord(keyValue.Key), };
			keyEditor.Submitted += (key) => SetKey(keyValue, key);
			valueEditor.AddChangeWatcher(() => valueEditor.Value, (value) => SetValue(keyValue, value));
			rows.AddNode(new Widget {
				Layout = new HBoxLayout {
					DefaultCell = new DefaultLayoutCell(Alignment.Center),
					Spacing = Spacing
				},
				Nodes = {
					keyEditor,
					valueEditor,
					deleteButton,
					Spacer.HStretch(),
				},
			});
		}

		private void DeleteRecord(string key)
		{
			using (Document.Current.History.BeginTransaction()) {
				RemoveFromDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, key);
				Document.Current.History.CommitTransaction();
			}
		}

		private void AddDefaultRecord()
		{
			ClearWarningsAndValidate();
			if (dictionary.ContainsKey("")) {
				AddWarning($"Key already exists.", ValidationResult.Warning);
				return;
			}
			using (Document.Current.History.BeginTransaction()) {
				InsertIntoDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, "", new AnimationBlending());
				Document.Current.History.CommitTransaction();
			}
		}

		private void SetValue(KeyValuePair keyValue, double value)
		{
			using (Document.Current.History.BeginTransaction()) {
				var blending = new AnimationBlending() {
					Option = (int)value == 0 ? null : new BlendingOption((int)value)
				};
				InsertIntoDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, keyValue.Key, blending);
				keyValue.Value = value;
				Document.Current.History.CommitTransaction();
			}
		}

		private void SetKey(KeyValuePair keyValue, string key)
		{
			ClearWarningsAndValidate();
			if (key == keyValue.Key) {
				return;
			}
			if (dictionary.ContainsKey(key)) {
				AddWarning($"Key \"{key}\" already exists.", ValidationResult.Warning);
				return;
			}
			using (Document.Current.History.BeginTransaction()) {
				RemoveFromDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, keyValue.Key);
				keyValue.Key = key;
				var blending = new AnimationBlending() {
					Option = keyValue.Value == 0 ? null : new BlendingOption(keyValue.Value),
				};
				InsertIntoDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, key, blending);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
