using System;
using System.Collections.Generic;
using Lime.SignedDistanceField;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 10)]
	[TangerineVisualHintGroup("/All/Nodes/Text")]
	public class SimpleText : Widget, IText
	{
		private SpriteList spriteList;
		private SerializableFont font;
		private string text;
		private string[] localizeArguments;
		private Rectangle extent;
		private float fontHeight;
		private float spacing;
		private HAlignment hAlignment;
		private VAlignment vAlignment;
		private Color4 textColor;
		private Vector2 uncutTextSize;
		private bool uncutTextSizeValid;
		private string displayText;
		private TextOverflowMode overflowMode;
		private bool wordSplitAllowed;
		private TextProcessorDelegate textProcessor;
		private int gradientMapIndex = -1;
		private float letterSpacing;

		/// <summary>
		/// Processes a text assigned to any SimpleText instance.
		/// </summary>
		public static event TextProcessorDelegate GlobalTextProcessor;

		/// <summary>
		/// Processes a text assigned to this SimpleText instance.
		/// </summary>
		public event TextProcessorDelegate TextProcessor
		{
			add
			{
				textProcessor += value;
				Invalidate();
			}
			remove
			{
				textProcessor -= value;
				Invalidate();
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(29)]
		public SerializableFont Font
		{
			get { return font; }
			set
			{
				if (value != font) {
					font = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(30)]
		public override string Text
		{
			get { return text ?? string.Empty; }
			set
			{
				if (value != text) {
					text = value;
					Invalidate();
				}
			}
		}

		public string DisplayText
		{
			get
			{
				if (displayText != null) {
					return displayText;
				}

				if (Localizable) {
					if (localizeArguments != null) {
						displayText = Text.Localize(localizeArguments);
					} else {
						displayText = Text.Localize();
					}
				} else {
					displayText = Text;
				}
				GlobalTextProcessor?.Invoke(ref displayText, this);
				textProcessor?.Invoke(ref displayText, this);
				return displayText;
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(31)]
		[TangerineValidRange(0f, float.MaxValue, WarningLevel = ValidationResult.Warning)]
		public float FontHeight
		{
			get => fontHeight;
			set
			{
				if (value != fontHeight) {
					fontHeight = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(1)]
		public float Spacing
		{
			get { return spacing; }
			set
			{
				if (value != spacing) {
					spacing = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(2)]
		public HAlignment HAlignment
		{
			get { return hAlignment; }
			set
			{
				if (value != hAlignment) {
					hAlignment = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(3)]
		public VAlignment VAlignment
		{
			get { return vAlignment; }
			set
			{
				if (value != vAlignment) {
					vAlignment = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(4)]
		public TextOverflowMode OverflowMode
		{
			get { return overflowMode; }
			set
			{
				if (overflowMode != value) {
					overflowMode = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(5)]
		public bool WordSplitAllowed
		{
			get { return wordSplitAllowed; }
			set
			{
				if (wordSplitAllowed != value) {
					wordSplitAllowed = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(6)]
		public Color4 TextColor
		{
			get { return textColor; }
			set { textColor = value; }
		}

		[YuzuMember]
		public int GradientMapIndex
		{
			get {
				return gradientMapIndex;
			}
			set {
				gradientMapIndex = value;
				Invalidate();
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(7)]
		public float LetterSpacing
		{
			get { return letterSpacing; }
			set
			{
				if (letterSpacing != value) {
					letterSpacing = value;
					Invalidate();
				}
			}
		}

		[YuzuMember]
		public bool ForceUncutText { get; set; }

		internal bool AutoMinSize { get; set; }

		internal bool AutoMaxSize { get; set; }

		public TextRenderingMode RenderMode { get; set; }

		public override Vector2 EffectiveMinSize
		{
			get
			{
				return Vector2.Max(
					MeasuredMinSize,
					(ForceUncutText || AutoMinSize)
						? Vector2.Max(MinSize, MeasureUncutText() + Padding)
						: MinSize
				);
			}
		}

		public override Vector2 EffectiveMaxSize
		{
			get
			{
				return Vector2.Max(
					EffectiveMinSize,
					Vector2.Min(
						MeasuredMaxSize,
						(ForceUncutText || AutoMaxSize)
							? Vector2.Min(MaxSize, MeasureUncutText() + Padding)
							: MaxSize
					)
				);
			}
		}

		public bool TrimWhitespaces { get; set; }

		public ICaretPosition Caret { get; set; } = DummyCaretPosition.Instance;

		public event Action<string> Submitted;

		public bool Localizable { get; set; }

		public SimpleText()
		{
			Presenter = DefaultPresenter.Instance;
			Font = new SerializableFont();
			FontHeight = 15;
			TextColor = Color4.White;
			ForceUncutText = true;
			Localizable = true;
			TrimWhitespaces = true;
			Text = string.Empty;
			RenderMode = TextRenderingMode.TwoPasses;
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible && ClipRegionTest(chain.ClipRegion)) {
				AddSelfAndChildrenToRenderChain(chain, Layer);
			}
		}

		void IText.Submit()
		{
			if (Submitted != null) {
				Submitted(Text);
			}
		}

		public bool CanDisplay(char ch)
		{
			return Font.CharSource.Get(ch, fontHeight) != FontChar.Null;
		}

		protected override void OnSizeChanged(Vector2 sizeDelta)
		{
			base.OnSizeChanged(sizeDelta);
			Invalidate();
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			PrepareSpriteListAndSyncCaret();
			var component = Components.Get<SignedDistanceFieldComponent>();
			if (component == null) {
				var ro = RenderObjectPool<TextRenderObject>.Acquire();
				ro.CaptureRenderState(this);
				ro.SpriteList = spriteList;
				ro.GradientMapIndex = GradientMapIndex;
				ro.RenderMode = RenderMode;
				ro.Color = GlobalColor * textColor;
				return ro;
			} else {
				var ro = component.GetRenderObject();
				foreach (var item in ro.Objects) {
					item.CaptureRenderState(this);
					item.SpriteList = spriteList;
					item.Color = GlobalColor * TextColor;
				}
				return ro;
			}
		}

		void IText.SyncCaretPosition()
		{
			PrepareSpriteListAndSyncCaret();
		}

		private void PrepareSpriteListAndSyncCaret()
		{
			if (!Caret.IsValid) {
				spriteList = null;
			}
			PrepareSpriteListAndExtent();
		}

		private void PrepareSpriteListAndExtent()
		{
			if (CleanDirtyFlags(DirtyFlags.Material)) {
				Invalidate();
			}
			if (spriteList != null) {
				return;
			}
			if (OverflowMode == TextOverflowMode.Minify) {
				var savedSpacing = spacing;
				var savedHeight = fontHeight;
				FitTextInsideWidgetArea();
				spriteList = new SpriteList();
				extent = RenderHelper(spriteList, Caret);
				spacing = savedSpacing;
				fontHeight = savedHeight;
			} else {
				spriteList = new SpriteList();
				extent = RenderHelper(spriteList, Caret);
			}
		}

		/// <summary>
		/// Gets the text's bounding box.
		/// </summary>
		public Rectangle MeasureText()
		{
			PrepareSpriteListAndExtent();
			return extent;
		}

		public Vector2 MeasureUncutText()
		{
			if (!uncutTextSizeValid) {
				uncutTextSizeValid = true;
				uncutTextSize = MeasureTextLine(DisplayText);
			}
			return uncutTextSize;
		}

		public override void StaticScale(float ratio, bool roundCoordinates)
		{
			fontHeight *= ratio;
			spacing *= ratio;
			base.StaticScale(ratio, roundCoordinates);
		}

		private static CaretPosition dummyCaret = new CaretPosition();

		/// <summary>
		/// Changes FontHeight and Spacing to make the text inside widget's area.
		/// </summary>
		public void FitTextInsideWidgetArea(float minFontHeight = 10)
		{
			var minH = minFontHeight;
			var maxH = FontHeight;
			if (maxH <= minH) {
				return;
			}
			var bestHeight = minH;
			var spacingKoeff = Spacing / FontHeight;
			while (maxH - minH > 1) {
				var rect = RenderHelper(null, dummyCaret);
				var fit = rect.Width <= ContentWidth && rect.Height <= ContentHeight;
				if (fit) {
					minH = FontHeight;
					bestHeight = Mathf.Max(bestHeight, FontHeight);
				} else {
					maxH = FontHeight;
				}
				FontHeight = (minH + maxH) / 2;
				Spacing = FontHeight * spacingKoeff;
			}
			FontHeight = bestHeight.Floor();
			Spacing = bestHeight * spacingKoeff;
		}

		private Rectangle RenderHelper(SpriteList spriteList, ICaretPosition caret)
		{
			var lines = SplitText(DisplayText);
			if (TrimWhitespaces) {
				TrimLinesWhitespaces(lines);
			}
			var pos = new Vector2(0, Padding.Top + CalcVerticalTextPosition(lines));
			caret.StartSync();
			if (string.IsNullOrEmpty(DisplayText)) {
				pos.X = CalcXByAlignment(lineWidth: 0);
				caret.EmptyText(pos);
				return Rectangle.Empty;
			}
			caret.ClampTextPos(DisplayText.Length);
			caret.ClampLine(lines.Count);
			var rect = new Rectangle(Vector2.PositiveInfinity, Vector2.NegativeInfinity);
			int i = 0;
			foreach (var line in lines) {
				bool lastLine = ++i == lines.Count;
				caret.ClampCol(line.Length - (lastLine ? 0 : 1));
				float lineWidth = MeasureTextLine(line).X;
				pos.X = CalcXByAlignment(lineWidth);
				if (spriteList != null) {
					Renderer.DrawTextLine(
						font: Font,
						position: pos,
						text: line,
						color: Color4.White,
						fontHeight: FontHeight,
						start: 0,
						length: line.Length,
						letterSpacing: font.Spacing + letterSpacing,
						list: spriteList,
						onDrawChar: caret.Sync,
						tag: -1
					);
				}
				var lineRect = new Rectangle(pos.X, pos.Y, pos.X + lineWidth, pos.Y + FontHeight);
				if (lastLine) {
					// There is no end-of-text character, so simulate it.
					caret.Sync(line.Length, new Vector2(lineRect.Right, lineRect.Top), Vector2.Down * fontHeight);
				}
				pos.Y += Spacing + FontHeight;
				caret.NextLine();
				rect = Rectangle.Bounds(rect, lineRect);
			}
			caret.FinishSync();
			return rect;
		}

		private static void TrimLinesWhitespaces(List<string> lines)
		{
			for (int i = 0; i < lines.Count; i++) {
				lines[i] = lines[i].Trim();
			}
		}

		private float CalcVerticalTextPosition(List<string> lines)
		{
			var totalHeight = CalcTotalHeight(lines.Count);
			if (VAlignment == VAlignment.Bottom) {
				return ContentSize.Y - totalHeight;
			} else if (VAlignment == VAlignment.Center) {
				return ((ContentSize.Y - totalHeight) * 0.5f).Round();
			}
			return 0;
		}

		private float CalcTotalHeight(int numLines)
		{
			return Math.Max(FontHeight * numLines + Spacing * (numLines - 1), FontHeight);
		}

		private float CalcXByAlignment(float lineWidth)
		{
			switch (HAlignment) {
				case HAlignment.Left:
					return Padding.Left;
				case HAlignment.Right:
					return Size.X - Padding.Right - lineWidth;
				case HAlignment.Center:
					return ((ContentSize.X - lineWidth) * 0.5f + Padding.Left).Round();
				default:
					return Padding.Left;
			}
		}

		private List<string> SplitText(string text)
		{
			var strings = new List<string>(text.Split('\n'));
			// Add linebreaks to make editor happy.
			for (int i = 0; i < strings.Count - 1; i++) {
				strings[i] += '\n';
			}
			if (OverflowMode == TextOverflowMode.Ignore) {
				return strings;
			}
			for (var i = 0; i < strings.Count; i++) {
				if (OverflowMode == TextOverflowMode.Ellipsis) {
					// Clipping the last line of the text.
					if (CalcTotalHeight(i + 2) > ContentHeight) {
						strings[i] = ClipLineWithEllipsis(strings[i]);
						while (strings.Count > i + 1) {
							strings.RemoveAt(strings.Count - 1);
						}
						break;
					}
				}
				// Trying to split long lines. If a line can't be split it gets clipped.
				while (MeasureTextLine(strings[i]).X > Math.Abs(ContentWidth)) {
					if (
						!TextLineSplitter.CarryLastWordToNextLine(
							strings, i, WordSplitAllowed, IsTextLinePartFitToWidth
						)
					) {
						if (OverflowMode == TextOverflowMode.Ellipsis) {
							strings[i] = ClipLineWithEllipsis(strings[i]);
						}
						break;
					}
				}
			}
			return strings;
		}

		private bool IsTextLinePartFitToWidth(string line, int start, int count)
		{
			return Font.MeasureTextLine(line, FontHeight, start, count, letterSpacing + font.Spacing).X <= ContentWidth;
		}

		private Vector2 MeasureTextLine(string line)
		{
			return Font.MeasureTextLine(line, FontHeight, letterSpacing + font.Spacing);
		}

		private string ClipLineWithEllipsis(string line)
		{
			var lineWidth = MeasureTextLine(line).X;
			if (lineWidth <= ContentWidth) {
				return line;
			}
			while (line.Length > 0 && lineWidth > ContentWidth) {
				lineWidth = MeasureTextLine(line + "...").X;
				line = line.Substring(0, line.Length - 1);
			}
			line += "...";
			return line;
		}

		public void Invalidate()
		{
			displayText = null;
			Caret.InvalidatePreservingTextPos();
			spriteList = null;
			uncutTextSizeValid = false;
			InvalidateParentConstraintsAndArrangement();
			Window.Current?.Invalidate();
		}

		public bool GetCharPair(Vector2 point, out Tuple<SpriteList.CharDef, SpriteList.CharDef> pair)
		{
			PrepareSpriteListAndExtent();
			return spriteList.GetCharPair(point, out pair);
		}

		public void SetLocalizeArguments(params string[] args)
		{
			localizeArguments = args;
			Invalidate();
		}
	}
}
