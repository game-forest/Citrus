using System;

namespace Tests.Win
{
	public class Application
	{
		[STAThread]
		public static void Main(string[] args)
		{
			Lime.Application.Initialize(new Lime.ApplicationOptions());
			Tests.Application.Application.Initialize();
			Lime.Application.Run();
		}
	}
}