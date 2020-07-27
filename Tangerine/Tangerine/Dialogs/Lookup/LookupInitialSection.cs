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

		public override void FillLookup(LookupWidget lookupWidget)
		{
			LookupDialog.Sections.Help.FillLookup(lookupWidget);
			LookupDialog.Sections.Commands.FillLookup(lookupWidget);
		}

		public override void ApplyingLookupFilter(LookupWidget lookupWidget, string text)
		{
			if (string.IsNullOrEmpty(text)) {
				LookupDialog.Sections.DropAndPush(this);
				return;
			}
			foreach (var section in LookupDialog.Sections.List) {
				if (!string.IsNullOrEmpty(section.Prefix) && text.StartsWith(section.Prefix)) {
					LookupDialog.Sections.DropAndPush(section);
					return;
				}
			}
		}

		public override IEnumerable<LookupItem> ApplyLookupFilter(string text, List<LookupItem> items) =>
			!string.IsNullOrEmpty(text) ? base.ApplyLookupFilter(text, items) : emptyItems;
	}
}
