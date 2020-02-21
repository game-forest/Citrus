using System;
using System.Collections.Generic;
using System.Threading;
using Lime;

namespace Orange.Source
{
	public static class OrangeActionsHelper
	{
		public static IEnumerator<object> ExecuteOrangeAction(Func<string> action, Action onBegin, Action onEnd,
			Func<Action, IEnumerator<object>> onCreateOrNotAsynchTask)
		{
			var startTime = DateTime.Now;
			onBegin();
			var executionResult = "Build Failed! Unknown Error.";
			try {
				executionResult = "Done.";
				Action mainAction = () => {
					var savedAssetBundle = AssetBundle.Initialized ? AssetBundle.Current : null;
					var bundle = new Tangerine.Core.TangerineAssetBundle(Workspace.Instance.AssetsDirectory);
					AssetBundle.SetCurrent(bundle, resetTexturePool: false);
					try {
						var errorDetails = SafeExecuteWithErrorDetails(action);
						if (errorDetails != null) {
							if (errorDetails.Length > 0) {
								Console.WriteLine(errorDetails);
							}
							executionResult = "Build Failed!";
						}
					} finally {
						bundle.Dispose();
						AssetBundle.SetCurrent(savedAssetBundle, resetTexturePool: false);
					}
				};
				if (onCreateOrNotAsynchTask != null) {
					yield return onCreateOrNotAsynchTask(mainAction);
				} else {
					mainAction();
				}
			} finally {
				Console.WriteLine(executionResult);
				Console.WriteLine(@"Elapsed time {0:hh\:mm\:ss}", DateTime.Now - startTime);
				onEnd();
			}
		}

		private static string SafeExecuteWithErrorDetails(Func<string> action)
		{
			try {
				return action();
			} catch (MSBuildNotFound e) {
				bool dialogResult = The.UI.AskConfirmation("You need to download and install MSBuild 15.0. Download?");
				if (dialogResult) {
					System.Diagnostics.Process.Start(e.DownloadUrl);
				}
				return e.ToString();
			} catch (System.Exception ex) {
				return ex.ToString();
			}
		}

	}
}
