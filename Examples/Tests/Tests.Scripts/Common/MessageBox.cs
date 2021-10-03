using System;
using Lime;

namespace Tests.Scripts.Common
{
	public class MessageBox : IDisposable
	{
		private readonly Frame scene;
		private InternalResult internalResult;

		public bool IsClosed { get; private set; }

		public static async Coroutine<MessageBoxResult> Show(string message, MessageBoxButtons messageBoxButtons = MessageBoxButtons.RetryIgnore)
		{
			Logger.Instance.Script($@"{nameof(MessageBox)}(""{message}"") asking...");
			using (var messageBox = new MessageBox(message, messageBoxButtons)) {
				using (new Command.FrozenTimeFlow()) {
					await Command.WaitWhile(() => !messageBox.IsClosed);
				}
				Logger.Instance.Script($@"{nameof(MessageBox)}(""{message}""): {messageBox.internalResult}");

				switch (messageBox.internalResult) {
					case InternalResult.Abort:
						throw new System.Exception("Aborting test!");
					case InternalResult.Retry:
						return MessageBoxResult.Retry;
					case InternalResult.Ignore:
						return MessageBoxResult.Ignore;
					case InternalResult.Pause:
						using (new Command.NormalTimeFlow()) {
							await Command.Wait(10);
						}
						return await Show(message, messageBoxButtons);
					default:
						throw new ArgumentOutOfRangeException();
				}
			}
		}

		private MessageBox(string message, MessageBoxButtons messageBoxButtons)
		{
			scene = Tests.RemoteScripting.Frame.Find<Frame>("Intrusion").Clone<Frame>();

			The.World.PushNode(scene);
			scene.Visible = true;
			var anchors = scene.Anchors;
			scene.Anchors = Anchors.None;
			scene.Width = scene.ParentWidget.Width;
			scene.Anchors = anchors;

			scene.Position = The.World.GlobalCenter;
			scene.Layer = RenderChain.LayerCount - 1;
			scene.Input.RestrictScope();
			scene["TextFailMessage"].Text = message;

			scene["BtnAbort"].Clicked = () => Close(InternalResult.Abort);
			scene["BtnPause"].Clicked = () => Close(InternalResult.Pause);

			if (messageBoxButtons == MessageBoxButtons.RetryIgnore || messageBoxButtons == MessageBoxButtons.Retry) {
				scene["BtnRetry"].Clicked = () => Close(InternalResult.Retry);
			} else {
				scene["BtnRetry"].Visible = false;
			}
			if (messageBoxButtons == MessageBoxButtons.RetryIgnore || messageBoxButtons == MessageBoxButtons.Ignore) {
				scene["BtnSkip"].Clicked = () => Close(InternalResult.Ignore);
			} else {
				scene["BtnSkip"].Visible = false;
			}
		}

		private void Close(InternalResult result)
		{
			IsClosed = true;
			internalResult = result;
			scene.Visible = false;
		}

		public void Dispose()
		{
			scene.UnlinkAndDispose();
		}

		private enum InternalResult
		{
			Retry,
			Ignore,
			Abort,
			Pause,
		}
	}

	public enum MessageBoxResult
	{
		Retry,
		Ignore,
	}

	public enum MessageBoxButtons
	{
		RetryIgnore,
		Retry,
		Ignore,
	}
}
