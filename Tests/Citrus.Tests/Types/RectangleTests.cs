using System.Globalization;
using Lime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Citrus.Tests.Types
{
	[TestClass]
	public class RectangleTests
	{
		[TestMethod]
		public void IntRectangleCastTest()
		{
			Assert.That.AreEqual(IntRectangle.Empty, (IntRectangle)Rectangle.Empty);
			Assert.That.AreEqual(new IntRectangle(0, 0, 1, 1), (IntRectangle)new Rectangle(0, 0, 1, 1));
			Assert.That.AreEqual(
				new IntRectangle(IntVector2.Zero, IntVector2.One),
				(IntRectangle)new Rectangle(Vector2.Zero, Vector2.One)
			);
			Assert.That.AreEqual(
				new IntRectangle(IntVector2.Zero, IntVector2.One), (IntRectangle)new Rectangle(0, 0, 1.5f, 1.5f)
			);
		}

		[TestMethod]
		public void EqualsTest()
		{
			var rectangleOne = new Rectangle(Vector2.Zero, Vector2.One);
			Assert.IsTrue(rectangleOne.Equals(new Rectangle(0, 0, 1, 1)));
			Assert.IsTrue(rectangleOne.Equals((object)new Rectangle(0, 0, 1, 1)));
			Assert.IsTrue(new Rectangle(0, 0, 1, 1) == rectangleOne);
			Assert.IsTrue(Rectangle.Empty != rectangleOne);
			Assert.AreEqual(rectangleOne.GetHashCode(), rectangleOne.GetHashCode());
		}

		[TestMethod]
		public void SizeTest()
		{
			var rectangle = new Rectangle(Vector2.Zero, Vector2.One);
			Assert.That.AreEqual(new Vector2(1, 1), rectangle.Size);
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
			var rectangle = new Rectangle(Vector2.Zero, Vector2.One);
			Assert.That.AreEqual(rectangle.B, new Vector2(rectangle.Right, rectangle.Bottom));
			Assert.That.AreEqual(rectangle.A, new Vector2(rectangle.Left, rectangle.Top));
			var expectedCenter = rectangle.A + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
			Assert.That.AreEqual(expectedCenter, rectangle.Center);
			rectangle.Left++;
			rectangle.Top++;
			rectangle.Right++;
			rectangle.Bottom++;
			Assert.That.AreEqual(rectangle.B, new Vector2(rectangle.Right, rectangle.Bottom));
			Assert.That.AreEqual(rectangle.A, new Vector2(rectangle.Left, rectangle.Top));
			expectedCenter = rectangle.A + new Vector2(rectangle.Width / 2, rectangle.Height / 2);
			Assert.That.AreEqual(expectedCenter, rectangle.Center);
		}

		[TestMethod]
		public void NormalizedTest()
		{
			var rectangle = new Rectangle(Vector2.One, Vector2.Zero);
			Assert.IsTrue(rectangle.Width < 0);
			Assert.IsTrue(rectangle.Height < 0);
			rectangle = rectangle.Normalized;
			Assert.IsTrue(rectangle.Width > 0);
			Assert.IsTrue(rectangle.Height > 0);
		}

		[TestMethod]
		public void ContainsTest()
		{
			var rectangle = new Rectangle(Vector2.Zero, Vector2.One);
			Assert.IsTrue(rectangle.Contains(rectangle.A));
			Assert.IsTrue(rectangle.Contains(Vector2.Half));
			Assert.IsFalse(rectangle.Contains(rectangle.B));
			Assert.IsFalse(rectangle.Contains(rectangle.B + Vector2.One));
		}

		[TestMethod]
		public void IntersectTest()
		{
			var intersection = Rectangle.Intersect(new Rectangle(0, 0, 2, 2), new Rectangle(1, 1, 3, 3));
			var expectedIntersection = new Rectangle(1, 1, 2, 2);
			Assert.That.AreEqual(expectedIntersection, intersection);
			intersection = Rectangle.Intersect(new Rectangle(0, 0, 3, 3), new Rectangle(1, 1, 2, 2));
			expectedIntersection = new Rectangle(1, 1, 2, 2);
			Assert.That.AreEqual(expectedIntersection, intersection);
		}

		[TestMethod]
		public void BoundsTest()
		{
			var bounds = Rectangle.Bounds(new Rectangle(0, 0, 2, 2), new Rectangle(1, 1, 3, 3));
			var expectedBounds = new Rectangle(0, 0, 3, 3);
			Assert.That.AreEqual(expectedBounds, bounds);
			bounds = Rectangle.Bounds(new Rectangle(0, 0, 3, 3), new Rectangle(1, 1, 2, 2));
			expectedBounds = new Rectangle(0, 0, 3, 3);
			Assert.That.AreEqual(expectedBounds, bounds);
		}

		[TestMethod]
		public void IncludePointTest()
		{
			var rectangle = new Rectangle(0, 0, 1, 1).IncludingPoint(new Vector2(2, 2));
			var expextedRectangle = new Rectangle(0, 0, 2, 2);
			Assert.That.AreEqual(expextedRectangle, rectangle);
			rectangle = new Rectangle(0, 0, 2, 2).IncludingPoint(new Vector2(1, 1));
			expextedRectangle = new Rectangle(0, 0, 2, 2);
			Assert.That.AreEqual(expextedRectangle, rectangle);
		}

		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual("0, 0, 0, 0", Rectangle.Empty.ToString());
			Assert.AreEqual("0.5, 0.5, 0.5, 0.5", new Rectangle(0.5f, 0.5f, 0.5f, 0.5f).ToString());
			var savedCulture = CultureInfo.CurrentCulture;
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
			Assert.AreEqual("0.5, 0.5, 0.5, 0.5", new Rectangle(0.5f, 0.5f, 0.5f, 0.5f).ToString());
			CultureInfo.CurrentCulture = savedCulture;
			Assert.AreEqual("0, 0, 1, 1", new Rectangle(Vector2.Zero, Vector2.One).ToString());
		}

		[TestMethod]
		public void TransformTest()
		{
			var rectangle = new Rectangle(Vector2.Zero, Vector2.One);
			rectangle = rectangle.Transform(Matrix32.Scaling(Vector2.Half));
			var expectedRectangle = new Rectangle(Vector2.Zero, Vector2.Half);
			Assert.That.AreEqual(expectedRectangle, rectangle);
		}
	}
}
