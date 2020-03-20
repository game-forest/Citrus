using System;
using System.ComponentModel.Composition;
using System.IO;
using Lime;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(Orange.OrangePlugin.MenuItems))]
		[ExportMetadata(nameof(IMenuItemMetadata.Label), "Benchmark Binary Deserialization")]
		public static void BenchmarkBinaryDeserialization()
		{
			var savedAssetBundle = AssetBundle.Current;
			var target = The.UI.GetActiveTarget();
			var ac = new AssetCooker(target);
			var bundles = ac.GetListOfAllBundles();
			Helper(".t3d");
			Helper(".tan");

			void Helper(string extension, int n = 10)
			{
				long totalFiles = 0;
				long totalSize = 0;
				foreach (var bundleName in bundles) {
					var bundle = new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName));
					AssetBundle.Current = bundle;
					foreach (var f in bundle.EnumerateFiles(null, extension)) {
						totalFiles++;
						totalSize += bundle.GetFileSize(f);
					}
				}
				Console.WriteLine($"[{extension}]: total files: {totalFiles}");
				Console.WriteLine($"[{extension}]: total bytes: {totalSize}");
				Console.WriteLine($"[{extension}]: total average file size: {(double)totalSize / (double)totalFiles}");
				for (int i = 0; i < n; i++) {
					long time = 0;
					var sw = new System.Diagnostics.Stopwatch();
					foreach (var bundleName in bundles) {
						var bundle = new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName));
						AssetBundle.Current = bundle;
						foreach (var f in bundle.EnumerateFiles()) {
							if (f.EndsWith(extension)) {
								sw.Start();
								var _ = Node.CreateFromAssetBundle(Path.ChangeExtension(f, null));
								time += sw.ElapsedMilliseconds;
								sw.Reset();
							}
						}
					}
					Console.WriteLine($"[{i + 1}/{n}] read {extension} from bundles read time: {time}ms");
					Console.WriteLine($"[{i + 1}/{n}]: bytes per ms {(double)totalSize / (double)(time)}");
				}
			}
			AssetBundle.Current = savedAssetBundle;
		}
	}
}
