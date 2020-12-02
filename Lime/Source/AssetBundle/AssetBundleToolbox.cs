using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

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
			List<AssetBundle> previousBundlePatches, AssetBundle currentBundle, AssetBundle currentBundlePatch,
			int baseBundleVersion)
		{
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
					// Don't compare last write time because it can be different for the same files on different machines.
					if (
						currentBundle.GetAttributes(file) == patch.GetAttributes(file) &&
						currentBundle.GetCookingUnitHash(file) == patch.GetCookingUnitHash(file) &&
						AreFilesEqual(file, patch, currentBundle)
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
			currentManifest.Write(currentBundlePatch);
		}

		private static void ImportAsset(string file, AssetBundle sourceBundle, AssetBundle destinationBundle)
		{
			using (var stream = sourceBundle.OpenFileRaw(file)) {
				destinationBundle.ImportFileRaw(
					file, stream, sourceBundle.GetCookingUnitHash(file),
					sourceBundle.GetAttributes(file));
			}
		}

		private static readonly byte[] buffer1 = new byte[1024 * 128];
		private static readonly byte[] buffer2 = new byte[1024 * 128];

		private static bool AreFilesEqual(string file, AssetBundle bundle1, AssetBundle bundle2)
		{
			using (var stream1 = bundle1.OpenFileRaw(file)) {
				using (var stream2 = bundle2.OpenFileRaw(file)) {
					var reader1 = new BinaryReader(stream1);
					var reader2 = new BinaryReader(stream2);
					for (;;) {
						var c1= reader1.Read(buffer1, 0, buffer1.Length);
						var c2= reader2.Read(buffer2, 0, buffer2.Length);
						if (c1 != c2) {
							return false;
						}
						if (c1 == 0) {
							return true;
						}
						if (!AreByteArraysEqual(buffer1, buffer2)) {
							return false;
						}
					}
				}
			}
		}

		private static unsafe bool AreByteArraysEqual(byte[] array1, byte[] array2)
		{
			if (array1 == null && array2 == null) {
				return true;
			} else if (
				array1 == null ||
				array2 == null ||
				array1.Length != array2.Length
			) {
				return false;
			}
			fixed (byte* ptr1 = array1) {
				fixed (byte* ptr2 = array2) {
					return memcmp(ptr1, ptr2, array1.Length) == 0;
				}
			}
		}

#if iOS
		const string stdlib = "libc";
#else
		const string stdlib = "msvcrt";
#endif

		[DllImport(stdlib, EntryPoint = "memcmp", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
		private static unsafe extern int memcmp(byte* ptr1, byte* ptr2, int count);
	}
}
