using System;
using System.IO;
using Lime;

namespace Orange
{
	internal static class BundleUtils
	{
		/// <summary>
		/// Unpacks <see cref="Lime.PackedAssetBundle"/> at <paramref name="sourcePath"/> into
		/// <see cref="Lime.UnpackedAssetBundle"/> at <paramref name="destinationPath"/>.
		/// Destination bundle is created if not exists. Otherwise if it exists all files present in destination
		/// but not present in source will be removed.
		/// </summary>
		/// <remarks>
		/// Destination bundle is wrapped into <see cref="Orange.VerboseAssetBundle"/> to produce console output.
		/// </remarks>
		/// <param name="sourcePath">Path of the source <see cref="PackedAssetBundle"/></param>
		/// <param name="destinationPath">Path of the output directory.</param>
		public static void UnpackBundle(string sourcePath, string destinationPath)
		{
			if (!Directory.Exists(destinationPath)) {
				Directory.CreateDirectory(destinationPath);
			}
			using (var sourceBundle = new PackedAssetBundle(sourcePath))
			using (var destination = new VerboseAssetBundle(new UnpackedAssetBundle(destinationPath))) {
				foreach (var file in destination.EnumerateFiles()) {
					if (!sourceBundle.FileExists(file)) {
						try {
							destination.DeleteFile(file);
						} catch (System.Exception e) {
							Console.WriteLine($"Error: caught an exception when deleting file '{file}': {e}");
						}
					}
				}
				foreach (var file in sourceBundle.EnumerateFiles()) {
					var exists = destination.FileExists(file);
					if (!exists || destination.GetFileContentsHash(file) != sourceBundle.GetFileContentsHash(file)) {
						try {
							if (exists) {
								destination.DeleteFile(file);
							}
							using var sourceStream = sourceBundle.OpenFile(file);
							destination.ImportFile(
								file,
								sourceStream,
								sourceBundle.GetFileCookingUnitHash(file),
								sourceBundle.GetFileAttributes(file)
							);
						} catch (System.Exception e) {
							Console.WriteLine(
								$"Error: caught an exception when unpacking bundle '{sourcePath}', file '{file}': {e}"
							);
						}
					}
				}
				try {
					DeleteEmptyDirectories(destinationPath);
				} catch (System.Exception e) {
					Console.WriteLine(
						$"Error: caught an exception when deleting empty directories at '{destinationPath}': '{e}'."
					);
				}
			}

			static void DeleteEmptyDirectories(string baseDirectory)
			{
				foreach (var directory in Directory.GetDirectories(baseDirectory)) {
					DeleteEmptyDirectories(directory);
					if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0) {
						Directory.Delete(directory, false);
					}
				}
			}
		}
	}
}
