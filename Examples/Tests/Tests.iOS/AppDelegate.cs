using AVFoundation;
using Foundation;
using UIKit;

namespace Tests.iOS
{
	[Register ("TestsAppDelegate")]
	public class TestsAppDelegate : Lime.AppDelegate
	{
		public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
		{
			AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient);
			Lime.Application.Initialize(new Lime.ApplicationOptions {
				DecodeAudioInSeparateThread = false,
			});

			Tests.Application.Application.Initialize();
			return true;
		}

		public static void Main(string[] args)
		{
			UIApplication.Main(args, null, "TestsAppDelegate");
		}
	}
}


