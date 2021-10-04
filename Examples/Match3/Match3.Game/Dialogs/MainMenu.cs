using Lime;

namespace Match3.Dialogs
{
	public class MainMenu : Dialog<Scenes.Data.MainMenu>
	{
		public MainMenu()
		{
			SoundManager.PlayMusic("Theme");
			Scene._BtnPlay.It.Clicked = CrossfadeInto<GameScreen>;
			Scene._BtnOptions.It.Clicked = Open<Options>;
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
