namespace Tests.Dialogs
{
	public class SplashScreen : Dialog<Scenes.Data.Splash>
	{
		public SplashScreen()
		{
			Scene.RunAnimationStart();
			Root.AnimationStopped += CrossfadeInto<MainMenu>;
		}
	}
}
