using Match3.Types;
using Lime;

namespace Match3.Dialogs
{
	[ScenePath("Shell/GameScreen")]
	public class GameScreen : Dialog
	{
		public GameScreen()
		{
			SoundManager.PlayMusic("Ingame");
			var exitButton = Root["BtnExit"];
			exitButton.Clicked = ReturnToMenu;
		}

		protected override void Update(float delta)
		{
			if (Input.WasKeyPressed(Key.Escape)) {
				ReturnToMenu();
			}
		}

		protected override bool HandleAndroidBackButton()
		{
			ReturnToMenu();
			return true;
		}

		private void ReturnToMenu()
		{
			var confirmation = new Confirmation("Are you sure?");
			confirmation.OkClicked += () => CrossFadeInto("Shell/MainMenu");
			DialogManager.Open(confirmation);
		}
	}
}
