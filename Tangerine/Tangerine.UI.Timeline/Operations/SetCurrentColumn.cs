using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Timeline.Operations
{
	public sealed class SetCurrentColumn : Operation
	{
		private static bool isScrollingFrozen;
		// Evgenii Polikutin: needed for RulerbarMouseScrollProcessor to avoid extra operations
		public static bool IsFrozen;

		private readonly int Column;
		private readonly Animation Animation;

		public override bool IsChangingDocument => false;

		public static void Perform(int column, Animation animation)
		{
			if (Document.Current.PreviewScene) {
				Document.Current.TogglePreviewAnimation();
			}
			DocumentHistory.Current.Perform(new SetCurrentColumn(column, animation));
		}

		public static void Perform(int column)
		{
			Perform(column, Document.Current.Animation);
		}

		public static void RollbackHistoryWithoutScrolling()
		{
			isScrollingFrozen = true;
			try {
				DocumentHistory.Current.RollbackTransaction();
			} finally {
				isScrollingFrozen = false;
			}
		}

		private SetCurrentColumn(int column, Animation animation)
		{
			Column = column;
			Animation = animation;
		}

		public sealed class Processor : OperationProcessor<SetCurrentColumn>
		{
			class Backup { public int Column; }

			protected override void InternalRedo(SetCurrentColumn op)
			{
				op.Save(new Backup { Column = Timeline.Instance.CurrentColumn });
				SetColumn(op.Column, op.Animation);
			}

			protected override void InternalUndo(SetCurrentColumn op)
			{
				if (IsFrozen) {
					return;
				}
				SetColumn(op.Restore<Backup>().Column, op.Animation);
			}

			private static void SetColumn(int value, Animation animation)
			{
				Document.Current.SetCurrentAnimationFrame(animation, value);
				if (!isScrollingFrozen) {
					Timeline.Instance.EnsureColumnVisible(value);
				}
			}
		}
	}
}
