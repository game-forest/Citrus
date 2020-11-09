using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Types
{
	[TestClass]
	public class RayTests
	{
		private static Ray unitRay = new Ray(Vector3.Zero, new Vector3(1, 0, 0));

		[TestMethod]
		public void EqualsTest()
		{
			Assert.IsTrue(unitRay.Equals(unitRay));
			Assert.IsTrue(unitRay.Equals((object) unitRay));
			Assert.IsTrue(unitRay == unitRay);
			Assert.IsTrue(unitRay != new Ray(Vector3.Zero, Vector3.Zero));
			Assert.AreEqual(unitRay.GetHashCode(), unitRay.GetHashCode());
		}

		[TestMethod]
		public void IntersectsTest()
		{
			var disjoiningSphere = new BoundingSphere(new Vector3(-2, 0, 0), 1);
			Assert.AreEqual(null, unitRay.Intersects(disjoiningSphere));
			var containingSphere = new BoundingSphere(Vector3.Zero, 1);
			Assert.AreEqual(0, unitRay.Intersects(containingSphere));
			var intersectingSphere = new BoundingSphere(new Vector3(2, 0, 0), 1);
			Assert.AreEqual(1, unitRay.Intersects(intersectingSphere));
		}
	}
}
