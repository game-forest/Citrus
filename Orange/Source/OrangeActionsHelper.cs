using System;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;
using Environment = Lime.Environment;

namespace Orange.Source
{
	public static class OrangeActionsHelper
	{
		public static async Task<bool> ExecuteOrangeAction(
			Func<string> action, Action onBegin, Action onEnd, bool background
		)
		{
			var startTime = DateTime.Now;
			onBegin?.Invoke();
			var executionResult = "Build Failed! Unknown Error.";
			bool hasError = false;
			try {
				executionResult = "Done.";

				void MainAction()
				{
					var savedAssetBundle = AssetBundle.Initialized ? AssetBundle.Current : null;
					AssetBundle bundle = null;
					if (Workspace.Instance.AssetsDirectory != null) {
						bundle = new TangerineAssetBundle(Workspace.Instance.AssetsDirectory);
						AssetBundle.SetCurrent(bundle);
					}
					try {
						var errorDetails = SafeExecuteWithErrorDetails(action);
						if (errorDetails != null) {
							if (errorDetails.Length > 0) {
								Console.WriteLine(errorDetails);
							}
							hasError = true;
							executionResult = "Build Failed!";
						}
					} finally {
						if (bundle != null) {
							bundle.Dispose();
							AssetBundle.SetCurrent(savedAssetBundle);
						}
					}
				}

				if (background) {
					await System.Threading.Tasks.Task.Run(MainAction);
				} else {
					MainAction();
				}
			} finally {
				Console.WriteLine(executionResult);
				Console.WriteLine(@"Elapsed time {0:hh\:mm\:ss}", DateTime.Now - startTime);
				onEnd?.Invoke();
			}
			return !hasError;
		}

		private static string SafeExecuteWithErrorDetails(Func<string> action)
		{
			try {
				return action();
			} catch (MSBuildNotFound e) {
				bool dialogResult = The.UI.AskConfirmation("You need to download and install MSBuild 15.0. Download?");
				if (dialogResult) {
					Environment.OpenUrl(e.DownloadUrl);
				}
				return e.ToString();
			} catch (System.Exception ex) {
				return ex.ToString();
			}
		}

	}
}
