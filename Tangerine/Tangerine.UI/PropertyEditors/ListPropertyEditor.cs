using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI
{
	public class ListPropertyEditor<TList, TElement>
		: ExpandablePropertyEditor<TList> where TList : IList<TElement>, IList
	{
		private readonly Func<PropertyEditorParams, Widget, IList, IEnumerable<IPropertyEditor>> onAdd;
		private IList list;
		private List<int> pendingRemoval;

		public ListPropertyEditor(IPropertyEditorParams editorParams, Func<PropertyEditorParams, Widget, IList, IEnumerable<IPropertyEditor>> onAdd) : base(editorParams)
		{
			this.onAdd = onAdd;
			pendingRemoval = new List<int>();
			if (EditorParams.Objects.Skip(1).Any()) {
				// Dont create editor interface if > 1 objects are selected
				EditorContainer.AddNode(new Widget() {
					Layout = new HBoxLayout(),
					Nodes = { new ThemedSimpleText { Text = "Edit of list properties isnt supported for multiple selection.", ForceUncutText = false } },
					Presenter = new WidgetFlatFillPresenter(Theme.Colors.WarningBackground)
				});
				return;
			}
			ExpandableContent.Padding = new Thickness(left: 4.0f, right: 0.0f, top: 4.0f, bottom: 4.0f);
			list = PropertyValue(EditorParams.Objects.First()).GetValue();
			Expanded = true;

			EditorContainer.AddNode(new ThemedAddButton() {
				Clicked = () => {
					Expanded = true;
					if (list == null) {
						var pi = EditorParams.PropertyInfo;
						var o = EditorParams.Objects.First();
						pi.SetValue(o, list = Activator.CreateInstance<TList>());
					}
					var newElement = typeof(TElement) == typeof(string) ?
						(TElement)(object)string.Empty : typeof(TElement).IsInterface ?
							default :  Activator.CreateInstance<TElement>();
					using (Document.Current.History.BeginTransaction()) {
						int newIndex = list.Count;
						InsertIntoList.Perform(list, newIndex, newElement);
						Document.Current.History.CommitTransaction();
					}
				}
			});
			var current = PropertyValue(EditorParams.Objects.First());
			ContainerWidget.AddChangeWatcher(() => (list?.Count ?? 0) - pendingRemoval.Count, RemoveAndBuild);
			ContainerWidget.AddChangeWatcher(() => current.GetValue(), l => {
				list = l;
				pendingRemoval.Clear();
				if (list != null) {
					RemoveAndBuild(list.Count);
				}
			});
		}

		private void RemoveAndBuild(int newCount)
		{
			if (list == null) {
				return;
			}

			// We have to remove items exactly before rebuilding
			// because there is no mechanism to update dataflows
			// (in this case -- CoalescedPropertyValue with IndexedProperty underneath)
			if (pendingRemoval.Count > 0) {
				pendingRemoval.Sort();
				using (Document.Current.History.BeginTransaction()) {
					for (int i = pendingRemoval.Count - 1; i >= 0; --i) {
						RemoveFromList.Perform(list, pendingRemoval[i]);
					}
					pendingRemoval.Clear();
					Document.Current.History.CommitTransaction();
				}
			}

			for (int i = ExpandableContent.Nodes.Count - 1; i >= 0; --i) {
				ExpandableContent.Nodes[i].UnlinkAndDispose();
			}
			for (int i = 0; i < newCount; ++i) {
				AfterInsertNewElement(i);
			}
		}

		private void AfterInsertNewElement(int index)
		{
			var elementContainer = new Widget { Layout = new VBoxLayout() };
			var p = new PropertyEditorParams(
				elementContainer, new[] { list }, EditorParams.RootObjects,
				EditorParams.PropertyInfo.PropertyType, "Item", EditorParams.PropertyPath + $".Item[{index}]"
			) {
				NumericEditBoxFactory = EditorParams.NumericEditBoxFactory,
				History = EditorParams.History,
				DefaultValueGetter = () => default,
				IndexInList = index,
				IsAnimableByPath = EditorParams.IsAnimableByPath && list is IAnimable,
				DisplayName = $"{index}:"
			};
			p.PropertySetter = p.IsAnimable
				? (PropertySetterDelegate)((@object, name, value) =>
					SetAnimableProperty.Perform(@object, name, value, CoreUserPreferences.Instance.AutoKeyframes))
				: (@object, name, value) => SetIndexedProperty.Perform(@object, name, index, value);
			var editor = onAdd(p, elementContainer, list).ToList().First();
			var removeButton = new ThemedDeleteButton {
				Enabled = Enabled
			};

			ExpandableContent.Nodes.Insert(index, elementContainer);
			removeButton.Clicked += () => pendingRemoval.Add(p.IndexInList);
			editor.EditorContainer.AddNode(removeButton);
		}
	}
}
