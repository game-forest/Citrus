#if WIN
using System;
using System.ComponentModel.Composition;

namespace Orange
{
	static partial class Actions
	{
		[Export(nameof(OrangePlugin.MenuItems))]
		[ExportMetadata("Label", "Optimize PNGs")]
		[ExportMetadata("Priority", 51)]
		public static void OptimizePNG()
		{
			// f for format file size
			Func<ulong, string> f = (ulong size) => {
				double dsize = size;
				string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
				int order = 0;
				while (size >= 1024 && order < suffixes.Length - 1) {
					order++;
					size /= 1024;
					dsize /= 1024.0;
				}
				return $"{dsize:0.##} {suffixes[order]}";
			};
			ulong totalLengthAfter = 0;
			ulong totalLengthBefore = 0;
			using (new DirectoryChanger(The.Workspace.AssetsDirectory)) {
				foreach (var path in Lime.AssetBundle.Current.EnumerateFiles(null, ".png")) {
					ulong lengthBefore = (ulong)new System.IO.FileInfo(path).Length;
					totalLengthBefore += lengthBefore;
					TextureConverter.OptimizePNG(path);
					ulong lengthAfter = (ulong)new System.IO.FileInfo(path).Length;
					totalLengthAfter += lengthAfter;
					Console.WriteLine(
						$"{path} : {f(lengthBefore)} => {f(lengthAfter)}, " +
						$"diff: {f(lengthBefore - lengthAfter)}"
					);
				}
			}
			Console.WriteLine(
				$"Totals: {f(totalLengthBefore)} => {f(totalLengthAfter)}, " +
				$"diff: {f(totalLengthBefore - totalLengthAfter)}"
			);
		}
	}
}
#endif // WIN
