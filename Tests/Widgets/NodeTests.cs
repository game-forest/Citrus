using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests.Widgets
{
	[TestClass]
	public class NodeTests
	{
		private Node root;
		private Node child1;
		private Node child2;
		private Node grandChild;

		[TestInitialize]
		public void TestSetUp()
		{
			root = new TestNode {Id = "Root"};
			child1 = new TestNode { Id = "Child1"};
			child2 = new TestNode { Id = "Child2"};
			grandChild = new TestNode { Id = "Grandchild"};
			root.AddNode(child1);
			root.AddNode(child2);
			child1.AddNode(grandChild);
		}

		[TestMethod]
		public void DisposeTest()
		{
			root.Dispose();
			Assert.AreEqual(0, root.Nodes.Count);
			Assert.AreEqual(0, child1.Nodes.Count);
		}

		[TestMethod]
		public void GetRootTest()
		{
			Assert.AreSame(root, root.GetRoot());
			Assert.AreSame(root, child1.GetRoot());
			Assert.AreSame(root, grandChild.GetRoot());
		}

		private const string AnimationName = "Animation";
		private const string MarkerName = "Marker";

		[TestMethod]
		public void TryRunAnimationWithoutAnimationTest()
		{
			Assert.IsFalse(root.TryRunAnimation(MarkerName));
			Assert.IsFalse(root.TryRunAnimation(MarkerName, AnimationName));
		}

		[TestMethod]
		public void TryRunAnimationWithMarkerTest()
		{
			Marker marker;
			root.DefaultAnimation.Markers.Add(marker = new Marker(MarkerName, 0, MarkerAction.Play));
			Assert.IsFalse(root.TryRunAnimation(MarkerName, AnimationName));
			Assert.IsTrue(root.TryRunAnimation(MarkerName));
			Assert.AreEqual(MarkerName, root.CurrentAnimation);
			root.DefaultAnimation.Markers.Remove(marker);
			CollectionAssert.DoesNotContain(root.DefaultAnimation.Markers.ToArray(), marker);
		}

		[TestMethod]
		public void TryRunAnimationWithMarkerAndIdTest()
		{
			var animation = new Animation() { Id = "Animation" };
			animation.Markers.Add(new Marker(MarkerName, 0, MarkerAction.Play));
			root.Animations.Add(animation);
			Assert.IsTrue(root.TryRunAnimation(MarkerName, AnimationName));
			Assert.AreEqual(MarkerName, animation.RunningMarkerId);
			root.Animations.Remove(animation);
			CollectionAssert.DoesNotContain(root.Animations.ToArray(), animation);
		}

		[TestMethod]
		public void RunAnimationWithoutAnimationTest()
		{
			Assert.ThrowsException<Exception>(() => root.RunAnimation(MarkerName));
			Assert.ThrowsException<Exception>(() => root.RunAnimation(MarkerName, AnimationName));
		}

		[TestMethod]
		public void RunAnimationWithMarkerTest()
		{
			root.DefaultAnimation.Markers.Add(new Marker(MarkerName, 0, MarkerAction.Play));
			root.RunAnimation(MarkerName);
			Assert.AreEqual(MarkerName, root.CurrentAnimation);
			Assert.ThrowsException<Exception>(() => root.RunAnimation(MarkerName, AnimationName));
		}

		[TestMethod]
		public void RunAnimationWithMarkerAndIdTest()
		{
			var animation = new Animation {
				Id = AnimationName
			};
			animation.Markers.Add(new Marker(MarkerName, 0, MarkerAction.Play));
			root.Animations.Add(animation);
			root.RunAnimation(MarkerName, AnimationName);
			Assert.AreEqual(MarkerName, animation.RunningMarkerId);
		}

		[TestMethod]
		[Ignore("Need to implement reliable way to check cloned objects.")]
		public void DeepCloneSafeTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Need to implement reliable way to check cloned objects.")]
		public void DeepCloneSafeTest1()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Need to implement reliable way to check cloned objects.")]
		public void DeepCloneFastTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		[Ignore("Need to implement reliable way to check cloned objects.")]
		public void DeepCloneFastTest1()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void ToStringTest()
		{
			root.Id = "";
			child1.Tag = "Special";
			Assert.AreEqual("TestNode", root.ToString());
			Assert.AreEqual("'Child1' (Special) in TestNode", child1.ToString());
			Assert.AreEqual("'Grandchild' in 'Child1' (Special) in TestNode", grandChild.ToString());
		}

		[TestMethod]
		public void UnlinkTest()
		{
			child1.Unlink();
			CollectionAssert.DoesNotContain(root.Nodes.ToArray(), child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child2);
		}

		[TestMethod]
		public void UnlinkAndDisposeTest()
		{
			child1.UnlinkAndDispose();
			CollectionAssert.DoesNotContain(root.Nodes.ToArray(), child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child2);
			Assert.AreEqual(0, child1.Nodes.Count);
		}

		[TestMethod]
		public void UpdateTest()
		{
			var nodes = new List<Node> {root, child1, grandChild};
			foreach (var node in nodes) {
				node.DefaultAnimation.Markers.Add(new Marker("Start", 0, MarkerAction.Play));
				node.RunAnimation("Start");
			}
			const float FrameDelta = (float)AnimationUtils.SecondsPerFrame;
			var animationFrames = nodes.Select(node => node.AnimationFrame);
			for (var i = 0; i < 10; i++) {
				root.Update(FrameDelta);
				Assert.IsTrue(animationFrames.All((f) => i + 1 == f));
			}
		}

		[TestMethod]
		public void AddNodeTest()
		{
			root = new TestNode { Id = "Parent" };
			child1 = new TestNode { Id = "Child1" };
			child2 = new TestNode { Id = "Child2" };
			root.AddNode(child1);
			root.AddNode(child2);
			CollectionAssert.Contains(root.Nodes.ToArray(), child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child2);
			Assert.IsTrue(root.Nodes.IndexOf(child1) < root.Nodes.IndexOf(child2));
		}

		[TestMethod]
		public void AddToNodeTest()
		{
			root = new TestNode { Id = "Parent" };
			child1 = new TestNode { Id = "Child1" };
			child2 = new TestNode { Id = "Child2" };
			child1.AddToNode(root);
			child2.AddToNode(root);
			CollectionAssert.Contains(root.Nodes.ToArray(), child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child2);
			Assert.IsTrue(root.Nodes.IndexOf(child1) < root.Nodes.IndexOf(child2));
		}

		[TestMethod]
		public void PushNodeTest()
		{
			root = new TestNode { Id = "Parent" };
			child1 = new TestNode { Id = "Child1" };
			child2 = new TestNode { Id = "Child2" };
			root.PushNode(child2);
			root.PushNode(child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child2);
			Assert.IsTrue(root.Nodes.IndexOf(child1) < root.Nodes.IndexOf(child2));
		}

		[TestMethod]
		public void PushToNodeTest()
		{
			root = new TestNode { Id = "Parent" };
			child1 = new TestNode { Id = "Child1" };
			child2 = new TestNode { Id = "Child2" };
			child2.PushToNode(root);
			child1.PushToNode(root);
			CollectionAssert.Contains(root.Nodes.ToArray(), child1);
			CollectionAssert.Contains(root.Nodes.ToArray(), child2);
			Assert.IsTrue(root.Nodes.IndexOf(child1) < root.Nodes.IndexOf(child2));
		}

		[TestMethod]
		public void FindTest()
		{
			Assert.AreSame(grandChild, root.Find<Node>("Grandchild"));
			Assert.AreSame(child2, root.Find<Node>("Child{0}", 2));
			Assert.AreSame(grandChild, root.Find<Node>("Child{0}/Grandchild", 1));
			var e = Assert.ThrowsException<Exception>(() => grandChild.Find<Node>("Root"));
			Assert.AreEqual("'Root' of Node not found for ''Grandchild' in 'Child1' in 'Root''", e.Message);
		}

		[TestMethod]
		public void TryFindTest()
		{
			Assert.AreSame(grandChild, root.TryFind<Node>("Grandchild"));
			Assert.AreSame(child1, root.TryFind<Node>("Child{0}", 1));
			Node node;
			Assert.IsFalse(root.TryFind("Child2/Grandchild", out node));
			Assert.IsNull(node);
			Assert.IsTrue(root.TryFind("Child1/Grandchild", out node));
			Assert.AreSame(grandChild, node);
			Assert.IsNull(grandChild.TryFind<Node>("Root"));
		}

		[TestMethod]
		public void FindNodeTest()
		{
			Assert.AreSame(grandChild, root.FindNode("Grandchild"));
			Assert.AreSame(grandChild, root.FindNode("Child1/Grandchild"));
			var e = Assert.ThrowsException<Exception>(() => grandChild.FindNode("Root"));
			Assert.AreEqual("'Root' not found for ''Grandchild' in 'Child1' in 'Root''", e.Message);
		}

		[TestMethod]
		public void TryFindNodeTest()
		{
			Assert.AreSame(grandChild, root.TryFindNode("Grandchild"));
			Assert.AreSame(grandChild, root.TryFindNode("Child1/Grandchild"));
			Assert.IsNull(grandChild.TryFindNode("Root"));
		}

		[TestMethod]
		public void DescendatsTest()
		{
			CollectionAssert.Contains(root.Descendants.ToArray(), child1);
			CollectionAssert.Contains(root.Descendants.ToArray(), child2);
			CollectionAssert.Contains(root.Descendants.ToArray(), grandChild);
			CollectionAssert.Contains(child1.Descendants.ToArray(), grandChild);
			Assert.AreEqual(0, grandChild.Descendants.Count());
		}

		[TestMethod]
		[Ignore("Wait until development on this function stops.")]
		public void StaticScaleTest()
		{
			Assert.Fail();
		}

		[TestMethod]
		public void AdvanceAnimationTest()
		{
			var node = new TestNode();
			node.DefaultAnimation.Markers.Add(new Marker("Start", 0, MarkerAction.Play));
			node.RunAnimation("Start");
			const float FrameDelta = (float)(AnimationUtils.SecondsPerFrame);
			for (var i = 0; i < 100000; i++) {
				node.DefaultAnimation.Advance(FrameDelta);
				// this line was just i without + 1 before tests were ported to mstest
				Assert.AreEqual(i + 1, node.AnimationFrame);
			}
		}

		[TestMethod]
		public void PreloadAssetsTest()
		{
			var node = new NodeWithAssets();
			node.PreloadAssets();
		}

		private class NodeWithAssets : Widget
		{
			public ITexture Texture
			{ get; set; }
			public SerializableFont Font
			{ get; set; }
		}

		private class NodeWithSideEffects : Widget
		{
			public override void AddToRenderChain(RenderChain chain)
			{
				base.AddToRenderChain(chain);
				chain.Add(this, Presenter);
			}
		}
	}
}
