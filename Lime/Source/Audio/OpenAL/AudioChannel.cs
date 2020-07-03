#if OPENAL
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

#if iOS
using Lime.OpenALSoft;
#else
using OpenTK.Audio.OpenAL;
#endif

namespace Lime
{
	public class AudioChannel : IDisposable, IAudioChannelInternal, IAudioChannel
	{
		public const int BufferSize = 1024 * 32;
		public const int NumBuffers = 8;

		public AudioChannelGroup Group { get; set; }
		public float Priority;
		public DateTime StartupTime;
		public int Id;

		private readonly object streamingSync = new object();
		private volatile bool streaming;

		private int source;
		private float volume = 1;
		private float pitch = 1;
		private float pan = 0;
		private bool looping;
		private FadePurpose fadePurpose;
		private float fadeVolume;
		private float fadeSpeed;
		private List<int> allBuffers;
		private Stack<int> processedBuffers;

		private IAudioDecoder decoder;
		private readonly IntPtr decodedData;
		private AudioChannelState previousState = AudioChannelState.Invalid;
		internal Action<AudioChannel, AudioChannelState, AudioChannelState> OnStateChanged;
		private float delayTime;

		internal enum FadePurpose
		{
			None,
			Play,
			Stop,
			Pause,
			Suspend,
			Exclusive,
		}

		public bool Streaming { get { return streaming; } }

		public float Pitch
		{
			get => pitch;
			set => SetPitch(value);
		}

		public float Volume
		{
			get => volume;
			set => SetVolume(value);
		}

		public AudioChannelState State
		{
			get
			{
				switch (AL.GetSourceState(source)) {
					case ALSourceState.Initial:
						return AudioChannelState.Initial;
					case ALSourceState.Paused:
						return AudioChannelState.Paused;
					case ALSourceState.Playing:
						return AudioChannelState.Playing;
					case ALSourceState.Stopped:
					default:
						return AudioChannelState.Stopped;
				}
			}
		}

		public Sound Sound { get; private set; }

		public float Pan
		{
			get => pan;
			set => SetPan(value);
		}

		public string SamplePath { get; set; }
		public float FadeInTime { get; internal set; }
		public float FadeOutTime { get; internal set; }

		public AudioChannel(int index)
		{
			Sound = null;
			this.Id = index;
			decodedData = Marshal.AllocHGlobal(BufferSize);
			CreateOpenALResources();
		}

		public void CreateOpenALResources()
		{
			using (new PlatformAudioSystem.ErrorChecker()) {
				allBuffers = new List<int>();
				for (int i = 0; i < NumBuffers; i++) {
					allBuffers.Add(AL.GenBuffer());
				}
				source = AL.GenSource();
			}
			processedBuffers = new Stack<int>(allBuffers);
			SetPan(Pan);
			SetVolume(Volume);
			SetPitch(Pitch);
		}

		public void DisposeOpenALResources()
		{
			AL.SourceStop(source);
			AL.DeleteSource(source);
			foreach (var bid in allBuffers) {
				AL.DeleteBuffer(bid);
			}
		}

		public void Dispose()
		{
			if (!AudioSystem.Active) {
				return;
			}
			if (decoder != null) {
				decoder.Dispose();
			}
			DisposeOpenALResources();
			Marshal.FreeHGlobal(decodedData);
		}

		private void SetPan(float value)
		{
			if (!AudioSystem.Active) {
				return;
			}
			pan = value.Clamp(-1, 1);
			var sourcePosition = Vector2.CosSinRough(pan * Mathf.HalfPi);
			using (new PlatformAudioSystem.ErrorChecker()) {
				AL.Source(source, ALSource3f.Position, sourcePosition.Y, 0, sourcePosition.X);
			}
		}

