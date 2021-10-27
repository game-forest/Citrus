using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests.Types
{
	[TestClass]
	class IntRectangleTests
	{
		[TestMethod]
		public void RectangleCastTest()
		{
			Assert.That.AreEqual(Rectangle.Empty, (Rectangle)IntRectangle.Empty);
			Assert.That.AreEqual(new Rectangle(0, 0, 1, 1), (Rectangle)new IntRectangle(0, 0, 1, 1));
			Assert.That.AreEqual(new Rectangle(Vector2.Zero, Vector2.One), (Rectangle)new IntRectangle(IntVector2.Zero, IntVector2.One));
		}

		[TestMethod]
		public void WindowRectCastTest()
		{
			Assert.That.AreEqual(new WindowRect(), (WindowRect)IntRectangle.Empty);
			Assert.That.AreEqual(new WindowRect {X = 0, Y = 0, Height = 1, Width = 1}, (WindowRect)new IntRectangle(0, 0, 1, 1));
			Assert.That.AreEqual(new WindowRect { X = 0, Y = 0, Height = 1, Width = 1 }, (WindowRect)new IntRectangle(IntVector2.Zero, IntVector2.One));
		}

		[TestMethod]
		public void EqualsTest()
		{
			var rectangleOne = new IntRectangle(IntVector2.Zero, IntVector2.One);
			Assert.IsTrue(rectangleOne.Equals(new IntRectangle(0, 0, 1, 1)));
			Assert.IsTrue(rectangleOne.Equals((object)new IntRectangle(0, 0, 1, 1)));
			Assert.IsTrue(new IntRectangle(0, 0, 1, 1) == rectangleOne);
			Assert.IsTrue(IntRectangle.Empty != rectangleOne);
			Assert.AreEqual(rectangleOne.GetHashCode(), rectangleOne.GetHashCode());
		}

		[TestMethod]
		public void NormalizedTest()
		{
			var rectangle = new IntRectangle(IntVector2.One, IntVector2.Zero);
			Assert.IsTrue(rectangle.Width < 0);
			Assert.IsTrue(rectangle.Height < 0);
			rectangle = rectangle.Normalized;
			Assert.IsTrue(rectangle.Width > 0);
			Assert.IsTrue(rectangle.Height > 0);
		}

		[TestMethod]
		public void SizeTest()
		{
			var rectangle = new IntRectangle(IntVector2.Zero, IntVector2.One);
			Assert.That.AreEqual(new IntVector2(1, 1), rectangle.Size);
			Assert.AreEqual(1, rectangle.Width);
			Assert.AreEqual(1, rectangle.Height);
			rectangle.Width++;
			rectangle.Height++;
			Assert.AreEqual(2, rectangle.Width);
			Assert.AreEqual(2, rectangle.Height);
		}

		[TestMethod]
		public void SidesTest()
		{
			var rectangle = new IntRectangle(IntVector2.Zero, IntVector2.One);
			Assert.That.AreEqual(rectangle.B, new IntVector2(rectangle.Right, rectangle.Bottom));
			Assert.That.AreEqual(rectangle.A, new IntVector2(rectangle.Left, rectangle.Top));
			var expectedCenter = rectangle.A + new IntVector2(rectangle.Width / 2, rectangle.Height / 2);
			Assert.That.AreEqual(expectedCenter, rectangle.Center);
			rectangle.Left++;
			rectangle.Top++;
			rectangle.Right++;
			rectangle.Bottom++;
			Assert.That.AreEqual(rectangle.B, new IntVector2(rectangle.Right, rectangle.Bottom));
			Assert.That.AreEqual(rectangle.A, new IntVector2(rectangle.Left, rectangle.Top));
			expectedCenter = rectangle.A + new IntVector2(rectangle.Width / 2, rectangle.Height / 2);
			Assert.That.AreEqual(expectedCenter, rectangle.Center);
		}

		[TestMethod]
		public void ContainsTest()
		{
			var rectangle = new IntRectangle(IntVector2.Zero, IntVector2.One);
			Assert.IsTrue(rectangle.Contains(rectangle.A));
			Assert.IsFalse(rectangle.Contains(rectangle.B));
			Assert.IsFalse(rectangle.Contains(rectangle.B + IntVector2.One));
		}

		[TestMethod]
		public void IntersectTest()
		{
			var intersection = IntRectangle.Intersect(new IntRectangle(0, 0, 2, 2), new IntRectangle(1, 1, 3, 3));
			var expectedIntersection = new IntRectangle(1, 1, 2, 2);
			Assert.That.AreEqual(expectedIntersection, intersection);
			intersection = IntRectangle.Intersect(new IntRectangle(0, 0, 3, 3), new IntRectangle(1, 1, 2, 2));
			expectedIntersection = new IntRectangle(1, 1, 2, 2);
			Assert.That.AreEqual(expectedIntersection, intersection);
		}

		[TestMethod]
		public void OffsetByTest()
		{
			var rectangle = new IntRectangle(0, 0, 1, 1).OffsetBy(new IntVector2(2, 2));
			var expextedRectangle = new IntRectangle(2, 2, 3, 3);
			Assert.That.AreEqual(expextedRectangle, rectangle);
		}

		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual("0, 0, 0, 0", IntRectangle.Empty.ToString());
			Assert.AreEqual("1, 1, 2, 2", new IntRectangle(1, 1, 2, 2).ToString());
			Assert.AreEqual("0, 0, 1, 1", new IntRectangle(IntVector2.Zero, IntVector2.One).ToString());
		}
	}
}
