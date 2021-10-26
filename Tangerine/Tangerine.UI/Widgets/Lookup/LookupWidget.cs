using System;
using System.Collections.Generic;
using System.Linq;
using Lime;

namespace Tangerine.UI
{
	public class LookupWidget : Widget
	{
		private readonly List<LookupItem> items = new List<LookupItem>();
		private readonly List<LookupItem> filteredItems = new List<LookupItem>();
		private int selectedIndex = -1;
		private ILookupDataSource dataSource;
		private ILookupFilter filter = new LookupFuzzyFilter();
		private bool isFilterDirty;
		private string previousFilterText;

		public readonly ThemedEditBox FilterEditBox;
		public readonly Widget BreadcrumbWidget;
		public readonly ThemedSimpleText HintSimpleText;
		public readonly ThemedScrollView ScrollView;

		public event Action Submitted;
		public event Action NavigatedBack;
		public event Action Canceled;
		public event Action FilterApplied;

		public string FilterText
		{
			get => FilterEditBox.Text;
			set
			{
				FilterEditBox.Text = value;
				if (FilterEditBox.Editor.Enabled) {
					FilterEditBox.Editor.CaretPos.TextPos = FilterEditBox.Text.Length;
				}
			}
		}

		public string HintText
		{
			get => HintSimpleText.Text;
			set => HintSimpleText.Text = value;
		}

		public ILookupDataSource DataSource
		{
			get => dataSource;
			set
			{
				if (dataSource == value) {
					return;
				}
				dataSource = value;
				ClearItems();
				dataSource?.Fill(this);
			}
		}

		public ILookupFilter Filter
		{
			get => filter;
			set
			{
				if (filter == value) {
					return;
				}
				filter = value;
				isFilterDirty = true;
			}
		}

		public LookupItem SelectedItem => selectedIndex >= 0 && selectedIndex < filteredItems.Count ? filteredItems[selectedIndex] : null;

