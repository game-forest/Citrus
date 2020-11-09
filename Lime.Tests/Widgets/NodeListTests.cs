using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Widgets
{
	[TestClass]
	public class NodeListTests
	{
		[TestMethod]
		public void IsReadOnlyTest()
		{
			Assert.IsFalse(new NodeList(new TestNode()).IsReadOnly);
		}

		[TestMethod]
		public void IndexOfTest()
		{
			var list = new NodeList(new TestNode());
			var node = new TestNode();
			Assert.AreEqual(-1, list.IndexOf(node));
			list.Add(node);
			Assert.AreEqual(0, list.IndexOf(node));
		}

		[TestMethod]
		public void CopyToEmptyTest()
		{
			var list = new NodeList(new TestNode());
			Node[] array = new Node[3];
			list.CopyTo(array, 0);
			CollectionAssert.Contains(array, null);
			Assert.AreEqual(1, array.Distinct().Count());
		}

		[TestMethod]
		public void CopyToNotEmptyTest()
		{
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var list = new NodeList(new TestNode()) { node1, node2, node3 };
			Node[] array = new Node[3];
			list.CopyTo(array, 0);
			CollectionAssert.AreEquivalent(list.ToArray(), array);
		}

		[TestMethod]
		public void SortEmptyTest()
		{
			var list = new NodeList(new TestNode());
			list.Sort((first, second) => 0);
			Assert.AreEqual(0, list.Count);
		}

		[TestMethod]
		public void SortNotEmptyTest()
		{
			var node1 = new Widget { Layer = 3 };
			var node2 = new Widget { Layer = 2 };
			var node3 = new Widget { Layer = 1 };
			var list = new NodeList(new TestNode()) { node1, node2, node3 };
			Comparison<Node> comparison = (first, second) => first.Layer <= second.Layer ? 1 : 0;
			list.Sort(comparison);
			var expectedList = new List<Node> { node3, node2, node1 };
			CollectionAssert.AreEquivalent(expectedList.ToArray(), list.ToArray());
		}

		[TestMethod]
		public void ContainsTest()
		{
			var list = new NodeList(new TestNode());
			var node = new TestNode();
			Assert.IsFalse(list.Contains(node));
			list.Add(node);
			Assert.IsTrue(list.Contains(node));
		}

		[TestMethod]
		public void EnumeratorEmptyTest()
		{
			var list = new NodeList(new TestNode());
			var enumerator = list.GetEnumerator();
			Assert.IsNull(enumerator.Current);
			Assert.IsFalse(enumerator.MoveNext());
		}

		[TestMethod]
		public void EnumeratorNotEmptyTest()
		{
			var node1 = new Widget { Layer = 3 };
			var node2 = new Widget { Layer = 2 };
			var node3 = new Widget { Layer = 1 };
			var list = new NodeList(new TestNode()) { node1, node2, node3 };
			var enumerator = list.GetEnumerator();
			for (int i = 0; i < 10; i++) {
				Assert.IsNull(enumerator.Current);
				foreach (var node in new List<Node> {node1, node2, node3}) {
					Assert.IsTrue(enumerator.MoveNext());
					Assert.AreEqual(node, enumerator.Current);
				}
				Assert.IsFalse(enumerator.MoveNext());
				// BUG: This expression is not mandatory
				enumerator.Reset();
			}
		}

		[TestMethod]
		public void PushTest()
		{
			var owner = new TestNode();
			var list = new NodeList(owner);
			var node1 = new TestNode { Id = "Node1" };
			list.Push(node1);
			Assert.AreSame(list[0], node1);
			Assert.AreSame(node1.Parent, owner);
			Assert.IsNull(node1.NextSibling);
			var node2 = new TestNode { Id = "Node2" };
			list.Push(node2);
			Assert.AreSame(list[0], node2);
			Assert.AreSame(node2.NextSibling, node1);
			Assert.IsNull(node1.NextSibling);
			Assert.AreSame(node2.Parent, owner);
		}

		[TestMethod]
		public void AddTest()
		{
			var owner = new TestNode();
			var list = new NodeList(owner);
			var node1 = new TestNode { Id = "Node1" };
			list.Add(node1);
			Assert.AreSame(list.Last(), node1);
			Assert.AreSame(node1.Parent, owner);
			Assert.IsNull(node1.NextSibling);
			var node2 = new TestNode { Id = "Node2" };
			list.Add(node2);
			Assert.AreSame(list.Last(), node2);
			Assert.AreSame(node1.NextSibling, node2);
			Assert.IsNull(node2.NextSibling);
			Assert.AreSame(node2.Parent, owner);
		}

		[TestMethod]
		public void AddAdoptedNodeTest()
		{
			var owner = new TestNode();
			var list = new NodeList(owner);
			var node = new TestNode();
			list.Add(node);
			var e = Assert.ThrowsException<ArgumentException>(() => list.Add(node));
			Assert.AreEqual("Can't adopt a node twice. Call node.Unlink() first", e.Message);
		}

		[TestMethod]
		public void AddRangeTest()
		{
			var owner = new TestNode();
			var list = new NodeList(owner);
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var insertingList = new List<Node>{ node1, node2, node3 };
			list.AddRange(insertingList);
			for (int i = 0; i < list.Count; i++) {
				Assert.AreSame(list[i], insertingList[i]);
				var nextSibling = i < list.Count - 1 ? list[i + 1] : null;
				Assert.AreSame(list[i].NextSibling, nextSibling);
			}
			foreach (var node in list) {
				Assert.AreSame(owner, node.Parent);
			}
		}

		[TestMethod]
		public void FirstOrDefaultEmptyTest()
		{
			var list = new NodeList(new TestNode());
			Assert.IsNull(list.FirstOrDefault());
		}

		[TestMethod]
		public void FirstOrDefaultNotEmptyTest()
		{
			var node = new TestNode();
			var list = new NodeList(new TestNode()) {node};
			Assert.AreSame(node, list.FirstOrDefault());
		}

		[TestMethod]
		public void InsertTest()
		{
			var owner = new TestNode();
			var list = new NodeList(owner);
			var node1 = new TestNode {Id = "Node1"};
			list.Insert(0, node1);
			Assert.AreSame(list[0], node1);
			Assert.AreSame(node1.Parent, owner);
			Assert.IsNull(node1.NextSibling);
			var node2 = new TestNode { Id = "Node2"};
			list.Insert(1, node2);
			Assert.AreSame(list[1], node2);
			Assert.AreSame(node1.NextSibling, node2);
			Assert.IsNull(node2.NextSibling);
			Assert.AreSame(node2.Parent, owner);
		}

		[TestMethod]
		public void RemoveEmptyTest()
		{
			var list = new NodeList(new TestNode());
			Assert.IsFalse(list.Remove(null));
		}

		[TestMethod]
		public void RemoveNotEmptyTest()
		{
			var owner = new TestNode();
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var list = new NodeList(owner) { node1, node2, node3 };
			Assert.IsTrue(list.Remove(node2));
			CollectionAssert.Contains(list.ToArray(), node1);
			CollectionAssert.DoesNotContain(list.ToArray(), node2);
			Assert.IsTrue(list.Contains(node3));
			Assert.AreSame(node3, node1.NextSibling);
			Assert.IsNull(node2.Parent);
			Assert.IsNull(node2.NextSibling);
		}

		[TestMethod]
		public void ClearEmptyTest()
		{
			var list = new NodeList(new TestNode());
			list.Clear();
			Assert.AreEqual(0, list.Count);
		}

		[TestMethod]
		public void ClearNotEmptyTest()
		{
			var owner = new TestNode();
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var nodes = new List<Node> {node1, node2, node3};
			var list = new NodeList(owner) {node1, node2, node3};
			list.Clear();
			Assert.AreEqual(0, list.Count);
			foreach (var node in nodes) {
				Assert.IsNull(node.Parent);
				Assert.IsNull(node.NextSibling);
			}
		}

		[TestMethod]
		public void TryFindTest()
		{
			var list = new NodeList(new TestNode());
			var node1 = new TestNode { Id = "Node1" };
			var node2 = new TestNode { Id = "Node2" };
			Assert.IsNull(list.TryFind("Node1"));
			Assert.IsNull(list.TryFind("Node2"));
			list.Add(node1);
			list.Add(node2);
			Assert.AreSame(list.TryFind("Node1"), node1);
			Assert.AreSame(list.TryFind("Node2"), node2);
		}

		[TestMethod]
		public void RemoveAtEmptyTest()
		{
			var list = new NodeList(new TestNode());
			Assert.ThrowsException<IndexOutOfRangeException>(() => list.RemoveAt(0));
		}

		[TestMethod]
		public void RemoveAtNotEmptyTest()
		{
			var owner = new TestNode();
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var list = new NodeList(owner) { node1, node2, node3 };
			list.RemoveAt(1);
			CollectionAssert.Contains(list.ToArray(), node1);
			CollectionAssert.DoesNotContain(list.ToArray(), node2);
			CollectionAssert.Contains(list.ToArray(), node3);
			Assert.AreSame(node3, node1.NextSibling);
			Assert.IsNull(node2.Parent);
			Assert.IsNull(node2.NextSibling);
		}

		[TestMethod]
		public void IndexerGetEmptyTest()
		{
			var list = new NodeList(new TestNode());
			Node node;
			Assert.ThrowsException<IndexOutOfRangeException>(() => node = list[0]);
		}

		[TestMethod]
		public void IndexerGetNotEmptyTest()
		{
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var list = new NodeList(new TestNode()) { node1, node2, node3 };
			Node node;
			Assert.ThrowsException<ArgumentOutOfRangeException>(() => node = list[-1]);
			Assert.ThrowsException<ArgumentOutOfRangeException>(() => node = list[3]);
			Assert.AreSame(node2, list[1]);
		}

		[TestMethod]
		public void IndexerSetEmptyTest()
		{
			var list = new NodeList(new TestNode());
			Assert.ThrowsException<IndexOutOfRangeException>(() => list[0] = new TestNode());
		}

		[TestMethod]
		public void IndexerSetNotEmptyTest()
		{
			var owner = new TestNode();
			var node1 = new TestNode();
			var node2 = new TestNode();
			var node3 = new TestNode();
			var list = new NodeList(owner) { node1, node2, node3 };
			var newNode = new TestNode();
			list[1] = newNode;
			Assert.AreEqual(1, list.IndexOf(newNode));
			CollectionAssert.DoesNotContain(list.ToArray(), node2);
			Assert.AreSame(node1.NextSibling, newNode);
			Assert.AreSame(newNode.NextSibling, node3);
			Assert.AreSame(newNode.Parent, owner);
			Assert.IsNull(node2.Parent);
			Assert.IsNull(node2.NextSibling);
		}
	}
}
