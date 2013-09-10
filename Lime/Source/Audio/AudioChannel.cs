using System;
using System.Collections.Generic;
#if OPENAL
using OpenTK.Audio.OpenAL;
#endif
using System.Runtime.InteropServices;

#if !OPENAL
public enum ALSourceState
{
	Initial,
	Playing,
	Stopped
}
#endif

namespace Lime
{
	public interface IAudioChannel
	{
		bool Streaming { get; }
		ALSourceState State { get; }
		float Pan { get; set; }
		void Resume(float fadeinTime = 0);
		void Stop(float fadeoutTime = 0);
		float Volume { get; set; }
		float Pitch { get; set; }
		string SamplePath { get; set; }
		Sound Sound { get; }
		void Bump();
	}

	public class NullAudioChannel : IAudioChannel
	{
		public static NullAudioChannel Instance = new NullAudioChannel();

		public ALSourceState State { get { return ALSourceState.Stopped; } }
		public bool Streaming { get { return false; } }
		public float Pan { get { return 0; } set { } }
		public void Resume(float fadeinTime = 0) {}
		public void Stop(float fadeoutTime = 0) {}
		public float Volume { get { return 0; } set { } }
		public float Pitch { get { return 1; } set { } }
		public void Bump() {}
		public string SamplePath { get; set; }
		public Sound Sound { get { return null; } }
	}

#if !OPENAL
	internal class AudioChannel : NullAudioChannel, IDisposable
	{
		public AudioChannelGroup Group;
		public float Priority;
		public DateTime StartupTime = DateTime.Now;
		public int Id;

		public AudioChannel(int index)
		{
			Id = index;
		}

		public void Update(float delta)
		{
		}

		public Sound Play(IAudioDecoder decoder, bool looping)
		{
			return new Sound();
		}

		public void Pause()
		{
		}

		public void Dispose()
		{
		}
	}

#else
	internal class AudioChannel : IDisposable, IAudioChannel
	{
		public const int BufferSize = 1024 * 32;
		public const int NumBuffers = 8;

		public AudioChannelGroup Group;
		public float Priority;
		public DateTime StartupTime;
		public int Id;

		private object streamingSync = new object();
		private volatile bool streaming;

		private int source;
		private float volume = 1;
		private float pitch = 1;
		private bool looping;
		private float fadeVolume;
		private float fadeSpeed;
		private int lastBumpedRenderCycle;
		private List<int> allBuffers;
		private Stack<int> processedBuffers;

		private Sound sound = null;
		private IAudioDecoder decoder;
		private IntPtr decodedData;

		// The channel can be locked while a sound is being preloaded
		public bool Locked { get; set; }

		public bool Streaming { get { return streaming; } }

		public float Pitch
		{
			get { return pitch; }
			set { SetPitch(value); }
		}

		public float Volume
		{
			get { return volume; }
			set { SetVolume(value); }
		}

		public Sound Sound { get { return sound; } }

		// Not implemented yet
		public float Pan { get; set; }

		public string SamplePath { get; set; }

		public AudioChannel(int index)
		{
			this.Id = index;
			decodedData = Marshal.AllocHGlobal(BufferSize);
			using (new AudioSystem.ErrorChecker()) {
				allBuffers = new List<int>(AL.GenBuffers(NumBuffers));
				source = AL.GenSource();
			}
			processedBuffers = new Stack<int>(allBuffers);
		}
		
		public void Dispose()
		{
			if (decoder != null) {
				decoder.Dispose();
			}
			AL.SourceStop(source);
			AL.DeleteSource(source);
			AL.DeleteBuffers(allBuffers.ToArray());
			Marshal.FreeHGlobal(decodedData);
		}

		public ALSourceState State { 
			get {
				return AL.GetSourceState(source); 
			}
		}

		internal void Play(Sound sound, IAudioDecoder decoder, bool looping, bool paused, float fadeinTime)
		{
			var state = AL.GetSourceState(source);
			if (state != ALSourceState.Initial && state != ALSourceState.Stopped) {
				throw new Lime.Exception("AudioSource must be stopped before play");
			}
			lock (streamingSync) {
				if (streaming) {
					throw new Lime.Exception("Can't play on the channel because it is already being played");
				}
				this.looping = looping;
				if (this.decoder != null) {
					this.decoder.Dispose();
				}
				this.decoder = decoder;
			}
			DetachBuffers();
			if (Sound != null) {
				Sound.Channel = NullAudioChannel.Instance;
			}
			this.sound = sound;
			sound.Channel = this;
			StartupTime = DateTime.Now;
			if (!paused) {
				Resume(fadeinTime);
			}
		}

		private void DetachBuffers()
		{
			using (new AudioSystem.ErrorChecker(throwException: false)) {
				AL.Source(source, ALSourcei.Buffer, 0);
			}
			processedBuffers = new Stack<int>(allBuffers);
		}

