using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Orange.FbxImporter;
using SHA256 = Lime.SHA256;

namespace Orange
{
	class SyncModels : ICookingStage
	{
		private readonly AssetCooker assetCooker;

		public SyncModels(AssetCooker assetCooker)
		{
			this.assetCooker = assetCooker;
		}

		public IEnumerable<(string, SHA256)> EnumerateCookingUnits()
		{
			foreach (var fbx in assetCooker.InputBundle.EnumerateFiles(null, ".fbx")) {
				var hash = SHA256.Compute(assetCooker.InputBundle.GetFileHash(fbx), AssetCooker.CookingRulesMap[fbx].Hash);
				var attachment = System.IO.Path.ChangeExtension(fbx, Model3DAttachment.FileExtension);
				if (assetCooker.InputBundle.FileExists(attachment)) {
					hash = SHA256.Compute(hash, SHA256.Compute(
						assetCooker.InputBundle.GetFileHash(attachment), AssetCooker.CookingRulesMap[attachment].Hash));
				}
				yield return (fbx, hash);
			}
		}

		public void Cook(string cookingUnit, SHA256 cookingUnitHash)
		{
			var cookingRules = AssetCooker.CookingRulesMap[cookingUnit];
			var compression = cookingRules.ModelCompression;
			Model3D model;
			var options = new FbxImportOptions {
				Path = cookingUnit,
				Target = assetCooker.Target,
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
			var animationPathPrefix = AssetCooker.GetModelAnimationPathPrefix(cookingUnit);
			ExportModelAnimations(model, animationPathPrefix, assetAttributes, cookingUnitHash);
			model.RemoveAnimatorsForExternalAnimations();
			var externalMeshPath = AssetCooker.GetModelExternalMeshPath(cookingUnit);
			ExportModelMeshes(model, externalMeshPath, assetAttributes, cookingUnitHash);
			InternalPersistence.Instance.WriteObjectToBundle(
				assetCooker.OutputBundle, cookingUnit, model, Persistence.Format.Binary, cookingUnitHash, assetAttributes);
		}

		private void ExportModelAnimations(Model3D model, string pathPrefix, AssetAttributes assetAttributes, SHA256 cookingUnitHash)
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
					assetCooker.OutputBundle, path, data, Persistence.Format.Binary,
					cookingUnitHash, assetAttributes);
			}
		}

		private void ExportModelMeshes(Model3D model, string path, AssetAttributes assetAttributes, SHA256 cookingUnitHash)
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
				model.MeshContentPath = System.IO.Path.ChangeExtension(path, null);
				InternalPersistence.Instance.WriteObjectToBundle(
					assetCooker.OutputBundle, path, data, Persistence.Format.Binary, cookingUnitHash, assetAttributes);
				Console.WriteLine("+ " + path);
			}
		}
	}
}
