using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Lime;
using SHA256 = Lime.SHA256;

namespace Orange
{
	public static class TextureConverter
	{
		public static void RunEtcTool(Bitmap bitmap, AssetBundle bundle, string path, AssetAttributes attributes, bool mipMaps, bool highQualityCompression, SHA256 cookingUnitHash)
		{
			var hasAlpha = bitmap.HasAlpha;
			var bledBitmap = hasAlpha ? TextureConverterUtils.BleedAlpha(bitmap) : null;
			var args = "{0} -format " + (hasAlpha ? "RGBA8" : "RGB8") + " -jobs {1} " +
			           " -effort " + (highQualityCompression ? "60" : "40") +
			           (mipMaps ? " -mipmaps 4" : "") + " -output {2}";
			var hashString = GetTextureHashString(bledBitmap ?? bitmap, ".etc", args);
			var cachePath = AssetCache.Instance.Load(hashString);
			if (cachePath != null) {
				bundle.ImportFile(cachePath, path, cookingUnitHash, attributes);
				return;
			}
			var ktxPath = Toolbox.GetTempFilePathWithExtension(".ktx");
			var pngPath = Path.ChangeExtension(ktxPath, ".png");
			var etcTool = GetToolPath("EtcTool");
			try {
				(bledBitmap ?? bitmap).SaveTo(pngPath);
				if (Process.Start(etcTool, args.Format(pngPath, 8, ktxPath)) != 0) {
					throw new Lime.Exception($"ETCTool error\nCommand line: {etcTool} {args}\"");
				}
				bundle.ImportFile(ktxPath, path, cookingUnitHash, attributes);
				AssetCache.Instance.Save(ktxPath, hashString);
			} finally {
				bledBitmap?.Dispose();
				DeletePossibleLockedFile(pngPath);
				DeletePossibleLockedFile(ktxPath);
			}
		}

		public static void RunPVRTexTool(Bitmap bitmap, AssetBundle bundle, string path, AssetAttributes attributes, bool mipMaps, bool highQualityCompression,
			PVRFormat pvrFormat, SHA256 cookingUnitHash)
		{
			int width = bitmap.Width;
			int height = bitmap.Height;
			bool hasAlpha = bitmap.HasAlpha;
			int potWidth = TextureConverterUtils.GetNearestPowerOf2(width, 8, 2048);
			int potHeight = TextureConverterUtils.GetNearestPowerOf2(height, 8, 2048);
			var args = new StringBuilder();
			switch (pvrFormat) {
				case PVRFormat.PVRTC4:
					if (!hasAlpha) {
						args.Append(" -f PVRTC1_2");
					} else {
						args.Append(" -f PVRTC1_4");
					}
					width = height = Math.Max(potWidth, potHeight);
					break;
				case PVRFormat.PVRTC4_Forced:
					args.Append(" -f PVRTC1_4");
					width = height = Math.Max(potWidth, potHeight);
					break;
				case PVRFormat.PVRTC2:
					args.Append(" -f PVRTC1_2");
					width = height = Math.Max(potWidth, potHeight);
					break;
				case PVRFormat.ETC2:
					args.Append(" -f ETC1 -q etcfast");
					break;
				case PVRFormat.RGB565:
					if (hasAlpha) {
						Console.WriteLine("WARNING: texture has alpha channel. " +
						                  "Used 'RGBA4444' format instead of 'RGB565'.");
						args.Append(" -f r4g4b4a4 -dither");
					} else {
						args.Append(" -f r5g6b5");
					}
					break;
				case PVRFormat.RGBA4:
					args.Append(" -f r4g4b4a4 -dither");
					break;
				case PVRFormat.ARGB8:
					args.Append(" -f r8g8b8a8");
					break;
			}
			if (highQualityCompression && (new[] { PVRFormat.PVRTC2, PVRFormat.PVRTC4, PVRFormat.PVRTC4_Forced }.Contains(pvrFormat))) {
				args.Append(" -q pvrtcbest");
			}
			Bitmap bledBitmap = null;
			if (bitmap.HasAlpha) {
				bledBitmap = TextureConverterUtils.BleedAlpha(bitmap);
			}
			if (mipMaps) {
				args.Append(" -m");
			}
			args.AppendFormat(" -r {0},{1} -shh", width, height);
			var hashString = GetTextureHashString(bledBitmap ?? bitmap, ".pvr", args.ToString());
			var cachePath = AssetCache.Instance.Load(hashString);
			if (cachePath != null) {
				bundle.ImportFile(cachePath, path, cookingUnitHash, attributes);
				return;
			}
			var pvrPath = Toolbox.GetTempFilePathWithExtension(".pvr");
			var tgaPath = Path.ChangeExtension(pvrPath, ".tga");
			try {
				TextureConverterUtils.SaveToTGA(bledBitmap ?? bitmap, tgaPath, swapRedAndBlue: true);
				if (bledBitmap != null && bledBitmap != bitmap) {
					bledBitmap.Dispose();
				}
				args.AppendFormat(" -i \"{0}\" -o \"{1}\"", tgaPath, pvrPath);
#if MAC
				var pvrTexTool = GetToolPath("PVRTexTool");
#else
				var pvrTexTool = GetToolPath("PVRTexToolCli");
#endif
				if (Process.Start(pvrTexTool, args.ToString()) != 0) {
					throw new Lime.Exception($"PVRTextTool error\nCommand line: {pvrTexTool} {args}\"");
				}
				bundle.ImportFile(pvrPath, path, cookingUnitHash, attributes);
				AssetCache.Instance.Save(pvrPath, hashString);
			} finally {
				DeletePossibleLockedFile(tgaPath);
				DeletePossibleLockedFile(pvrPath);
			}
		}

