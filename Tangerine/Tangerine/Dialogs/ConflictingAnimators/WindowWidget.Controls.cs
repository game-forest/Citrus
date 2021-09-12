using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public partial class WindowWidget
	{
		private sealed class Controls : Widget
		{
        	public readonly ThemedButton SearchButton;
        	public readonly ThemedButton CancelButton;
        	public readonly ThemedCheckBox GlobalCheckBox;
        	public readonly ThemedCheckBox ExternalCheckBox;
            public readonly ThemedCaption SceneCaption;

            public Controls()
            {
	            Layout = new HBoxLayout { Spacing = 8 };
	            LayoutCell = new LayoutCell(Alignment.LeftCenter);
	            AddNode(SearchButton = new ThemedButton { Text = "Search" });
	            AddNode(CancelButton = new ThemedButton { Text = "Cancel" });
	            AddNode(GlobalCheckBox = new ThemedCheckBox());
	            AddNode(ExternalCheckBox = new ThemedCheckBox());
	            AddNode(SceneCaption = new ThemedCaption());
	            Decorate();
            }

            private void Decorate()
            {
	            SceneCaption.Tasks.AddLoop(() => {
		            var document = ThemedCaption.Stylize(Document.Current?.DisplayName, TextStyleIdentifiers.Bold);
		            SceneCaption.Visible = Document.Current != null;
		            SceneCaption.Text = $"Observed Document: {document}";
		            SceneCaption.AdjustWidthToText();
	            });

	            GlobalCheckBox.AddCaption("Global");
	            ExternalCheckBox.AddCaption("External Scenes");
	            Spacer.HFill().ShrinkHeight().InsertBefore(SceneCaption);
            }
		}
	}
}
