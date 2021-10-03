using Yuzu;

namespace Tests
{
	public class Profile
	{
		public static Profile Instance;

		[YuzuAfterDeserialization]
		public void AfterDeserialization()
		{ }
	}
}