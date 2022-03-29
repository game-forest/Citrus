using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using Lime;
using Yuzu;

namespace Orange
{
	internal static class SaveDeducedCookingRules
	{
		// Yuzu can't into tuples
		public class FileAndRules
		{
			[YuzuMember]
			public string Name { get; set; }

			[YuzuMember]
			public ParticularCookingRules Rules { get; set; }
		}

		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Save deduced cooking rules")]
		public static void SaveDeducedCookingRulesAction()
		{
			var data = new List<(string TargetName, List<FileAndRules> RuleList)>();
			foreach (var target in Workspace.Instance.Targets) {
				string name = target.Name;
				var map = CookingRulesBuilder.Build(AssetBundle.Current, target);
				var l = map.Select(i => new FileAndRules {
					Name = i.Key,
					Rules = i.Value.EffectiveRules,
				}).ToList();
				l.Sort((a, b) => a.Name.CompareTo(b.Name));
				data.Add((name, new List<FileAndRules>(l)));
			}
			var dialog = new FileDialog {
				Mode = FileDialogMode.SelectFolder,
				Title = "Select a directory",
			};
			bool? dialogCanceled = null;
			// Showing UI must be executed on the UI thread.
			Application.InvokeOnMainThread(() => dialogCanceled = !dialog.RunModal());
			while (!dialogCanceled.HasValue) {
				System.Threading.Thread.Sleep(50);
			}
			if (dialogCanceled.Value) {
				return;
			}
			var p = new Persistence(
				Lime.Persistence.NewDefaultYuzuOptions(),
				new Yuzu.Json.JsonSerializeOptions {
					SaveClass = Yuzu.Json.JsonSaveClass.None,
					Unordered = false,
					EnumAsString = true,
				}
			);
			foreach (var i in data) {
				var filename = Path.Combine(
					dialog.FileName,
					The.Workspace.ProjectName + "_" + i.TargetName + ".json"
				);
				p.WriteToFile(filename, i.RuleList, Persistence.Format.Json);
			}
		}
	}
}