		public LookupWidget()
		{
			Layout = new VBoxLayout { Spacing = 8 };
			Padding = new Thickness { Top = 5 };

			AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					(BreadcrumbWidget = new ThemedSimpleText {
						Layout = new HBoxLayout(),
						Padding = new Thickness(right: Theme.Metrics.ControlsPadding.Right),
					}),
					(FilterEditBox = new ThemedEditBox()),
				}
			});
			HintSimpleText = new ThemedSimpleText {
				Layout = new VBoxLayout(),
				Padding = Theme.Metrics.ControlsPadding,
				VAlignment = VAlignment.Center,
				Color = Theme.Colors.GrayText,
			};
			FilterEditBox.AddNode(HintSimpleText);
			FilterEditBox.LateTasks.AddLoop(() => {
				if (isFilterDirty || FilterText != previousFilterText) {
					FilterChanged(FilterText);
				}
			});

			ScrollView = new ThemedScrollView {
				Content = { Layout = new VBoxLayout() }
			};
			AddNode(ScrollView);

			const int ItemsPerPage = 10;
			void SelectPreviousItem() => SelectItem(selectedIndex - 1);
			void SelectNextItem() => SelectItem(selectedIndex + 1);
			void SelectPreviousPage() => SelectItem(selectedIndex - ItemsPerPage);
			void SelectNextPage() => SelectItem(selectedIndex + ItemsPerPage);

			var bindings = new[] {
				new { Command = Commands.Submit, Action = (Action)Submit },
				new { Command = Commands.Cancel, Action = (Action)Cancel },
				new { Command = Commands.SelectPreviousItem, Action = (Action)SelectPreviousItem },
				new { Command = Commands.SelectNextItem, Action = (Action)SelectNextItem },
				new { Command = Commands.SelectPreviousPage, Action = (Action)SelectPreviousPage },
				new { Command = Commands.SelectNextPage, Action = (Action)SelectNextPage },
			};
			Components.GetOrAdd<PreEarlyUpdateBehavior>().Updating += () => {
				if (string.IsNullOrEmpty(FilterText) && Commands.NavigateBack.Consume()) {
					NavigateBack();
				}
				foreach (var binding in bindings) {
					if (binding.Command.Consume()) {
						binding.Action.Invoke();
						break;
					}
				}
			};
		}

		private void FilterChanged(string text)
		{
			HintSimpleText.Visible = string.IsNullOrEmpty(text);

			isFilterDirty = false;
			previousFilterText = text;
			filter?.Applying(this, text);

			DeselectItem();
			filteredItems.Clear();
			foreach (var item in items) {
				item.IsSelected = false;
			}
			ScrollView.Content.Nodes.Clear();

			filteredItems.AddRange(filter == null ? items : Filter.Apply(text, items));
			foreach (var item in filteredItems) {
				ScrollView.Content.Nodes.Add(item.Widget);
			}
			SelectItem(index: 0);
			ScrollView.ScrollPosition = 0;

			filter?.Applied(this);
			FilterApplied?.Invoke();
		}

		public void MarkFilterAsDirty() => isFilterDirty = true;

		public void SetBreadcrumbsNavigation(params string[] breadcrumbs) => SetBreadcrumbsNavigation((IEnumerable<string>)breadcrumbs);

		public void SetBreadcrumbsNavigation(IEnumerable<string> breadcrumbs)
		{
			BreadcrumbWidget.Nodes.Clear();
			var index = 0;
			foreach (var breadcrumb in breadcrumbs.Reverse()) {
				var button = new ThemedButton(breadcrumb) {
					MinMaxHeight = Theme.Metrics.DefaultEditBoxSize.Y,
				};
				BreadcrumbWidget.PushNode(button);
				var simpleText = button.Descendants.OfType<SimpleText>().First();
				var v = simpleText.Font.MeasureTextLine(breadcrumb, simpleText.FontHeight, simpleText.LetterSpacing);
				button.MinMaxWidth =
					v.X +
					button.Padding.Left + button.Padding.Right +
					simpleText.Padding.Left + simpleText.Padding.Right +
					Theme.Metrics.ControlsPadding.Left + Theme.Metrics.ControlsPadding.Right;
				var indexClosed = index++;
				button.Clicked += () => {
					for (var i = 0; i < indexClosed; i++) {
						NavigateBack();
					}
					FilterEditBox.SetFocus();
				};
			}
		}

		public void AddRange(IEnumerable<LookupItem> collection)
		{
			foreach (var item in collection) {
				AddItem(item);
			}
		}

		public void AddItem(string text, Action action) => AddItem(new LookupItem(text, action));

		public void AddItem(LookupItem item)
		{
			item.Owner = this;
			item.CreateVisuals();
			items.Add(item);
			filteredItems.Add(item);
			ScrollView.Content.AddNode(item.Widget);
		}

		public void SelectItem(int index)
		{
			DeselectItem();
			selectedIndex = Mathf.Clamp(index, 0, filteredItems.Count - 1);
			var selectedItem = SelectedItem;
			if (selectedItem != null) {
				selectedItem.IsSelected = true;
				var p = selectedItem.Widget.Y;
				if (ScrollView.ScrollPosition > p) {
					ScrollView.ScrollPosition = p;
				} else {
					p += selectedItem.Widget.Height;
					if (ScrollView.ScrollPosition + ScrollView.Height < p) {
						ScrollView.ScrollPosition = p - ScrollView.Height;
					}
				}
			} else {
				selectedIndex = -1;
			}
			Window.Current.Invalidate();
		}

		private void DeselectItem()
		{
			var selectedItem = SelectedItem;
			if (selectedItem != null) {
				selectedItem.IsSelected = false;
			}
			selectedIndex = -1;
		}

		public int FindIndex(Predicate<LookupItem> match) => filteredItems.FindIndex(match);

		private void Submit()
		{
			var selectedItem = SelectedItem;
			if (selectedItem != null) {
				Submit(selectedItem);
			} else {
				Cancel();
			}
		}

		public void Submit(LookupItem item)
		{
			item.Action.Invoke();
			Submitted?.Invoke();
		}

		public void NavigateBack() => NavigatedBack?.Invoke();

		public void Cancel()
		{
			FilterText = string.Empty;
			Canceled?.Invoke();
		}

		public void Clear()
		{
			ClearItems();
			FilterText = string.Empty;
		}

		public void ClearItems(bool disposeItems = true)
		{
			DeselectItem();
			foreach (var item in items) {
				item.Owner = null;
				if (disposeItems) {
					item.Widget.UnlinkAndDispose();
				} else {
					item.Widget.Unlink();
				}
			}
			items.Clear();
			filteredItems.Clear();
		}

		[NodeComponentDontSerialize]
		[UpdateStage(typeof(PreEarlyUpdateStage))]
		private class PreEarlyUpdateBehavior : BehaviorComponent
		{
			public event Action Updating;

			protected internal override void Update(float delta) => Updating?.Invoke();
		}

		private static class Commands
		{
			public static readonly ICommand Submit = new Command(Key.Enter);
			public static readonly ICommand Cancel = new Command(Key.Escape);
			public static readonly ICommand NavigateBack = new Command(Key.BackSpace);
			public static readonly ICommand SelectPreviousItem = new Command(Key.Up);
			public static readonly ICommand SelectNextItem = new Command(Key.Down);
			public static readonly ICommand SelectPreviousPage = new Command(Key.PageUp);
			public static readonly ICommand SelectNextPage = new Command(Key.PageDown);
		}
	}
}
