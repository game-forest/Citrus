namespace Match3.Dialogs
{
	[ScenePath("Shell/Splash")]
	public class SplashScreen : Dialog
	{
		public SplashScreen()
		{
			Root.RunAnimation("Start");
			Root.AnimationStopped += () => CrossFadeInto("Shell/MainMenu");
		}
	}
}
