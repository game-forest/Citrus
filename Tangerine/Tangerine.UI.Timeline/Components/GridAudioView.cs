using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Components
{
	public class GridAudioView : GridNodeView
	{
		private readonly Audio audio;

		public GridAudioView(Audio audio) : base(audio)
		{
			this.audio = audio;
		}

		protected override void Render(Widget widget)
		{
			base.Render(widget);
			Animator<AudioAction> actionAnimator;
			if (!audio.Animators.TryFind(nameof(Audio.Action), out actionAnimator, Document.Current.AnimationId)) {
				return;
			}
			foreach (var key in actionAnimator.ReadonlyKeys) {
				if (key.Value == AudioAction.Play) {
					var sample = GetSampleAtFrame(key.Frame);
					if (sample == null || sample.Path == null) {
						continue;
					}
					var waveform = Timeline.Instance.WaveformCache.GetWaveform(sample.Path);
					var pos = new Vector2(key.Frame * TimelineMetrics.ColWidth + 1, 0);
					foreach (var p in waveform.Parts) {
						var size = new Vector2(
							p.Width * TimelineMetrics.ColWidth / WaveformCache.PixelsPerFrame,
							widget.Height
						);
						Renderer.DrawRect(pos, pos + size, ColorTheme.Current.TimelineGrid.WaveformBackground);
						Renderer.DrawSprite(
							texture1: p.Texture,
							color: ColorTheme.Current.TimelineGrid.WaveformColor,
							position: pos,
							size: size,
							uv0: Vector2.Zero,
							uv1: Vector2.One
						);
						pos.X += size.X;
					}
				}
			}
		}

		private SerializableSample GetSampleAtFrame(int frame)
		{
			Animator<SerializableSample> sampleAnimator;
			if (!audio.Animators.TryFind(nameof(Audio.Sample), out sampleAnimator, Document.Current.AnimationId)) {
				return audio.Sample;
			}
			var keys = sampleAnimator.ReadonlyKeys;
			var sample = keys.Count > 0 ? keys[0].Value : audio.Sample;
			foreach (var key in keys) {
				if (key.Frame <= frame) {
					sample = key.Value;
				}
			}
			return sample;
		}
	}

	public class Waveform
	{
		public struct Part
		{
			public int Width;
			public ITexture Texture;
		}

		public readonly List<Part> Parts = new List<Part>();
	}

	public class WaveformCache
	{
		public const int PixelsPerFrame = 30;

		private readonly Dictionary<string, Waveform> waveforms = new Dictionary<string, Waveform>();

		public WaveformCache(IFileSystemWatcher fsWatcher)
		{
			fsWatcher.Changed += HandleFileSystemEvent;
			fsWatcher.Deleted += HandleFileSystemEvent;
			fsWatcher.Renamed += (oldName, _) => HandleFileSystemEvent(oldName);
		}

		private void HandleFileSystemEvent(string path)
		{
			if (path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) {
				Purge();
			}
		}

		public void Purge()
		{
			waveforms.Clear();
		}

		public unsafe Waveform GetWaveform(string path)
		{
			Waveform waveform;
			if (waveforms.TryGetValue(path, out waveform)) {
				return waveform;
			}
			waveform = new Waveform();
			var textureWidth = 256;
			var textureHeight = 64;
			try {
				using (var fs = AssetBundle.Current.OpenFile(path + ".ogg")) {
					using (var decoder = AudioDecoderFactory.CreateDecoder(fs)) {
						var stereo = decoder.GetFormat() == AudioFormat.Stereo16;
						while (true) {
							var maxPartLength = textureWidth / (AnimationUtils.FramesPerSecond * PixelsPerFrame);
							var maxPartSamples = (int)(maxPartLength * decoder.GetFrequency());
							var maxBlocks = maxPartSamples / decoder.GetBlockSize() * (stereo ? 4 : 2);
							var samples = Marshal.AllocHGlobal(maxBlocks * decoder.GetBlockSize());
							try {
								var numBlocks = decoder.ReadBlocks(samples, 0, maxBlocks);
								if (numBlocks == 0) {
									break;
								}
								var numSamples = numBlocks * decoder.GetBlockSize() / (stereo ? 4 : 2);
								var pixels = new Color4[textureWidth * textureHeight];
								int width = numSamples * textureWidth / maxPartSamples;
								if (stereo) {
									BuildMonoWaveform(
										samples: (short*)samples,
										stride: 2,
										numSamples: numSamples,
										pixels: pixels,
										textureWidth: textureWidth,
										width: width,
										top: 0,
										bottom: textureHeight / 2 - 1
									);
									BuildMonoWaveform(
										samples: (short*)samples + 1,
										stride: 2,
										numSamples: numSamples,
										pixels: pixels,
										textureWidth: textureWidth,
										width: width,
										top: textureHeight / 2 + 1,
										bottom: textureHeight - 1
									);
								} else {
									BuildMonoWaveform(
										samples: (short*)samples,
										stride: 1,
										numSamples: numSamples,
										pixels: pixels,
										textureWidth: textureWidth,
										width: width,
										top: 0,
										bottom: textureHeight - 1
									);
								}
								var texture = new Texture2D();
								texture.LoadImage(pixels, textureWidth, textureHeight);
								waveform.Parts.Add(new Waveform.Part { Texture = texture, Width = width });
							} finally {
								Marshal.FreeHGlobal(samples);
							}
						}
					}
				}
			} catch {
				var texture = new Texture2D();
				texture.LoadImage(new Color4[1], 1, 1);
				waveform.Parts.Add(new Waveform.Part { Texture = texture, Width = 1 });
			}
			waveforms.Add(path, waveform);
			return waveform;
		}

		private static unsafe void BuildMonoWaveform(
			short* samples,
			int stride,
			int numSamples,
			Color4[] pixels,
			int textureWidth,
			int width,
			int top,
			int bottom
		) {
			int currentSample = 0;
			int accumulator = 0;
			for (int i = 0; i < width && currentSample < numSamples; i++) {
				int rangeMin = short.MaxValue;
				int rangeMax = short.MinValue;
				if (width > numSamples) {
					throw new NotImplementedException("Magnified waveform isn't supported yet");
				} else {
					while (accumulator < numSamples && currentSample < numSamples) {
						int s = *samples;
						samples += stride;
						currentSample++;
						if (s < rangeMin) {
							rangeMin = s;
						}

						if (s > rangeMax) {
							rangeMax = s;
						}

						accumulator += width;
					}
					accumulator -= numSamples;
				}
				int a = (top + bottom) / 2 + (bottom - top) * Math.Max(rangeMin, -32767) / 65536;
				int b = (top + bottom) / 2 + (bottom - top) * Math.Min(rangeMax, 32767) / 65536;
				for (int j = a; j <= b; j++) {
					pixels[i + j * textureWidth] = Color4.White;
				}
			}
		}
	}
}
