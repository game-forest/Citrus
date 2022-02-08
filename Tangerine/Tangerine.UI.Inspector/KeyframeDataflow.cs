using System;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Inspector
{
	internal class KeyframeDataflow : IDataflow<IKeyframe>
	{
		private readonly object obj;
		private readonly string propertyPath;

		private int effectiveAnimatorsVersion = int.MinValue;
		private int animatorVersion = int.MinValue;
		private int animationFrame = int.MinValue;
		private Animation animation;
		private IAnimator animator;

		public IKeyframe Value { get; private set; }
		public bool GotValue { get; private set; }

		public KeyframeDataflow(object obj, string propertyPath)
		{
			this.obj = obj;
			this.propertyPath = propertyPath;
		}

		public static IDataflowProvider<R> GetProvider<R>(IPropertyEditorParams context, Func<IKeyframe, R> selector)
		{
			IDataflowProvider<R> provider = null;
			foreach (var obj in context.RootObjects) {
				var p = new DataflowProvider<IKeyframe>(
					() => new KeyframeDataflow(obj, context.PropertyPath)
				).Select(selector);
				provider = (provider == null) ? p : provider.SameOrDefault(p);
			}
			return provider;
		}

		public void Poll()
		{
			GotValue = false;
			if (!(obj is IAnimationHost animationHost)) {
				return;
			}
			if (GotValue |= Document.Current.AnimationFrame != animationFrame) {
				animationFrame = Document.Current.AnimationFrame;
			}
			if (GotValue |= Document.Current.Animation != animation) {
				animation = Document.Current.Animation;
				effectiveAnimatorsVersion = int.MinValue;
			}
			if (GotValue |= effectiveAnimatorsVersion != animation.EffectiveAnimatorsVersion) {
				effectiveAnimatorsVersion = animation.EffectiveAnimatorsVersion;
				animator = FindAnimator();
			}
			if (GotValue |= animator != null && animator.ReadonlyKeys.Version != animatorVersion) {
				if (animator != null) {
					animatorVersion = animator.ReadonlyKeys.Version;
					Value = FindKeyframe();
				} else {
					animatorVersion = int.MinValue;
					Value = null;
				}
			}
		}

		private IAnimator FindAnimator()
		{
			var pathComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate(propertyPath);
			foreach (var i in animation.ValidatedEffectiveAnimators) {
				if (
					i.TargetPropertyPathComparisonCode == pathComparisonCode
					&& i.Animable.GetAnimationHost() == obj && i is IAnimator a
				) {
					return a;
				}
			}
			return null;
		}

		private IKeyframe FindKeyframe() => FindAnimator()?.ReadonlyKeys.GetByFrame(Document.Current.AnimationFrame);
	}
}
