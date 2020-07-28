using System;
using System.Collections;
using System.Collections.Generic;
using Lime;

namespace Tangerine.Core
{
	// DFS-enumerates descendants of root node(excluding root node).
	// If given descendant has an animation with Id same as animationId
	// this descendant is returned but it's descendants are skipped
	public class DescendantsSkippingNamesakeAnimationOwnersEnumerable : IEnumerable<Node>
	{
		private readonly Node root;
		private readonly string animationId;

		public DescendantsSkippingNamesakeAnimationOwnersEnumerable(Node root, string animationId)
		{
			this.root = root;
			this.animationId = animationId;
		}

		public IEnumerator<Node> GetEnumerator() => new Enumerator(root, animationId);

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		private class Enumerator : IEnumerator<Node>
		{
			private readonly Node root;
			private readonly string animationId;
			private bool skip;
			private Node current;

			public Enumerator(Node root, string animationId)
			{
				this.root = root;
				this.animationId = animationId;
			}

			public Node Current
			{
				get
				{
					if (current == null) {
						throw new InvalidOperationException();
					}
					return current;
				}
			}

			object IEnumerator.Current => Current;

			public void Dispose() => current = null;

			public bool MoveNext()
			{
				if (current == null) {
					current = root;
					skip = false;
				}
				Node node = null;
				if (!skip) {
					node = current.FirstChild;
				} else {
					skip = false;
				}
				if (node != null) {
					current = node;
					skip = current.Animations.TryFind(animationId, out _);
					return true;
				}
				if (current == root) {
					return false;
				}
				while (current.NextSibling == null) {
					current = current.Parent;
					if (current == root) {
						return false;
					}
				}
				current = current.NextSibling;
				skip = current.Animations.TryFind(animationId, out _);
				return true;
			}

			public void Reset() => current = null;
		}
	}
}
