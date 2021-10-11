using System;
using System.Collections.Generic;
using System.Threading;
using Lime;
using Tangerine.Core;
using Tangerine.UI.Widgets.ConflictingAnimators;
using Task = System.Threading.Tasks.Task;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	internal sealed partial class WindowWidget : ThemedInvalidableWindowWidget
	{
		private readonly SearchResultsView searchResultsView;
		private readonly Controls controls;
		private readonly WorkIndicator workIndicator;

		private Task currentSearchTask;
		private CancellationTokenSource searchCancellation;
		private ConflictFinder.Content contentSource;
		private ConflictFinder.WorkProgress workProgress;
		
		public WindowWidget(Lime.Window window) : base(window)
		{
			currentSearchTask = Task.CompletedTask;
			contentSource = ConflictFinder.Content.CurrentDocument;
			var size = CoreUserPreferences.Instance.ConflictingAnimatorsWindowSize;
			if (size != Vector2.PositiveInfinity) {
				window.DecoratedSize = size;
			}
			window.Resized += rotated => 
				CoreUserPreferences.Instance.ConflictingAnimatorsWindowSize = window.DecoratedSize;
			var contentWidget = new Widget {
				Layout = new VBoxLayout { Spacing = 8 },
				Padding = new Thickness(8),
				Nodes = {
					(searchResultsView = new SearchResultsView()), 
					(controls = new Controls())
				},
				FocusScope = new KeyboardFocusScope(this)
			};
			workIndicator = new WorkIndicator();
			Layout = new VBoxLayout { Spacing = 0 };
			Padding = new Thickness(0);
			AddNode(contentWidget);
			AddNode(workIndicator);
			SetupCallbacks();
		}

		private async void SearchAsync()
		{
			if (!currentSearchTask.IsCompleted) {
				return;
			}
			try {
				searchCancellation = new CancellationTokenSource();
				IEnumerable<ConflictInfo> conflicts = null;
				var cancellationToken = searchCancellation.Token;
				(conflicts, workProgress) = ConflictFinder.Enumerate(contentSource, cancellationToken);
				controls.WorkProgress = workProgress;
				workIndicator.WorkProgress = workProgress;
				Thread.MemoryBarrier();
				currentSearchTask = Task.Run(() => {
					foreach (var conflict in conflicts) {
						cancellationToken.ThrowIfCancellationRequested();
						searchResultsView.Enqueue(conflict);
					}
				}, searchCancellation.Token);
				await currentSearchTask;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (System.Exception exception) {
				Console.WriteLine(exception);
			}
		}
		
		private void SetupCallbacks()
		{
			controls.SearchButton.Clicked += () => {
				if (currentSearchTask.IsCompleted) {
					searchResultsView.Clear();
					SearchAsync();
				}
			};
			controls.CancelButton.Clicked += () => {
				if (!currentSearchTask.IsCompleted) {
					searchCancellation?.Cancel();
				}
			};
			controls.GlobalCheckBox.Changed += (args) => {
				var enabled = args.Value;
				contentSource = enabled ?
					ConflictFinder.Content.AssetDatabase :
					ConflictFinder.Content.CurrentDocument;
			};
		}
	}
}
