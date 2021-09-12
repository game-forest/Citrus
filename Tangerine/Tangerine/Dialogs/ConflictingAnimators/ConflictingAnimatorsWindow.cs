using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public class ConflictingAnimatorsWindow
	{
		private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
		private const string DefaultTitle = @"Conflicting Animators";
		private const WindowStyle Style = WindowStyle.Regular;
		private const WindowType Type = WindowType.Common;
		private const bool FixedSize = false;

		private static Vector2? savedPosition;
		private static readonly System.Drawing.Icon icon = new System.Drawing.Icon(
			new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()
		);

		public readonly WindowWidget WindowWidget;

		public ConflictingAnimatorsWindow()
		{
			var display = DockManager.Instance.MainWindowWidget.Window.Display;
			var displayCenter = display.Position + display.Size / 2;
			var window = new Window(new WindowOptions {
				Icon = icon,
				Title = DefaultTitle,
				Style = Style,
				Type = Type,
				FixedSize = FixedSize,
			});
			window.DecoratedPosition = savedPosition ?? (displayCenter - window.DecoratedSize / 2f);
			window.Closing += Window_Closing;

			WindowWidget = new WindowWidget(window);
			WindowWidget.Window.Restore();
			WindowWidget.SetFocus();

			Project.Closing += Project_Closing;
			Project.Opening += Project_Opening;
		}

		private void Project_Closing()
		{
			WindowWidget.Window.Title = DefaultTitle;
		}

		private void Project_Opening(string path)
		{
			var proj = System.IO.Path.GetFileNameWithoutExtension(path);
			WindowWidget.Window.Title = $"{proj} - {DefaultTitle}";
		}

		private bool Window_Closing(CloseReason reason)
		{
			savedPosition = WindowWidget?.Window?.DecoratedPosition;
			WindowWidget?.UnlinkAndDispose();
			Project.Closing -= Project_Closing;
			Project.Opening -= Project_Opening;
			return true;
		}
	}
}
