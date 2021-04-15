using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;

namespace Tangerine.Core.Operations
{
	public sealed class RenameAnimationProcessor : OperationProcessor<SetProperty>
	{
		protected override void InternalDo(SetProperty op)
		{
			if (op.Obj is Animation animation && op.Property.Name == nameof(Animation.Id)) {
				foreach (var a in animation.ValidatedEffectiveAnimators.OfType<IAnimator>().ToList()) {
					SetProperty.Perform(a, nameof(IAnimator.AnimationId), op.Value);
				}
			}
		}

		protected override void InternalRedo(SetProperty op) { }
		protected override void InternalUndo(SetProperty op) { }
	}
}
