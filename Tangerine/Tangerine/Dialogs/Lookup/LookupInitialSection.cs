using System;
using System.Collections.Generic;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupInitialSection : LookupSection
	{
		private static readonly LookupItem[] emptyItems = new LookupItem[0];

		public override string Breadcrumb { get; } = null;
		public override string Prefix { get; } = null;
		public override string HintText => "Type '?' to open the help menu";

		public LookupInitialSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			Sections.Help.FillLookup(lookupWidget);
			Sections.Commands.FillLookup(lookupWidget);
		}

		protected override void ApplyingLookupFilter(LookupWidget lookupWidget, string text)
		{
			if (string.IsNullOrEmpty(text)) {
				return;
			}
			foreach (var section in Sections.List) {
				if (
					!string.IsNullOrEmpty(section.Prefix)
					&& text.StartsWith(section.Prefix, StringComparison.InvariantCultureIgnoreCase)
				) {
					Sections.DropAndPush(section);
					return;
				}
			}
		}

		protected override IEnumerable<LookupItem> ApplyLookupFilter(string text, IReadOnlyList<LookupItem> items) =>
			!string.IsNullOrEmpty(text) ? base.ApplyLookupFilter(text, items) : emptyItems;
	}
}
