#if WIN
using System;
#elif MAC
using AppKit;
#endif
using System.Linq;
using Lime;

namespace Orange
{
	internal class MainClass
	{
#if WIN
		[STAThread]
		public static void Main(string[] args)
		{
			var culture = System.Globalization.CultureInfo.InvariantCulture;
			System.Threading.Thread.CurrentThread.CurrentCulture = culture;
			PluginLoader.RegisterAssembly(typeof(MainClass).Assembly);
			var thisExe = System.Reflection.Assembly.GetExecutingAssembly();
			var resources = thisExe.GetManifestResourceNames();
			var supportedRenderingBackends = Lime.Application.EnumerateSupportedRenderingBackends().ToList();
			var renderingBackend = RenderingBackend.OpenGL;
			if (supportedRenderingBackends.Contains(RenderingBackend.Vulkan)) {
				renderingBackend = RenderingBackend.Vulkan;
			}
			Application.Initialize(new ApplicationOptions {
				RenderingBackend = renderingBackend,
			});
			OrangeApp.Initialize();
			Application.Run();
		}
#elif MAC
		static void Main(string[] args)
		{
			Application.Initialize();
			NSApplication.SharedApplication.DidFinishLaunching += (sender, e) => OrangeApp.Initialize();
			Application.Run();
		}
#endif
	}
}
