using Lime;

namespace Tangerine.Core
{
	public enum NodeVisibility
	{
		Default = 0,
		Hidden = 1,
		Shown = 2,
	}

	public class NodeEditorState
	{
		readonly Node node;
		public string ThumbnailData { get; set; }
		public NodeVisibility Visibility
		{
			get
			{
				if (node.GetTangerineFlag(TangerineFlags.Shown)) {
					return NodeVisibility.Shown;
				} else if (node.GetTangerineFlag(TangerineFlags.Hidden)) {
					return NodeVisibility.Hidden;
				} else {
					return NodeVisibility.Default;
				}
			}
			set
			{
				node.SetTangerineFlag(TangerineFlags.Shown, value == NodeVisibility.Shown);
				node.SetTangerineFlag(TangerineFlags.Hidden, value == NodeVisibility.Hidden);
			}
		}

		public bool Locked
		{
			get => node.GetTangerineFlag(TangerineFlags.Locked);
			set => node.SetTangerineFlag(TangerineFlags.Locked, value);
		}

		public int ColorIndex
		{
			get
			{
				return
					(node.GetTangerineFlag(TangerineFlags.ColorBit1) ? 1 : 0) |
					(node.GetTangerineFlag(TangerineFlags.ColorBit2) ? 2 : 0) |
					(node.GetTangerineFlag(TangerineFlags.ColorBit3) ? 4 : 0);
			}
			set
			{
				node.SetTangerineFlag(TangerineFlags.ColorBit1, (value & 1) == 1);
				node.SetTangerineFlag(TangerineFlags.ColorBit2, ((value >>= 1) & 1) == 1);
				node.SetTangerineFlag(TangerineFlags.ColorBit3, ((value >>= 1) & 1) == 1);
			}
		}

		public NodeEditorState(Node node)
		{
			this.node = node;
		}
	}
}
