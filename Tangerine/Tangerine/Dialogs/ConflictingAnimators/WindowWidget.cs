using System;
using System.Threading;
using Lime;
using Orange;
using Tangerine.Core;
using Console = System.Console;
using SearchFlags = Tangerine.Dialogs.ConflictingAnimators.ConflictInfoProvider.SearchFlags;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public sealed partial class WindowWidget : ThemedInvalidableWindowWidget
	{
		private readonly Controls controls;
		private readonly SearchResultsView results;
		private readonly ConflictInfoProvider provider = new ConflictInfoProvider();

		private CancellationTokenSource searchCancellationSource;
		private SearchFlags searchFlags;

		public WindowWidget(Window window) : base(window)
		{
			Layout = new VBoxLayout { Spacing = 8 };
			Padding = new Thickness(8);
			FocusScope = new KeyboardFocusScope(this);
			AddNode(results = new SearchResultsView());
			AddNode(controls = new Controls());
			SetupCallbacks();
		}

		private void SearchCancel()
		{
			searchCancellationSource?.Cancel();
			searchCancellationSource = null;
		}

		private async void SearchAsync()
		{
			searchCancellationSource = new CancellationTokenSource();
			try {
				await System.Threading.Tasks.Task.Run(Search, searchCancellationSource.Token);
				searchCancellationSource = null;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (System.Exception exception) {
				Console.WriteLine(exception);
				searchCancellationSource = null;
			} finally {
				provider.Invalidate();
			}
		}

		private void Search()
		{
			var cancellationToken = searchCancellationSource?.Token;
			AssetBundle.Current = new TangerineAssetBundle(The.Workspace.AssetsDirectory);
			foreach (var info in provider.Enumerate(searchFlags, cancellationToken)) {
				cancellationToken?.ThrowIfCancellationRequested();
				results.Enqueue(info);
			}
		}

		public override void Dispose()
		{
			base.Dispose();
			provider.Dispose();
		}
	}
}
