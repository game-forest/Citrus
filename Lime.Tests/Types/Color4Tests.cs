using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Types
{
	[TestClass]
	class Color4Tests
	{
		[TestMethod]
		public void ConstructorTest()
		{
			var color1 = new Color4(255, 127, 25, 0);
			var color2 = new Color4(0x00197FFF);
			var color3 = Color4.FromFloats(1, 0.5f, 0.1F, 0);
			Assert.AreEqual(0, color1.A);
			Assert.AreEqual(0, color2.A);
			Assert.AreEqual(0, color3.A);
			Assert.AreEqual(25, color1.B);
			Assert.AreEqual(25, color2.B);
			Assert.AreEqual(25, color3.B);
			Assert.AreEqual(127, color1.G);
			Assert.AreEqual(127, color2.G);
			Assert.AreEqual(127, color3.G);
			Assert.AreEqual(255, color1.R);
			Assert.AreEqual(255, color2.R);
			Assert.AreEqual(255, color3.R);
			Assert.AreEqual(0x00197FFF, color1.ABGR);
			Assert.AreEqual(0x00197FFF, color2.ABGR);
			Assert.AreEqual(0x00197FFF, color3.ABGR);
		}

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(Color4.White.Equals(Color4.White));
		}

		[TestMethod]
		public void MultiplicationTest()
		{
			Assert.That.AreEqual(Color4.Black, Color4.White * Color4.Black);
			Assert.That.AreEqual(Color4.Black, Color4.Black * Color4.Green);
			Assert.That.AreEqual(Color4.Blue, Color4.Blue * Color4.White);
			Assert.That.AreEqual(Color4.DarkGray, Color4.Gray * Color4.Gray);
		}

		[TestMethod]
		public void PremulAlphaTest()
		{
			Assert.That.AreEqual(Color4.White, Color4.PremulAlpha(Color4.White));
			Assert.That.AreEqual(Color4.Black, Color4.PremulAlpha(Color4.Black));
			var halfTransparentWhite = Color4.White;
			halfTransparentWhite.A = 128;
			var halfTransparentGray = Color4.Gray;
			halfTransparentGray.A = 128;
			Assert.That.AreEqual(halfTransparentGray, Color4.PremulAlpha(halfTransparentWhite));
		}

		[TestMethod]
		public void LerpTest()
		{
			Assert.That.AreEqual(Color4.Orange, Color4.Lerp(0, Color4.Orange, Color4.Black));
			Assert.That.AreEqual(Color4.DarkGray, Color4.Lerp(0.5f, Color4.Gray, Color4.Black));
			Assert.That.AreEqual(Color4.Black, Color4.Lerp(1, Color4.Black, Color4.Black));
		}

		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual("#FF.FF.FF.FF", Color4.White.ToString());
			Assert.AreEqual("#00.00.00.FF", Color4.Black.ToString());
			Assert.AreEqual("#FF.80.00.FF", Color4.Orange.ToString());
		}
	}
}
