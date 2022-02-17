using System.Collections.Generic;
using Yuzu;

namespace Lime
{
	public enum AudioAction
	{
		Play,
		Stop,
		Pause,
	}

	[TangerineRegisterNode(Order = 3)]
	public class Audio : Node
	{
		public static bool GloballyEnable = true;
		private Sound sound = new Sound();

		[YuzuMember]
		[TangerineKeyframeColor(19)]
		public SerializableSample Sample { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(20)]
		public bool Looping { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(21)]
		[TangerineValidRange(0.0f, float.PositiveInfinity)]
		[TangerineDisplayName("Fade Out Time")]
		public float FadeTime { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(21)]
		[TangerineValidRange(0.0f, float.PositiveInfinity)]
		[TangerineDisplayName("Fade In Time")]
		public float FadeInTime { get; set; }

		private float volume = 0.5f;

		[YuzuMember]
		[TangerineKeyframeColor(22)]
		[TangerineValidRange(0.0f, 1.0f)]
		public float Volume
		{
			get => volume;
			set
			{
				volume = value;
				sound.Volume = volume * auxiliaryVolume;
			}
		}

		private float pan = 0;

		[YuzuMember]
		[TangerineKeyframeColor(23)]
		[TangerineValidRange(-1.0f, 1.0f)]
		public float Pan
		{
			get => pan;
			set
			{
				pan = value;
				sound.Pan = pan;
			}
		}

		private float pitch = 1;

		[YuzuMember]
		[TangerineKeyframeColor(24)]
		[TangerineValidRange(0.0625f, 16.0f)]
		public float Pitch
		{
			get => pitch;
			set
			{
				pitch = value;
				sound.Pitch = pitch * auxiliaryPitch;
			}
		}

		[Trigger]
		[TangerineKeyframeColor(15)]
		public AudioAction Action { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(25)]
		public AudioChannelGroup Group { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(26)]
		public float Priority { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(27)]
		public bool Continuous { get; set; }

		/// <summary>
		/// Mute all audio channels withing same <see cref="Group"/> when this audio is playing.
		/// Multiple exclusive audio mute and unmute each other in stack order.
		/// <see cref="FadeInTime"/> and <see cref="FadeTime"/> are used for transitions between exclusive audio.
		/// </summary>
		[YuzuMember]
		[TangerineStaticProperty]
		[TangerineTooltip(@"Mute all audio channels within same audio group when this audio is playing.
Multiple exclusive audio mute and unmute each other in stack order.
Use Fade In Time and Fade Out Time for transitions.")]
		public bool Exclusive { get; set; }

		private float auxiliaryVolume = 1f;

		public float AuxiliaryVolume
		{
			get => auxiliaryVolume;
			set
			{
				auxiliaryVolume = value;
				sound.Volume = volume * auxiliaryVolume;
			}
		}

		private float auxiliaryPitch = 1f;

		public float AuxiliaryPitch
		{
			get => auxiliaryPitch;
			set
			{
				auxiliaryPitch = value;
				sound.Pitch = pitch * auxiliaryPitch;
			}
		}

		public Audio()
		{
			RenderChainBuilder = null;
			Priority = 0.5f;
		}

		public virtual void Play()
		{
			if (Sample != null) {
				sound = Sample.Play(
					group: Group,
					paused: false,
					fadeInTime: FadeInTime,
					looping: Looping,
					priority: Priority,
					volume: Volume * AuxiliaryVolume,
					pan: Pan,
					pitch: Pitch * AuxiliaryPitch,
					exclusive: Exclusive,
					fadeOutTime: FadeTime
				);
				sound.StopChecker = ShouldStop;
			}
		}

		public virtual void Stop() => sound.Stop(FadeTime);

		public virtual void Pause() => sound.Pause(FadeTime);

		public virtual void Resume() => sound.Resume(FadeInTime);

		private bool ShouldStop() => !Continuous && (Manager == null || GloballyFrozen);

		public bool IsPlaying() => !sound.IsStopped && !sound.IsPaused;

		public override void AddToRenderChain(RenderChain chain)
		{
		}

		public override void OnTrigger(string property, object value, double animationTimeCorrection = 0)
		{
			base.OnTrigger(property, value, animationTimeCorrection);
			if (property == "Action") {
				var action = (AudioAction)value;
				if (GloballyEnable && !GetTangerineFlag(TangerineFlags.Hidden) && !GloballyFrozen) {
					switch (action) {
						case AudioAction.Play:
							if (sound.IsPaused) {
								Resume();
							} else {
								Play();
							}
							break;
						case AudioAction.Stop:
							Stop();
							break;
						case AudioAction.Pause:
							Pause();
							break;
					}
				}
			}
		}
	}

	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(Audio))]
	public class AudioRandomizerComponent : NodeBehavior
	{
		[YuzuMember]
		public AnimableList<SerializableSample> Samples { get; private set; }

		[YuzuMember]
		public NumericRange Pitch { get; set; } = new NumericRange(1, 0);

		[YuzuMember]
		public NumericRange Volume { get; set; } = new NumericRange(1, 0);

		private int previousSampleIndex;

		public AudioRandomizerComponent()
		{
			Samples = new AnimableList<SerializableSample> { Owner = this };
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			Samples.Owner = this;
			previousSampleIndex = Mathf.RandomInt(Samples.Count);
		}

		public override void OnTrigger(string property, object value, double animationTimeCorrection = 0)
		{
			if (Samples.Count > 0) {
				var audio = (Audio)Owner;
				audio.Sample = NextSample();
				audio.Pitch = Pitch.NormalRandomNumber();
				audio.Volume = Volume.NormalRandomNumber();
			}
		}

		private SerializableSample NextSample()
		{
			if (Samples.Count == 1) {
				return Samples[0];
			}
			int randomIndex = Mathf.RandomInt(Samples.Count - 1);
			if (randomIndex >= previousSampleIndex) {
				randomIndex++;
			}
			previousSampleIndex = randomIndex;
			return Samples[randomIndex];
		}
	}
}
