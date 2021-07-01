using System;
using System.Collections.Generic;
using System.Linq;

namespace Lime
{
	public static class AssetBundleToolbox
	{
		/// <summary>
		/// Creates a new asset bundle patch.
		/// </summary>
		/// <param name="previousBundlePatches">List of previously created bundle patches.</param>
		/// <param name="currentBundle">Up-to-date bundle with all assets.</param>
		/// <param name="currentBundlePatch">Output bundle patch.</param>
		/// <param name="baseBundleVersion">Version on the last bundle patch in the previousBundlePatches list.</param>
		public static void CreatePatch(
			List<AssetBundle> previousBundlePatches,
			AssetBundle currentBundle,
			AssetBundle currentBundlePatch,
			int baseBundleVersion
		) {
			// Build up list of files inherited from the base bundle.
			var inheritedFiles = new HashSet<string>();
			foreach (var patch in previousBundlePatches) {
				foreach (var file in patch.EnumerateFiles()) {
					inheritedFiles.Add(file);
				}
				var manifest = PackedAssetBundle.Manifest.Create(patch);
				foreach (var deletedFile in manifest.DeletedAssets) {
					inheritedFiles.Remove(deletedFile);
				}
			}
			// Import new or modified files.
			var previousPatchesReversed = previousBundlePatches.ToList();
			previousPatchesReversed.Reverse();
			foreach (var file in currentBundle.EnumerateFiles()) {
				if (!inheritedFiles.Contains(file)) {
					// Import a new file.
					ImportAsset(file, currentBundle, currentBundlePatch);
					continue;
				}
				var fileModified = true;
				foreach (var patch in previousPatchesReversed) {
					if (!patch.FileExists(file)) {
						continue;
					}
					if (
						// TODO: think if checking hash only is enough.
						// Attributes are produced from cooking rules which are part of cooking unit hash.
						currentBundle.GetFileAttributes(file) == patch.GetFileAttributes(file) &&
						currentBundle.GetFileCookingUnitHash(file) == patch.GetFileCookingUnitHash(file) &&
						currentBundle.GetFileContentsHash(file) == patch.GetFileContentsHash(file)
					) {
						fileModified = false;
					}
					break;
				}
				if (fileModified) {
					ImportAsset(file, currentBundle, currentBundlePatch);
				}
			}
			// Prepare the current bundle manifest.
			var currentManifest = new PackedAssetBundle.Manifest();
			// Build up deleted files list.
			foreach (var file in inheritedFiles) {
				if (inheritedFiles.Contains(file) && !currentBundle.FileExists(file) && file != PackedAssetBundle.Manifest.FileName) {
					currentManifest.DeletedAssets.Add(file);
				}
			}
			currentManifest.BaseBundleVersion = baseBundleVersion;
			currentManifest.Save(currentBundlePatch);
		}

		private static void ImportAsset(string file, AssetBundle sourceBundle, AssetBundle destinationBundle)
		{
			using var stream = sourceBundle.OpenFileRaw(file);
			destinationBundle.ImportFileRaw(
				destinationPath: file,
				stream: stream,
				unpackedSize: sourceBundle.GetFileUnpackedSize(file),
				hash: sourceBundle.GetFileContentsHash(file),
				cookingUnitHash: sourceBundle.GetFileCookingUnitHash(file),
				attributes: sourceBundle.GetFileAttributes(file)
			);
		}
	}
}
