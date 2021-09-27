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
		private const string LegacyAnimationName = "<Legacy>";
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
					Nodes = {
						new ThemedSimpleText {
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
								new ThemedAddButton { Clicked = AddAnimationBlendingMenu, },
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
			var keyEditor = EditorParams.DropDownListFactory();
			var valueEditor = EditorParams.NumericEditBoxFactory();
			var animationExists = false;
			foreach (var animationId in GetAnimationIds()) {
				var (text, value) = animationId == null
					? (LegacyAnimationName, "") : (animationId, animationId);
				animationExists |= value == kv.Key;
				keyEditor.Items.Add(new CommonDropDownList.Item(text, value));
			}
			// Text is used over Value because animation can be renamed or deleted.
			if (animationExists) {
				keyEditor.Value = kv.Key;
			} else {
				keyEditor.Text = kv.Key;
				AddWarning($"Animation \"{kv.Key}\" doesn't exist.", ValidationResult.Warning);
			}
			valueEditor.MaxWidth = float.PositiveInfinity;
			valueEditor.Value = (float)(kv.Value?.Option?.Frames ?? 0f);
			var keyValue = new KeyValuePair { Key = kv.Key, Value = valueEditor.Value, };
			var deleteButton = new ThemedDeleteButton { Clicked = () => DeleteRecord(keyValue.Key), };
			keyEditor.Changed += (args) => SetKey(keyValue, (string)args.Value);
			valueEditor.Submitted += ((_) => SetValue(keyValue, valueEditor.Value));
			keyEditor.Tasks.AddLoop(() => {
				if (!dictionary.TryGetValue(keyValue.Key, out var animationBlending)) {
					Rebuild();
					return;
				}
				var value = (int)(animationBlending.Option?.Frames ?? 0);
				if (value != (int)keyValue.Value) {
					keyValue.Value = value;
					valueEditor.Value = value;
				}
			});
			rows.AddNode(new Widget {
				Layout = new HBoxLayout {
					DefaultCell = new DefaultLayoutCell(Alignment.Center),
					Spacing = Spacing,
				},
				Nodes = {
					keyEditor,
					valueEditor,
					deleteButton,
					Spacer.HStretch(),
				},
			});
		}

		private IEnumerable<string> GetAnimationIds()
		{
			var node = (Node)EditorParams.RootObjects.First();
			var sceneTree = Document.Current.GetSceneItemForObject(node);
			if (node == null) {
				yield break;
			}
			yield return null;
			var used = new HashSet<string>();
			do {
				while (!sceneTree.TryGetNode(out _)) {
					sceneTree = sceneTree.Parent;
				}
				foreach (var animationSceneItem in sceneTree.Rows) {
					var animation = animationSceneItem.GetAnimation();
					if (animation == null || animation.Id == null) {
						continue;
					}
					if (used.Add(animation.Id)) {
						yield return animation.Id;
					}
				}
				sceneTree = sceneTree.Parent;
			} while (sceneTree != null);
		}

		private void DeleteRecord(string key)
		{
			using (Document.Current.History.BeginTransaction()) {
				RemoveFromDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, key);
				Document.Current.History.CommitTransaction();
			}
		}

		private void AddAnimationBlendingMenu()
		{
			var menu = new Menu();
			foreach (var animationId in GetAnimationIds()) {
				var (text, key) = animationId == null
					? (LegacyAnimationName, "") : (animationId, animationId);
				if (!dictionary.ContainsKey(key)) {
					menu.Add(new Command(text, () => AddAnimationBlending(key)));
				}
			}
			if (menu.Count != 0) {
				menu.Popup();
			} else {
				ClearWarningsAndValidate();
				AddWarning("There is no animation left to blend.", ValidationResult.Info);
			}
		}

		private void AddAnimationBlending(string animationId)
		{
			ClearWarningsAndValidate();
			if (dictionary.ContainsKey(animationId)) {
				AddWarning($"Key already exists.", ValidationResult.Warning);
				return;
			}
			using (Document.Current.History.BeginTransaction()) {
				InsertIntoDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, animationId, new AnimationBlending());
				Document.Current.History.CommitTransaction();
			}
		}

		private void SetValue(KeyValuePair keyValue, double value)
		{
			if ((int)value == (int)keyValue.Value) {
				return;
			}
			using (Document.Current.History.BeginTransaction()) {
				var blending = new AnimationBlending() {
					Option = (int)value == 0 ? null : new BlendingOption { Frames = (int)value, },
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
					Option = keyValue.Value == 0
						? null : new BlendingOption() { Frames = (int)keyValue.Value, },
				};
				InsertIntoDictionary<Dictionary<string, AnimationBlending>, string, AnimationBlending>
					.Perform(dictionary, key, blending);
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
