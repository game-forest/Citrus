using System.Collections.Generic;
using System.IO;
using Lime;

namespace Orange
{
	class SyncScenes : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return sceneExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return sceneExtension; } }

		private readonly string sceneExtension = ".tan";

		public SyncScenes(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.GetUpdateOperationCount(sceneExtension);

		public void Action() => AssetCooker.SyncUpdated(sceneExtension, sceneExtension, Converter);

		private bool Converter(string srcPath, string dstPath)
		{
			var node = InternalPersistence.Instance.ReadObjectFromBundle<Node>(AssetCooker.InputBundle, srcPath);
			InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, dstPath, node, Persistence.Format.Binary, sceneExtension,
				AssetCooker.InputBundle.GetFileLastWriteTime(srcPath), AssetAttributes.None, AssetCooker.CookingRulesMap[srcPath].SHA1);
			return true;
		}
	}
}
