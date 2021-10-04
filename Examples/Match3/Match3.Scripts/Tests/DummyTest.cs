using Match3.Scripts.Common;

namespace Match3.Scripts.Tests
{
	/// <summary>
	/// This test should be used for quick testing only. Use it if you want to check something small.
	/// If you have changes in this file that you want to commit - you are probably doing something wrong
	/// </summary>
	public class DummyTest : TestCoroutine
	{
		private static Command.TimeFlow GetDefaultTestTimeFlow() => new Command.FramesSkippingTimeFlow(applyImmediately: false);

		public DummyTest() : base(GetDefaultTestTimeFlow()) { }

		protected override async Coroutine RunTest()
		{
			// TODO: Write your code here
		}
	}
}
