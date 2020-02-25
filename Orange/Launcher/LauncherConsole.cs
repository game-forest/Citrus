using System;
using System.IO;
using McMaster.Extensions.CommandLineUtils;
namespace Launcher
{
	// It's a workaround suggested by McMaster until they cope with Xamarin issue.
	public class LauncherConsole : IConsole
	{
		public static LauncherConsole Instance { get; } = new LauncherConsole();

		public TextWriter Error => Console.Error;

		public TextReader In => Console.In;

		public TextWriter Out => Console.Out;

		public bool IsInputRedirected => Console.IsInputRedirected;

		public bool IsOutputRedirected => Console.IsOutputRedirected;

		public bool IsErrorRedirected => Console.IsErrorRedirected;

		public ConsoleColor ForegroundColor
		{
			get => Console.ForegroundColor;
			set => Console.ForegroundColor = value;
		}

		public ConsoleColor BackgroundColor
		{
			get => Console.BackgroundColor;
			set => Console.BackgroundColor = value;
		}

		public void ResetColor () => Console.ResetColor();

		public event ConsoleCancelEventHandler CancelKeyPress
		{
			add { }
			remove { }
		}
	}
}
