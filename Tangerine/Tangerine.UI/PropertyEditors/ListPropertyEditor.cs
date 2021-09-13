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
		private readonly Func<PropertyEditorParams, Widget> onAdd;
		private IList list;
		private readonly List<int> pendingRemovals;
		private readonly List<IAnimator> animatorsToRemove = new List<IAnimator>();

		public ListPropertyEditor(
			IPropertyEditorParams editorParams,
			Func<PropertyEditorParams, Widget> onAdd
		) : base(editorParams)
		{
			this.onAdd = onAdd;
			pendingRemovals = new List<int>();
			if (EditorParams.Objects.Skip(1).Any()) {
				// Don't create editor interface if > 1 objects are selected
				EditorContainer.AddNode(new Widget() {
					Layout = new HBoxLayout(),
					Nodes = { new ThemedSimpleText {
						Text = "Editing of list properties isn't supported when multiple nodes are selected.",
						ForceUncutText = false
					} },
					Presenter = new WidgetFlatFillPresenter(Theme.Colors.WarningBackground)
				});
				return;
			}
			ExpandableContent.Padding = new Thickness(left: 4.0f, right: 0.0f, top: 4.0f, bottom: 4.0f);
			list = PropertyValue(EditorParams.Objects.First()).GetValue();

			EditorContainer.AddNode(
				new ThemedAddButton() {
					Clicked = () => {
						Expanded = true;
						if (list == null) {
							var pi = EditorParams.PropertyInfo;
							if (IsSetterPrivate()) {
								ShowPrivateSetterAlert();
								return;
							}
							// TODO: this should be part of transaction to be able to undo
							var o = EditorParams.Objects.First();
							pi.SetValue(o, list = Activator.CreateInstance<TList>());
						}
						var newElement = typeof(TElement) == typeof(string)
							? (TElement)(object)string.Empty
							: typeof(TElement).IsInterface || typeof(TElement).IsAbstract
								? default
								:  Activator.CreateInstance<TElement>();
						using (Document.Current.History.BeginTransaction()) {
							int newIndex = list.Count;
							InsertIntoList.Perform(list, newIndex, newElement);
							Document.Current.History.CommitTransaction();
						}
					}
				}
			);
			EditorContainer.AddNode(new ThemedDeleteButton { Clicked = () => { pendingRemovals.Add(2); pendingRemovals.Add(5); } });
			var current = PropertyValue(EditorParams.Objects.First());
			// Commands (e.g. Undo) are processed between the Early Update and before the Late Update.
			// NodeManager processes regular ChangeWatcher at EarlyUpdateStage, and LateChangeWatcher at
			// LateUpdateStage. So the order is:
			// ChangeWatcher (EarlyUpdateStage) => Commands => LateChangeWatcher (LateUpdateStage).
			// It is possible that there are nested change watchers somewhere down the hierarchy, watching for
			// coalesced value changes. When value is being deleted by both user or Undo command
			// we must remove nodes owning those change watchers before they'll be processed. Otherwise
			// they'll crash trying to access removed list element. To fix this as a temporary solution
			// we made PreLateChangeWatcher which is processed at PreLateUpdateStage which is before LateUpdateStage.
			ContainerWidget.AddPreLateChangeWatcher(
				getter: () => (list?.Count ?? 0) - pendingRemovals.Count,
				action: RemoveAndBuild
			);
			ContainerWidget.AddChangeWatcher(
				getter: () => current.GetValue(),
				action: l => {
					list = l;
					pendingRemovals.Clear();
					if (list != null) {
						RemoveAndBuild(list.Count);
					}
				}
			);
		}

		private void RemoveAndBuild(int newCount)
		{
			if (list == null) {
				return;
			}

			// We have to remove items exactly before rebuilding
			// because there is no mechanism to update dataflows
			// (in this case -- CoalescedPropertyValue with IndexedProperty underneath)
			if (pendingRemovals.Count > 0) {
				pendingRemovals.Sort();
				var animators = (list as IAnimable)?.GetAnimationHost()?.Animators;
				using (Document.Current.History.BeginTransaction()) {
					var oldCount = list.Count;
					for (int i = pendingRemovals.Count - 1; i >= 0; --i) {
						RemoveFromList.Perform(list, pendingRemovals[i]);
					}
					if (animators != null) {
						foreach (var removed in pendingRemovals) {
							var propertyPath = EditorParams.PropertyPath + $".Item[{removed}]";
							animatorsToRemove.AddRange(
								animators.Where(a => a.TargetPropertyPath.Contains(propertyPath))
							);
							foreach (var animator in animatorsToRemove) {
								var min = animator.Keys[0].Frame;
								var max = animator.Keys[animator.Keys.Count - 1].Frame;
								RemoveKeyframeRange.Perform(animator, min, max);
							}
							animatorsToRemove.Clear();
						}
						if (pendingRemovals.Count != 0) {
							var from = EditorParams.PropertyPath + $".Item[";
							foreach (var animator in animators) {
								var tpp = animator.TargetPropertyPath;
								if (tpp.StartsWith(from)) {
									var closingBracketIndex = tpp.IndexOf(']', from.Length);
									var index = int.Parse(tpp.Substring(from.Length, closingBracketIndex - from.Length));
									var offset = ~pendingRemovals.BinarySearch(index);
									if (offset == 0) {
										continue;
									}
									var newPropertyPath =
										from
										+ (index - offset).ToString()
										+ tpp.Substring(closingBracketIndex);
									ChangeAnimatorsTargetPropertyPath.Perform(animator, newPropertyPath);
								}
							}
						}
					}
					pendingRemovals.Clear();
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
			var p = new PropertyEditorParams(
				new[] { list },
				EditorParams.RootObjects,
				EditorParams.PropertyInfo.PropertyType,
				"Item",
				EditorParams.PropertyPath + $".Item[{index}]"
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
			var editorWidget = onAdd(p);
			ExpandableContent.Nodes.Insert(index, editorWidget);
			var removeButton = new ThemedDeleteButton { Enabled = Enabled };
			removeButton.Clicked += () => pendingRemovals.Add(p.IndexInList);
			var editorContainer = editorWidget.Components.Get<PropertyEditorComponent>()
				.PropertyEditor.EditorContainer;
			var upButton = new ToolbarButton(IconPool.GetTexture("Universal.ArrowUp"));
			upButton.Clicked += () => MoveItemTo(index, index - 1);
			var downButton = new ToolbarButton(IconPool.GetTexture("Universal.ArrowDown"));
			downButton.Clicked += () => MoveItemTo(index, index + 1);
			editorContainer.AddNode(upButton);
			editorContainer.AddNode(downButton);
			editorContainer.AddNode(removeButton);
		}

		public void MoveItemTo(int index, int to)
		{
			if (to < 0 || to >= list.Count) {
				return;
			}
			var pathFrom = EditorParams.PropertyPath + $".Item[{index}]";
			var pathTo = EditorParams.PropertyPath + $".Item[{to}]";
			var animators = (list as IAnimable)?.GetAnimationHost()?.Animators;
			using (Document.Current.History.BeginTransaction()) {
				var value = list[index];
				RemoveFromList.Perform(list, index);
				InsertIntoList.Perform(list, to, value);
				if (animators != null) {
					foreach (var animator in animators) {
						if (animator.TargetPropertyPath.StartsWith(pathFrom)) {
							var newPropertyPath = animator.TargetPropertyPath.Replace(pathFrom, pathTo);
							ChangeAnimatorsTargetPropertyPath.Perform(animator, newPropertyPath);
						} else if (animator.TargetPropertyPath.StartsWith(pathTo)) {
							var newPropertyPath = animator.TargetPropertyPath.Replace(pathTo, pathFrom);
							ChangeAnimatorsTargetPropertyPath.Perform(animator, newPropertyPath);
						}
					}
				}
				Document.Current.History.CommitTransaction();
			}
			RemoveAndBuild(list.Count);
		}
	}
}
