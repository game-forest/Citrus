using System.Collections.Generic;
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

		protected bool RequireProjectOrAddAlertItem(LookupWidget lookupWidget, string alertText)
		{
			if (Project.Current != null) {
				return true;
			}
			lookupWidget.AddItem(new LookupDialogItem(
				lookupWidget,
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
				lookupWidget,
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

		protected virtual IEnumerable<LookupItem> ApplyLookupFilter(string text, List<LookupItem> items)
		{
			var itemsTemp = new List<(LookupItem item, int HeaderDistance, int NameDistance)>();
			if (!string.IsNullOrEmpty(text)) {
				var headerMatches = new List<int>(text.Length);
				var nameMatches = new List<int>(text.Length);
				foreach (var lookupItem in items) {
					var item = (LookupDialogItem)lookupItem;
					var doesHeaderMatchFuzzySearch = LookupFuzzyFilter.DoesTextMatchFuzzySearch(item.Header.Text, text, headerMatches, out var headerDistance);
					var doesNameMatchFuzzySearch = LookupFuzzyFilter.DoesTextMatchFuzzySearch(item.Name.Text, text, nameMatches, out var nameDistance);
					if (doesHeaderMatchFuzzySearch || doesNameMatchFuzzySearch) {
						itemsTemp.Add((item, doesHeaderMatchFuzzySearch ? headerDistance : int.MaxValue, doesNameMatchFuzzySearch ? nameDistance : int.MaxValue));
						item.Header.HighlightSymbolsIndices = doesHeaderMatchFuzzySearch ? headerMatches.ToArray() : null;
						item.Name.HighlightSymbolsIndices = doesNameMatchFuzzySearch ? nameMatches.ToArray() : null;
					} else {
						item.Header.HighlightSymbolsIndices = null;
						item.Name.HighlightSymbolsIndices = null;
					}
					headerMatches.Clear();
					nameMatches.Clear();
				}
				itemsTemp.Sort((lhs, rhs) => {
					var headerCompare = lhs.HeaderDistance.CompareTo(rhs.HeaderDistance);
					return headerCompare != 0 ? headerCompare : lhs.NameDistance.CompareTo(rhs.NameDistance);
				});
				foreach (var (item, _, _) in itemsTemp) {
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
}