		public static void RunNVCompress(Bitmap bitmap, AssetBundle bundle, string path, AssetAttributes attributes, DDSFormat format, bool mipMaps, SHA256 cookingUnitHash)
		{
			bool compressed = format == DDSFormat.DXTi;
			Bitmap bledBitmap = null;
			if (bitmap.HasAlpha) {
				bledBitmap = TextureConverterUtils.BleedAlpha(bitmap);
			}
			string mipsFlag = mipMaps ? string.Empty : "-nomips";
			string compressionMethod;
			if (compressed) {
				compressionMethod = bitmap.HasAlpha ? "-bc3" : "-bc1";
			} else {
#if WIN
				compressionMethod = "-rgb";
#else
				compressionMethod = "-rgb -rgbfmt bgra8";
#endif
			}
			var nvcompress = GetToolPath("nvcompress");

			string args = "{0} {1}".Format(mipsFlag, compressionMethod);
			var hashString = GetTextureHashString(bledBitmap ?? bitmap, ".dds", args);
			var cachePath = AssetCache.Instance.Load(hashString);
			if (cachePath != null) {
				bundle.ImportFile(cachePath, path, cookingUnitHash, attributes);
				return;
			}
			var ddsPath = Toolbox.GetTempFilePathWithExtension(".dds");
			var tgaPath = Path.ChangeExtension(ddsPath, ".tga");
			var srcPath = Path.Combine(Directory.GetCurrentDirectory(), tgaPath);
			var dstPath = Path.Combine(Directory.GetCurrentDirectory(), ddsPath);
			try {
				TextureConverterUtils.SaveToTGA(bledBitmap ?? bitmap, tgaPath, swapRedAndBlue: compressed);
				if (bledBitmap != null && bledBitmap != bitmap) {
					bledBitmap.Dispose();
				}
				args += $" \"{srcPath}\" \"{dstPath}\"";
				if (Process.Start(nvcompress, args.Format(mipsFlag, compressionMethod, srcPath, dstPath), options: Process.Options.RedirectErrors) != 0) {
					throw new Lime.Exception($"NVCompress error\nCommand line: {nvcompress} {args}\"");
				}
				bundle.ImportFile(ddsPath, path, cookingUnitHash, attributes);
				AssetCache.Instance.Save(ddsPath, hashString);
			} finally {
				DeletePossibleLockedFile(ddsPath);
				DeletePossibleLockedFile(tgaPath);
			}
		}

		// Invokes PngOptimizerCL on png with option not to optimize idat chunk.
		// It removes all unwanted chunks like itxt and text.
		public static void OptimizePNG(string dstPath)
		{
			var pngOptimizerPath = GetToolPath("PngOptimizerCL");
			dstPath = MakeAbsolutePath(dstPath);
			var args = $"--KeepPixels \"{dstPath}\"";
			if (Process.Start(pngOptimizerPath, args, options: Process.Options.RedirectErrors) != 0) {
				throw new Lime.Exception($"Error converting '{dstPath}'\nCommand line: {pngOptimizerPath} {args}");
			}
		}

		private static string MakeAbsolutePath(string path)
		{
			if (!Path.IsPathRooted(path)) {
				path = Path.Combine(Directory.GetCurrentDirectory(), path);
			}
			return path;
		}

		private static void DeletePossibleLockedFile(string file)
		{
			while (true) {
				try {
					File.Delete(file);
				}
				catch (System.Exception) {
					System.Threading.Thread.Sleep(100);
					continue;
				}
				break;
			}
		}

		private static string GetToolPath(string tool)
		{
			var appDirectory = Toolbox.GetApplicationDirectory();
#if MAC
			var toolChainDirectory = Path.Combine(appDirectory, "Toolchain.Mac");
			if (!Directory.Exists(toolChainDirectory)) {
				toolChainDirectory = appDirectory;
			}
#else
			var toolChainDirectory = Path.Combine(appDirectory, "Toolchain.Win");
#endif
			var toolPath = Path.Combine(toolChainDirectory, tool);
#if MAC
			Process.Start("/bin/chmod", $"+x {toolPath}");
#endif
			return toolPath;
		}

		private static readonly System.Security.Cryptography.SHA256 sha256 = System.Security.Cryptography.SHA256.Create();
		private static string GetTextureHashString(Bitmap bitmap, string extension, string commandLineArgs)
		{
			using var stream = new MemoryStream();
			bitmap.SaveTo(stream);
			var extensionBytes = Encoding.UTF8.GetBytes(extension);
			stream.Write(extensionBytes, 0, extensionBytes.Length);
			var commandLineArgsBytes = Encoding.UTF8.GetBytes(commandLineArgs);
			stream.Write(commandLineArgsBytes, 0, commandLineArgsBytes.Length);
			stream.Position = 0;
			return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLower();
		}
	}
}
