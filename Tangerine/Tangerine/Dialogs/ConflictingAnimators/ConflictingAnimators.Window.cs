using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public static partial class ConflictingAnimators
	{
		public class Window
		{
			private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
			private const string DefaultTitle = @"Conflicting Animators";
			private const WindowStyle Style = WindowStyle.Regular;
			private const WindowType Type = WindowType.Common;
			private const bool FixedSize = false;

			private static Vector2? savedPosition;

#if WIN
			private static readonly System.Drawing.Icon icon = new System.Drawing.Icon(
				new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()
			);
#endif

			private readonly WindowWidget windowWidget;

			public Window()
			{
				var display = DockManager.Instance.MainWindowWidget.Window.Display;
				var displayCenter = display.Position + display.Size / 2;
				var window = new Lime.Window(new WindowOptions {
#if WIN
					Icon = icon,
#endif
					Title = DefaultTitle,
					Style = Style,
					Type = Type,
					FixedSize = FixedSize,
				});
				window.DecoratedPosition = savedPosition ?? (displayCenter - window.DecoratedSize / 2f);
				window.Closing += WindowClosing;

				windowWidget = new WindowWidget(window);
				windowWidget.SetFocus();

				Project.Closing += ProjectClosing;
				Project.Opening += ProjectOpening;
			}

			private void ProjectClosing()
			{
				windowWidget.Window.Title = DefaultTitle;
			}

			private void ProjectOpening(string path)
			{
				var proj = System.IO.Path.GetFileNameWithoutExtension(path);
				windowWidget.Window.Title = $"{proj} - {DefaultTitle}";
			}

			private bool WindowClosing(CloseReason reason)
			{
				savedPosition = windowWidget?.Window?.DecoratedPosition;
				windowWidget?.UnlinkAndDispose();
				Project.Closing -= ProjectClosing;
				Project.Opening -= ProjectOpening;
				return true;
			}
		}
	}
}
