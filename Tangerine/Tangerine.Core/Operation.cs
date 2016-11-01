﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Tangerine.Core
{
	public interface IOperation
	{
		long BatchId { get; set; }
		bool IsChangingDocument { get; }
	}

	public abstract class Operation : IOperation
	{
		public long BatchId { get; set; }
		public abstract bool IsChangingDocument { get; }

		readonly Dictionary<Type, object> backup = new Dictionary<Type, object>();

		public void Save<T>(T data)
		{
			backup.Add(typeof(T), data);
		}

		public T Restore<T>()
		{
			var r = backup[typeof(T)];
			backup.Remove(typeof(T));
			return (T)r;
		}
	}

	public interface IOperationProcessor
	{
		void Do(IOperation operation);
		void Undo(IOperation operation);
		void Redo(IOperation operation);
	}

	public abstract class OperationProcessor<T> : IOperationProcessor where T: IOperation
	{
		public void Do(IOperation op) => Redo(op);

		public void Redo(IOperation op)
		{
			if (op is T) {
				InternalRedo((T)op);
			}
		}

		public void Undo(IOperation op)
		{
			if (op is T) {
				InternalUndo((T)op);
			}
		}

		protected abstract void InternalRedo(T op);
		protected abstract void InternalUndo(T op);
	}

	public abstract class SymmetricOperationProcessor : IOperationProcessor
	{
		public void Do(IOperation op) => Process(op);
		public void Undo(IOperation op) => Process(op);
		public void Redo(IOperation op) => Process(op);
		public abstract void Process(IOperation op);
	}
}
