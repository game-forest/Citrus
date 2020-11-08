using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Widgets
{
	[TestClass]
	public class WidgetTests
	{
		private Widget root;
		private Widget child1;
		private Widget child2;
		private Widget grandChild;

		[TestInitialize]
		public void TestSetUp()
		{
			root = new Widget { Id = "Root" };
			child1 = new Widget { Id = "Child1" };
			child2 = new Widget { Id = "Child2" };
			grandChild = new Widget { Id = "Grandchild" };
			root.AddNode(child1);
			root.AddNode(child2);
			child1.AddNode(grandChild);
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void WasClickedTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void DisposeTest()
		{
			var widgets = new List<Widget> {root, child1, child2, grandChild};
			foreach (var widget in widgets) {
				widget.Tasks.Add(EmptyTask);
				widget.LateTasks.Add(EmptyTask);
			}
			root.Dispose();
			foreach (var widget in widgets) {
				Assert.AreEqual(0, widget.Tasks.Count);
				Assert.AreEqual(0, widget.LateTasks.Count);
			}
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void RefreshLayoutTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void CalcContentSizeTest()
		{
			root.Size = Vector2.Half;
			Assert.That.AreEqual(Vector2.Half, root.CalcContentSize());
			root.Size = Vector2.One;
			Assert.That.AreEqual(Vector2.One, root.CalcContentSize());
		}

		[TestMethod]
		[Ignore("Need to implement reliable way to check cloned objects.")]
		public void DeepCloneFastTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void UpdateTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void RaiseUpdatingTest()
		{
			const float ExpectedDelta = 0.1f;
			var updatingRaised = false;
			var actualDelta = 0f;
			root.Updating += delta => {
				updatingRaised = true;
				actualDelta = delta;
			};
			root.RaiseUpdating(ExpectedDelta);
			Assert.IsTrue(updatingRaised);
			Assert.AreEqual(ExpectedDelta, actualDelta);
		}

		[TestMethod]
		public void RaiseUpdatedTest()
		{
			const float ExpectedDelta = 0.1f;
			var updatedRaised = false;
			var actualDelta = 0f;
			root.Updated += delta => {
				updatedRaised = true;
				actualDelta = delta;
			};
			root.RaiseUpdated(ExpectedDelta);
			Assert.IsTrue(updatedRaised);
			Assert.AreEqual(ExpectedDelta, actualDelta);
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void CalcLocalToParentTransformTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void StaticScaleTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void AddToRenderChainTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void IsMouseOverTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void HitTestTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void GetEffectiveLayerTest()
		{
			Assert.Fail();
		}

		private IEnumerator<object> EmptyTask()
		{
			yield return null;
		}
	}
}
