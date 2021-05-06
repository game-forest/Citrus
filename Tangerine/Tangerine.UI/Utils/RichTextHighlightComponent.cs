using System.Text;
using Lime;

namespace Tangerine.UI
{
	[NodeComponentDontSerialize]
	[UpdateStage(typeof(PostLateUpdateStage))]
	public class RichTextHighlightComponent : BehaviorComponent
	{
		private readonly string highlightTextStyle;
		private string text;
		private int[] highlightSymbolsIndices;
		private bool isRichTextDirty = true;

		public bool Enabled { get; set; } = true;

		public string Text
		{
			get => text;
			set
			{
				text = value;
				isRichTextDirty = true;
			}
		}

		public int[] HighlightSymbolsIndices
		{
			get => highlightSymbolsIndices;
			set
			{
				highlightSymbolsIndices = value;
				isRichTextDirty = true;
			}
		}

		public RichTextHighlightComponent(string text, string highlightTextStyle)
		{
			Text = text;
			this.highlightTextStyle = highlightTextStyle;
		}

		protected internal override void Update(float delta)
		{
			if (!Enabled || !isRichTextDirty) {
				return;
			}
			isRichTextDirty = false;
			var richText = (RichText)Owner;
			if (highlightSymbolsIndices == null || highlightSymbolsIndices.Length == 0) {
				richText.Text = RichText.Escape(text);
				return;
			}

			var tagLength = 5 + highlightTextStyle.Length * 2;
			var blocksCount = 0;
			var lastIndex = int.MinValue;
			foreach (var i in highlightSymbolsIndices) {
				if (lastIndex + 1 != i) {
					blocksCount++;
				}
				lastIndex = i;
			}
			var sb = new StringBuilder(text.Length + tagLength * blocksCount);
			var isInTag = false;
			var hIndex = 0;
			for (var i = 0; i < text.Length || isInTag; i++) {
				if (hIndex < highlightSymbolsIndices.Length && highlightSymbolsIndices[hIndex] == i) {
					hIndex++;
					if (!isInTag) {
						isInTag = true;
						sb.Append('<');
						sb.Append(highlightTextStyle);
						sb.Append('>');
					}
				} else if (isInTag) {
					isInTag = false;
					sb.Append("</");
					sb.Append(highlightTextStyle);
					sb.Append('>');
				}
				if (i < text.Length) {
					AppendEscapedChar(text[i]);
				}
			}
			richText.Text = sb.ToString();

			void AppendEscapedChar(char @char)
			{
				switch (@char) {
					case '<': sb.Append("&lt;"); break;
					case '>': sb.Append("&gt;"); break;
					default: sb.Append(@char); break;
				}
			}
		}
	}
}
