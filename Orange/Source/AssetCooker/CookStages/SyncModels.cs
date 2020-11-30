using System;
using System.IO;
using Lime;
using System.Collections.Generic;
using System.Linq;
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
			ExportModelAnimations(model, animationPathPrefix, assetAttributes, cookingRules.SHA1);
			model.RemoveAnimatorsForExternalAnimations();
			var externalMeshPath = AssetCooker.GetModelExternalMeshPath(dstPath);
			if (AssetCooker.OutputBundle.FileExists(externalMeshPath)) {
				AssetCooker.DeleteFileFromBundle(externalMeshPath);
			}
			ExportModelMeshes(model, externalMeshPath, assetAttributes, cookingRules.SHA1);
			InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, dstPath, model, Persistence.Format.Binary, t3dExtension,
				SHA1.Compute(AssetCooker.InputBundle.GetSourceSHA1(srcPath), cookingRules.SHA1), assetAttributes);
			return true;
		}

		private void ExportModelAnimations(Model3D model, string pathPrefix, AssetAttributes assetAttributes, SHA1 cookingRulesSHA1)
		{
			foreach (var animation in model.Animations) {
				if (animation.IsLegacy) {
					continue;
				}
				var pathWithoutExt = pathPrefix + animation.Id;
				pathWithoutExt = Animation.FixAntPath(pathWithoutExt);
				var path = pathWithoutExt + ".ant";
				var data = animation.GetData();
				animation.ContentsPath = pathWithoutExt;
				InternalPersistence.Instance.WriteObjectToBundle(
					AssetCooker.OutputBundle, path, data, Persistence.Format.Binary, ".ant",
					SHA1.Compute(AssetCooker.InputBundle.GetSourceSHA1(path), cookingRulesSHA1), assetAttributes);
				Console.WriteLine("+ " + path);
			}
		}

		private void ExportModelMeshes(Model3D model, string path, AssetAttributes assetAttributes, byte[] cookingRulesSHA1)
		{
			var data = new Model3D.MeshData();
			var submeshes = model.Descendants
				.OfType<Mesh3D>()
				.SelectMany(m => m.Submeshes);
			foreach (var sm in submeshes) {
				data.Meshes.Add(sm.Mesh);
				sm.Mesh = null;
			}
			if (data.Meshes.Count > 0) {
				model.MeshContentPath = Path.ChangeExtension(path, null);
				InternalPersistence.Instance.WriteObjectToBundle(AssetCooker.OutputBundle, path, data, Persistence.Format.Binary, ".msh", AssetCooker.InputBundle.GetFileLastWriteTime(path), assetAttributes, cookingRulesSHA1);
				Console.WriteLine("+ " + path);
			}
		}
	}
}
