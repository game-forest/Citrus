﻿using NanoVG;

namespace FontStashSharp
{
	internal class FontSystemState
	{
		public int Font;
		public Alignment Align;
		public float Size;
		public uint Color;
		public float Blur;
		public float Spacing;

		public FontSystemState Clone()
		{
			return new FontSystemState
			{
				Font = Font,
				Align = Align,
				Size = Size,
				Color = Color,
				Blur = Blur,
				Spacing = Spacing
			};
		}
	}
}