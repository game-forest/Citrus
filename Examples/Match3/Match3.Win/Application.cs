using System;

namespace Match3.Win
{
	public class Application
	{
		[STAThread]
		public static void Main(string[] args)
		{
			Lime.Application.Initialize(new Lime.ApplicationOptions());
			Match3.Application.Application.Initialize();
			Lime.Application.Run();
		}
	}
}