		internal bool Play(Sound sound, IAudioDecoder decoder, bool looping, bool paused, float fadeInTime)
		{
			if (!AudioSystem.Active) {
				return false;
			}
			var state = AL.GetSourceState(source);
			if (state != ALSourceState.Initial && state != ALSourceState.Stopped) {
				// Don't know why is it happens, but it's better to warn than crash the game
				Debug.Write("AudioSource must be stopped before play");
				return false;
			}
			lock (streamingSync) {
				if (streaming) {
					throw new Lime.Exception("Can't play on channel because it is in use");
				}
				this.looping = looping;
				this.decoder?.Dispose();
				this.decoder = decoder;
			}
			DetachBuffers();
			if (Sound != null) {
				Sound.ChannelInternal = NullAudioChannel.Instance;
			}
			this.Sound = sound;
			sound.ChannelInternal = this;
			StartupTime = DateTime.Now;
			if (!paused) {
				Resume(fadeInTime);
			}
			return true;
		}

		private void DetachBuffers()
		{
			using (new PlatformAudioSystem.ErrorChecker(throwException: false)) {
				AL.Source(source, ALSourcei.Buffer, 0);
			}
			processedBuffers = new Stack<int>(allBuffers);
		}

		public void Resume(float fadeInTime = 0)
		{
			FadeInTime = fadeInTime;
			if (!AudioSystem.Active) {
				return;
			}
			if (decoder == null) {
				throw new InvalidOperationException("Audio decoder is not set");
			}
			delayTime = PlatformAudioSystem.GetDelayBeforePlayOrResume(this);
			if (delayTime == 0.0f) {
				ResumeWithoutDelay();
			}
		}

		private void ResumeWithoutDelay()
		{
			FadeIn(FadePurpose.Play);
			Volume = volume;
			PlayImmediate();
		}

		private void PlayImmediate()
		{
			streaming = true;
			using (new PlatformAudioSystem.ErrorChecker()) {
				AL.SourcePlay(source);
			}
		}

		public void Pause(float fadeOutTime = 0)
		{
			FadeOutTime = fadeOutTime;
			if (!AudioSystem.Active) {
				return;
			}
			if (!FadeOut(FadePurpose.Pause)) {
				PauseImmediate();
			}
			Volume = volume;
		}

		private void PauseImmediate()
		{
			using (new PlatformAudioSystem.ErrorChecker()) {
				AL.SourcePause(source);
			}
		}

		public void Stop(float fadeOutTime = 0)
		{
			FadeOutTime = fadeOutTime;
			if (!AudioSystem.Active) {
				return;
			}
			if (!FadeOut(FadePurpose.Stop)) {
				StopImmediate();
			}
			Volume = volume;
		}

		private void StopImmediate()
		{
			lock (streamingSync) {
				streaming = false;
				using (new PlatformAudioSystem.ErrorChecker(throwException: false)) {
					AL.SourceStop(source);
				}
			}
		}

		public PlayParameters Suspend(float fadeOutTime = 0)
		{
			FadeOutTime = fadeOutTime;
			if (!AudioSystem.Active) {
				return null;
			}
			if (!FadeOut(FadePurpose.Suspend)) {
				SuspendImmediate();
			}
			Volume = volume;
			return new PlayParameters {
				Decoder = decoder,
				Group = Group,
				Pan = pan,
				Volume = volume,
				Pitch = pitch,
				Path = SamplePath,
				Priority = Priority,
				Looping = looping,
			};
		}

		internal bool FadeIn(FadePurpose purpose)
		{
			if (FadeInTime > 0) {
				fadeVolume = 0;
				fadeSpeed = 1 / FadeInTime;
				fadePurpose = purpose;
				return true;
			} else {
				fadeSpeed = 0;
				fadeVolume = 1;
				fadePurpose = FadePurpose.None;
				return false;
			}
		}

