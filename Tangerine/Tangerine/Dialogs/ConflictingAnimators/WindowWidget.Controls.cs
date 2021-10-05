using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	internal sealed partial class WindowWidget
	{
		private sealed class Controls : Widget
		{
			public readonly ThemedButton SearchButton;
			public readonly ThemedButton CancelButton;
        	public readonly ThemedCheckBox GlobalCheckBox;

            public ConflictFinder.WorkProgress WorkProgress { get; set; }
            
            public Controls()
            {
	            Layout = new HBoxLayout { Spacing = 8 };
	            LayoutCell = new LayoutCell(Alignment.LeftCenter, stretchX: 0, stretchY: 0);
	            
	            SearchButton = new ThemedButton { Text = "Search", Visible = true };
	            CancelButton = new ThemedButton { Text = "Cancel", Visible = false };
	            GlobalCheckBox = new ThemedCheckBox();
	            var sceneCaption = new ThemedCaption();

				AddNode(SearchButton);
				AddNode(CancelButton);
				AddNode(new Widget {
					Layout = new HBoxLayout { Spacing = 2 },
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Nodes = {
						GlobalCheckBox,
						new ThemedCaption("Global")
					}
				});
				AddNode(Spacer.HFill());
				AddNode(sceneCaption);

				sceneCaption.Tasks.AddLoop(() => {
					sceneCaption.Visible = Document.Current != null;
					var documentName = ThemedCaption.Stylize(
						text: Document.Current?.DisplayName,
						id: TextStyleIdentifiers.Bold
					);
					sceneCaption.Text = $"Observed Document: {documentName}";
					sceneCaption.AdjustWidthToText();
				});
				
				WorkProgress = ConflictFinder.WorkProgress.Done;
	            Updating += delta => {
		            var isCompleted = WorkProgress.IsCompleted;
		            SearchButton.Visible = isCompleted;
		            CancelButton.Visible = !isCompleted;
	            };
            }
		}
	}
}
