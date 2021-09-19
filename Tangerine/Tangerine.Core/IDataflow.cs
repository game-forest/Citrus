using System;
using System.Collections.Generic;
using System.Reflection;

namespace Tangerine.Core
{
	/// <summary>
	/// Exposes the dataflow.
	/// </summary>
	public interface IDataflowProvider<T>
	{
		IDataflow<T> GetDataflow();
	}

	/// <summary>
	/// Represents the poll-based stream of values.
	/// </summary>
	public interface IDataflow<out T>
	{
		/// <summary>
		/// Polls the dataflow.
		/// </summary>
		void Poll();
		/// <summary>
		/// Indicates whether a new value has arrived.
		/// </summary>
		bool GotValue { get; }
		/// <summary>
		/// Returns the last received value from the dataflow.
		/// </summary>
		T Value { get; }
	}

	public class DataflowProvider<T> : IDataflowProvider<T>
	{
		private readonly Func<IDataflow<T>> func;

		public DataflowProvider(Func<IDataflow<T>> func)
		{
			this.func = func;
		}

		public IDataflow<T> GetDataflow() => func();
	}

	public class DelegateDataflowProvider<T> : IDataflowProvider<T>
	{
		private Func<T> Getter { get; }

		public DelegateDataflowProvider(Func<T> getter)
		{
			Getter = getter;
		}

		IDataflow<T> IDataflowProvider<T>.GetDataflow() => new DelegateDataflow<T>(Getter);
	}

	public class PropertyDataflowProvider<T> : IDataflowProvider<T>
	{
		private Func<T> Getter { get; }

		public PropertyDataflowProvider(object obj, string propertyName)
		{
			var pi = obj.GetType().GetProperty(propertyName);
			var getMethod = pi.GetGetMethod();
			Getter = (Func<T>)Delegate.CreateDelegate(typeof(Func<T>), obj, getMethod);
		}

		IDataflow<T> IDataflowProvider<T>.GetDataflow() => new DelegateDataflow<T>(Getter);
	}

	public class IndexedPropertyDataflowProvider<T> : IDataflowProvider<T>
	{
		public Func<T> Getter { get; }

		public IndexedPropertyDataflowProvider(object obj, string propertyName, int index)
		{
			var pi = obj.GetType().GetProperty(propertyName);
			var getMethod = pi.GetGetMethod();
			Getter = () => (T)getMethod.Invoke(obj, new object[] { index });
		}

		IDataflow<T> IDataflowProvider<T>.GetDataflow() => new DelegateDataflow<T>(Getter);
	}

	internal class DelegateDataflow<T> : IDataflow<T>
	{
		private readonly Func<T> getter;

		public T Value { get; private set; }
		public bool GotValue { get; private set; }

		public DelegateDataflow(Func<T> getter)
		{
			this.getter = getter;
		}

		public void Poll()
		{
			Value = getter();
			GotValue = true;
		}
	}

	public struct CoalescedValue<T>
	{
		public T Value { get; private set; }
		public bool IsDefined { get; private set; }

		public CoalescedValue(T value, bool isDefined)
		{
			Value = value;
			IsDefined = isDefined;
		}
	}

	/// <summary>
	/// CoalescedDataflow is a dataflow combining multiple dataflows.
	/// When it polls it polls all of them at the same time and compares the values.
	/// If all values are equal resulting <see cref="CoalescedValue{T}.IsDefined"/> is true and
	/// it's Value is equal to value of all dataflows. Otherwise result's value is set to default value
	/// and <see cref="CoalescedValue{T}.IsDefined"/> is false. Custom default value can be passed to constructor.
	/// </summary>
	/// <typeparam name="T">Underlying value type must be the same for all dataflows.</typeparam>
	public class CoalescedDataflow<T> : IDataflow<CoalescedValue<T>>
	{
		private readonly List<IDataflow<T>> dataflows = new List<IDataflow<T>>();
		private readonly T defaultValue;
		private readonly Func<T, T, bool> comparator;

