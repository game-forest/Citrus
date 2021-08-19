using System.ComponentModel.Composition;
using System;
using System.Collections.Generic;
using Lime;
using System.IO;
using Tangerine.Core;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(Orange.OrangePlugin.MenuItems))]
		[ExportMetadata(nameof(IMenuItemMetadata.Label), "Search for External Scenes with Incompatible Placeholder")]
		public static void SearchForExternalsWithIncompatiblePlaceholder()
		{
			Dictionary<string, Type> rootTypes = new Dictionary<string, Type>();
			var savedAssetBundle = AssetBundle.Current;
			AssetBundle.Current = new TangerineAssetBundle(The.Workspace.AssetsDirectory);
			foreach (var file in AssetBundle.Current.EnumerateFiles(null, ".tan")) {
				try {
					var node = Node.Load(
						path: Path.ChangeExtension(file, null),
						ignoreExternals: true
					);
					foreach (var n in node.SelfAndDescendants) {
						if (n.ContentsPath != null) {
							var externalPath = Path.ChangeExtension(n.ContentsPath, "tan");
							if (!AssetBundle.Current.FileExists(externalPath)) {
								Console.WriteLine(
									$"External scene `{externalPath}` referred by `{file}` doesn't exist."
								);
								continue;
							}
							if (n.GetType() != GetExternalType(n.ContentsPath)) {
								Console.WriteLine($"Types doesn't match external:"
									+ $"`{externalPath}:{GetExternalType(n.ContentsPath).FullName}', "
									+ $"placeholder: '{file}:{n.GetType().FullName}'");
							}
						}
					}
				} catch (System.Exception e) {
					Console.WriteLine($"Error while loading scene: `{file}': '{e}'");
				}
			}
			AssetBundle.Current = savedAssetBundle;

			Type GetExternalType(string f)
			{
				f = Path.ChangeExtension(f, null);
				if (!rootTypes.TryGetValue(f, out var type)) {
					var node = Node.Load(
						path: f,
						ignoreExternals: true
					);
					rootTypes.Add(f, type = node.GetType());
				}
				return type;
			}
		}
	}
}
