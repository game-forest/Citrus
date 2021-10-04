using Yuzu;

namespace Match3
{
	public class Profile
	{
		public static Profile Instance;

		[YuzuAfterDeserialization]
		public void AfterDeserialization()
		{ }
	}
}