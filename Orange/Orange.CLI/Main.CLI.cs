using System;

namespace Orange
{
	internal class MainClass
	{
#if WIN
		[STAThread]
#endif
		private static void Main(string[] args)
		{
#if MAC
			Lime.Application.Initialize();
#endif
			var culture = System.Globalization.CultureInfo.InvariantCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = culture;
			PluginLoader.RegisterAssembly(typeof(MainClass).Assembly);
			UserInterface.Instance = new ConsoleUI();
			UserInterface.Instance.Initialize();
		}
	}
}
