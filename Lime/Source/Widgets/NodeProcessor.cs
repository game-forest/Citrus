using System;

namespace Lime
{
	public class NodeProcessor
	{
		public NodeManager Manager { get; internal set; }

		public virtual void Start() { }

		public virtual void Stop(NodeManager manager) { }

		public virtual void Update(float delta) { }
	}

	public abstract class NodeComponentProcessor : NodeProcessor
	{
		public Type TargetComponentType { get; }

		protected NodeComponentProcessor(Type targetComponentType)
		{
			TargetComponentType = targetComponentType;
		}

		protected internal virtual void InternalAdd(NodeComponent component, Node owner) { }

		protected internal virtual void InternalRemove(NodeComponent component, Node owner) { }

		protected internal virtual void InternalOnOwnerFrozenChanged(NodeComponent component, Node owner) { }
	}

	public class NodeComponentProcessor<TComponent> : NodeComponentProcessor
		where TComponent : class
	{
		protected NodeComponentProcessor() : base(typeof(TComponent)) { }

		protected internal sealed override void InternalAdd(NodeComponent component, Node owner)
		{
			Add(component as TComponent, owner);
		}

		protected internal sealed override void InternalRemove(NodeComponent component, Node owner)
		{
			Remove(component as TComponent, owner);
		}

		protected internal sealed override void InternalOnOwnerFrozenChanged(NodeComponent component, Node owner)
		{
			OnOwnerFrozenChanged(component as TComponent, owner);
		}

		protected virtual void Add(TComponent component, Node owner) { }

		protected virtual void Remove(TComponent component, Node owner) { }

		protected virtual void OnOwnerFrozenChanged(TComponent component, Node owner) { }
	}
}
