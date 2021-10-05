using System;
using Lime;
using Tangerine.UI;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	internal sealed partial class WindowWidget
	{
		private class WorkIndicator : Widget
		{
			private const int StageCount = 5;
			private const float IconLengthInTextureCoordinates = 1f / StageCount;

			private readonly Image image;
			private readonly ThemedSimpleText textWidget;
			private readonly WidgetFlatFillPresenter backgroundPresenter;

			public ConflictFinder.WorkProgress WorkProgress { get; set; }

			public WorkIndicator()
			{
				WorkProgress = ConflictFinder.WorkProgress.Done;
				image = new Image(IconPool.GetTexture("Universal.WorkIndicator")) {
					MinMaxSize = new Vector2(16),
					Size = new Vector2(16),
					UV0 = new Vector2(1 * IconLengthInTextureCoordinates, 0),
					UV1 = new Vector2(2 * IconLengthInTextureCoordinates, 1)
				};
				image.Material = new RedChannelToColorMaterial();
				textWidget = new ThemedSimpleText {
					Padding = new Thickness(left: 4),
					Color = Color4.White
				};
				backgroundPresenter = new WidgetFlatFillPresenter(new Color4(76, 175, 80));
				Layout = new HBoxLayout();
				Presenter = backgroundPresenter;
				MinMaxHeight = 16;
				MinWidth = 100;
				AddNode(Spacer.HSpacer(8));
				AddNode(image);
				AddNode(textWidget);
				AddNode(Spacer.HFill());

				float animationTime = 0;
				int animationStage = 0;
				bool isCompletedCached = false;
				Updating += delta => {
					animationTime += delta;
					bool isCompleted = WorkProgress.IsCompleted;
					bool isCancelled = WorkProgress.IsCancelled;
					bool isException = WorkProgress.IsException;
					if (isCompleted & !isCompletedCached) {
						isCompletedCached = true;
						animationTime = 0f;
						animationStage = 0;
						if (isException) {
							backgroundPresenter.Color = new Color4(255, 87, 34);
							textWidget.Text = "Exception";
						} else if (isCancelled) {
							backgroundPresenter.Color = new Color4(255, 152, 0);
							textWidget.Text = "Cancelled";
						} else {
							backgroundPresenter.Color = new Color4(76, 175, 80);
							textWidget.Text = "Done";
						}
					}
					if (!isCompleted) {
						textWidget.Text = WorkProgress.CurrentFile ?? string.Empty;
						if (isCompletedCached) {
							isCompletedCached = false;
							animationTime = 0f;
							animationStage = 1;
							backgroundPresenter.Color = new Color4(0, 122, 204);
						}
					}
					if (animationTime > 0.5f) {
						animationTime -= 0.5f;
						animationStage = isCompleted ? 0 : Math.Max(1, (animationStage + 1) % StageCount);
					}
					image.UV0 = new Vector2(animationStage * IconLengthInTextureCoordinates, 0);
					image.UV1 = new Vector2((animationStage + 1) * IconLengthInTextureCoordinates, 1);
				};
			}
		}
	}
}
