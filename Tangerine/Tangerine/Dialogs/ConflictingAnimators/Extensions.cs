using Lime;
using Orange;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public static class Extensions
	{
		public static void InsertBefore(this Node node, Node target)
		{
			node.Unlink();
			var parent = target.Parent;
			parent.Nodes.Insert(parent.Nodes.IndexOf(target), node);
		}

		public static void InsertAfter(this Node node, Node target)
		{
			node.Unlink();
			var parent = target.Parent;
			parent.Nodes.Insert(parent.Nodes.IndexOf(target) + 1, node);
		}

		public static Spacer ShrinkHeight(this Spacer spacer, float maxHeight = 0.0f)
		{
			spacer.Height = spacer.MaxHeight = maxHeight;
			return spacer;
		}

		public static Spacer ShrinkWidth(this Spacer spacer, float maxWidth = 0.0f)
		{
			spacer.Width = spacer.MaxWidth = maxWidth;
			return spacer;
		}

		public static Widget AddCaption(this ThemedCheckBox checkBox, string text)
		{
			var caption = new ThemedCaption(text);
			var wrapped = new Frame {
				Layout = new HBoxLayout { Spacing = 2 },
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Nodes = { caption }
			};
			if (checkBox.ParentWidget != null) {
				wrapped.InsertBefore(checkBox);
				checkBox.Unlink();
			}
			wrapped.PushNode(checkBox);
			return wrapped;
		}

		public static Rectangle CalcRect(this Widget w)
		{
			var wp = w.ParentWidget;
			var p = wp.Padding;
			return new Rectangle(
				-w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
				-w.Position + wp.Size + new Vector2(p.Right, p.Bottom)
			);
		}

		public static void Toggle(
			this ref ConflictInfoProvider.SearchFlags lhs,
			ConflictInfoProvider.SearchFlags rhs,
		    bool value
		) {
			if (value) {
				lhs |= rhs;
			} else {
				lhs &= ~rhs;
			}
		}

		public static bool Contains(this ConflictInfoProvider.SearchFlags lhs, ConflictInfoProvider.SearchFlags rhs)
		{
			return (lhs & rhs) != 0;
		}
	}
}
