using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Types
{
	[TestClass]
	public class Vector3Tests
	{
		[TestMethod]
		public void Vector2CastTest()
		{
			Assert.That.AreEqual(Vector2.Zero, (Vector2)Vector3.Zero);
			Assert.That.AreEqual(Vector2.Half, (Vector2)Vector3.Half);
			Assert.That.AreEqual(new Vector2(1.7f, 2.2f), (Vector2)new Vector3(1.7f, 2.2f, 3.5f));
		}

		[TestMethod]
		public void LerpTest()
		{
			Assert.That.AreEqual(Vector3.Zero, Vector3.Lerp(0, Vector3.Zero, Vector3.One));
			Assert.That.AreEqual(Vector3.Half, Vector3.Lerp(0.5f, Vector3.Zero, Vector3.One));
			Assert.That.AreEqual(Vector3.One, Vector3.Lerp(1, Vector3.Zero, Vector3.One));
		}

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(Vector3.Half.Equals(Vector3.Half));
			Assert.IsTrue(Vector3.Half.Equals((object)Vector3.Half));
			Assert.IsTrue(Vector3.Half == Vector3.Half);
			Assert.IsTrue(Vector3.Zero != Vector3.One);
			Assert.AreEqual(Vector3.Half.GetHashCode(), Vector3.Half.GetHashCode());
		}

		[TestMethod]
		public void OperationsTest()
		{
			Assert.That.AreEqual(Vector3.Zero, Vector3.One * Vector3.Zero);
			Assert.That.AreEqual(Vector3.Zero, Vector3.One - Vector3.One);
			Assert.That.AreEqual(Vector3.One, 0.5f * (Vector3.One * 2));
			Assert.That.AreEqual(Vector3.One, Vector3.Half / Vector3.Half);
			Assert.That.AreEqual(Vector3.Half, Vector3.One / 2);
			Assert.That.AreEqual(Vector3.One, Vector3.Half + Vector3.Half);
			Assert.That.AreEqual(new Vector3(-1, -1, -1), -Vector3.One);
			Assert.AreEqual(0.75f, Vector3.DotProduct(Vector3.Half, Vector3.Half));
			Assert.That.AreEqual(Vector3.Zero, Vector3.CrossProduct(Vector3.Half, Vector3.One));
		}

		[TestMethod]
		public void NormalizedTest()
		{
			var unitVector = new Vector3(1 / Mathf.Sqrt(3));
			Assert.That.AreEqual(Vector3.Zero, Vector3.Zero.Normalized);
			Assert.That.AreEqual(unitVector, Vector3.One.Normalized);
			Assert.That.AreEqual(unitVector, Vector3.Half.Normalized);
		}

		[TestMethod]
		public void LengthTest()
		{
			Assert.AreEqual(0, Vector3.Zero.Length);
			Assert.AreEqual(Mathf.Sqrt(0.75f), Vector3.Half.Length);
			Assert.AreEqual(Mathf.Sqrt(3), Vector3.One.Length);
			Assert.AreEqual(0, Vector3.Zero.SqrLength);
			Assert.AreEqual(0.75f, Vector3.Half.SqrLength);
			Assert.AreEqual(3, Vector3.One.SqrLength);
		}

		// TODO: No Parse?
		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual("0, 0, 0", Vector3.Zero.ToString(CultureInfo.InvariantCulture));
			Assert.AreEqual("0.5, 0.5, 0.5", Vector3.Half.ToString(CultureInfo.InvariantCulture));
			Assert.AreEqual("0,5, 0,5, 0,5", Vector3.Half.ToString(CultureInfo.CreateSpecificCulture("ru-RU")));
			var savedCulture = CultureInfo.CurrentCulture;
			CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
			Assert.AreEqual("0.5, 0.5, 0.5", Vector3.Half.ToString());
			CultureInfo.CurrentCulture = savedCulture;
			Assert.AreEqual("0.5, 0.5, 0.5", Vector3.Half.ToString(CultureInfo.InvariantCulture));
			Assert.AreEqual("1, 1, 1", Vector3.One.ToString(CultureInfo.InvariantCulture));
		}
	}
}
