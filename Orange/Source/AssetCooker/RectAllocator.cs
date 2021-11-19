using System;
using Lime;
using System.Collections.Generic;

namespace Orange
{
	public class RectAllocator
	{
		List<IntRectangle> rects = new List<IntRectangle>();

		int totalSquare;
		int allocatedSquare;

		public double GetPackRate() { return allocatedSquare / (double)totalSquare; }

		public RectAllocator(Size size)
		{
			totalSquare = size.Width * size.Height;
			rects.Add(new IntRectangle(0, 0, size.Width, size.Height));
		}

		public bool Allocate(Size size, int padding, out IntRectangle rect)
		{
			int j = -1;
			IntRectangle r;
			int spareSquare = Int32.MaxValue;
			int topPadding;
			int leftPadding;
			for (int i = 0; i < rects.Count; i++) {
				r = rects[i];
				leftPadding = r.Left == 0 ? 0 : padding;
				topPadding = r.Top == 0 ? 0 : padding;
				var requestedWidth = size.Width + leftPadding;
				var requestedHeight = size.Height + topPadding;
				if (r.Width >= requestedWidth && r.Height >= requestedHeight) {
					int z = r.Width * r.Height - requestedWidth * requestedHeight;
					if (z < spareSquare) {
						j = i;
						spareSquare = z;
					}
				}
			}
			if (j < 0) {
				rect = IntRectangle.Empty;
				return false;
			}
			// Split the rest, minimizing the sum of parts perimeters.
			r = rects[j];
			leftPadding = r.Left == 0 ? 0 : padding;
			topPadding = r.Top == 0 ? 0 : padding;
			var occupiedWidth = size.Width + leftPadding + padding;
			var occupiedHeight = size.Height + topPadding + padding;
			rect = new IntRectangle(
				r.A.X + leftPadding,
				r.A.Y + topPadding,
				r.A.X + leftPadding + size.Width,
				r.A.Y + topPadding + size.Height
			);
			int a = 2 * r.Width + r.Height - occupiedWidth;
			int b = 2 * r.Height + r.Width - occupiedHeight;
			if (a < b) {
				rects[j] = new IntRectangle(
					r.A.X,
					r.A.Y + topPadding + size.Height + padding,
					r.B.X,
					r.B.Y
				);
				rects.Add(new IntRectangle(
					r.A.X + leftPadding + size.Width + padding,
					r.A.Y,
					r.B.X,
					r.A.Y + topPadding + size.Height + padding
				));
			} else {
				rects[j] = new IntRectangle(
					r.A.X,
					r.A.Y + topPadding + size.Height + padding,
					r.A.X + leftPadding + size.Width + padding,
					r.B.Y
				);
				rects.Add(new IntRectangle(
					r.A.X + leftPadding + size.Width + padding,
					r.A.Y,
					r.B.X,
					r.B.Y
				));
			}
			allocatedSquare += occupiedWidth * occupiedHeight;
			return true;
		}
	}
}

