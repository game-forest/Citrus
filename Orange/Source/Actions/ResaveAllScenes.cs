using System.ComponentModel.Composition;
using System;
using Lime;
using System.IO;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(Orange.OrangePlugin.MenuItems))]
		[ExportMetadata(nameof(IMenuItemMetadata.Label), "Resave All Scenes")]
		public static void ResaveAllScenes()
		{
			foreach (var file in AssetBundle.Current.EnumerateFiles()) {
				var filename = Path.GetFileName(file);
				if (!filename.EndsWith("tan", StringComparison.OrdinalIgnoreCase)) {
					continue;
				}
				var node = Node.CreateFromAssetBundle(
					path: Path.ChangeExtension(file, null),
					ignoreExternals: true
				);
				InternalPersistence.Instance.WriteObjectToBundle(
					bundle: AssetBundle.Current,
					path: file,
					instance: node,
					format: Persistence.Format.Json,
					sourceExtension: "tan",
					time: AssetBundle.Current.GetFileLastWriteTime(file),
					attributes: AssetAttributes.None,
					cookingRulesSHA1: null
				);
			}
		}
	}
}
