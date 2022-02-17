using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public abstract class LookupSection
	{
		protected readonly LookupSections Sections;

		public readonly ILookupDataSource DataSource;
		public readonly ILookupFilter Filter;

		public abstract string Breadcrumb { get; }
		public abstract string Prefix { get; }
		public virtual string HelpText => null;
		public virtual string HintText => null;

		protected LookupSection(LookupSections sections)
		{
			Sections = sections;
			DataSource = new DelegateLookupDataSource(FillLookup);
			Filter = new DelegateLookupFilter(ApplyingLookupFilter, ApplyLookupFilter, AppliedLookupFilter);
		}

		public abstract void FillLookup(LookupWidget lookupWidget);

		public virtual void Dropped() { }

		protected bool RequireProjectOrAddAlertItem(LookupWidget lookupWidget, string alertText)
		{
			if (Project.Current != null) {
				return true;
			}
			lookupWidget.AddItem(new LookupDialogItem(
				alertText,
				null,
				() => {
					new FileOpenProject();
					Sections.Drop();
				}
			));
			return false;
		}

		protected bool RequireDocumentOrAddAlertItem(LookupWidget lookupWidget, string alertText)
		{
			if (Document.Current != null) {
				return true;
			}
			lookupWidget.AddItem(new LookupDialogItem(
				alertText,
				null,
				() => {
					new FileOpen();
					Sections.Drop();
				}
			));
			return false;
		}

		protected virtual void ApplyingLookupFilter(LookupWidget lookupWidget, string text) { }

		protected virtual IEnumerable<LookupItem> ApplyLookupFilter(string text, IReadOnlyList<LookupItem> items) =>
			ApplyLookupFilter(text, items, CancellationToken.None);

		protected IEnumerable<LookupItem> ApplyLookupFilter(
			string text, IReadOnlyList<LookupItem> items, CancellationToken cancellationToken
		) {
			var itemsTemp =
				new List<(
					LookupDialogItem Item,
					int HeaderDistance,
					int HeaderGapCount,
					int NameDistance,
					int NameGapCount
				)>();
			var itemsHighlights = new List<(
				LookupDialogItem Item,
				int[] HeaderHighlightSymbolsIndices,
				int[] NameHighlightSymbolsIndices
			)>(items.Count);
			if (!string.IsNullOrEmpty(text)) {
				var headerMatches = new List<int>(text.Length);
				var nameMatches = new List<int>(text.Length);
				foreach (var lookupItem in items) {
					cancellationToken.ThrowIfCancellationRequested();
					var item = (LookupDialogItem)lookupItem;
					var doesHeaderMatchFuzzySearch = Sections.FuzzyStringSearch.DoesTextMatch(
						item.Header.Text,
						text,
						headerMatches,
						out var headerDistance,
						out var headerGapCount
					);
					var doesNameMatchFuzzySearch = Sections.FuzzyStringSearch.DoesTextMatch(
						item.Name.Text,
						text,
						nameMatches,
						out var nameDistance,
						out var nameGapCount
					);
					if (doesHeaderMatchFuzzySearch || doesNameMatchFuzzySearch) {
						itemsTemp.Add((
							item,
							doesHeaderMatchFuzzySearch ? headerDistance : int.MaxValue,
							doesHeaderMatchFuzzySearch ? headerGapCount : int.MaxValue,
							doesNameMatchFuzzySearch ? nameDistance : int.MaxValue,
							doesNameMatchFuzzySearch ? nameGapCount : int.MaxValue
						));
						itemsHighlights.Add((
							item,
							doesHeaderMatchFuzzySearch ? headerMatches.ToArray() : null,
							doesNameMatchFuzzySearch ? nameMatches.ToArray() : null)
						);
					} else {
						itemsHighlights.Add((item, null, null));
					}
					headerMatches.Clear();
					nameMatches.Clear();
				}
				itemsTemp.Sort((lhs, rhs) => {
					var result = lhs.HeaderGapCount.CompareTo(rhs.HeaderGapCount);
					if (result != 0) {
						return result;
					}
					result = lhs.HeaderDistance.CompareTo(rhs.HeaderDistance);
					if (result != 0) {
						return result;
					}
					if (lhs.HeaderGapCount != int.MaxValue && lhs.HeaderDistance != int.MaxValue) {
						return lhs.Item.Header.Text.Length.CompareTo(rhs.Item.Header.Text.Length);
					}
					result = lhs.NameGapCount.CompareTo(rhs.NameGapCount);
					if (result != 0) {
						return result;
					}
					result = lhs.NameDistance.CompareTo(rhs.NameDistance);
					return result != 0 ? result : lhs.Item.Name.Text.Length.CompareTo(rhs.Item.Name.Text.Length);
				});
				foreach (var (item, headerHighlightSymbolsIndices, nameHighlightSymbolsIndices) in itemsHighlights) {
					item.Header.HighlightSymbolsIndices = headerHighlightSymbolsIndices;
					item.Name.HighlightSymbolsIndices = nameHighlightSymbolsIndices;
				}
				foreach (var (item, _, _, _, _) in itemsTemp) {
					yield return item;
				}
			} else {
				foreach (var lookupItem in items) {
					var item = (LookupDialogItem)lookupItem;
					item.Header.HighlightSymbolsIndices = null;
					item.Name.HighlightSymbolsIndices = null;
					yield return item;
				}
			}
		}

		protected void AppliedLookupFilter(LookupWidget lookupwidget) { }
	}

	public abstract class LookupSectionLimited : LookupSection
	{
		protected IReadOnlyList<LookupItem> MutableItemList { get; set; } = new List<LookupItem>(0);
		protected bool Active { get; set; }

		protected LookupSectionLimited(LookupSections sections) : base(sections) { }

		public override void Dropped()
		{
			MutableItemList = new List<LookupItem>(0);
			Active = false;
		}

		protected override void ApplyingLookupFilter(LookupWidget lookupWidget, string text)
		{
			var filteredItemsLimit = CoreUserPreferences.Instance.LookupItemsLimit >= 1
				? CoreUserPreferences.Instance.LookupItemsLimit
				: 30;
			var filteredItems = base.ApplyLookupFilter(text, MutableItemList).Take(filteredItemsLimit).ToList();
			lookupWidget.ClearItems(disposeItems: false);

			// Adding nodes after LayoutProcessor.Update() cause flickering
			Application.InvokeOnNextUpdate(() => {
				if (Active) {
					lookupWidget.AddRange(filteredItems);
					lookupWidget.SelectItem(index: 0);
					lookupWidget.ScrollView.ScrollPosition = 0;
				}
			});
		}

		protected override IEnumerable<LookupItem> ApplyLookupFilter(string text, IReadOnlyList<LookupItem> items)
		{
			return items;
		}
	}
}
