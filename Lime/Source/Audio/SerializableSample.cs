using Yuzu;

namespace Lime
{
	public class SerializableSample
	{
		public string Path;

		public SerializableSample() {}

		public SerializableSample(string path)
		{
			SerializationPath = path;
		}

		[YuzuMember]
		public string SerializationPath
		{
			get => InternalPersistence.Current?.ShrinkPath(Path) ?? Path;
			set => Path = InternalPersistence.Current?.ExpandPath(value) ?? value;
		}

		public Sound Play(
			AudioChannelGroup group,
			bool paused,
			float fadeInTime = 0,
			bool looping = false,
			float priority = 0.5f,
			float volume = 1,
			float pan = 0,
			float pitch = 1,
			bool exclusive = false,
			float fadeOutTime = 0
		) {
			return AudioSystem.Play(
				path: Path,
				group: group,
				looping: looping,
				priority: priority,
				fadeInTime: fadeInTime,
				paused: paused,
				volume: volume,
				pan: pan,
				pitch: pitch,
				exclusive: exclusive,
				fadeOutTime: fadeOutTime
			);
		}
	}
}
