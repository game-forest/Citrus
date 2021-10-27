using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Running;
using Lime;

namespace Benchmarks
{
	public static class RandomExtensions
	{
		public static void Shuffle<T>(this Random rng, T[] array)
		{
			int n = array.Length;
			while (n > 1) {
				int k = rng.Next(n--);
				T temp = array[n];
				array[n] = array[k];
				array[k] = temp;
			}
		}
	}

	public class NodeComponentsBenchmark
	{

		private static Func<NodeComponent>[] multipleAddComponentsFactories2 = new Func<NodeComponent>[] {
			() => new XComponent(),
			() => new XComponent(),
			() => new XComponent(),
			() => new XComponent(),
			() => new YComponent(),
			() => new YComponent(),
			() => new YComponent(),
			() => new YComponent(),
			() => new ZComponent(),
			() => new ZComponent(),
			() => new ZComponent(),
			() => new ZComponent(),
		};

		[AllowMultipleComponents]
		private class XComponent : NodeComponent
		{

		}

		[AllowMultipleComponents]
		private class YComponent : NodeComponent
		{

		}

		[AllowMultipleComponents]
		private class ZComponent : NodeComponent
		{

		}


		const int nodeCount = 100000;
		private Node[] nodes;
		private int[] indicies;
		private NodeComponent[][] nodeComponents;
		public Random rng = new Random(1);
		private Type[] types;

		public NodeComponentsBenchmark()
		{
			nodes = new Node[nodeCount];
			nodeComponents = new NodeComponent[nodeCount][];
			for (int i = 0; i < nodeCount; i++) {
				nodes[i] = new Widget();
				nodeComponents[i] = new NodeComponent[] {
					new LayoutCell(), new LinearLayout(), new LayoutConstraints(),
					new AnimationBlender(), new EditorParams(), new AwakeBehavior(),
					new Node.AssetBundlePathComponent(), new AudioRandomizerComponent(),
				};
				rng.Shuffle(nodeComponents[i]);
			}
			types = nodeComponents[0].Select(c => c.GetType()).ToArray();
			indicies = Enumerable.Range(0, nodeCount).ToArray();
		}

		[GlobalSetup(Targets = new string[] {
			nameof(SequentionalNodeAccess_AddAndRemoveComponents),
			nameof(SequentionalNodeAccess_AddAndRemoveByType),
			nameof(SequentionalNodeAccess_AddAndRemoveTemplate),
			nameof(SequentionalNodeAccess_RandomAccessByType),
			nameof(SequentionalNodeAccess_RandomAccessByTemplate),
			nameof(SequentionalNodeAccess_SequentionalAccess),
			nameof(SequentionalNodeAccess_AddAndRemoveMultipleComponentsOfSameType),
		})]
		public void SortIndicies()
		{
			Array.Sort(indicies);
		}

		[IterationSetup(Targets = new string[] {
			nameof(RandomNodeAccess_AddAndRemoveComponents),
			nameof(RandomNodeAccess_AddAndRemoveByType),
			nameof(RandomNodeAccess_AddAndRemoveTemplate),
			nameof(RandomNodeAccess_RandomAccessByType),
			nameof(RandomNodeAccess_RandomAccessByTemplate),
			nameof(RandomNodeAccess_SequentionalAccess),
			nameof(RandomNodeAccess_AddAndRemoveMultipleComponentsOfSameType),
		})]
		public void ShuffleIndicies()
		{
			rng.Shuffle(indicies);
		}

		[Benchmark]
		public void SequentionalNodeAccess_AddAndRemoveComponents()
		{
			AddAndRemoveComponentsRound();
		}

		[Benchmark]
		public void RandomNodeAccess_AddAndRemoveComponents()
		{
			AddAndRemoveComponentsRound();
		}


