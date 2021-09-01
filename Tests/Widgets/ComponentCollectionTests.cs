using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;
using System;

namespace Citrus.Tests.Widgets
{
	[TestClass]
	public class ComponentCollectionTests
	{
		[ComponentSettings(AllowMultiple = true, StartEquivalenceClass = false)]
		private class BaseTestComponent : Component { }

		private class DerivedTestComponent : BaseTestComponent { }

		[ComponentSettings(StartEquivalenceClass = true, AllowMultiple = true)]
		public class BaseEqualityClassStarterAllowMultipleComponent : Component { }

		public class DerivedEqualityClassStarterAllowMultipleComponent :
			BaseEqualityClassStarterAllowMultipleComponent { }

		[ComponentSettings(StartEquivalenceClass = true, AllowMultiple = false)]
		public class BaseEqualityClassStarterComponent : Component { }

		public class DerivedEqualityClassStarterComponent : BaseEqualityClassStarterComponent { }

		private class DummyComponent : Component
		{

		}

		[TestMethod]
		public void AddTest()
		{
			var components = new ComponentCollection<Component>();
			var testComponent1 = new BaseTestComponent();
			var testComponent2 = new DummyComponent();
			components.Add(testComponent1);
			components.Add(testComponent2);
			Assert.AreEqual(2, components.Count);
			foreach (var c in components) {
				Assert.IsTrue(c == testComponent1 || c == testComponent2);
			}
			Assert.ThrowsException<InvalidOperationException>(() => components.Add(testComponent1));
		}

		[TestMethod]
		public void MultipleComponentsOfSameTypeAddTest()
		{
			var components = new ComponentCollection<Component>();
			{
				// Can add base mutiple times but that doesn't apply to dervied
				// because they're in the separate equality classes.
				var base1 = new BaseTestComponent();
				var base2 = new BaseTestComponent();
				var derived1 = new DerivedTestComponent();
				var derived2 = new DerivedTestComponent();
				components.Add(base1);
				components.Add(base2);
				components.Add(derived1);
				Assert.AreEqual(3, components.Count);
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(derived2));
				components.Clear();
			}
			{
				// Base declares equality class and spreads along inheritance hierarchy.
				// Can't add both base and derived as well as 2 base or 2 derived.
				var base1 = new BaseEqualityClassStarterComponent();
				var base2 = new BaseEqualityClassStarterComponent();
				var derived1 = new DerivedEqualityClassStarterComponent();
				var derived2 = new DerivedEqualityClassStarterComponent();
				components.Add(base1);
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(derived1));
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(base2));
				components.Clear();
				components.Add(derived1);
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(derived2));
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(base1));
				components.Clear();
			}
			{
				// Base declares equality class, spreads along inheritance hierarchy
				// and allows to add multiple components of declared equality class.
				// Can add 2 base and 2 dervied.
				var base1 = new BaseEqualityClassStarterAllowMultipleComponent();
				var base2 = new BaseEqualityClassStarterAllowMultipleComponent();
				var derived1 = new DerivedEqualityClassStarterAllowMultipleComponent();
				var derived2 = new DerivedEqualityClassStarterAllowMultipleComponent();
				components.Add(base1);
				components.Add(base2);
				components.Add(derived1);
				components.Add(derived2);

			}
		}

		[TestMethod]
		public void GetTest()
		{
			var components = new ComponentCollection<Component>();
			var @base = new BaseTestComponent();
			var derived = new DerivedTestComponent();
			components.Add(derived);
			components.Add(@base);
			// This assert uses knowledge of internal structure of ComponentsCollection
			// and demonstrates that user should use Get wisely.
			Assert.AreEqual(derived, components.Get<BaseTestComponent>());
			Assert.AreEqual(derived, components.Get(typeof(BaseTestComponent)));
			Assert.AreEqual(derived, components.Get<DerivedTestComponent>());
			Assert.AreEqual(derived, components.Get(typeof(DerivedTestComponent)));
		}

		[TestMethod]
		public void GetAllTest()
		{
			var components = new ComponentCollection<Component>();
			var @base = new BaseTestComponent();
			var dummy = new DummyComponent();
			var derived = new DerivedTestComponent();
			components.Add(derived);
			components.Add(dummy);
			components.Add(@base);
			{
				var cs = components.GetAll<BaseTestComponent>().ToList();
				CollectionAssert.Contains(cs, @base);
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(2, cs.Count);
				cs.Clear();
				components.GetAll(cs);
				CollectionAssert.Contains(cs, @base);
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(2, cs.Count);
			}
			{
				var cs = components.GetAll(typeof(BaseTestComponent)).ToList();
				CollectionAssert.Contains(cs, @base);
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(2, cs.Count);
				cs.Clear();
				components.GetAll(typeof(BaseTestComponent), cs);
				CollectionAssert.Contains(cs, @base);
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(2, cs.Count);
			}

		}

		[TestMethod]
		public void ContainsTest()
		{
			var components = new ComponentCollection<Component>();
			var dummy = new DummyComponent();
			var derived = new DerivedTestComponent();
			components.Add(derived);
			components.Add(dummy);
			Assert.IsTrue(components.Contains<BaseTestComponent>());
			Assert.IsTrue(components.Contains<DerivedTestComponent>());
			Assert.IsTrue(components.Contains<DummyComponent>());
			Assert.IsTrue(components.Contains<Component>());
			Assert.IsTrue(components.Contains(typeof(BaseTestComponent)));
			Assert.IsTrue(components.Contains(typeof(DerivedTestComponent)));
			Assert.IsTrue(components.Contains(typeof(DummyComponent)));
			Assert.IsTrue(components.Contains(typeof(Component)));
			Assert.IsFalse(components.Contains<BaseEqualityClassStarterAllowMultipleComponent>());
			Assert.IsFalse(components.Contains(typeof(BaseEqualityClassStarterAllowMultipleComponent)));
		}

		[TestMethod]
		public void RemoveTest()
		{
			var components = new ComponentCollection<Component>();
			var @base = new BaseTestComponent();
			var dervied = new DerivedTestComponent();
			var dummyComponent = new DummyComponent();
			components.Add(@base);
			components.Add(dummyComponent);
			components.Add(dervied);
			Assert.IsTrue(components.Remove(dervied));
			Assert.AreEqual(2, components.Count);
			components.Add(dervied);
			Assert.IsTrue(components.Remove(@base.GetType()));
			Assert.AreEqual(1, components.Count);
			components.Add(@base);
			components.Add(dervied);
			Assert.IsTrue(components.Remove<Component>());
			Assert.AreEqual(0, components.Count);
			components.Add(@base);
			components.Add(dummyComponent);
			components.Add(dervied);
			components.Clear();
			Assert.AreEqual(0, components.Count);
		}
		private class DummyNodeComponent : NodeComponent { }

		[ComponentSettings(AllowMultiple = true, StartEquivalenceClass = true)]
		private class BaseTestNodeComponent : NodeComponent { }


		private class DerivedTestNodeComponent : BaseTestNodeComponent { }

		[TestMethod]
		public void NodeComponentCollectionOnRemoveTest()
		{
			var node = new Widget();
			var components = node.Components;
			var dummy = new DummyNodeComponent();
			var test1 = new BaseTestNodeComponent();
			var test2 = new DerivedTestNodeComponent();
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
