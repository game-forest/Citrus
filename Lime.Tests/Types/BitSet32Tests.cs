using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Types
{
	[TestClass]
	public class BitSet32Tests
	{
		[TestMethod]
		public void AnyTest()
		{
			var bitSet = new BitSet32(0);
			Assert.IsFalse(bitSet.Any());
			bitSet[0] = true;
			Assert.IsTrue(bitSet.Any());
		}

		[TestMethod]
		public void AllTest()
		{
			var bitSet = new BitSet32(uint.MaxValue);
			Assert.IsTrue(bitSet.All());
			bitSet[0] = false;
			Assert.IsFalse(bitSet.All());
		}

		[TestMethod]
		public void EqualsTest()
		{
			var bitSet1 = new BitSet32(0);
			var bitSet2 = new BitSet32(1);
			Assert.IsFalse(bitSet1.Equals(bitSet2));
			Assert.IsTrue(bitSet1 != bitSet2);
			bitSet1[0] = true;
			Assert.IsTrue(bitSet1[0].Equals(bitSet2[0]));
			Assert.IsTrue(bitSet1 == bitSet2);
			Assert.IsTrue(bitSet1.Equals((object)bitSet2));
			Assert.AreEqual(bitSet2.GetHashCode(), bitSet1.GetHashCode());
		}

		[TestMethod]
		public void ToStringTest()
		{
			Assert.AreEqual("0", new BitSet32(0).ToString());
			Assert.AreEqual("1", new BitSet32(1).ToString());
			Assert.AreEqual("10", new BitSet32(2).ToString());
			Assert.AreEqual("10000000001", new BitSet32(1025).ToString());
			Assert.AreEqual("11111111111111111111111111111111", new BitSet32(uint.MaxValue).ToString());
		}
	}
}
