#if WIN
using System;
using System.Collections.Generic;
using Lime;
using System.Linq;

namespace Tangerine
{
	public class MainApplication
	{
		[STAThread]
		public static void Main(string[] args)
		{
			var thisExe = System.Reflection.Assembly.GetExecutingAssembly();
			string [] resources = thisExe.GetManifestResourceNames();
			var supportedRenderingBackends = Lime.Application.EnumerateSupportedRenderingBackends().ToList();
			var renderingBackend = RenderingBackend.OpenGL;
			if (supportedRenderingBackends.Contains(RenderingBackend.Vulkan)) {
				renderingBackend = RenderingBackend.Vulkan;
			}
			Application.Initialize(new ApplicationOptions {
				RenderingBackend = renderingBackend
			});
			TangerineApp.Initialize(args);
			Lime.Application.Run();
		}
	}
}
#endif
