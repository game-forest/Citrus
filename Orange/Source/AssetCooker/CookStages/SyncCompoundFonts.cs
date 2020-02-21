using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncCompoundFonts : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return fontExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return fontExtension; } }

		private readonly string fontExtension = ".cft";

		public SyncCompoundFonts(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.GetUpdateOperationCount(fontExtension);

		public void Action() => AssetCooker.SyncUpdated(fontExtension, fontExtension, Converter);

		private bool Converter(string srcPath, string dstPath)
		{
			var font = InternalPersistence.Instance.ReadObjectFromBundle<SerializableCompoundFont>(AssetCooker.InputBundle, srcPath);
			InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, dstPath, font, Persistence.Format.Binary, fontExtension,
				AssetCooker.InputBundle.GetFileLastWriteTime(srcPath), AssetAttributes.None, AssetCooker.CookingRulesMap[srcPath].SHA1);
			return true;
		}
	}
}
