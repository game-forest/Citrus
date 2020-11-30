using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncFonts : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return fontExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return fontExtension; } }

		private readonly string fontExtension = ".tft";

		public SyncFonts(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.GetUpdateOperationCount(fontExtension);

		public void Action() => AssetCooker.SyncUpdated(fontExtension, fontExtension, Converter);

		private bool Converter(string srcPath, string dstPath)
		{
			var font = InternalPersistence.Instance.ReadObjectFromBundle<Font>(AssetCooker.InputBundle, srcPath);
			InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, dstPath, font, Persistence.Format.Binary, fontExtension,
				SHA1.Compute(AssetCooker.InputBundle.GetSourceSHA1(srcPath), AssetCooker.CookingRulesMap[srcPath].SHA1), AssetAttributes.None);
			return true;
		}
	}
}
