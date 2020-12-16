using EmptyProject.Scripts.Common;
using EmptyProject.Scripts.Coroutines;

namespace EmptyProject.Scripts.Tests
{
	public class ExampleTest : TestCoroutine
	{
		private static Command.TimeFlow GetDefaultTestTimeFlow() => new Command.FramesSkippingTimeFlow(applyImmediately: false);

		public ExampleTest() : base(GetDefaultTestTimeFlow()) { }

		protected override async Coroutine RunTest()
		{
			var gameScreen = await MainMenuCoroutines.OpenGameScreen();
			await GameScreenCoroutines.Close(gameScreen);
			using (new Command.NormalTimeFlow()) {
				var options = await MainMenuCoroutines.OpenOptions();
				await OptionsCoroutines.Close(options);
			}
		}
	}
}
