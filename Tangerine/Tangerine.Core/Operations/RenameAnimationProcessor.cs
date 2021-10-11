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
				var newAnimationName = (string)op.Value;
				foreach (var animator in animation.ValidatedEffectiveAnimators.OfType<IAnimator>().ToList()) {
					foreach (var otherAnimator in animator.Owner.Animators.ToList()) {
						if (
							otherAnimator != animator &&
							otherAnimator.AnimationId == newAnimationName &&
							otherAnimator.TargetPropertyPath == animator.TargetPropertyPath
						) {
							foreach (var key in otherAnimator.Keys) {
								if (animator.Keys.All(i => i.Frame != key.Frame)) {
									SetKeyframe.Perform(animator, animation, key.Clone());
								}
							}
							RemoveFromList<AnimatorList, IAnimator>.Perform(animator.Owner.Animators, otherAnimator);
						}
					}
					SetProperty.Perform(animator, nameof(IAnimator.AnimationId), newAnimationName);
				}
			}
		}

		protected override void InternalRedo(SetProperty op) { }
		protected override void InternalUndo(SetProperty op) { }
	}
}
