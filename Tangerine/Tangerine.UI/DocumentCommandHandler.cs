using System;
using Lime;

namespace Tangerine.UI
{
	public abstract class DocumentCommandHandler : CommandHandler
	{
		public override void RefreshCommand(ICommand command)
		{
			command.Enabled = Core.Document.Current != null
				&& GetEnabled()
				&& !Core.Document.Current.History.IsTransactionActive;
			command.Checked = Core.Document.Current != null
				&& GetChecked();
		}

		public sealed override void Execute()
		{
			Core.Document.Current.History.DoTransaction(ExecuteTransaction);
		}

		public abstract void ExecuteTransaction();

		public virtual bool GetEnabled() => true;
		public virtual bool GetChecked() => false;
	}

	public class DocumentDelegateCommandHandler : DocumentCommandHandler
	{
		private readonly Action executeTransaction;
		private readonly Func<bool> getEnabled;

		public DocumentDelegateCommandHandler(Action executeTransaction, Func<bool> getEnabled = null)
		{
			this.executeTransaction = executeTransaction;
			this.getEnabled = getEnabled;
		}

		public override void ExecuteTransaction() => executeTransaction();

		public override bool GetEnabled() => getEnabled?.Invoke() ?? true;
	}
}
