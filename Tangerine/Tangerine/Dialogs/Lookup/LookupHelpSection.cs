using System;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupHelpSection : LookupSection
	{
		private const string PrefixConst = "?";

		public override string Breadcrumb { get; } = "Help";
		public override string Prefix { get; } = PrefixConst;

		public LookupHelpSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			foreach (var section in Sections.List) {
				if (!string.IsNullOrEmpty(section.HelpText)) {
					Action action;
					if (!string.IsNullOrEmpty(section.Prefix)) {
						action = () => Sections.DropAndPush(section);
					} else {
						action = () => { };
					}
					lookupWidget.AddItem(new LookupDialogItem(lookupWidget, section.HelpText, null, action));
				}
			}
		}

		protected override void ApplyingLookupFilter(LookupWidget lookupWidget, string text)
		{
			foreach (var section in Sections.List) {
				if (!string.IsNullOrEmpty(section.Prefix) && text.StartsWith(section.Prefix)) {
					Sections.DropAndPush(section);
					return;
				}
			}
		}
	}
}
