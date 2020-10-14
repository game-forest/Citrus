using System.Collections.Generic;

namespace Tangerine.UI
{
	public interface ILookupFilter
	{
		void Applying(LookupWidget lookupWidget, string text);
		IEnumerable<LookupItem> Apply(string text, IReadOnlyList<LookupItem> items);
		void Applied(LookupWidget lookupWidget);
	}

	public class DelegateLookupFilter : ILookupFilter
	{
		public delegate void ApplyingDelegate(LookupWidget lookupWidget, string text);
		public delegate IEnumerable<LookupItem> ApplyDelegate(string text, IReadOnlyList<LookupItem> items);
		public delegate void AppliedDelegate(LookupWidget lookupWidget);

		private readonly ApplyingDelegate applying;
		private readonly ApplyDelegate apply;
		private readonly AppliedDelegate applied;

		public DelegateLookupFilter(ApplyDelegate apply)
		{
			this.apply = apply;
		}

		public DelegateLookupFilter(ApplyingDelegate applying, ApplyDelegate apply, AppliedDelegate applied)
		{
			this.applying = applying;
			this.apply = apply;
			this.applied = applied;
		}

		public virtual void Applying(LookupWidget lookupWidget, string text) => applying?.Invoke(lookupWidget, text);

		public IEnumerable<LookupItem> Apply(string text, IReadOnlyList<LookupItem> items) => apply?.Invoke(text, items);

		public virtual void Applied(LookupWidget lookupWidget) => applied?.Invoke(lookupWidget);
	}

	public class LookupFuzzyFilter : ILookupFilter
	{
		private readonly FuzzyStringSearch fuzzyStringSearch = new FuzzyStringSearch();

		public virtual void Applying(LookupWidget lookupWidget, string text) { }

		public virtual IEnumerable<LookupItem> Apply(string text, IReadOnlyList<LookupItem> items)
		{
			var itemsTemp = new List<(LookupItem item, int Distance, int GapCount)>();
			if (!string.IsNullOrEmpty(text)) {
				var matches = new List<int>(text.Length);
				foreach (var item in items) {
					if (fuzzyStringSearch.DoesTextMatch(item.Name.Text, text, matches, out var distance, out var gapCount)) {
						itemsTemp.Add((item, distance, gapCount));
						item.Name.HighlightSymbolsIndices = matches.ToArray();
					} else {
						item.Name.HighlightSymbolsIndices = null;
					}
					matches.Clear();
				}
				itemsTemp.Sort((lhs, rhs) => {
					var result = lhs.GapCount.CompareTo(rhs.GapCount);
					if (result != 0) {
						return result;
					}
					result = lhs.Distance.CompareTo(rhs.Distance);
					return result != 0 ? result : lhs.item.Name.Text.Length.CompareTo(rhs.item.Name.Text.Length);
				});
				foreach (var (item, _, _) in itemsTemp) {
					yield return item;
				}
			} else {
				foreach (var item in items) {
					item.Name.HighlightSymbolsIndices = null;
					yield return item;
				}
			}
		}

		public virtual void Applied(LookupWidget lookupWidget) { }
	}
}
