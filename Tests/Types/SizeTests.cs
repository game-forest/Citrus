using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests.Types
{
	[TestClass]
	public class SizeTests
	{
		[TestMethod]
		public void Vector2CastTest()
		{
			Assert.That.AreEqual(Vector2.Zero, (Vector2)new Size(0, 0));
			Assert.That.AreEqual(Vector2.One, (Vector2)new Size(1, 1));
		}

		[TestMethod]
		public void IntVector2CastTest()
		{
			Assert.That.AreEqual(IntVector2.Zero, (IntVector2)new Size(0, 0));
			Assert.That.AreEqual(IntVector2.One, (IntVector2)new Size(1, 1));
		}

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(new Size(0, 0).Equals(new Size(0, 0)));
			Assert.IsTrue(new Size(0, 0).Equals((object)new Size(0, 0)));
			Assert.IsTrue(new Size(0, 0) == new Size(0, 0));
			Assert.IsTrue(new Size(0, 0) != new Size(1, 1));
			Assert.AreEqual(new Size(0, 0).GetHashCode(), new Size(0, 0).GetHashCode());
		}

		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual("0, 0", new Size(0, 0).ToString());
			Assert.AreEqual("1, 1", new Size(1, 1).ToString());
		}
	}
}
