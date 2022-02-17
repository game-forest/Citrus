using System;
using System.IO;

namespace Lime
{
	public class MovieTexture : Texture2D
	{
		private Stream rgbStream;
		private Stream alphaStream;
		private OgvDecoder rgbDecoder;
		private OgvDecoder alphaDecoder;
		private Color4[] pixels;
		private double gameTime;
		private double videoTime;
		private string path;

		public bool Looped { get; set; }
		public bool Paused { get; private set; }
		public bool Stopped { get; private set; }
		public string Path { get { return path; } set { SetPath(value); } }

		public MovieTexture()
		{
			Stopped = true;
		}

		private void SetPath(string value)
		{
			path = value;
			Stop();
		}

		private void Open()
		{
			if (Path == null) {
				throw new ArgumentException();
			}
			Stop();
			if (TryLoadFile(Path + ".ogv", out rgbStream)) {
				rgbDecoder = new OgvDecoder(rgbStream);
			}
			if (
				TryLoadFile(Path + "_alpha.ogv", out alphaStream)
				|| TryLoadFile(Path + "_Alpha.ogv", out alphaStream)
			) {
				alphaDecoder = new OgvDecoder(alphaStream);
			}
			this.ImageSize = rgbDecoder.FrameSize;
			this.SurfaceSize = ImageSize;
			pixels = new Color4[ImageSize.Width * ImageSize.Height];
		}

		private bool TryLoadFile(string path, out Stream result)
		{
			if (!AssetBundle.Current.FileExists(path)) {
				result = null;
				return false;
			}
			using var stream = AssetBundle.Current.OpenFile(path);
			result = new MemoryStream((int)stream.Length);
			stream.CopyTo(result);
			result.Position = 0;
			return true;
		}

		public void Play()
		{
			if (Stopped) {
				Open();
				Stopped = false;
			}
			if (Paused) {
				Paused = false;
			}
		}

		public void Pause(bool value)
		{
			Paused = value;
		}

		public override void Dispose()
		{
			Stop();
			base.Dispose();
		}

		public void Stop()
		{
			Stopped = true;
			videoTime = 0;
			gameTime = 0;
			if (rgbDecoder != null) {
				rgbDecoder.Dispose();
				rgbDecoder = null;
			}
			if (rgbStream != null) {
				rgbStream.Dispose();
				rgbStream = null;
			}
			if (alphaDecoder != null) {
				alphaDecoder.Dispose();
				alphaDecoder = null;
			}
			if (alphaStream != null) {
				alphaStream.Dispose();
				alphaStream = null;
			}
		}

		public void Update(float delta)
		{
			if (Paused || Stopped) {
				return;
			}
			gameTime += delta;
			if (videoTime > gameTime) {
				return;
			}

			while (true) {
				if (!rgbDecoder.DecodeFrame()) {
					if (Looped) {
						Restart();
						rgbDecoder.DecodeFrame();
					} else {
						Stop();
						return;
					}
				}
				if (alphaDecoder != null) {
					alphaDecoder.DecodeFrame();
				}
				videoTime = rgbDecoder.GetPlaybackTime();
				if (videoTime >= gameTime) {
					break;
				}
			}
			rgbDecoder.FillTextureRGBX8(pixels, ImageSize.Width, ImageSize.Height);
			if (alphaDecoder != null) {
				alphaDecoder.FillTextureAlpha(pixels, ImageSize.Width, ImageSize.Height);
			}
			LoadImage(pixels, ImageSize.Width, ImageSize.Height);
		}

		public void Restart()
		{
			Stop();
			Play();
		}
	}
}