		public bool GotValue { get; private set; }
		public CoalescedValue<T> Value { get; private set; }

		public CoalescedDataflow(T defaultValue = default, Func<T, T, bool> comparator = null)
		{
			this.defaultValue = defaultValue;
			this.comparator = comparator;
		}

		public void AddDataflow(IDataflow<T> dataflow)
		{
			dataflows.Add(dataflow);
		}

		public void Poll()
		{
			if (dataflows.Count == 0) {
				return;
			}
			var areAllEqual = true;
			dataflows[0].Poll();
			var firstValue = dataflows[0].Value;
			foreach (var dataflow in dataflows) {
				dataflow.Poll();
				GotValue = GotValue || dataflow.GotValue;
				areAllEqual = areAllEqual && (comparator?.Invoke(firstValue, dataflow.Value) ??
					 EqualityComparer<T>.Default.Equals(firstValue, dataflow.Value));
			}
			if (GotValue) {
				Value = new CoalescedValue<T>(areAllEqual ? firstValue : defaultValue, areAllEqual);
			}
		}
	}

	public static class DataflowMixins
	{
		public static bool Poll<T>(this IDataflow<T> dataflow, out T value)
		{
			dataflow.Poll();
			value = dataflow.Value;
			return dataflow.GotValue;
		}
	}

	public static class DataflowProviderMixins
	{
		public static IDataflowProvider<R> Select<T, R>(this IDataflowProvider<T> arg, Func<T, R> selector)
		{
			return new DataflowProvider<R>(() => new SelectDataflow<T, R>(arg.GetDataflow(), selector));
		}

		public static IDataflowProvider<Tuple<T1, T2>> Coalesce<T1, T2>(
			this IDataflowProvider<T1> arg1, IDataflowProvider<T2> arg2
		) {
			return new DataflowProvider<Tuple<T1, T2>>(
				() => new CoalesceDataflow<T1, T2>(arg1.GetDataflow(), arg2.GetDataflow())
			);
		}

		public static IDataflowProvider<T> Where<T>(this IDataflowProvider<T> arg, Predicate<T> predicate)
		{
			return new DataflowProvider<T>(() => new WhereDataflow<T>(arg.GetDataflow(), predicate));
		}

		public static IDataflowProvider<T> DistinctUntilChanged<T>(this IDataflowProvider<T> arg)
		{
			return new DataflowProvider<T>(() => new DistinctUntilChangedDataflow<T>(arg.GetDataflow()));
		}

		public static Consumer<T> WhenChanged<T>(this IDataflowProvider<T> arg, Action<T> action)
		{
			return DistinctUntilChanged(arg).Consume(action);
		}

		public static IDataflowProvider<T> SameOrDefault<T>(
			this IDataflowProvider<T> arg1, IDataflowProvider<T> arg2, T defaultValue = default(T)
		) {
			return new DataflowProvider<T>(
				() => new SameOrDefaultDataflow<T>(arg1.GetDataflow(), arg2.GetDataflow(), defaultValue)
			);
		}

		public static IDataflowProvider<T> Skip<T>(this IDataflowProvider<T> arg, int count)
		{
			return new DataflowProvider<T>(() => new SkipDataflow<T>(arg.GetDataflow(), count));
		}

		public static void AddDataflow<T>(
			this CoalescedDataflow<T> coalescedValuesProvider, IDataflowProvider<T> provider
		) {
			coalescedValuesProvider.AddDataflow(provider.GetDataflow());
		}

		public static Consumer<T> Consume<T>(this IDataflowProvider<T> arg, Action<T> action)
		{
			return new Consumer<T>(arg.GetDataflow(), action);
		}

		public static T GetValue<T>(this IDataflowProvider<T> arg)
		{
			var dataflow = arg.GetDataflow();
			dataflow.Poll();
			return dataflow.Value;
		}

		public static bool TryGetValue<T>(this IDataflowProvider<T> arg, out T result)
		{
			var dataflow = arg.GetDataflow();
			dataflow.Poll();
			result = dataflow.GotValue ? dataflow.Value : default(T);
			return dataflow.GotValue;
		}

