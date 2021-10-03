using System.Linq;
using Tests.Scripts.Common;
using Lime;
using Command = Tests.Scripts.Common.Command;

namespace Tests.Scripts.Tests
{
	public class BoundingBoxesTool : TestCoroutine
	{
		private static readonly WidgetBoundsPresenter presenter = new WidgetBoundsPresenter(Color4.Red, 5);

		private static Command.TimeFlow GetDefaultTestTimeFlow() => new Command.NormalTimeFlow(applyImmediately: false);

		public BoundingBoxesTool() : base(GetDefaultTestTimeFlow()) { }

		protected override async Coroutine RunTest()
		{
			while (true) {
				foreach (var widget in The.World.Descendants.OfType<Widget>()) {
					if (!widget.HitTestTarget || widget.CompoundPostPresenter.Contains(presenter)) {
						continue;
					}
					widget.CompoundPostPresenter.Add(presenter);
				}
				await Command.Wait(1);
			}
		}
	}
}
