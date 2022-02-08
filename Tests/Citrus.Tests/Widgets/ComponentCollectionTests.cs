using System;
using System.Linq;
using Lime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Citrus.Tests.Widgets
{
	[TestClass]
	public class ComponentCollectionTests
	{
		private interface ISomeInterface { }

		private class BaseTestComponent : Component { }

		private class DerivedTestComponent : BaseTestComponent, ISomeInterface { }

		[AllowMultipleComponents]
		public class BaseAllowMultipleComponent : Component { }

		public class DerivedAllowMultipleComponent :
			BaseAllowMultipleComponent { }

		[AllowOnlyOneComponent]
		private class BaseAllowOnlyOneComponent : Component { }

		private class DerivedAllowOnlyOneComponent : BaseAllowOnlyOneComponent { }

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
				// Default behaviour: can't add multiple components of exactly the same type
				var base1 = new BaseTestComponent();
				var base2 = new BaseTestComponent();
				var derived1 = new DerivedTestComponent();
				var derived2 = new DerivedTestComponent();
				components.Add(base1);
				components.Add(derived1);
				Assert.AreEqual(2, components.Count);
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(derived2));
				Assert.ThrowsException<InvalidOperationException>(() => components.Add(base2));
				components.Clear();
			}
			{
				// Base is marked with AllowOnlyOneComponentAttribute that disallows additions of
				// multiple components of Base type or it's subtypes.
				var base1 = new BaseAllowOnlyOneComponent();
				var base2 = new BaseAllowOnlyOneComponent();
				var derived1 = new DerivedAllowOnlyOneComponent();
				var derived2 = new DerivedAllowOnlyOneComponent();
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
				// Base is marked with AllowMultipleComponentsAttribute that allows additions of
				// multiple components of Base type or it's subtypes.
				var base1 = new BaseAllowMultipleComponent();
				var base2 = new BaseAllowMultipleComponent();
				var derived1 = new DerivedAllowMultipleComponent();
				var derived2 = new DerivedAllowMultipleComponent();
				components.Add(base1);
				components.Add(base2);
				components.Add(derived1);
				components.Add(derived2);
			}
		}

		[TestMethod]
		public void CanAddTestTest()
		{
			var components = new ComponentCollection<Component>();
			{
				// Default behaviour: can't add multiple components of exactly the same type
				var base1 = new BaseTestComponent();
				var derived1 = new DerivedTestComponent();
				Assert.IsTrue(components.CanAdd<BaseTestComponent>());
				Assert.IsTrue(components.CanAdd<DerivedTestComponent>());
				Assert.IsTrue(components.CanAdd(typeof(BaseTestComponent)));
				Assert.IsTrue(components.CanAdd(typeof(DerivedTestComponent)));
				components.Add(base1);
				components.Add(derived1);
				Assert.AreEqual(2, components.Count);
				Assert.IsFalse(components.CanAdd<BaseTestComponent>());
				Assert.IsFalse(components.CanAdd<DerivedTestComponent>());
				Assert.IsFalse(components.CanAdd(typeof(BaseTestComponent)));
				Assert.IsFalse(components.CanAdd(typeof(DerivedTestComponent)));
				components.Clear();
			}
			{
				// Base is marked with AllowOnlyOneComponentAttribute that disallows additions of
				// multiple components of Base type or it's subtypes.
				var base1 = new BaseAllowOnlyOneComponent();
				var derived1 = new DerivedAllowOnlyOneComponent();
				Assert.IsTrue(components.CanAdd<BaseAllowOnlyOneComponent>());
				Assert.IsTrue(components.CanAdd<DerivedAllowOnlyOneComponent>());
				Assert.IsTrue(components.CanAdd(typeof(BaseAllowOnlyOneComponent)));
				Assert.IsTrue(components.CanAdd(typeof(DerivedAllowOnlyOneComponent)));
				components.Add(base1);
				Assert.IsFalse(components.CanAdd<BaseAllowOnlyOneComponent>());
				Assert.IsFalse(components.CanAdd<DerivedAllowOnlyOneComponent>());
				Assert.IsFalse(components.CanAdd(typeof(BaseAllowOnlyOneComponent)));
				Assert.IsFalse(components.CanAdd(typeof(DerivedAllowOnlyOneComponent)));
				components.Clear();
				components.Add(derived1);
				Assert.IsFalse(components.CanAdd<BaseAllowOnlyOneComponent>());
				Assert.IsFalse(components.CanAdd<DerivedAllowOnlyOneComponent>());
				Assert.IsFalse(components.CanAdd(typeof(BaseAllowOnlyOneComponent)));
				Assert.IsFalse(components.CanAdd(typeof(DerivedAllowOnlyOneComponent)));
				components.Clear();
			}
			{
				// Base is marked with AllowMultipleComponentsAttribute that allows additions of
				// multiple components of Base type or it's subtypes.
				var base1 = new BaseAllowMultipleComponent();
				var base2 = new BaseAllowMultipleComponent();
				var derived1 = new DerivedAllowMultipleComponent();
				var derived2 = new DerivedAllowMultipleComponent();
				Assert.IsTrue(components.CanAdd<BaseAllowMultipleComponent>());
				Assert.IsTrue(components.CanAdd<DerivedAllowMultipleComponent>());
				Assert.IsTrue(components.CanAdd(typeof(BaseAllowMultipleComponent)));
				Assert.IsTrue(components.CanAdd(typeof(DerivedAllowMultipleComponent)));
				components.Add(base1);
				components.Add(base2);
				components.Add(derived1);
				components.Add(derived2);
				Assert.IsTrue(components.CanAdd<BaseAllowMultipleComponent>());
				Assert.IsTrue(components.CanAdd<DerivedAllowMultipleComponent>());
				Assert.IsTrue(components.CanAdd(typeof(BaseAllowMultipleComponent)));
				Assert.IsTrue(components.CanAdd(typeof(DerivedAllowMultipleComponent)));
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
			Assert.AreEqual(derived, components.Get<ISomeInterface>());
			Assert.AreEqual(derived, components.Get(typeof(ISomeInterface)));
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
			{
				var cs = components.GetAll<ISomeInterface>().ToList();
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(1, cs.Count);
				cs.Clear();
				components.GetAll(cs);
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(1, cs.Count);
			}
			{
				var cs = components.GetAll(typeof(ISomeInterface)).ToList();
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(1, cs.Count);
				cs.Clear();
				components.GetAll(typeof(ISomeInterface), cs);
				CollectionAssert.Contains(cs, derived);
				Assert.AreEqual(1, cs.Count);
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
			Assert.IsTrue(components.Contains<ISomeInterface>());
			Assert.IsTrue(components.Contains(typeof(BaseTestComponent)));
			Assert.IsTrue(components.Contains(typeof(DerivedTestComponent)));
			Assert.IsTrue(components.Contains(typeof(DummyComponent)));
			Assert.IsTrue(components.Contains(typeof(Component)));
			Assert.IsTrue(components.Contains(typeof(ISomeInterface)));
			Assert.IsFalse(components.Contains<BaseAllowMultipleComponent>());
			Assert.IsFalse(components.Contains(typeof(BaseAllowMultipleComponent)));
		}

		[TestMethod]
		public void RemoveTest()
		{
			var components = new ComponentCollection<Component>();
			var @base = new BaseTestComponent();
			var dervied = new DerivedTestComponent();
			var dummyComponent = new DummyComponent();
			Assert.IsFalse(components.Remove<Component>());
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
			components.Add(@base);
			components.Add(dummyComponent);
			components.Add(dervied);
			Assert.IsTrue(components.Remove<ISomeInterface>());
			Assert.IsFalse(components.Remove(typeof(ISomeInterface)));
			Assert.AreEqual(2, components.Count);
		}
		private class DummyNodeComponent : NodeComponent { }

		[AllowMultipleComponents]
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

		[TestMethod]
		public void OnRemoveWithSideEffectTest()
		{
			var node = new Widget();
			var components = node.Components;

			{
				var weird = new RemoveComponentOnRemoveComponent();
				components.Add(weird);
				components.Add(new BaseTestNodeComponent());
				Assert.IsTrue(components.Remove<RemoveComponentOnRemoveComponent>());
				Assert.IsFalse(components.Remove<BaseTestNodeComponent>());
				Assert.AreEqual(0, components.Count);
			}

			{
				var weird = new RemoveComponentOnAddComponent();
				components.Add(new BaseTestNodeComponent());
				Assert.AreEqual(1, components.Count);
				components.Add(weird);
				Assert.AreEqual(1, components.Count);
				Assert.IsFalse(components.Remove<BaseTestNodeComponent>());
				Assert.IsTrue(components.Remove<RemoveComponentOnAddComponent>());
			}

			{
				var weird = new AddComponentOnRemoveComponent();
				components.Add(weird);
				Assert.IsTrue(components.Remove(weird));
				Assert.AreEqual(1, components.Count);
				Assert.IsTrue(components.Contains<BaseTestNodeComponent>());
				components.Add(weird);
				components.Clear();
				Assert.AreEqual(0, components.Count);
				components.Add(weird);
				Assert.IsTrue(components.Remove<Component>());
				Assert.AreEqual(0, components.Count);
				components.Add(weird);
				components.Add(new DummyNodeComponent());
				Assert.IsTrue(components.Remove<AddComponentOnRemoveComponent>());
				Assert.IsTrue(components.Remove<DummyNodeComponent>());
				Assert.IsTrue(components.Remove<BaseTestNodeComponent>());
			}

			{
				var weird = new AddComponentOnAddComponent();
				components.Add(weird);
				Assert.AreEqual(2, components.Count);
				Assert.IsTrue(components.Remove<AddComponentOnAddComponent>());
				Assert.IsTrue(components.Remove<BaseTestNodeComponent>());
			}
		}

		public class RemoveComponentOnRemoveComponent : NodeComponent
		{
			protected override void OnOwnerChanged(Node oldOwner)
			{
				base.OnOwnerChanged(oldOwner);
				if (oldOwner != null) {
					oldOwner.Components.Remove<BaseTestNodeComponent>();
				}
			}
		}
		public class RemoveComponentOnAddComponent : NodeComponent
		{
			protected override void OnOwnerChanged(Node oldOwner)
			{
				base.OnOwnerChanged(oldOwner);
				if (oldOwner == null && Owner != null) {
					Owner.Components.Remove<BaseTestNodeComponent>();
				}
			}
		}

		public class AddComponentOnAddComponent : NodeComponent
		{
			protected override void OnOwnerChanged(Node oldOwner)
			{
				base.OnOwnerChanged(oldOwner);
				if (oldOwner == null && Owner != null) {
					Owner.Components.Add(new BaseTestNodeComponent());
				}
			}
		}

		public class AddComponentOnRemoveComponent : NodeComponent
		{
			protected override void OnOwnerChanged(Node oldOwner)
			{
				base.OnOwnerChanged(oldOwner);
				if (oldOwner != null) {
					oldOwner.Components.Add(new BaseTestNodeComponent());
				}
			}
		}
	}
}
