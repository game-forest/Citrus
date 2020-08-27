using Lime;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine
{
	public class LookupDialog
	{
		public LookupDialog(LookupSections.SectionType? sectionType = null)
		{
			Vector2? displayCenter = null;
			try {
				var display = CommonWindow.Current.Display;
				displayCenter = display.Position + display.Size / 2;
			} catch (System.ObjectDisposedException) {
				// Suppress
			}
			var window = new Window(new WindowOptions {
				Title = "Go To Anything",
				Style = WindowStyle.Borderless,
			});
			if (!displayCenter.HasValue) {
				var display = DockManager.Instance.MainWindowWidget.Window.Display;
				displayCenter = display.Position + display.Size / 2;
			}
			window.DecoratedPosition = displayCenter.Value - window.DecoratedSize / 2f;

			LookupWidget lookupWidget;
			var windowWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
					(lookupWidget = new LookupWidget {
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
					})
				},
			};
			windowWidget.FocusScope = new KeyboardFocusScope(windowWidget);
			windowWidget.LateTasks.AddLoop(() => {
				if (window.Visible && !window.Active) {
					lookupWidget.Cancel();
				}
			});
			var sections = new LookupSections(lookupWidget);

			void CloseWindow()
			{
				window.Close();
				lookupWidget.UnlinkAndDispose();
			}

			void LookupSubmitted()
			{
				if (sections.StackCount == 0) {
					CloseWindow();
				}
			}

			void LookupCanceled()
			{
				sections.Drop();
				CloseWindow();
			}

			lookupWidget.Submitted += LookupSubmitted;
			lookupWidget.Canceled += LookupCanceled;
			windowWidget.FocusScope.SetDefaultFocus();
			sections.Initialize(sectionType);

			// TODO: Adaptive window height. Fix problem with "window.Resized += deviceRotated => UpdateAndResize(0);" at DefaultWindowWidget class.
			//var layoutProcessor = windowWidget.Manager.Processors.OfType<LayoutProcessor>().First();
			//layoutProcessor.Update(0);
			//var minHeight = 0f;
			//var vSpacing = ((VBoxLayout)lookupWidget.Layout).Spacing;
			//minHeight += windowWidget.Padding.Top + windowWidget.Padding.Bottom;
			//minHeight += lookupWidget.Padding.Top + lookupWidget.Padding.Bottom;
			//var isFirstWidget = true;
			//foreach (var widget in lookupWidget.Nodes.OfType<Widget>()) {
			//	if (widget.Visible) {
			//		if (widget != lookupWidget.ScrollView) {
			//			if (!isFirstWidget) {
			//				minHeight += vSpacing;
			//			}
			//			minHeight += widget.Height;
			//		}
			//		isFirstWidget = false;
			//	}
			//}

			//var previousLookupItemsCount = int.MinValue;
			//var previousLookupItemHeight = float.MinValue;
			//windowWidget.Components.GetOrAdd<PostLateUpdateBehavior>().Updating += () => {
			//	var lookupItemsCount = lookupWidget.ScrollView.Content.Nodes.Count;
			//	var lookupItemHeight = lookupItemsCount > 0 ? lookupWidget.ScrollView.Content.Nodes.OfType<Widget>().First().Height : 0;
			//	if (lookupItemsCount != previousLookupItemsCount || lookupItemHeight != previousLookupItemHeight) {
			//		layoutProcessor.Update(0);

			//		lookupItemHeight = lookupItemsCount > 0 ? lookupWidget.ScrollView.Content.Nodes.OfType<Widget>().First().Height : 0;
			//		previousLookupItemsCount = lookupItemsCount;
			//		previousLookupItemHeight = lookupItemHeight;

			//		var scrollViewHeight = 0f;
			//		if (lookupItemsCount > 0) {
			//			scrollViewHeight += vSpacing;
			//			scrollViewHeight += Math.Min(lookupItemsCount, 10) * lookupItemHeight;
			//		}

			//		windowWidget.Height = minHeight + scrollViewHeight;
			//		window.ClientSize = new Vector2(window.ClientSize.X, minHeight + scrollViewHeight);

			//		System.Console.WriteLine($"Scroll height: {scrollViewHeight:F0}; Window height: {minHeight + scrollViewHeight:F0}");
			//	}
			//};
		}

		//[NodeComponentDontSerialize]
		//[UpdateStage(typeof(PostLateUpdateStage))]
		//private class PostLateUpdateBehavior : BehaviorComponent
		//{
		//	public event Action Updating;

		//	protected override void Update(float delta) => Updating?.Invoke();
		//}
	}
}
