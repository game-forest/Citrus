using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Types
{
	[TestClass]
	class IntVector2Tests
	{
		[TestMethod]
		public void Vector2CastTest()
		{
			Assert.That.AreEqual(Vector2.Zero, (Vector2)IntVector2.Zero);
			Assert.That.AreEqual(new Vector2(1, 2), (Vector2)new IntVector2(1, 2));
			Assert.That.AreEqual(Vector2.Zero, IntVector2.Zero.ToVector2());
		}

		[TestMethod]
		public void SizeCastTest()
		{
			Assert.That.AreEqual(new Size(0, 0), (Size)IntVector2.Zero);
			Assert.That.AreEqual(new Size(1, 2), (Size)new IntVector2(1, 2));
		}

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(IntVector2.One.Equals(IntVector2.One));
			Assert.IsTrue(IntVector2.One.Equals((object)IntVector2.One));
			Assert.IsTrue(IntVector2.One == IntVector2.One);
			Assert.IsTrue(IntVector2.Zero != IntVector2.One);
			Assert.AreEqual(IntVector2.One.GetHashCode(), IntVector2.One.GetHashCode());
		}

		[TestMethod]
		public void OperationsTest()
		{
			Assert.That.AreEqual(IntVector2.Zero, IntVector2.One - IntVector2.One);
			Assert.That.AreEqual(new IntVector2(2, 2), 1 * (IntVector2.One * 2));
			Assert.That.AreEqual(IntVector2.One, new IntVector2(2, 2) / 2);
			Assert.That.AreEqual(new IntVector2(2, 2), IntVector2.One + IntVector2.One);
			Assert.That.AreEqual(new IntVector2(-1, -1), -IntVector2.One);
		}
	}
}