		public void Resume(float fadeinTime = 0)
		{
			if (decoder == null) {
				throw new InvalidOperationException("Can't resume sound before it has decoded");
			}
			if (fadeinTime > 0) {
				fadeVolume = 0;
				fadeSpeed = 1 / fadeinTime;
			} else {
				fadeSpeed = 0;
				fadeVolume = 1;
			}
			Volume = volume;
			streaming = true;
			if (State == ALSourceState.Paused) {
				using (new AudioSystem.ErrorChecker()) {
					AL.SourcePlay(source);
				}
			}
		}

		public void Pause()
		{
			using (new AudioSystem.ErrorChecker()) {
				AL.SourcePause(source);
			}
		}

		public void Stop(float fadeoutTime = 0)
		{
			if (fadeoutTime > 0) {
				// fadeVolume = 1;
				fadeSpeed = -1 / fadeoutTime;
				return;
			} else {
				fadeSpeed = 0;
				fadeVolume = 0;
			}
			lock (streamingSync) {
				streaming = false;
				using (new AudioSystem.ErrorChecker(throwException: false)) {
					AL.SourceStop(source);
				}
			}
		}

		private void SetPitch(float value)
		{
			pitch = Mathf.Clamp(value, 0.0625f, 16);
			using (new AudioSystem.ErrorChecker()) {
				AL.Source(source, ALSourcef.Pitch, pitch);
			}
		}

		private void SetVolume(float value)
		{
			volume = Mathf.Clamp(value, 0, 1);
			float gain = volume * AudioSystem.GetGroupVolume(Group) * fadeVolume;
			using (new AudioSystem.ErrorChecker()) {
				AL.Source(source, ALSourcef.Gain, gain);
			}
		}

		public void Bump()
		{
			lastBumpedRenderCycle = Renderer.RenderCycle;
		}

		public void Update(float delta)
		{
			if (streaming) {
				lock (streamingSync) {
					if (streaming) {
						QueueBuffer(resumePlay: true);
					}
				}
			}
			if (fadeSpeed != 0) {
				fadeVolume += delta * fadeSpeed;
				if (fadeVolume > 1) {
					fadeSpeed = 0;
					fadeVolume = 1;
				} else if (fadeVolume < 0) {
					fadeSpeed = 0;
					fadeVolume = 0;
					Stop();
				}
				Volume = volume;
			}
			if (sound != null && sound.IsBumpable && Renderer.RenderCycle - lastBumpedRenderCycle > 3) {
				Stop(0.1f);
			}
		}

		void QueueBuffer(bool resumePlay)
		{
			if (decoder == null) {
				throw new InvalidOperationException("AudioChannel is streaming while decoder is not set");
			}
			UnqueueProcessedBuffers();
			// queue one buffer
			int buffer = AcquireBuffer();
			if (buffer != 0) {
				if (FillBuffer(buffer)) {
					using (new AudioSystem.ErrorChecker()) {
						AL.SourceQueueBuffer(source, buffer);
					}
				} else {
					processedBuffers.Push(buffer);
					streaming = false;
				}
			}
			if (resumePlay) {
				switch (State) {
					case ALSourceState.Stopped:
					case ALSourceState.Initial:
						AL.SourcePlay(source);
						break;
				}
			}
		}

		bool FillBuffer(int buffer)
		{
			int totalRead = 0;
			int needToRead = BufferSize / decoder.GetBlockSize();
			while (true) {
				int actuallyRead = decoder.ReadBlocks(decodedData, totalRead, needToRead - totalRead);
				totalRead += actuallyRead;
				if (totalRead == needToRead || !looping) {
					break;
				}
				decoder.Rewind();
			}
			if (totalRead > 0) {
				using (new AudioSystem.ErrorChecker(throwException: false)) {
					ALFormat format = (decoder.GetFormat() == AudioFormat.Stereo16) ? ALFormat.Stereo16 : ALFormat.Mono16;
					AL.BufferData(buffer, format, decodedData,
						totalRead * decoder.GetBlockSize(), decoder.GetFrequency());
				}
				return true;
			}
			return false;
		}

		void UnqueueProcessedBuffers()
		{
			AL.GetError();
			int numProcessed;
			AL.GetSource(source, ALGetSourcei.BuffersProcessed, out numProcessed);
			for (int i = 0; i < numProcessed; i++) {
				int buffer = AL.SourceUnqueueBuffer(source);
				if (buffer != 0) {
					processedBuffers.Push(buffer);
				}
			}
		}
		
		int AcquireBuffer()
		{
			int c = processedBuffers.Count;
			if (c == 0) {
				return 0;
			} else {
				var buffer = processedBuffers.Pop();
				return buffer;
			}
		}
	}
#endif
}