		public void AddAndRemoveComponentsRound()
		{
			foreach (var i in indicies) {
				var removed = nodeComponents[i];
				var node = nodes[i];
				foreach (var c in removed) {
					node.Components.Add(c);
				}
			}
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var c in nodeComponents[i]) {
					node.Components.Remove(c);
				}
				rng.Shuffle(nodeComponents[i]);
			}
		}

		[Benchmark]
		public void SequentionalNodeAccess_AddAndRemoveByType()
		{
			AddAndRemoveByTypeRound();
		}

		[Benchmark]
		public void RandomNodeAccess_AddAndRemoveByType()
		{
			AddAndRemoveByTypeRound();
		}

		public void AddAndRemoveByTypeRound()
		{
			foreach (var i in indicies) {
				var removed = nodeComponents[i];
				var node = nodes[i];
				foreach (var c in removed) {
					node.Components.Add(c);
				}
			}
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var t in types) {
					node.Components.Remove(t);
				}
				node.Components.Clear();
				rng.Shuffle(nodeComponents[i]);
			}
		}

		[Benchmark]
		public void SequentionalNodeAccess_AddAndRemoveTemplate()
		{
			AddAndRemoveTemplateRound();
		}

		[Benchmark]
		public void RandomNodeAccess_AddAndRemoveTemplate()
		{
			AddAndRemoveTemplateRound();
		}

		public void AddAndRemoveTemplateRound()
		{
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var c in nodeComponents[i]) {
					node.Components.Add(c);
				}
			}
			foreach (var i in indicies) {
				var node = nodes[i];
				node.Components.Remove<LayoutCell>();
				node.Components.Remove<Layout>();
				node.Components.Remove<LayoutConstraints>();
				node.Components.Remove<AnimationBlender>();
				node.Components.Remove<EditorParams>();
				node.Components.Remove<AwakeBehavior>();
				node.Components.Remove<AnimationBlender>();
				node.Components.Remove<AudioRandomizerComponent>();
				node.Components.Remove<Node.AssetBundlePathComponent>();
				node.Components.Clear();
				rng.Shuffle(nodeComponents[i]);
			}
		}

		[Benchmark]
		public void SequentionalNodeAccess_RandomAccessByType()
		{
			RandomAccessByTypeRound();
		}

		[Benchmark]
		public void RandomNodeAccess_RandomAccessByType()
		{
			RandomAccessByTypeRound();
		}

		public void RandomAccessByTypeRound()
		{
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var c in nodeComponents[i]) {
					node.Components.Add(c);
				}
				for (int j = 0; j < 1000; j++) {
					var type = Mathf.RandomOf(rng, types);
					var c = node.Components.Get(type);
				}
			}
			foreach (var i in indicies) {
				nodes[i].Components.Clear();
				rng.Shuffle(nodeComponents[i]);
			}
		}


		[Benchmark]
		public void SequentionalNodeAccess_RandomAccessByTemplate()
		{
			RandomAccessByTemplateRound();
		}

		[Benchmark]
		public void RandomNodeAccess_RandomAccessByTemplate()
		{
			RandomAccessByTemplateRound();
		}

		public void RandomAccessByTemplateRound()
		{
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var c in nodeComponents[i]) {
					node.Components.Add(c);
				}

			}
			foreach (var i in indicies) {
				var node = nodes[i];
				for (int j = 0; j < 111; j++) {
					NodeComponent c = node.Components.Get<LayoutCell>();
					c = node.Components.Get<Layout>();
					c = node.Components.Get<LayoutConstraints>();
					c = node.Components.Get<AnimationBlender>();
					c = node.Components.Get<EditorParams>();
					c = node.Components.Get<AwakeBehavior>();
					c = node.Components.Get<AnimationBlender>();
					c = node.Components.Get<AudioRandomizerComponent>();
					c = node.Components.Get<Node.AssetBundlePathComponent>();
				}
			}
			foreach (var i in indicies) {
				nodes[i].Components.Clear();
				rng.Shuffle(nodeComponents[i]);
			}
		}


		[Benchmark]
		public void SequentionalNodeAccess_SequentionalAccess()
		{
			SequentionalAccessRound();
		}

		[Benchmark]
		public void RandomNodeAccess_SequentionalAccess()
		{
			SequentionalAccessRound();
		}

		public void SequentionalAccessRound()
		{
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var c in nodeComponents[i]) {
					node.Components.Add(c);
				}
			}
			foreach (var i in indicies) {
				var node = nodes[i];
				foreach (var c in node.Components) {
					if (c.Owner != node) {
						break;
					}
				}
			}
			foreach (var i in indicies) {
				nodes[i].Components.Clear();
				rng.Shuffle(nodeComponents[i]);
			}
		}

		[Benchmark]
		public void SequentionalNodeAccess_AddAndRemoveMultipleComponentsOfSameType()
		{
			AddAndRemoveMultipleComponentsOfSameTypeRound();
		}

		[Benchmark]
		public void RandomNodeAccess_AddAndRemoveMultipleComponentsOfSameType()
		{
			AddAndRemoveMultipleComponentsOfSameTypeRound();
		}

		public void AddAndRemoveMultipleComponentsOfSameTypeRound()
		{
			foreach (var i in indicies) {
				var components = nodes[i].Components;
				var count = rng.Next(10);
				while (count-- > 0) {
					var factory = Mathf.RandomOf(rng, multipleAddComponentsFactories2);
					components.Add(factory());
				}
			}
			foreach (var i in indicies) {
				var components = nodes[i].Components;
				var branch = i % 4;
				if (branch == 0) {
					components.Remove<XComponent>();
					components.Remove<YComponent>();
					components.Remove<ZComponent>();
				} else if (branch == 1) {
					components.Remove<XComponent>();
					components.Remove<ZComponent>();
					components.Remove<YComponent>();
				} else if (branch == 2) {
					components.Remove<ZComponent>();
					components.Remove<XComponent>();
					components.Remove<YComponent>();
				} else {
					components.Clear();
				}
			}
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			var summary = BenchmarkRunner.Run<NodeComponentsBenchmark>();
		}
	}
}
