using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;

namespace Orange
{
	class RemoveDeprecatedModels : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield break; } }
		public IEnumerable<string> BundleExtensions { get { yield return modelExtension; } }

		private readonly string modelExtension = ".model";

		public RemoveDeprecatedModels(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.InputBundle.EnumerateFiles(null, modelExtension).Count();

		public void Action()
		{
			foreach (var path in AssetCooker.InputBundle.EnumerateFiles(null, modelExtension)) {
				if (AssetCooker.CookingRulesMap.ContainsKey(path)) {
					AssetCooker.CookingRulesMap.Remove(path);
				}
				Logger.Write($"Removing deprecated .model file: {path}");
				AssetCooker.InputBundle.DeleteFile(path);
				UserInterface.Instance.IncreaseProgressBar();
			}
		}
	}
}
