using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Lime
{
	public interface IContext
	{
		IContext Activate();
		void Deactivate();
		ContextScope Scoped();
	}

	public struct ContextScope : IDisposable
	{
		private IContext context;
		public ContextScope(IContext context) { this.context = context; }
		public void Dispose() { context.Deactivate(); }
	}

	public class Context : IContext
	{
		private struct ActivationRecord
		{
			public Context Context;
			public object OldValue;
		}

		private object value;
		private Property property;

		private static ThreadLocal<Stack<ActivationRecord>> stack =
			new ThreadLocal<Stack<ActivationRecord>>(() => new Stack<ActivationRecord>());

		internal Context(Property property, object value)
		{
			this.property = property;
			this.value = value;
		}

		protected Context(string propertyName)
		{
			this.property = new Property(GetType(), propertyName);
			this.value = this;
		}

		internal Context(Property property)
		{
			this.property = property;
			this.value = this;
		}

		public IContext Activate()
		{
			var r = new ActivationRecord { Context = this, OldValue = property.Getter() };
			stack.Value.Push(r);
			property.Setter(value);
			return this;
		}

		public void Deactivate()
		{
			var r = stack.Value.Pop();
			if (r.Context != this) {
				throw new InvalidOperationException();
			}
			property.Setter(r.OldValue);
		}

		public ContextScope Scoped()
		{
			return new ContextScope(this);
		}
	}

	public class CombinedContext : IContext
	{
		private IContext[] contexts;

		public CombinedContext(params IContext[] contexts)
		{
			this.contexts = contexts;
		}

		public CombinedContext(IEnumerable<IContext> contexts)
		{
			this.contexts = contexts.ToArray();
		}

		public IContext Activate()
		{
			foreach (var i in contexts) {
				i.Activate();
			}
			return this;
		}

		public void Deactivate()
		{
			for (int i = contexts.Length - 1; i >= 0; i--) {
				contexts[i].Deactivate();
			}
		}

		public ContextScope Scoped()
		{
			return new ContextScope(this);
		}
	}

	// TODO: Property wrappers in this file should either hidden from public or refactored:
	// - improve performance
	// - remove unrelated constructors
	internal class Property
	{
		public Func<object> Getter { get; private set; }
		public Action<object> Setter { get; private set; }

		public Property(Func<object> getter, Action<object> setter)
		{
			Getter = getter;
			Setter = setter;
		}

		public static Property Create<T>(Func<T> getter, Action<T> setter)
		{
			return new Property(() => getter(), x => setter((T)x));
		}

		public Property(Type singleton, string propertyName = "Instance")
		{
			var pi = singleton.GetProperty(propertyName);
			Getter = () => pi.GetValue(null, null);
			Setter = val => pi.SetValue(null, val, null);
		}

		public Property(object obj, string propertyName)
		{
			var pi = obj.GetType().GetProperty(propertyName);
			Getter = () => pi.GetValue(obj, null);
			Setter = val => pi.SetValue(obj, val, null);
		}

		public object Value
		{
			get { return Getter(); }
			set { Setter(value); }
		}
	}

	internal class Property<T>
	{
		public Func<T> Getter { get; private set; }
		public Action<T> Setter { get; private set; }

		public Property(object obj, string propertyName)
		{
			var pi = obj.GetType().GetProperty(propertyName);
			Getter = () => (T)pi.GetValue(obj, null);
			Setter = val => pi.SetValue((T)obj, val, null);
		}
		public Property(Func<T> getter, Action<T> setter)
		{
			Getter = getter;
			Setter = setter;
		}
		public T Value
		{
			get { return Getter(); }
			set { Setter(value); }
		}
	}

	internal class IndexedProperty
	{
		public Func<object> Getter { get; private set; }
		public Action<object> Setter { get; private set; }

		public IndexedProperty(object obj, string propertyName, int index)
		{
			var pi = obj.GetType().GetProperty(propertyName);
			Getter = () => pi.GetGetMethod().Invoke(obj, new object[] { index });
			Setter = val => pi.GetSetMethod().Invoke(obj, new object[] { index, val });
		}

		public object Value
		{
			get { return Getter(); }
			set { Setter(value); }
		}
	}
}
