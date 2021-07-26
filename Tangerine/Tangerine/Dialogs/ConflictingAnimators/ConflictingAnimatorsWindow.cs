using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine
{
	public class ConflictingAnimatorsWindow
	{
		public static class ConflictingAnimatorsWindowWidgetProvider {
			private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
			private const string DefaultTitle = @"Conflicting Animators";
			private const WindowStyle Style = WindowStyle.Regular;
			private const WindowType Type = WindowType.Common;
			private const bool FixedSize = false;

			private static Vector2? position;
			private static WindowWidget instance;

			public static WindowWidget Get()
			{
				if (instance == null) {
					var display = DockManager.Instance.MainWindowWidget.Window.Display;
					var displayCenter = display.Position + display.Size / 2;

					var window = new Window(new WindowOptions {
						Icon = new System.Drawing.Icon(new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()),
						Title = DefaultTitle,
						Style = Style,
						Type = Type,
						FixedSize = FixedSize,
					});
					window.DecoratedPosition = position ?? (displayCenter - window.DecoratedSize / 2f);
					window.Closing += OnClose;

					// TODO: Fix memory leaks.
					Project.Closing += () => window.Title = DefaultTitle;
					Project.Opening += path => window.Title = $"{System.IO.Path.GetFileNameWithoutExtension(Project.Current.CitprojPath)} - {DefaultTitle}";
					instance = new ConflictingAnimatorsWindowWidget(window);
				}
				return instance;
			}

			public static bool OnClose(CloseReason reason)
			{
				position = instance?.Window?.DecoratedPosition;
				instance = null;
				return true;
			}
		}

		public ConflictingAnimatorsWindow()
		{
			var windowWidget = ConflictingAnimatorsWindowWidgetProvider.Get();
			windowWidget.Window.Restore();
			windowWidget.SetFocus();
		}
	}
}
