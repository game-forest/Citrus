using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;
using System;

namespace Citrus.Tests.Widgets
{
	[TestClass]
	public class ComponentCollectionTests
	{
		[AllowMultiple]
		private class BaseTestComponent : Component
		{
			public int Value { get; set; }

			public BaseTestComponent(int value)
			{
				Value = value;
			}

			public override string ToString() => Value.ToString();
		}

		private class DerivedTestComponent : BaseTestComponent
		{
			public DerivedTestComponent(int value) : base(value)
			{
			}

			public DerivedTestComponent(int value, int anotherValue) : base(value)
			{
				AnotherValue = anotherValue;
			}

			public int AnotherValue { get; set; }
			public override string ToString() => $"{Value} {AnotherValue}";
		}

		private class DummyComponent : Component
		{

		}

		[TestMethod]
		public void ComponentCollectionTest()
		{
			var components = new ComponentCollection<Component>();
			var testComponent1 = new BaseTestComponent(1);
			var testComponent2 = new BaseTestComponent(2);
			components.Add(testComponent1);
			components.Add(testComponent2);
			Assert.AreEqual(2, components.Count);
			foreach (var c in components) {
				Assert.IsTrue(c == testComponent1 || c == testComponent2);
			}
			var cs = components.GetAll<BaseTestComponent>().ToList();
			Assert.AreEqual(components.Count, cs.Count);
			foreach (var c in cs) {
				Assert.IsTrue(c == testComponent1 || c == testComponent2);
			}
			var dummy = new DummyComponent();
			components.Add(dummy);
			Assert.IsTrue(components.Remove<BaseTestComponent>());
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
			var derivedTestComponent = new DerivedTestComponent(3, 4);
			components.Add(derivedTestComponent);
			Assert.AreEqual(3, components.Count);
			Assert.AreEqual(2, components.GetAll<BaseTestComponent>().Count());
			foreach (var c in components) {
				Assert.IsTrue(c == dummy || c is BaseTestComponent);
			}
			Assert.AreEqual(1, components.GetAll<DerivedTestComponent>().Count());
			Assert.IsTrue(components.Remove<BaseTestComponent>());
			Assert.AreEqual(0, components.GetAll<BaseTestComponent>().Count());
			Assert.AreEqual(1, components.GetAll<Component>().Count());
			Assert.ThrowsException<InvalidOperationException>(() => components.Add(new DummyComponent()));
		}

		private class DummyNodeComponent : NodeComponent
		{

		}

		[AllowMultiple]
		private class BaseTestNodeComponent : NodeComponent
		{
			public int Value { get; set; }

			public BaseTestNodeComponent(int value)
			{
				Value = value;
			}

			public override string ToString() => Value.ToString();
		}

		[AllowMultiple]
		private class DerivedTestNodeComponent : BaseTestNodeComponent
		{
			public DerivedTestNodeComponent(int value) : base(value) { }

			public override string ToString() => $"D{Value}";
		}

		[TestMethod]
		public void NodeComponentCollectionTest()
		{
			var node = new Widget();
			var components = node.Components;
			var dummy = new DummyNodeComponent();
			var test1 = new BaseTestNodeComponent(1);
			var test2 = new DerivedTestNodeComponent(2);
			components.Add(test1);
			components.Add(dummy);
			components.Add(test2);
			components.Remove<BaseTestNodeComponent>();
			Assert.IsTrue(dummy.Owner == node && test1.Owner == null && test2.Owner == null);
			components.Add(test1);
			components.Remove(dummy);
			Assert.IsTrue(dummy.Owner == null && test1.Owner == node && test2.Owner == null);
			components.Add(dummy);
			components.Add(test2);
			components.Remove(test2.GetType());
			Assert.IsTrue(dummy.Owner == node && test1.Owner == node && test2.Owner == null);
			components.Clear();
			Assert.IsTrue(dummy.Owner == null && test1.Owner == null && test2.Owner == null);
		}
	}
}
