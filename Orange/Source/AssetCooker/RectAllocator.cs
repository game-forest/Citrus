using System;
using Lime;
using System.Collections.Generic;

namespace Orange
{
	public class RectAllocator
	{
		private readonly List<IntRectangle> rectangles = new List<IntRectangle>();
		private readonly IntRectangle initialRectangle;
		private readonly int totalArea;
		private int allocatedArea;

		public double GetPackRate() => allocatedArea / (double)totalArea;

		public RectAllocator(Size size)
		{
			totalArea = size.Width * size.Height;
			rectangles.Add(initialRectangle = new IntRectangle(0, 0, size.Width, size.Height));
		}

		public bool Allocate(Size size, int padding, out IntRectangle rect)
		{
			int j = -1;
			IntRectangle r;
			int minSpareArea = int.MaxValue;
			int topPadding;
			int leftPadding;
			int rightPadding;
			int bottomPadding;
			for (int i = 0; i < rectangles.Count; i++) {
				r = rectangles[i];
				leftPadding = r.Left == initialRectangle.Left ? 0 : padding;
				topPadding = r.Top == initialRectangle.Top ? 0 : padding;
				rightPadding = r.Right == initialRectangle.Right ? 0 : padding;
				bottomPadding = r.Bottom == initialRectangle.Bottom ? 0 : padding;
				var requiredWidth = leftPadding + size.Width + rightPadding;
				var requiredHeight = topPadding + size.Height + bottomPadding;
				if (r.Width >= requiredWidth && r.Height >= requiredHeight) {
					int spareArea = r.Width * r.Height - requiredWidth * requiredHeight;
					if (spareArea < minSpareArea) {
						j = i;
						minSpareArea = spareArea;
					}
				}
			}
			if (j < 0) {
				rect = IntRectangle.Empty;
				return false;
			}
			// Split the rest, minimizing the sum of parts perimeters.
			r = rectangles[j];
			leftPadding = r.Left == initialRectangle.Left ? 0 : padding;
			topPadding = r.Top == initialRectangle.Top ? 0 : padding;
			rightPadding = padding.Clamp(0, r.Width - (leftPadding + size.Width));
			bottomPadding = padding.Clamp(0, r.Height - (topPadding + size.Height));
			var occupiedWidth = leftPadding + size.Width + rightPadding;
			var occupiedHeight = topPadding + size.Height + bottomPadding;
			rect = new IntRectangle(
				r.A.X + leftPadding,
				r.A.Y + topPadding,
				r.A.X + leftPadding + size.Width,
				r.A.Y + topPadding + size.Height
			);
			int a = 2 * r.Width + r.Height - occupiedWidth;
			int b = 2 * r.Height + r.Width - occupiedHeight;
			if (a < b) {
				rectangles[j] = new IntRectangle(r.A.X, r.A.Y + occupiedHeight, r.B.X, r.B.Y);
				rectangles.Add(new IntRectangle(r.A.X + occupiedWidth, r.A.Y, r.B.X, r.A.Y + occupiedHeight));
			} else {
				rectangles[j] = new IntRectangle(r.A.X, r.A.Y + occupiedHeight, r.A.X + occupiedWidth, r.B.Y);
				rectangles.Add(new IntRectangle(r.A.X + occupiedWidth, r.A.Y, r.B.X, r.B.Y));
			}
			allocatedArea += occupiedWidth * occupiedHeight;
			return true;
		}
	}
}

