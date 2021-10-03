using Tests.Types;
using Lime;

namespace Tests.Dialogs
{
	public class GameScreen : Dialog<Scenes.Data.GameScreen>
	{
		public GameScreen()
		{
			SoundManager.PlayMusic("Ingame");
			Scene._BtnExit.It.Clicked = ReturnToMenu;
			// This line ensures this module depends on <project_name>.Types module.
			// TODO: Leave that line only in TestProject and remove from all other examples.
			var emptyComponent = Root.Components.Get<EmptyComponent>();
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
			confirmation.OkClicked += CrossfadeInto<MainMenu>;
			Open(confirmation);
		}
	}
}
