using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Lime;

namespace Orange
{
	class SyncDeleted : AssetCookerCookStage, ICookStage
	{
		public IEnumerable<string> ImportedExtensions { get { yield break; } }
		public IEnumerable<string> BundleExtensions
		{
			get
			{
				foreach (var i in toDeleteExtensions) {
					yield return i;
				}
				yield return modelTanExtension;
			}
		}

		private readonly string[] toDeleteExtensions = { ".atlasPart", ".mask", ".texture", ".ant", ".msh" };
		private readonly string modelTanExtension = ".t3d";

		public SyncDeleted(AssetCooker assetCooker) : base(assetCooker) { }

		public int GetOperationCount()
		{
			var result = 0;
			var assetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var path in AssetCooker.InputBundle.EnumerateFiles()) {
				assetFiles.Add(path);
			}
			foreach (var path in AssetCooker.OutputBundle.EnumerateFiles()) {
				if (!path.StartsWith("Atlases") &&
				    !toDeleteExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase) &&
				    !assetFiles.Contains(Path.ChangeExtension(path, AssetCooker.GetOriginalAssetExtension(path)))) {
					result++;
				}
			}
			return result;
		}

		public void Action()
		{
			var assetFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (var path in AssetCooker.InputBundle.EnumerateFiles()) {
				assetFiles.Add(path);
			}
			foreach (var path in AssetCooker.OutputBundle.EnumerateFiles().ToList()) {
				// Ignoring texture atlases
				if (path.StartsWith("Atlases")) {
					continue;
				}
				// Ignore atlas parts, masks, animations
				var ext = Path.GetExtension(path);
				if (toDeleteExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase)) {
					continue;
				}
				var assetPath = Path.ChangeExtension(path, AssetCooker.GetOriginalAssetExtension(path));
				if (!assetFiles.Contains(assetPath)) {
					if (path.EndsWith(modelTanExtension, StringComparison.OrdinalIgnoreCase)) {
						AssetCooker.DeleteModelExternalAnimations(AssetCooker.GetModelAnimationPathPrefix(path));
						var externalMeshPath = AssetCooker.GetModelExternalMeshPath(path);
						if (AssetCooker.OutputBundle.FileExists(externalMeshPath)) {
							AssetCooker.DeleteFileFromBundle(externalMeshPath);
						}
					}
					var modelAttachmentExtIndex = path.LastIndexOf(Model3DAttachment.FileExtension);
					if (modelAttachmentExtIndex >= 0) {
						AssetCooker.ModelsToRebuild.Add(path.Remove(modelAttachmentExtIndex) + modelTanExtension);
					}
					AssetCooker.DeleteFileFromBundle(path);
					UserInterface.Instance.IncreaseProgressBar();
				}
			}
		}
	}
}
