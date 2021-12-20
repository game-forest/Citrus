using Tests.Types;
using Lime;
using System.Collections.Generic;

namespace Tests.Dialogs
{
	public class ShowContent : Dialog<Scenes.Data.ShowContent>
	{
		public ShowContent()
		{
			Scene._BtnExit.It.Clicked = ReturnToMenu;
			Scene._Idle.It.Clicked = RunIdleAnimation;
			Scene._Rotate.It.Clicked = RunRotateAnimation;
			Scene._Side.It.Clicked = RunSideAnimation;
			Scene._Up.It.Clicked = RunUpAnimation;
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

		private void RunIdleAnimation()
		{
			Scene.RunAnimationIdle();
		}
		private void RunRotateAnimation()
		{
			Scene.RunAnimationRotate();
		}
		private void RunSideAnimation()
		{
			Scene.RunAnimationSide();
		}
		private void RunUpAnimation()
		{
			Scene.RunAnimationUp();
		}
	}
}
