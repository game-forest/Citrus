using AVFoundation;
using Foundation;
using UIKit;

namespace Match3.iOS
{
	[Register ("Match3AppDelegate")]
	public class Match3AppDelegate : Lime.AppDelegate
	{
		public override bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
		{
			AVAudioSession.SharedInstance().SetCategory(AVAudioSessionCategory.Ambient);
			Lime.Application.Initialize(new Lime.ApplicationOptions {
				DecodeAudioInSeparateThread = false,
			});

			Match3.Application.Application.Initialize();
			return true;
		}

		public static void Main(string[] args)
		{
			UIApplication.Main(args, null, "Match3AppDelegate");
		}
	}
}


