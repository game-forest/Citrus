using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class Lookup
	{
		private readonly List<LookupItem> items = new List<LookupItem>();
		private readonly List<LookupItem> filteredItems = new List<LookupItem>();
		private int selectedIndex = -1;

		public readonly Widget Widget;
		public readonly ThemedEditBox FilterEditBox;
		public readonly ThemedScrollView ScrollView;

		public event Action Submitted;
		public event Action Canceled;

		public LookupItem SelectedItem => selectedIndex >= 0 && selectedIndex < filteredItems.Count ? filteredItems[selectedIndex] : null;

		public Lookup()
		{
			Widget = new Widget {
				Layout = new VBoxLayout { Spacing = 8 },
				Padding = new Thickness { Top = 5 },
				Nodes = {
					(FilterEditBox = new ThemedEditBox()),
					(
						ScrollView = new ThemedScrollView {
							Content = { Layout = new VBoxLayout() }
						}
					),
				}
			};
			FilterEditBox.AddChangeWatcher(() => FilterEditBox.Text, FilterChanged);

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
			var inputUpdateBehavior = Widget.Components.GetOrAdd<PreEarlyUpdateBehavior>();
			inputUpdateBehavior.Updating += () => {
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
			DeselectItem();
			filteredItems.Clear();

			var itemsTemp = new List<(LookupItem item, int Distance)>();
			var matches = new List<int>();
			if (!string.IsNullOrEmpty(text)) {
				foreach (var item in items) {
					var i = -1;
					var d = 0;
					foreach (var c in text) {
						if (i == item.Text.Length - 1) {
							i = -1;
						}
						var ip = i;
						var lci = item.Text.IndexOf(char.ToLowerInvariant(c), i + 1);
						var uci = item.Text.IndexOf(char.ToUpperInvariant(c), i + 1);
						i = lci != -1 && uci != -1 ? Math.Min(lci, uci) :
							lci == -1 ? uci : lci;
						if (i == -1) {
							break;
						}
						matches.Add(i);
						if (ip != -1) {
							d += (i - ip) * (i - ip);
						}
					}
					if (i != -1) {
						itemsTemp.Add((item, d));
						item.HighlightSymbolsIndices = matches.ToArray();
					} else {
						item.HighlightSymbolsIndices = null;
					}
					matches.Clear();
				}
				itemsTemp.Sort((a, b) => a.Distance.CompareTo(b.Distance));
				ScrollView.Content.Nodes.Clear();
				foreach (var (item, _) in itemsTemp) {
					ScrollView.Content.Nodes.Add(item.Widget);
					filteredItems.Add(item);
				}

				SelectItem(0);
			} else {
				ScrollView.Content.Nodes.Clear();
				foreach (var item in items) {
					item.HighlightSymbolsIndices = null;
					ScrollView.Content.Nodes.Add(item.Widget);
					filteredItems.Add(item);
				}
			}
		}

		public void AddItem(string text, Action action)
		{
			var item = new LookupItem(text, action, Submit);
			items.Add(item);
			filteredItems.Add(item);
			ScrollView.Content.AddNode(item.Widget);
		}

		private void SelectItem(int index)
		{
			DeselectItem();
			selectedIndex = Mathf.Clamp(index, 0, filteredItems.Count - 1);
			var selectedItem = SelectedItem;
			if (selectedItem != null) {
				selectedItem.Selected = true;
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
				selectedItem.Selected = false;
			}
			selectedIndex = -1;
		}

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
			Clear();
			Submitted?.Invoke();
		}

		public void Cancel()
		{
			Clear();
			Canceled?.Invoke();
		}

		private void Clear()
		{
			DeselectItem();
			foreach (var item in items) {
				item.Widget.UnlinkAndDispose();
			}
			items.Clear();
			filteredItems.Clear();
			FilterEditBox.Text = string.Empty;
		}

		[NodeComponentDontSerialize]
		[UpdateStage(typeof(PreEarlyUpdateStage))]
		private class PreEarlyUpdateBehavior : BehaviorComponent
		{
			public event Action Updating;

			protected override void Update(float delta) => Updating?.Invoke();
		}

		private static class Commands
		{
			public static readonly ICommand Submit = new Command(Key.Enter);
			public static readonly ICommand Cancel = new Command(Key.Escape);
			public static readonly ICommand SelectPreviousItem = new Command(Key.Up);
			public static readonly ICommand SelectNextItem = new Command(Key.Down);
			public static readonly ICommand SelectPreviousPage = new Command(Key.PageUp);
			public static readonly ICommand SelectNextPage = new Command(Key.PageDown);
		}
	}
}
