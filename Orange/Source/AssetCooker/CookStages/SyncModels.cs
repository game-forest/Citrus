using System;
using System.IO;
using Lime;
using System.Collections.Generic;
using Orange.FbxImporter;

namespace Orange
{
	class SyncModels : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield return fbxExtension; } }
		public IEnumerable<string> BundleExtensions { get { yield return t3dExtension; } }

		private readonly string fbxExtension = ".fbx";
		private readonly string t3dExtension = ".t3d";

		public SyncModels(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount() => AssetCooker.GetUpdateOperationCount(fbxExtension);

		public void Action() => AssetCooker.SyncUpdated(fbxExtension, t3dExtension, Converter, (srcPath, dstPath) => AssetCooker.ModelsToRebuild.Contains(dstPath));

		private bool Converter(string srcPath, string dstPath)
		{
			var cookingRules = AssetCooker.CookingRulesMap[srcPath];
			var compression = cookingRules.ModelCompression;
			Model3D model;
			var options = new FbxImportOptions {
				Path = srcPath,
				Target = AssetCooker.Target,
				CookingRulesMap = AssetCooker.CookingRulesMap
			};
			using (var fbxImporter = new FbxModelImporter(options)) {
				model = fbxImporter.LoadModel();
			}
			AssetAttributes assetAttributes;
			switch (compression) {
				case ModelCompression.None:
					assetAttributes = AssetAttributes.None;
					break;
				case ModelCompression.Deflate:
					assetAttributes = AssetAttributes.ZippedDeflate;
					break;
				case ModelCompression.LZMA:
					assetAttributes = AssetAttributes.ZippedLZMA;
					break;
				default:
					throw new ArgumentOutOfRangeException($"Unknown compression: {compression}");
			}
			var animationPathPrefix = AssetCooker.GetModelAnimationPathPrefix(dstPath);
			AssetCooker.DeleteModelExternalAnimations(animationPathPrefix);
			AssetCooker.ExportModelAnimations(model, animationPathPrefix, assetAttributes, cookingRules.SHA1);
			model.RemoveAnimatorsForExternalAnimations();
			InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, dstPath, model, Persistence.Format.Binary, t3dExtension,
				AssetCooker.InputBundle.GetFileLastWriteTime(srcPath), assetAttributes, cookingRules.SHA1);
			return true;
		}
	}
}
