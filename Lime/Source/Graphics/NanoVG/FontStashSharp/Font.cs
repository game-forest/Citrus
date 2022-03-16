﻿using StbSharp;

namespace FontStashSharp
{
	internal unsafe class Font
	{
		public StbTrueType.stbtt_fontinfo FontInfo = new StbTrueType.stbtt_fontinfo();
		public string Name;
		public byte[] Data;
		public float Ascender;
		public float Descender;
		public float LineHeight;
		public FontGlyph* Glyphs;
		public int GlyphsCount;
		public int GlyphsNumber;
		public readonly int[] Lut = new int[256];
		public readonly int[] Fallbacks = new int[20];
		public int FallbacksCount;
	}
}
