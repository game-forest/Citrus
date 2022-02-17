using System;
using Yuzu;

namespace Lime
{
#pragma warning disable CS0660, CS0661
	[YuzuCompact]
	public struct Thickness
	{
		[YuzuMember("0")]
		public float Left;

		[YuzuMember("1")]
		public float Right;

		[YuzuMember("2")]
		public float Top;

		[YuzuMember("3")]
		public float Bottom;

		public Vector2 LeftTop
		{
			get => new Vector2(Left, Top);
			set
			{
				Left = value.X;
				Top = value.Y;
			}
		}

		public Vector2 RightBottom
		{
			get => new Vector2(Right, Bottom);
			set
			{
				Right = value.X;
				Bottom = value.Y;
			}
		}

		public static Thickness Zero = new Thickness(0);

		public Thickness(float overall)
		{
			Left = Right = Top = Bottom = overall;
		}

		public Thickness(float horizontal, float vertical)
		{
			Left = Right = horizontal;
			Top = Bottom = vertical;
		}

		public Thickness(float left = 0.0f, float right = 0.0f, float top = 0.0f, float bottom = 0.0f)
		{
			Left = left;
			Right = right;
			Top = top;
			Bottom = bottom;
		}

		public static Thickness operator +(Thickness lhs, Thickness rhs)
		{
			return new Thickness(
				lhs.Left + rhs.Left, lhs.Right + rhs.Right, lhs.Top + rhs.Top, lhs.Bottom + rhs.Bottom
			);
		}

		public static Thickness operator -(Thickness lhs, Thickness rhs)
		{
			return new Thickness(
				lhs.Left - rhs.Left, lhs.Right - rhs.Right, lhs.Top - rhs.Top, lhs.Bottom - rhs.Bottom
			);
		}

		public static Vector2 operator +(Vector2 size, Thickness padding)
		{
			return size + padding.LeftTop + padding.RightBottom;
		}

		public static Vector2 operator -(Vector2 size, Thickness padding)
		{
			return size - padding.LeftTop - padding.RightBottom;
		}

		public static bool operator ==(Thickness lhs, Thickness rhs)
		{
			return lhs.Left == rhs.Left && lhs.Right == rhs.Right && lhs.Top == rhs.Right && lhs.Bottom == rhs.Bottom;
		}

		public static bool operator !=(Thickness lhs, Thickness rhs)
		{
			return lhs.Left != rhs.Left || lhs.Right != rhs.Right || lhs.Top != rhs.Top || lhs.Bottom != rhs.Bottom;
		}

		public override string ToString()
		{
			return FormattableString.Invariant($"{Left}, {Top}, {Right}, {Bottom}");
		}
	}
#pragma warning restore CS0660, CS0661
}
