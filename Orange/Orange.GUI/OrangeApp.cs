using System;
using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	public class OrangeApp
	{
		public static OrangeApp Instance { get; private set; }
		private OrangeInterface Interface { get; }

		public static void Initialize()
		{
			Instance = new OrangeApp();
		}

		private OrangeApp()
		{
			WidgetInput.AcceptMouseBeyondWidgetByDefault = false;
			LoadFont();
			UserInterface.Instance = Interface = new OrangeInterface();
			UserInterface.Instance.Initialize();
			The.Workspace.Load();
			CreateActionMenuItems();
		}

		private static void CreateActionMenuItems()
		{
			The.MenuController.CreateAssemblyMenuItems();
		}

		private static void LoadFont()
		{
			var defaultFonts = new List<IFont>();
			var fontResourcePaths = new string[] {
				"Orange.GUI.Resources.SegoeUIRegular.ttf",
				"Orange.GUI.Resources.NotoSansCJKtc-Regular.ttf",
			};
			foreach (var resource in fontResourcePaths) {
				try {
					defaultFonts.Add(
						new Lime.DynamicFont(new EmbeddedResource(resource, "Orange.GUI").GetResourceBytes())
					);
				} catch (SystemException e) {
					System.Console.WriteLine($"Couldn't load font {resource}: {e}");
				}
			}
			var compoundFont = new CompoundFont(defaultFonts);
			FontPool.Instance.AddFont(FontPool.DefaultFontName, compoundFont);
		}
	}
}