		private class SelectDataflow<T, R> : IDataflow<R>
		{
			private readonly IDataflow<T> arg;
			private readonly Func<T, R> selector;

			public R Value { get; private set; }
			public bool GotValue { get; private set; }

			public SelectDataflow(IDataflow<T> arg, Func<T, R> selector)
			{
				this.arg = arg;
				this.selector = selector;
			}

			public void Poll()
			{
				arg.Poll();
				if ((GotValue = arg.GotValue)) {
					Value = selector(arg.Value);
				}
			}
		}

		private class CoalesceDataflow<T1, T2> : IDataflow<Tuple<T1, T2>>
		{
			private readonly IDataflow<T1> arg1;
			private readonly IDataflow<T2> arg2;

			public bool GotValue { get; private set; }
			public Tuple<T1, T2> Value { get; private set; }

			public CoalesceDataflow(IDataflow<T1> arg1, IDataflow<T2> arg2)
			{
				this.arg1 = arg1;
				this.arg2 = arg2;
			}

			public void Poll()
			{
				arg1.Poll();
				if ((GotValue = arg1.GotValue)) {
					arg2.Poll();
					Value = new Tuple<T1, T2>(arg1.Value, arg2.Value);
				}
			}
		}

		private class WhereDataflow<T> : IDataflow<T>
		{
			private readonly IDataflow<T> arg;
			private readonly Predicate<T> predicate;

			public bool GotValue { get; private set; }
			public T Value { get; private set; }

			public WhereDataflow(IDataflow<T> arg, Predicate<T> predicate)
			{
				this.arg = arg;
				this.predicate = predicate;
			}

			public void Poll()
			{
				arg.Poll();
				if ((GotValue = arg.GotValue && predicate(arg.Value))) {
					Value = arg.Value;
				}
			}
		}

		private class DistinctUntilChangedDataflow<T> : IDataflow<T>
		{
			private readonly IDataflow<T> arg;
			private T previous;
			private bool hasValue;

			public bool GotValue { get; private set; }
			public T Value { get; private set; }

			public DistinctUntilChangedDataflow(IDataflow<T> arg)
			{
				this.arg = arg;
			}

			public void Poll()
			{
				arg.Poll();
				if ((GotValue = arg.GotValue)) {
					var current = arg.Value;
					if ((GotValue = !hasValue || !EqualityComparer<T>.Default.Equals(current, previous))) {
						Value = current;
						hasValue = true;
						previous = current;
					}
				}
			}
		}

		private class SkipDataflow<T> : IDataflow<T>
		{
			private readonly IDataflow<T> arg;
			private int countdown;
			private bool done;

			public bool GotValue { get; private set; }
			public T Value { get; private set; }

			public SkipDataflow(IDataflow<T> arg, int count)
			{
				this.arg = arg;
				countdown = count;
			}

			public void Poll()
			{
				arg.Poll();
				if ((GotValue = arg.GotValue && (done || countdown-- <= 0))) {
					done = true;
					Value = arg.Value;
				}
			}
		}

		private class SameOrDefaultDataflow<T> : IDataflow<T>
		{
			private readonly IDataflow<T> arg1;
			private readonly IDataflow<T> arg2;
			private readonly T defaultValue;

			public bool GotValue { get; private set; }
			public T Value { get; private set; }

			public SameOrDefaultDataflow(IDataflow<T> arg1, IDataflow<T> arg2, T defaultValue)
			{
				this.arg1 = arg1;
				this.arg2 = arg2;
				this.defaultValue = defaultValue;
			}

			public void Poll()
			{
				arg1.Poll();
				arg2.Poll();
				if ((GotValue = arg1.GotValue || arg2.GotValue)) {
					Value = EqualityComparer<T>.Default.Equals(arg1.Value, arg2.Value) ? arg1.Value : defaultValue;
				}
			}
		}
	}
}
