using System;
using Lime;
using Tangerine.UI;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public static partial class ConflictingAnimators
	{
		private class WorkIndicator : Widget
		{
			private const int StageCount = 5;
			private const float IconLengthInTextureCoordinates = 1f / StageCount;

			public ConflictFinder.WorkProgress WorkProgress { get; set; }

			public WorkIndicator()
			{
				WorkProgress = ConflictFinder.WorkProgress.Done;
				var image = new Image(IconPool.GetTexture("Universal.WorkIndicator")) {
					MinMaxSize = new Vector2(16),
					Size = new Vector2(16),
					UV0 = new Vector2(1 * IconLengthInTextureCoordinates, 0),
					UV1 = new Vector2(2 * IconLengthInTextureCoordinates, 1),
					Material = new RedChannelToColorMaterial(),
				};
				var textWidget = new ThemedSimpleText {
					Padding = new Thickness(left: 4),
					Color = Color4.White,
				};
				var backgroundPresenter = new WidgetFlatFillPresenter(new Color4(76, 175, 80));
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
				ConflictFinder.WorkProgress workProgressCached = null;
				Updating += delta => {
					animationTime += delta;
					bool isCompleted = WorkProgress.IsCompleted;
					if (isCompleted & (!isCompletedCached | WorkProgress != workProgressCached)) {
						workProgressCached = WorkProgress;
						isCompletedCached = true;
						animationTime = 0f;
						animationStage = 0;
						const string ConflictCountPrefix = "Count of potential conflicts";
						if (WorkProgress.IsException) {
							backgroundPresenter.Color = new Color4(255, 87, 34);
							textWidget.Text = "Exception";
						} else if (WorkProgress.IsCanceled) {
							backgroundPresenter.Color = new Color4(255, 152, 0);
							textWidget.Text = $"Cancelled. {ConflictCountPrefix} {WorkProgress.CurrentConflictCount}";
						} else {
							backgroundPresenter.Color = new Color4(76, 175, 80);
							textWidget.Text = $"Done. {ConflictCountPrefix} {WorkProgress.CurrentConflictCount}";
						}
					}
					if (!isCompleted) {
						if (isCompletedCached | WorkProgress != workProgressCached) {
							workProgressCached = WorkProgress;
							isCompletedCached = false;
							animationTime = 0f;
							animationStage = 1;
							backgroundPresenter.Color = new Color4(0, 122, 204);
						}
						textWidget.Text = WorkProgress.CurrentFile ?? string.Empty;
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
