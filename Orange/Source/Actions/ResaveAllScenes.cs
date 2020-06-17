using System.ComponentModel.Composition;
using System;
using System.Collections.Generic;
using Lime;
using System.IO;
using System.Linq;
using Tangerine.Core;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(Orange.OrangePlugin.MenuItems))]
		[ExportMetadata(nameof(IMenuItemMetadata.Label), "Resave All Scenes")]
		public static void ResaveAllScenes()
		{
			var savedAssetBundle = AssetBundle.Current;
			AssetBundle.Current = new TangerineAssetBundle(The.Workspace.AssetsDirectory);
			int removedAnimatorsCount = 0;
			foreach (var file in AssetBundle.Current.EnumerateFiles(null, ".tan")) {
				var node = Node.CreateFromAssetBundle(
					path: Path.ChangeExtension(file, null),
					ignoreExternals: true
				);
				removedAnimatorsCount += node.RemoveDanglingAnimators();
				InternalPersistence.Instance.WriteObjectToBundle(
					bundle: AssetBundle.Current,
					path: file,
					instance: node,
					format: Persistence.Format.Json,
					sourceExtension: "tan",
					time: File.GetLastWriteTime(file),
					attributes: AssetAttributes.None,
					cookingRulesSHA1: null
				);
			}
			if (removedAnimatorsCount != 0) {
				var message = removedAnimatorsCount == 1 ?
					"1 dangling animator has been removed?" :
					$"{removedAnimatorsCount} dangling animators have been removed!";
				Console.WriteLine(message);
			}
			AssetBundle.Current = savedAssetBundle;
		}
	}
}
