using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests.Types
{
	[TestClass]
	public class BoundingSphereTests
	{
		private static BoundingSphere unitSphere = new BoundingSphere(Vector3.Zero, 1);

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(unitSphere.Equals(unitSphere));
			Assert.IsTrue(unitSphere.Equals((object)unitSphere));
			Assert.IsTrue(unitSphere == unitSphere);
			Assert.IsTrue(unitSphere != new BoundingSphere(Vector3.Zero, 0));
			Assert.AreEqual(unitSphere.GetHashCode(), unitSphere.GetHashCode());
		}

		[TestMethod]
		public void ContainsBoundingSphereTest()
		{
			var disjoiningSphere = new BoundingSphere(new Vector3(3), 1);
			Assert.AreEqual(ContainmentType.Disjoint, unitSphere.Contains(ref disjoiningSphere));
			var containingSphere = new BoundingSphere(Vector3.Zero, 0.5f);
			Assert.AreEqual(ContainmentType.Contains, unitSphere.Contains(ref containingSphere));
			var intersectingSphere = new BoundingSphere(Vector3.One, 1);
			Assert.AreEqual(ContainmentType.Intersects, unitSphere.Contains(ref intersectingSphere));
		}

		[TestMethod]
		public void ContainsVectorTest()
		{
			var disjoiningVector = new Vector3(3);
			Assert.AreEqual(ContainmentType.Disjoint, unitSphere.Contains(ref disjoiningVector));
			var containingVector = Vector3.Zero;
			Assert.AreEqual(ContainmentType.Contains, unitSphere.Contains(ref containingVector));
			var intersectingVector = new Vector3(1, 0, 0);
			Assert.AreEqual(ContainmentType.Intersects, unitSphere.Contains(ref intersectingVector));
		}

		[TestMethod]
		public void CreateFromPointsTest()
		{
			Assert.ThrowsException<ArgumentNullException>(() => BoundingSphere.CreateFromPoints(null));
			var actualPoints = new List<Vector3> ();
			Assert.ThrowsException<ArgumentException>(() => BoundingSphere.CreateFromPoints(actualPoints));
			actualPoints = new List<Vector3> { Vector3.Zero };
			var actual = BoundingSphere.CreateFromPoints(actualPoints);
			Assert.That.AreEqual(new BoundingSphere(Vector3.Zero, 0), actual);
			actualPoints = new List<Vector3> {
				new Vector3(1, 0, 0),
				new Vector3(-1, 0, 0),
			};
			actual = BoundingSphere.CreateFromPoints(actualPoints);
			Assert.That.AreEqual(unitSphere, actual);
			actualPoints = new List<Vector3> {
				new Vector3(0, 1, 0),
				new Vector3(0, -1, 0),
			};
			actual = BoundingSphere.CreateFromPoints(actualPoints);
			Assert.That.AreEqual(unitSphere, actual);
			actualPoints = new List<Vector3> {
				new Vector3(0, 0, 1),
				new Vector3(0, 0, -1)
			};
			actual = BoundingSphere.CreateFromPoints(actualPoints);
			Assert.That.AreEqual(unitSphere, actual);
		}

		[TestMethod]
		public void TransformTest()
		{
			// TODO: Add more tests
			var actual = unitSphere.Transform(Matrix44.CreateTranslation(Vector3.One));
			var expected = new BoundingSphere(Vector3.One, 1);
			Assert.That.AreEqual(expected, actual);
		}
	}
}
