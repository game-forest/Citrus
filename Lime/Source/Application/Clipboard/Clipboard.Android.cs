#if ANDROID
using Android.Content;

namespace Lime
{
	public class ClipboardImplementation : IClipboardImplementation
	{
		public string Text
		{
			get
			{
				var clipboard = (ClipboardManager) ActivityDelegate.Instance.Activity.
					GetSystemService(Android.Content.Context.ClipboardService);

				if (
					clipboard.HasPrimaryClip &&
					clipboard.PrimaryClipDescription.HasMimeType(ClipDescription.MimetypeTextPlain)
				) {
					return clipboard.PrimaryClip.GetItemAt(0).Text;
				} else {
					return "";
				}
			}
			set
			{
				var clipboard = (ClipboardManager) ActivityDelegate.Instance.Activity.
					GetSystemService(Android.Content.Context.ClipboardService);
				clipboard.PrimaryClip = ClipData.NewPlainText("", value);
			}
		}
	}
}
#endif
