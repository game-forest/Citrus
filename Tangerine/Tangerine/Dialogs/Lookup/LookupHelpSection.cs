using System;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupHelpSection : LookupSection
	{
		private const string PrefixConst = "?";

		public override string Breadcrumb { get; } = "Help";
		public override string Prefix { get; } = PrefixConst;

		public override void FillLookup(LookupWidget lookupWidget)
		{
			foreach (var section in LookupDialog.Sections.List) {
				if (!string.IsNullOrEmpty(section.HelpText)) {
					Action action;
					if (!string.IsNullOrEmpty(section.Prefix)) {
						action = () => LookupDialog.Sections.DropAndPush(section);
					} else {
						action = () => { };
					}
					lookupWidget.AddItem(section.HelpText, action);
				}
			}
		}

		public override void ApplyingLookupFilter(LookupWidget lookupWidget, string text)
		{
			foreach (var section in LookupDialog.Sections.List) {
				if (!string.IsNullOrEmpty(section.Prefix) && text.StartsWith(section.Prefix)) {
					LookupDialog.Sections.DropAndPush(section);
					return;
				}
			}
		}
	}
}
