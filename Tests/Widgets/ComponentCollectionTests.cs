using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests.Widgets
{
	[TestClass]
	public class ComponentCollectionTests
	{
		private class TestComponent : Component
		{
			public int Value { get; set; }

			public TestComponent(int value)
			{
				Value = value;
			}

			public override string ToString() => Value.ToString();
		}

		private class DummyComponent : Component
		{

		}

		[TestMethod]
		public void Test()
		{
			var components = new ComponentCollection<Component>();
			var testComponent1 = new TestComponent(1);
			var testComponent2 = new TestComponent(2);
			components.Add(testComponent1);
			components.Add(testComponent2);
			Assert.AreEqual(2, components.Count);
			foreach (var c in components) {
				Assert.IsTrue(c == testComponent1 || c == testComponent2);
			}
			var cs = components.GetAll<TestComponent>().ToList();
			Assert.AreEqual(components.Count, cs.Count);
			foreach (var c in cs) {
				Assert.IsTrue(c == testComponent1 || c == testComponent2);
			}
			var dummy = new DummyComponent();
			components.Add(dummy);
			Assert.IsTrue(components.Remove<TestComponent>());
			Assert.AreEqual(1, components.Count);
			foreach (var c in components) {
				Assert.AreEqual(dummy, c);
			}
			components.Add(testComponent2);
			components.Add(testComponent1);
			Assert.IsTrue(components.Remove(testComponent1.GetType()));
			Assert.AreEqual(1, components.Count);
			foreach (var c in components) {
				Assert.AreEqual(dummy, c);
			}
			components.Add(testComponent1);
			components.Add(testComponent2);
			components.Remove(testComponent1);
			foreach (var c in components) {
				Assert.IsTrue(c == dummy || c == testComponent2);
			}
		}
	}
}
