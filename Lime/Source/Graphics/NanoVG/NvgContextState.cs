﻿using System.Runtime.InteropServices;

namespace NanoVG
{
	[StructLayout(LayoutKind.Sequential)]
	internal class NvgContextState
	{
		public int ShapeAntiAlias;
		public Paint Fill;
		public Paint Stroke;
		public float StrokeWidth;
		public float MiterLimit;
		public LineCap LineJoin;
		public LineCap LineCap;
		public float Alpha;
		public Transform Transform;
		public Scissor Scissor;
		public float FontSize;
		public float LetterSpacing;
		public float LineHeight;
		public float FontBlur;
		public Alignment TextAlign;
		public int FontId;

		public NvgContextState Clone()
		{
			return new NvgContextState
			{
				ShapeAntiAlias = ShapeAntiAlias,
				Fill = Fill,
				Stroke = Stroke,
				StrokeWidth = StrokeWidth,
				MiterLimit = MiterLimit,
				LineJoin = LineJoin,
				LineCap = LineCap,
				Alpha = Alpha,
				Transform = Transform,
				Scissor = Scissor,
				FontSize = FontSize,
				LetterSpacing = LetterSpacing,
				LineHeight = LineHeight,
				FontBlur = FontBlur,
				TextAlign = TextAlign,
				FontId = FontId
			};
		}
	}
}
