using System;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public sealed class TextStyleIdentifier : IEquatable<TextStyleIdentifier>, IComparable<TextStyleIdentifier>
	{
		private readonly string value;

		public TextStyleIdentifier(string value) => this.value = value;

		public override string ToString() => value;

		public override int GetHashCode() => value.GetHashCode();

		public int CompareTo(TextStyleIdentifier other) =>
			string.Compare(value, other.value, StringComparison.Ordinal);

		public override bool Equals(object other) =>
			GetType() == other?.GetType() && Equals(other as TextStyleIdentifier) ||
			other?.GetType() == typeof(string) && Equals(other as string);

		public bool Equals(string other) => value == other;

		public bool Equals(TextStyleIdentifier other) =>
			ReferenceEquals(this, other) || value == other?.value;

		public static bool operator ==(TextStyleIdentifier lhs, object rhs) => Equals(lhs, rhs);

		public static bool operator !=(TextStyleIdentifier lhs, object rhs) => !Equals(lhs, rhs);
	}
}
