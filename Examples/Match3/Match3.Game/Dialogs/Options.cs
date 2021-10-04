using System;
using System.Reflection;
using Lime;

namespace Match3.Dialogs
{

	[ScenePath("Shell/Options")]
	public class Options : Dialog
	{
		public Options()
		{
			CreateCheckBox(Root["MusicGroup"], nameof(MusicEnabled));
			CreateCheckBox(Root["SoundGroup"], nameof(SoundEnabled));
			CreateCheckBox(Root["VoiceGroup"], nameof(VoiceEnabled));
			CreateCheckBox(Root["FullScreenGroup"], nameof(Fullscreen));
			Root["BtnOk"].Clicked = Close;
		}

		private void CreateCheckBox(Widget checkGroup, string propertyName, bool autoUpdate = false)
		{
			var property = GetType().GetProperty(propertyName, BindingFlags.NonPublic | BindingFlags.Instance);
			var getValue = new Func<bool>(() => (bool)property.GetValue(this));
			var toggle = new Action(() => property.SetValue(this, !getValue()));
			CreateCheckBox(checkGroup, getValue, toggle, autoUpdate);
		}

		private static void CreateCheckBox(Widget checkGroup, Func<bool> getValue, Action toggleValue, bool autoUpdate = false)

		{
			var check = checkGroup["Check"];
			RunConditionalAnimation(getValue, () => check.RunAnimation("Checked"), () => check.RunAnimation("Unchecked"));
			checkGroup["BtnCheck"].Clicked = () => {
				toggleValue?.Invoke();
				RunConditionalAnimation(getValue, () => check.RunAnimation("Checked"), () => check.RunAnimation("Unchecked"));
			};
			if (autoUpdate) {
				checkGroup.Updating += delta => {
					if (check.IsStopped)
						RunConditionalAnimation(getValue, () => check.RunAnimation("Checked"), () => check.RunAnimation("Unchecked"));
				};
			}
		}

		private static void RunConditionalAnimation(Func<bool> getValue, Action trueAnimation, Action falseAnimation)
		{
			if (getValue())
				trueAnimation();
			else
				falseAnimation();
		}

		protected override void Closing()
		{
			base.Closing();
			AppData.Save();
		}

		private bool MusicEnabled
		{
			get { return SoundManager.MusicVolume > 0; }
			set { SoundManager.MusicVolume = value ? 1.0f : 0; }
		}

		private bool SoundEnabled
		{
			get { return SoundManager.SfxVolume > 0; }
			set { SoundManager.SfxVolume = value ? 1.0f : 0; }
		}

		private bool VoiceEnabled
		{
			get { return SoundManager.VoiceVolume > 0; }
			set { SoundManager.VoiceVolume = value ? 1.0f : 0; }
		}

		private bool Fullscreen
		{
			get { return Window.Fullscreen; }
			set { Window.Fullscreen = value; }
		}
	}
}
