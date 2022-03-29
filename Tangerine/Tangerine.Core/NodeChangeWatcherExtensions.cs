using System;
using Lime;

namespace Tangerine.Core
{
	public static class NodeChangeWatcherExtensions
	{
		public static void AddChangeWatcher<C, T>(this Node node, Func<T> getter, Action<T> action)
			where C : ConsumeBehavior, new()
		{
			node.Components.GetOrAdd<C>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<C, T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
			where C : ConsumeBehavior, new()
		{
			node.Components.GetOrAdd<C>().Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<T>(this Node node, Func<T> getter, Action<T> action)
		{
			node.Components.GetOrAdd<EarlyConsumeBehavior>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddChangeWatcher<T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
		{
			node.Components.GetOrAdd<EarlyConsumeBehavior>().Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddLateChangeWatcher<T>(this Node node, Func<T> getter, Action<T> action)
		{
			node.Components.GetOrAdd<LateConsumeBehavior>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddLateChangeWatcher<T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
		{
			node.Components.GetOrAdd<LateConsumeBehavior>()
				.Add(provider.DistinctUntilChanged().Consume(action));
		}

		public static void AddPreLateChangeWatcher<T>(this Node node, Func<T> getter, Action<T> action)
		{
			node.Components.GetOrAdd<PreLateConsumeBehavior>()
				.Add(new DelegateDataflowProvider<T>(getter).DistinctUntilChanged().Consume(action));
		}

		public static void AddPreLateChangeWatcher<T>(this Node node, IDataflowProvider<T> provider, Action<T> action)
		{
			node.Components.GetOrAdd<PreLateConsumeBehavior>()
				.Add(provider.DistinctUntilChanged().Consume(action));
		}
	}
}
