using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tangerine.Core.Operations
{
	public class ChangeAnimatorsTargetPropertyPath : Operation
	{
		public override bool IsChangingDocument => true;

		private string oldTargetPropertyPath;
		private string newTargetPropertyPath;
		private IAnimator animator;

		public ChangeAnimatorsTargetPropertyPath(IAnimator animator, string targetPropertyPath)
		{
			this.animator = animator;
			oldTargetPropertyPath = animator.TargetPropertyPath;
			newTargetPropertyPath = targetPropertyPath;
		}

		public static void Perform(IAnimator animator, string targetPropertyPath) =>
			Document.Current.History.Perform(new ChangeAnimatorsTargetPropertyPath(animator, targetPropertyPath));

		public sealed class Processor : OperationProcessor<ChangeAnimatorsTargetPropertyPath>
		{
			protected override void InternalRedo(ChangeAnimatorsTargetPropertyPath op) =>
				op.animator.TargetPropertyPath = op.newTargetPropertyPath;

			protected override void InternalUndo(ChangeAnimatorsTargetPropertyPath op) =>
				op.animator.TargetPropertyPath = op.oldTargetPropertyPath;
		}
	}
}
