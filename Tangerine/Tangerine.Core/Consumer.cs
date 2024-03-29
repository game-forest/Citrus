using System;
using System.Collections.Generic;
using Lime;

namespace Tangerine.Core
{
	public interface IConsumer
	{
		void Consume();
	}

	public class Consumer<T> : IConsumer
	{
		private readonly IDataflow<T> dataflow;
		private readonly Action<T> action;

		public Consumer(IDataflow<T> dataflow, Action<T> action)
		{
			this.dataflow = dataflow;
			this.action = action;
		}
		public void Consume()
		{
			dataflow.Poll();
			if (dataflow.GotValue) {
				action(dataflow.Value);
			}
		}
	}

	[NodeComponentDontSerialize]
	public abstract class ConsumeBehavior : BehaviorComponent
	{
		private readonly List<IConsumer> consumers = new List<IConsumer>();

		public ConsumeBehavior() { }

		public void Add(IConsumer consumer) => consumers.Add(consumer);

		protected internal override void Update(float delta)
		{
			foreach (var i in consumers) {
				i.Consume();
			}
		}
	}

	[UpdateStage(typeof(EarlyUpdateStage))]
	[NodeComponentDontSerialize]
	public class EarlyConsumeBehavior : ConsumeBehavior
	{
	}

	[UpdateStage(typeof(LateUpdateStage))]
	[NodeComponentDontSerialize]
	public class LateConsumeBehavior : ConsumeBehavior
	{
	}

	[UpdateStage(typeof(PreLateUpdateStage))]
	[NodeComponentDontSerialize]
	public class PreLateConsumeBehavior : ConsumeBehavior
	{
	}
}
