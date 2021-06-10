using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI.PropertyEditors
{
	public class DictionaryPropertyEditor<TDictionary, TValue> :
		ExpandablePropertyEditor<TDictionary> where TDictionary : IDictionary<string, TValue>, IDictionary
	{
		private readonly Func<
			Type,
			PropertyEditorParams,
			Widget,
			object,
			IEnumerable<IPropertyEditor>
		> populateEditors;
		private static TValue DefaultValue => typeof(TValue) == typeof(string)
			? (TValue)(object)string.Empty
			: typeof(TValue).IsInterface || typeof(TValue).IsAbstract
				? default
				: Activator.CreateInstance<TValue>();
		private readonly KeyValuePair keyValueToAdd = new KeyValuePair { Key = "", Value = DefaultValue, };
		private TDictionary dictionary;
		private readonly HashSet<KeyValuePair> pairs = new HashSet<KeyValuePair>();

		private class KeyValuePair
		{
			public string Key { get; set; }
			public TValue Value { get; set; }

			public override int GetHashCode() => Key.GetHashCode();
		}

		public DictionaryPropertyEditor(
			IPropertyEditorParams editorParams,
			Func<Type, PropertyEditorParams, Widget, object, IEnumerable<IPropertyEditor>> populateEditors
		) : base(editorParams) {
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
			this.populateEditors = populateEditors;
			dictionary = PropertyValue(EditorParams.Objects.First()).GetValue();
			var addButton = new ThemedAddButton {
				Clicked = () => {
					if (dictionary == null) {
						var pi = EditorParams.PropertyInfo;
						var o = EditorParams.Objects.First();
						pi.SetValue(o, dictionary = Activator.CreateInstance<TDictionary>());
					}
					if (dictionary.ContainsKey(keyValueToAdd.Key)) {
						AddWarning($"Key \"{keyValueToAdd.Key}\" already exists.", ValidationResult.Warning);
						return;
					}
					using (Document.Current.History.BeginTransaction()) {
						InsertIntoDictionary<TDictionary, string, TValue>.Perform(dictionary, keyValueToAdd.Key,
							keyValueToAdd.Value);
						ExpandableContent.Nodes.Add(CreateDefaultKeyValueEditor(keyValueToAdd.Key,
							keyValueToAdd.Value));
						Document.Current.History.CommitTransaction();
					}
					keyValueToAdd.Value = DefaultValue;
					keyValueToAdd.Key = string.Empty;
				},
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
			};
			var keyEditorContainer = CreateKeyEditor(
				editorParams: editorParams,
				keyValue: keyValueToAdd,
				submitted: s => keyValueToAdd.Key = s,
				button: addButton
			);
			ExpandableContent.Nodes.Add(keyEditorContainer);
			ExpandableContent.Nodes.Add(CreateValueEditor(editorParams, keyValueToAdd, populateEditors));
			Rebuild();
			EditorContainer.Tasks.AddLoop(() => {
				if (dictionary != null && ((ICollection)dictionary).Count != pairs.Count) {
					Rebuild();
				}
			});
			var current = PropertyValue(EditorParams.Objects.First());
			ContainerWidget.AddChangeWatcher(() => current.GetValue(), d => {
				dictionary = d;
				Rebuild();
			});
		}

		private void Rebuild()
		{
			pairs.Clear();
			if (dictionary != null) {
				ExpandableContent.Nodes.RemoveRange(2, ExpandableContent.Nodes.Count - 2);
				foreach (var kv in dictionary) {
					ExpandableContent.Nodes.Add(CreateDefaultKeyValueEditor(kv.Key, kv.Value));
				}
			}
		}

		private Widget CreateDefaultKeyValueEditor(string key, TValue value)
		{
			var keyValue = new KeyValuePair { Key = key, Value = value, };
			pairs.Add(keyValue);
			Widget keyEditorContainer;
			var deleteButton = new ThemedDeleteButton();
			var container = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(keyEditorContainer = CreateKeyEditor(
						editorParams: EditorParams,
						keyValue: keyValue, submitted: s => SetKey(keyValue, s),
						button: deleteButton
					)),
					CreateValueEditor(
						editorParams: EditorParams,
						keyValue: keyValue,
						populateEditors: populateEditors,
						setter: (o, name, v) => SetValue(keyValue, (TValue)v)
					),
				}
			};
			keyEditorContainer.Tasks.AddLoop(() => {
				if (dictionary.TryGetValue(keyValue.Key, out value)) {
					keyValue.Value = value;
				} else {
					Rebuild();
				}
			});
			deleteButton.Clicked += () => {
				pairs.Remove(keyValue);
				using (Document.Current.History.BeginTransaction()) {
					RemoveFromDictionary<TDictionary, string, TValue>.Perform(dictionary, keyValue.Key);
					Document.Current.History.CommitTransaction();
				}
				container.UnlinkAndDispose();
			};
			container.CompoundPresenter.Add(new WidgetFlatFillPresenter(
				pairs.Count % 2 == 0
					? ColorTheme.Current.Inspector.StripeBackground1
					: ColorTheme.Current.Inspector.StripeBackground2
			) { IgnorePadding = true });
			return container;
		}

		private void SetKey(KeyValuePair keyValue, string key)
		{
			if (dictionary.ContainsKey(key)) {
				AddWarning($"Key \"{key}\" already exists.", ValidationResult.Warning);
				return;
			}
			using (Document.Current.History.BeginTransaction()) {
				RemoveFromDictionary<TDictionary, string, TValue>.Perform(dictionary, keyValue.Key);
				keyValue.Key = key;
				InsertIntoDictionary<TDictionary, string, TValue>.Perform(dictionary, key, keyValue.Value);
				Document.Current.History.CommitTransaction();
			}
		}

		private void SetValue(KeyValuePair keyValue, TValue value)
		{
			using (Document.Current.History.BeginTransaction()) {
				InsertIntoDictionary<TDictionary, string, TValue>.Perform(dictionary, keyValue.Key, value);
				keyValue.Value = value;
				Document.Current.History.CommitTransaction();
			}
		}

		private static Widget CreateKeyEditor(
			IPropertyEditorParams editorParams,
			KeyValuePair keyValue,
			Action<string> submitted,
			Widget button
		) {
			var keyEditor = editorParams.EditBoxFactory();
			keyEditor.AddChangeWatcher(() => keyValue.Key, s => keyEditor.Text = s);
			keyEditor.Submitted += submitted;
			var keyLabelContainer = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell { StretchX = 1.0f },
				Nodes = {
					new ThemedSimpleText {
						Text = "Key",
						VAlignment = VAlignment.Center,
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						ForceUncutText = false,
						Padding = new Thickness(left: 5f),
						HitTestTarget = true,
						TabTravesable = new TabTraversable(),
					},
				},
			};
			var editorContainer = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell { StretchX = 2.0f, },
				Nodes = {
					new Widget {
						Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4 },
						Nodes = { keyEditor, Spacer.HSpacer(4), button, Spacer.HStretch(), },
					},
				},
			};
			return new Widget {
				Layout = new HBoxLayout { IgnoreHidden = false, },
				LayoutCell = new LayoutCell { StretchY = 0f, },
				Nodes = { keyLabelContainer, editorContainer, },
			};
		}

		private static Widget CreateValueEditor(
			IPropertyEditorParams editorParams,
			KeyValuePair keyValue,
			Func<Type, PropertyEditorParams, Widget, object, IEnumerable<IPropertyEditor>> populateEditors,
			PropertySetterDelegate setter = null
		) {
			var valueContainer = new Widget { Layout = new HBoxLayout() };
			var valuePropertyEditorParams = new PropertyEditorParams(
				valueContainer,
				new object[] { keyValue },
				editorParams.RootObjects,
				typeof(KeyValuePair),
				"Value",
				"Value"
			) {
				History = editorParams.History,
			};
			if (setter != null) {
				valuePropertyEditorParams.PropertySetter = setter;
			}
			var valueEditor = populateEditors(
				typeof(KeyValuePair),
				valuePropertyEditorParams,
				valueContainer, keyValue
			).First();
			// Hack in order to keep same background for KeyValue pair.
			valueEditor.ContainerWidget.CompoundPresenter
				.RemoveAt(valueEditor.ContainerWidget.CompoundPresenter.Count - 1);
			valueEditor.ContainerWidget.Padding = new Thickness(0f, 0f, 0f, 0f);
			return valueContainer;
		}
	}
}
