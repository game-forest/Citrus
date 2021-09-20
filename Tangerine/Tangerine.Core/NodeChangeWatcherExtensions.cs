using System;
using Lime;

namespace Tangerine.Core
{
	public static class NodeChangeWatcherExtensions
	{
		public static void AddChangeWatcher<C, T>(this Node node, Func<T> getter, Action<T> action)
			where C: ConsumeBehaviour, new()
		{
			node.Components.GetOrAdd<C>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<C, T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
			where C: ConsumeBehaviour, new()
		{
			node.Components.GetOrAdd<C>().Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<T>(this Node node, Func<T> getter, Action<T> action)
		{
			node.Components.GetOrAdd<EarlyConsumeBehaviour>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
		{
			node.Components.GetOrAdd<EarlyConsumeBehaviour>().Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddLateChangeWatcher<T>(this Node node, Func<T> getter, Action<T> action)
		{
			node.Components.GetOrAdd<LateConsumeBehaviour>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddLateChangeWatcher<T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
		{
			node.Components.GetOrAdd<LateConsumeBehaviour>()
				.Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddPreLateChangeWatcher<T>(this Node node, Func<T> getter, Action<T> action)
		{
			node.Components.GetOrAdd<PreLateConsumeBehaviour>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddPreLateChangeWatcher<T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
		{
			node.Components.GetOrAdd<PreLateConsumeBehaviour>()
				.Add(provider.DistinctUntilChanged().Consume(action));
		}
	}
}
