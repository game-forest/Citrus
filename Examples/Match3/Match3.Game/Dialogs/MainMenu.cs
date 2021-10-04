using Lime;

namespace Match3.Dialogs
{
	[ScenePath("Shell/MainMenu")]
	public class MainMenu : Dialog
	{
		public MainMenu()
		{
			SoundManager.PlayMusic("Theme");
			var playButton = Root["BtnPlay"];
			var optionsButton = Root["BtnOptions"];
			playButton.Clicked = () => CrossFadeInto("Shell/GameScreen");
			optionsButton.Clicked = () => DialogManager.Open("Shell/Options");
		}

		protected override bool HandleAndroidBackButton()
		{
			return false;
		}

		protected override void Update(float delta)
		{
			if (Input.WasKeyPressed(Key.Escape)) {
				Lime.Application.Exit();
			}
		}
	}
}
