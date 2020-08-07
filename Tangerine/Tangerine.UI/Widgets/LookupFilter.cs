using System;
using System.Collections.Generic;

namespace Tangerine.UI
{
	public interface ILookupFilter
	{
		void Applying(LookupWidget lookupWidget, string text);
		IEnumerable<LookupItem> Apply(string text, List<LookupItem> items);
		void Applied(LookupWidget lookupWidget);
	}

	public class DelegateLookupFilter : ILookupFilter
	{
		public delegate void ApplyingDelegate(LookupWidget lookupWidget, string text);
		public delegate IEnumerable<LookupItem> ApplyDelegate(string text, List<LookupItem> items);
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

		public IEnumerable<LookupItem> Apply(string text, List<LookupItem> items) => apply?.Invoke(text, items);

		public virtual void Applied(LookupWidget lookupWidget) => applied?.Invoke(lookupWidget);
	}

	public class LookupFuzzyFilter : ILookupFilter
	{
		public static LookupFuzzyFilter Instance = new LookupFuzzyFilter();

		private LookupFuzzyFilter() { }

		public virtual void Applying(LookupWidget lookupWidget, string text) { }

		public virtual IEnumerable<LookupItem> Apply(string text, List<LookupItem> items)
		{
			var itemsTemp = new List<(LookupItem item, int Distance)>();
			if (!string.IsNullOrEmpty(text)) {
				var matches = new List<int>(text.Length);
				foreach (var item in items) {
					if (DoesTextMatchFuzzySearch(item.Name.Text, text, matches, out var distance)) {
						itemsTemp.Add((item, distance));
						item.Name.HighlightSymbolsIndices = matches.ToArray();
					} else {
						item.Name.HighlightSymbolsIndices = null;
					}
					matches.Clear();
				}
				itemsTemp.Sort((a, b) => a.Distance.CompareTo(b.Distance));
				foreach (var (item, _) in itemsTemp) {
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

		public static bool DoesTextMatchFuzzySearch(string text, string pattern, ICollection<int> matches, out int value)
		{
			var i = -1;
			value = 0;
			if (string.IsNullOrEmpty(text)) {
				return string.IsNullOrEmpty(pattern);
			}
			if (string.IsNullOrEmpty(pattern)) {
				return true;
			}
			foreach (var c in pattern) {
				if (i == text.Length - 1) {
					break;
				}
				var ip = i;
				var lci = text.IndexOf(char.ToLowerInvariant(c), i + 1);
				var uci = text.IndexOf(char.ToUpperInvariant(c), i + 1);
				i = lci != -1 && uci != -1 ? Math.Min(lci, uci) :
					lci == -1 ? uci : lci;
				if (i == -1) {
					break;
				}
				matches.Add(i);
				if (ip != -1) {
					value += (i - ip) * (i - ip);
				}
			}
			return matches.Count == pattern.Length;
		}
	}
}
