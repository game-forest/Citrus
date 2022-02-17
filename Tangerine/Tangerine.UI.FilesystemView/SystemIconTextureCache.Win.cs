#if WIN
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Lime;
using Bitmap = System.Drawing.Bitmap;

namespace Tangerine.UI.FilesystemView
{
	public class SystemIconTextureProvider : ISystemIconTextureProvider
	{
		private static readonly Dictionary<string, ITexture> textureCache = new Dictionary<string, ITexture>();
		private static ITexture directoryTexture;
		public static SystemIconTextureProvider Instance { get; set; } = new SystemIconTextureProvider();

		public ITexture GetTexture(string path)
		{
			if (path.IsNullOrWhiteSpace()) {
				return TexturePool.Instance.GetTexture(null);
			}
			FileAttributes attr;
			try {
				attr = File.GetAttributes(path);
			} catch (System.Exception) {
				return TexturePool.Instance.GetTexture(null);
			}
			bool isDirectory = (attr & FileAttributes.Directory) == FileAttributes.Directory;
			var isRoot = false;
			if (isDirectory) {
				if (new DirectoryInfo(path).Parent == null) {
					isRoot = true;
				}
			}
			if (
				isDirectory &&
				!isRoot &&
				directoryTexture != null
			) {
				return directoryTexture;
			}
			var cacheKey = Path.GetExtension(path);
			if (string.Equals(cacheKey, ".exe", StringComparison.OrdinalIgnoreCase)) {
				cacheKey = path;
			}
			if (textureCache.ContainsKey(cacheKey)) {
				return textureCache[cacheKey];
			}
			var shInfo = new WinAPI.SHFILEINFO();
			IntPtr r = WinAPI.SHGetFileInfo(
				pszPath: path,
				dwFileAttribs: 0,
				psfi: out shInfo,
				cbFileInfo: (uint)Marshal.SizeOf(shInfo),
				uFlags: WinAPI.SHGFI.SHGFI_ICON | WinAPI.SHGFI.SHGFI_SMALLICON
			);
			if (r == IntPtr.Zero) {
				return TexturePool.Instance.GetTexture(null);
			}
			var t = new Texture2D();
			using (var icon = System.Drawing.Icon.FromHandle(shInfo.hIcon)) {
				var b = new Bitmap(icon.Size.Width, icon.Size.Height);
				using (Graphics g = Graphics.FromImage(b)) {
					g.DrawIcon(icon, 0, 0);
				}

				using (var s = new MemoryStream()) {
					b.Save(s, ImageFormat.Png);
					t.LoadImage(s);
				}
			}
			WinAPI.DestroyIcon(shInfo.hIcon);
			if (isDirectory && !isRoot) {
				directoryTexture = t;
			} else {
				textureCache.Add(cacheKey, t);
			}
			return t;
		}
	}
}
#endif
