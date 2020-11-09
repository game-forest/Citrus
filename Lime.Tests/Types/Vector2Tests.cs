using System;
using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Lime.Tests.Source.Types
{
	[TestClass]
	public class Vector2Tests
	{
		[TestMethod]
		public void IntVector2CastTest()
		{
			Assert.That.AreEqual(IntVector2.Zero, (IntVector2)Vector2.Zero);
			Assert.That.AreEqual(IntVector2.Zero, (IntVector2)new Vector2(0.5f));
			Assert.That.AreEqual(new IntVector2(1, 2), (IntVector2)new Vector2(1.7f, 2.2f));
		}

		[TestMethod]
		public void Vector3CastTest()
		{
			Assert.That.AreEqual(Vector3.Zero, (Vector3)Vector2.Zero);
			Assert.That.AreEqual(new Vector3(0.5f, 0.5f, 0), (Vector3)new Vector2(0.5f));
			Assert.That.AreEqual(new Vector3(1.7f, 2.2f, 0), (Vector3)new Vector2(1.7f, 2.2f));
		}

		[TestMethod]
		public void SizeCastTest()
		{
			Assert.That.AreEqual(new Size(0, 0), (Size)Vector2.Zero);
			Assert.That.AreEqual(new Size(0, 0), (Size)new Vector2(0.5f));
			Assert.That.AreEqual(new Size(1, 2), (Size)new Vector2(1.7f, 2.2f));
		}

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(Vector2.Half.Equals(Vector2.Half));
			Assert.IsTrue(Vector2.Half.Equals((object)Vector2.Half));
			Assert.IsTrue(Vector2.Half == Vector2.Half);
			Assert.IsTrue(Vector2.Zero != Vector2.One);
			Assert.AreEqual(Vector2.Half.GetHashCode(), Vector2.Half.GetHashCode());
		}

		[TestMethod]
		public void AngleDegTest()
		{
			Assert.AreEqual(0, Vector2.AngleDeg(Vector2.Right, Vector2.Right));
			Assert.AreEqual(180, Vector2.AngleDeg(Vector2.Up, Vector2.Down));
			Assert.AreEqual(-180, Vector2.AngleDeg(Vector2.Down, Vector2.Up));
		}

		[TestMethod]
		public void AngleRadTest()
		{
			Assert.AreEqual(0, Vector2.AngleRad(Vector2.Right, Vector2.Right));
			Assert.AreEqual(Mathf.Pi, Vector2.AngleRad(Vector2.Up, Vector2.Down));
			Assert.AreEqual(-Mathf.Pi, Vector2.AngleRad(Vector2.Down, Vector2.Up));
		}

		[TestMethod]
		public void LerpTest()
		{
			Assert.AreEqual(Vector2.Zero, Vector2.Lerp(0, Vector2.Zero, Vector2.One));
			Assert.AreEqual(Vector2.Half, Vector2.Lerp(0.5f, Vector2.Zero, Vector2.One));
			Assert.AreEqual(Vector2.One, Vector2.Lerp(1, Vector2.Zero, Vector2.One));
		}

		[TestMethod]
		public void DistanceTest()
		{
			Assert.AreEqual(Vector2.Zero.Length, Vector2.Distance(Vector2.Zero, Vector2.Zero));
			Assert.AreEqual(Vector2.Half.Length, Vector2.Distance(Vector2.Zero, Vector2.Half));
			Assert.AreEqual(Vector2.One.Length, Vector2.Distance(Vector2.Zero, Vector2.One));
		}

		[TestMethod]
		public void OperationsTest()
		{
			Assert.That.AreEqual(Vector2.Zero, Vector2.One * Vector2.Zero);
			Assert.That.AreEqual(Vector2.Zero, Vector2.One - Vector2.One);
			Assert.That.AreEqual(Vector2.One, 0.5f * (Vector2.One * 2));
			Assert.That.AreEqual(Vector2.One, Vector2.Half / Vector2.Half);
			Assert.That.AreEqual(Vector2.Half, Vector2.One / 2);
			Assert.That.AreEqual(Vector2.One, Vector2.Half + Vector2.Half);
			Assert.That.AreEqual(new Vector2(-1), -Vector2.One);
			Assert.AreEqual(0.5f, Vector2.DotProduct(Vector2.Half, Vector2.Half));
			Assert.AreEqual(0, Vector2.CrossProduct(Vector2.Half, Vector2.Half));
		}

		const float delta = 0.0001f;

		[TestMethod]
		public void CosSinRoughTest()
		{
			Assert.That.AreEqual(Vector2.Right, Vector2.CosSinRough(0), delta);
			Assert.That.AreEqual(Vector2.Left, Vector2.CosSinRough(Mathf.Pi), delta);
			Assert.That.AreEqual(Vector2.Down, Vector2.CosSinRough(Mathf.HalfPi), delta);
		}

		[TestMethod]
		public void RotateDegTest()
		{
			Assert.That.AreEqual(Vector2.Right, Vector2.RotateDeg(Vector2.Right, 0), delta);
			Assert.That.AreEqual(Vector2.Left, Vector2.RotateDeg(Vector2.Right, 180), delta);
			Assert.That.AreEqual(Vector2.Down, Vector2.RotateDeg(Vector2.Right, 90), delta);
		}

		[TestMethod]
		public void RotateRadTest()
		{
			Assert.That.AreEqual(Vector2.Right, Vector2.RotateRad(Vector2.Right, 0), delta);
			Assert.That.AreEqual(Vector2.Left, Vector2.RotateRad(Vector2.Right, Mathf.Pi), delta);
			Assert.That.AreEqual(Vector2.Down, Vector2.RotateRad(Vector2.Right, Mathf.HalfPi), delta);
		}

		[TestMethod]
		public void Atan2DegTest()
		{
			Assert.AreEqual(0, Vector2.Right.Atan2Deg);
			Assert.AreEqual(180, Vector2.Left.Atan2Deg);
			Assert.AreEqual(90, Vector2.Down.Atan2Deg);
		}

		[TestMethod]
		public void Atan2RadTest()
		{
			Assert.AreEqual(0, Vector2.Right.Atan2Rad);
			Assert.AreEqual(Mathf.Pi, Vector2.Left.Atan2Rad);
			Assert.AreEqual(Mathf.HalfPi, Vector2.Down.Atan2Rad);
		}

		[TestMethod]
		public void LengthTest()
		{
			Assert.AreEqual(0, Vector2.Zero.Length);
			Assert.AreEqual(Mathf.Sqrt(0.5f), Vector2.Half.Length);
			Assert.AreEqual(Mathf.Sqrt(2), Vector2.One.Length);
			Assert.AreEqual(0, Vector2.Zero.SqrLength);
			Assert.AreEqual(0.5f, Vector2.Half.SqrLength);
			Assert.AreEqual(2, Vector2.One.SqrLength);
		}

		[TestMethod]
		public void NormalizedTest()
		{
			var unitVector = new Vector2(1 / Mathf.Sqrt(2));
			Assert.That.AreEqual(Vector2.Zero, Vector2.Zero.Normalized);
			Assert.That.AreEqual(unitVector, Vector2.One.Normalized);
			Assert.That.AreEqual(unitVector, Vector2.Half.Normalized);
		}

		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual(Vector2.Zero.ToString(CultureInfo.InvariantCulture), "0, 0");
			Assert.AreEqual(Vector2.Half.ToString(CultureInfo.InvariantCulture), "0.5, 0.5");
			Assert.AreEqual("0,5, 0,5", Vector2.Half.ToString(CultureInfo.CreateSpecificCulture("ru-RU")));
			var savedCulture = CultureInfo.CurrentCulture;
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
			Assert.AreEqual("0.5, 0.5", Vector2.Half.ToString());
			CultureInfo.CurrentCulture = savedCulture;
			Assert.AreEqual(Vector2.One.ToString(CultureInfo.InvariantCulture), "1, 1");
		}

		[TestMethod]
		public void TryParseTest()
		{
			Vector2 vector;
			Assert.IsFalse(Vector2.TryParse("", out vector));
			Assert.IsFalse(Vector2.TryParse("1, 2, 3", out vector));
			Assert.IsFalse(Vector2.TryParse("1, ", out vector));
			Assert.IsTrue(Vector2.TryParse("1.5, 1.5", out vector));
			Assert.AreEqual(new Vector2(1.5f), vector);
		}

		[TestMethod]
		public void ParseTest()
		{
			Assert.ThrowsException<ArgumentNullException>(() => Vector2.Parse(null));
			Assert.ThrowsException<FormatException>(() => Vector2.Parse(""));
			Assert.ThrowsException<FormatException>(() => Vector2.Parse("1, 2, 3"));
			Assert.ThrowsException<FormatException>(() => Vector2.Parse("1, "));
			Assert.That.AreEqual(new Vector2(1.5f), Vector2.Parse("1.5, 1.5"));
		}
	}
}
