using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tangerine.Core.Operations
{
	public class SetAnimatorTargetPropertyPath : Operation
	{
		public override bool IsChangingDocument => true;

		private readonly string oldTargetPropertyPath;
		private readonly string newTargetPropertyPath;
		private readonly IAnimator animator;

		public SetAnimatorTargetPropertyPath(IAnimator animator, string targetPropertyPath)
		{
			this.animator = animator;
			oldTargetPropertyPath = animator.TargetPropertyPath;
			newTargetPropertyPath = targetPropertyPath;
		}

		public static void Perform(IAnimator animator, string targetPropertyPath)
		{
			Document.Current.History.Perform(new SetAnimatorTargetPropertyPath(animator, targetPropertyPath));
		}

		public sealed class Processor : OperationProcessor<SetAnimatorTargetPropertyPath>
		{
			protected override void InternalRedo(SetAnimatorTargetPropertyPath op)
			{
				op.animator.TargetPropertyPath = op.newTargetPropertyPath;
			}

			protected override void InternalUndo(SetAnimatorTargetPropertyPath op)
			{
				op.animator.TargetPropertyPath = op.oldTargetPropertyPath;
			}
		}
	}
}
