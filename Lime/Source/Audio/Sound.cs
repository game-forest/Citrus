#if OPENAL
#if !MONOMAC
using OpenTK.Audio.OpenAL;
#else
using MonoMac.OpenAL;
#endif
#endif

namespace Lime
{
	public class Sound
	{
		public Sound()
		{
			ChannelInternal = NullAudioChannel.Instance;
		}

		public IAudioChannel Channel => (IAudioChannel)ChannelInternal;

		internal IAudioChannelInternal ChannelInternal { get; set; }

		public System.Func<bool> StopChecker;

		public bool IsLoading { get; internal set; }

		public bool IsStopped => ChannelInternal.State == AudioChannelState.Stopped;

		public bool IsPaused => ChannelInternal.State == AudioChannelState.Paused;

		public float Volume
		{
			get => ChannelInternal.Volume;
			set => ChannelInternal.Volume = value;
		}

		public float Pitch
		{
			get => ChannelInternal.Pitch;
			set => ChannelInternal.Pitch = value;
		}

		public float Pan
		{
			get => ChannelInternal.Pan;
			set => ChannelInternal.Pan = value;
		}

		public void Resume(float fadeInTime = 0)
		{
			EnsureLoaded();
			ChannelInternal.Resume(fadeInTime);
		}

		public void Stop(float fadeOutTime = 0)
		{
			EnsureLoaded();
			ChannelInternal.Stop(fadeOutTime);
		}

		public void Pause(float fadeOutTime = 0)
		{
			EnsureLoaded();
			ChannelInternal.Pause(fadeOutTime);
		}

		public PlayParameters Suspend(float fadeOutTime = 0)
		{
			EnsureLoaded();
			return ChannelInternal.Suspend(fadeOutTime);
		}

		private void EnsureLoaded()
		{
			if (IsLoading) {
				throw new System.InvalidOperationException("The sound is being loaded");
			}
		}
	}
}
