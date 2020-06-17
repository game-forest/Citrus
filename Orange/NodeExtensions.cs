using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;

namespace Orange
{
	public static class NodeExtensions
	{
		public static int RemoveDanglingAnimators(this Node self)
		{
			var node = self;
			var scope = new Dictionary<string, int>();
			while (node.Parent != null) {
				node = node.Parent;
				FillAnimationsScope(node, scope);
			}
			return self.RemoveDanglingAnimators(scope);
		}

		private static int RemoveDanglingAnimators(this Node self, Dictionary<string, int> scope)
		{
			int result = 0;
			FillAnimationsScope(self, scope);
			var animators = self.Animators.ToList();
			foreach (var animator in animators) {
				if (animator.AnimationId == null || scope.TryGetValue(animator.AnimationId, out var count) && count > 0) {
					continue;
				}
				self.Animators.Remove(animator);
				result += 1;
			}
			foreach (var child in self.Nodes) {
				result += RemoveDanglingAnimators(child, scope);
			}
			RestoreAnimationScope(self, scope);
			return result;
		}

		private static void FillAnimationsScope(Node node, Dictionary<string, int> scope)
		{
			foreach (var animation in node.Animations) {
				if (animation.IsLegacy || animation.Id == null) {
					continue;
				}
				if (scope.TryGetValue(animation.Id, out var count)) {
					scope[animation.Id] = count + 1;
				} else {
					scope[animation.Id] = 1;
				}
			}
		}

		private static void RestoreAnimationScope(Node node, Dictionary<string, int> scope)
		{
			foreach (var animation in node.Animations) {
				if (animation.IsLegacy || animation.Id == null) {
					continue;
				}
				scope[animation.Id] -= 1;
			}
		}
	}
}
