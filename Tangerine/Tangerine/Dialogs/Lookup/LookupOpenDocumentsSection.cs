using Lime;
using Tangerine.Core;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupOpenDocumentsSection : LookupSection
	{
		private const string PrefixConst = "d";

		public override string Breadcrumb { get; } = "Select Document";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } =
			$"Type '{PrefixConst}:' to search for document in the open documents";

		public LookupOpenDocumentsSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null || Project.Current.Documents == null) {
				return;
			}
			foreach (var document in Project.Current.Documents) {
				var documentCopy = document;
				lookupWidget.AddItem(new LookupDialogItem(
					headerText: documentCopy.DisplayName,
					text: documentCopy.FullPath,
					() => {
						documentCopy.MakeCurrent();
						Sections.Drop();
					}
				));
			}
		}
	}
}
