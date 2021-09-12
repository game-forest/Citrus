using Lime;
using SearchFlags = Tangerine.Dialogs.ConflictingAnimators.ConflictInfoProvider.SearchFlags;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public partial class WindowWidget
	{
		private void SetupCallbacks()
		{
			controls.SearchButton.Clicked += SearchButton_Clicked;
			controls.CancelButton.Clicked += CancelButton_Clicked;
			controls.GlobalCheckBox.Changed += GlobalCheckBox_Changed;
			controls.ExternalCheckBox.Changed += ExternalCheckBox_Changed;
		}

		private void SearchButton_Clicked()
		{
			SearchCancel();
			results.Clear();
			provider.Invalidate();
			SearchAsync();
		}

		private void CancelButton_Clicked()
		{
			SearchCancel();
		}

		private void GlobalCheckBox_Changed(CheckBox.ChangedEventArgs args)
        {
        	var enabled = args.Value;
            var hasExternal = !enabled && controls.ExternalCheckBox.Checked;
            searchFlags.Toggle(SearchFlags.Global, enabled);
            searchFlags.Toggle(SearchFlags.External, hasExternal);
        	controls.ExternalCheckBox.Enabled = !enabled;
        	controls.ExternalCheckBox.HitTestTarget = !enabled;
        }

		private void ExternalCheckBox_Changed(CheckBox.ChangedEventArgs args)
		{
			searchFlags.Toggle(SearchFlags.External, args.Value);
		}
	}
}
