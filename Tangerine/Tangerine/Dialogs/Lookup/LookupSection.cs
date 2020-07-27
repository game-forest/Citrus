using System.Collections.Generic;
using Tangerine.UI;

namespace Tangerine
{
	public abstract class LookupSection
	{
		public readonly ILookupDataSource DataSource;
		public readonly ILookupFilter Filter;

		public abstract string Breadcrumb { get; }
		public abstract string Prefix { get; }
		public virtual string HelpText => null;
		public virtual string HintText => null;

		protected LookupSection()
		{
			DataSource = new DelegateLookupDataSource(FillLookup);
			Filter = new DelegateLookupFilter(ApplyingLookupFilter, ApplyLookupFilter, null);
		}

		public abstract void FillLookup(LookupWidget lookupWidget);

		public virtual void ApplyingLookupFilter(LookupWidget lookupWidget, string text) { }

		public virtual IEnumerable<LookupItem> ApplyLookupFilter(string text, List<LookupItem> items) => LookupFuzzyFilter.Instance.Apply(text, items);
	}
}
