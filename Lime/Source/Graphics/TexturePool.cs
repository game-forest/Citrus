using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections.Concurrent;

namespace Lime
{
	public sealed class TexturePool
	{

		public static event Texture2D.TextureMissingDelegate TextureMissing;

		private readonly Dictionary<string, WeakReference> textures = new Dictionary<string, WeakReference>();

		public readonly static TexturePool Instance = new TexturePool();

		public delegate void TextureCreatingDelegate(string path);
		public delegate void TextureCreatedDelegate(string path, ITexture texture);

		public event TextureCreatingDelegate TextureCreating;
		public event TextureCreatedDelegate TextureCreated;

		private TexturePool() {}

		[Obsolete("Use DiscardTexturesUnderPressure()")]
		public void DiscardUnusedTextures(int numCycles)
		{
			DiscardTexturesUnderPressure();
		}

		public void DiscardTexturesUnderPressure()
		{
			lock (textures) {
				foreach (WeakReference r in textures.Values.ToList()) {
					var texture = r.Target as ITexture;
					if (texture != null && !texture.IsDisposed) {
						texture.MaybeDiscardUnderPressure();
					}
				}
			}
		}

		public void DiscardAllTextures()
		{
			lock (textures) {
				foreach (WeakReference r in textures.Values.ToList()) {
					var texture = r.Target as ITexture;
					if (texture != null && !texture.IsDisposed) {
						texture.Dispose();
					}
				}
			}
		}

		public ITexture GetTexture(string path)
		{
			lock (textures) {
				if (path == null) {
					path = string.Empty;
				}
				if (path.StartsWith("#")) { // It's supposed render target texture
					path = path.ToLower();
				}
				ITexture texture;
				if (textures.TryGetValue(path, out var weakReference)) {
					texture = weakReference.Target as ITexture;
					if (texture != null && !texture.IsDisposed) {
						return texture;
					}
				}
				TextureCreating?.Invoke(path);
				texture = CreateTexture(path);
				textures[path] = new WeakReference(texture);
				TextureCreated?.Invoke(path, texture);
				return texture;
			}
		}

		private static ITexture CreateTexture(string path)
		{
			ITexture texture;
			
			if (string.IsNullOrEmpty(path)) {
				texture = new Texture2D();
				((Texture2D) texture).LoadStubImage(false);
				return texture;
			}
			
			texture = TryCreateRenderTarget(path) ?? TryLoadTextureAtlasPart(path + ".atlasPart");
			
			if (texture == null) {
				texture = new Texture2D();
				((Texture2D) texture).LoadImage(path, TextureMissing);
			}
			
			return texture;
		}

		private static ITexture TryCreateRenderTarget(string path)
		{
			if (path.Length != 2 || path[0] != '#') {
				return null;
			}
			switch (path[1]) {
				case 'a':
				case 'b':
					return new RenderTexture(256, 256);
				case 'c':
					return new RenderTexture(512, 512);
				case 'd':
				case 'e':
				case 'f':
				case 'g':
					return new RenderTexture(1024, 1024);
				default:
					return null;
			}
		}

		private static ITexture TryLoadTextureAtlasPart(string path)
		{
			if (!AssetBundle.Current.FileExists(path)) {
				return null;
			}
			var data = InternalPersistence.Instance.ReadObject<TextureAtlasElement.Params>(path);
			var texture = new TextureAtlasElement(data);
			return texture;
		}
	}
}
