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

		public IEnumerable<LookupItem> Apply(string text, List<LookupItem> items)
		{
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
					}
					matches.Clear();
				}
				itemsTemp.Sort((a, b) => a.Distance.CompareTo(b.Distance));
				foreach (var (item, _) in itemsTemp) {
					yield return item;
				}
			} else {
				foreach (var item in items) {
					yield return item;
				}
			}
		}

		public virtual void Applied(LookupWidget lookupWidget) { }
	}
}
