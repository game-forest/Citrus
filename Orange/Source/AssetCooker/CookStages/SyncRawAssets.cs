using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncRawAssets : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return extension; } }
		public IEnumerable<string> BundleExtensions { get { yield return extension; } }

		private readonly string extension;
		private readonly AssetAttributes attributes;

		public SyncRawAssets(AssetCooker assetCooker, string extension, AssetAttributes attributes = AssetAttributes.None)
			: base(assetCooker)
		{
			this.extension = extension;
			this.attributes = attributes;
		}

		public int GetOperationCount() => AssetCooker.GetUpdateOperationCount(extension);

		public void Action() => AssetCooker.SyncUpdated(extension, extension, Converter);

		private bool Converter(string srcPath, string dstPath)
		{
			AssetCooker.OutputBundle.ImportFile(AssetCooker.InputBundle.ToSystemPath(srcPath), dstPath, 0, extension, attributes,
				AssetCooker.InputBundle.GetFileLastWriteTime(srcPath), AssetCooker.CookingRulesMap[srcPath].SHA1);
			return true;
		}
	}
}
