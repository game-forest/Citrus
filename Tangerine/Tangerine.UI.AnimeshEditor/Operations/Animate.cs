using Tangerine.Core;

namespace Tangerine.UI.AnimeshEditor.Operations
{
	public static partial class AnimeshModification
	{
		public sealed class Animate : Operation
		{
			public override bool IsChangingDocument => true;

			private Animate() { }

			public static void Perform() => Document.Current.History.Perform(new Animate());

			public sealed class Processor : OperationProcessor<Animate>
			{
				protected override void InternalRedo(Animate op) => AnimeshTools.State = AnimeshTools.ModificationState.Animation;
				protected override void InternalUndo(Animate op) => AnimeshTools.State = AnimeshTools.ModificationState.Animation;
			}
		}
	}
}