		internal bool FadeOut(FadePurpose purpose)
		{
			if (FadeOutTime > 0) {
				fadeSpeed = -1 / FadeOutTime;
				fadePurpose = purpose;
				return true;
			} else {
				fadeSpeed = 0;
				fadeVolume = 0;
				fadePurpose = FadePurpose.None;
				return false;
			}
		}

		private void SuspendImmediate()
		{
			StopImmediate();
			// Set to null to prevent disposing the decoder on next Play().
			decoder = null;
		}

		private void SetPitch(float value)
		{
			if (!AudioSystem.Active) {
				return;
			}
			pitch = Mathf.Clamp(value, 0.0625f, 16);
			using (new PlatformAudioSystem.ErrorChecker()) {
				AL.Source(source, ALSourcef.Pitch, pitch);
			}
		}

		private void SetVolume(float value)
		{
			if (!AudioSystem.Active) {
				return;
			}
			volume = Mathf.Clamp(value, 0, 1);
			float gain = volume * fadeVolume * AudioSystem.GetGroupVolume(Group);
			using (new PlatformAudioSystem.ErrorChecker()) {
				AL.Source(source, ALSourcef.Gain, gain);
			}
		}

		public void Update(float delta)
		{
			if (!AudioSystem.Active) {
				return;
			}
			var state = State;
			if (previousState != state) {
				OnStateChanged?.Invoke(this, previousState, state);
				previousState = state;
			}
			if (streaming) {
				lock (streamingSync) {
					if (streaming) {
						QueueBuffers();
					}
				}
			}
			if (delayTime > 0.0f) {
				delayTime -= delta;
				if (delayTime <= 0.0f) {
					delayTime = 0.0f;
					ResumeWithoutDelay();
				}
			}
			if (fadePurpose != FadePurpose.None) {
				if (fadeSpeed != 0) {
					fadeVolume += delta * fadeSpeed;
					if (fadeVolume > 1 || fadeVolume < 0) {
						fadeSpeed = 0;
						fadeVolume = Mathf.Clamp(fadeVolume, 0, 1);
					}
					Volume = volume;
				}
				if (fadeSpeed == 0) {
					FadeFinished();
				}
			}
			if (streaming && (Sound?.StopChecker?.Invoke() ?? false)) {
				Stop(0.1f);
			}
		}

		private void FadeFinished()
		{
			switch (fadePurpose) {
				case FadePurpose.Play:
					break;
				case FadePurpose.Stop:
					StopImmediate();
					break;
				case FadePurpose.Pause:
					PauseImmediate();
					break;
				case FadePurpose.Suspend:
					SuspendImmediate();
					break;
				case FadePurpose.Exclusive:
					break;
			}
			fadePurpose = FadePurpose.None;
		}

		private void QueueBuffers()
		{
			if (decoder == null) {
				throw new InvalidOperationException("Audio decoder is not set");
			}
			UnqueueProcessedBuffers();
			bool addedbuffers = false;
			while (QueueOneBuffer()) {
				addedbuffers = true;
			}
			if (addedbuffers) {
				if (State == AudioChannelState.Stopped || State == AudioChannelState.Initial) {
					AL.SourcePlay(source);
				}
			}
		}

		private bool QueueOneBuffer()
		{
			int buffer = AcquireBuffer();
			if (buffer != 0) {
				if (FillBuffer(buffer)) {
					AL.SourceQueueBuffer(source, buffer);
					return true;
				} else {
					processedBuffers.Push(buffer);
					streaming = false;
				}
			}
			return false;
		}

		private bool FillBuffer(int buffer)
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
				ALFormat format = (decoder.GetFormat() == AudioFormat.Stereo16) ? ALFormat.Stereo16 : ALFormat.Mono16;
				int dataSize = totalRead * decoder.GetBlockSize();
				AL.BufferData(buffer, format, decodedData, dataSize, decoder.GetFrequency());
				return true;
			}
			return false;
		}

		private void UnqueueProcessedBuffers()
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

		private int AcquireBuffer()
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
}
#endif
