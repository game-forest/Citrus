using System.Collections.Generic;
using Lime;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public static class TextStyleIdentifiers
	{
		public static TextStyleIdentifier Bold    = new TextStyleIdentifier("Bold");
		public static TextStyleIdentifier Regular = new TextStyleIdentifier("Regular");
		public static TextStyleIdentifier PropertyColor = new TextStyleIdentifier("PropertyColor");
		public static TextStyleIdentifier AnimationLink = new TextStyleIdentifier("AnimationLink");
	}

	public static class TextStylePool
	{
		public static TextStyle Get(TextStyleIdentifier id) => pool[id].Clone<TextStyle>();

		private static Dictionary<TextStyleIdentifier, TextStyle> pool =
			new Dictionary<TextStyleIdentifier, TextStyle> {
				[TextStyleIdentifiers.Bold] = new TextStyle {
					Id = TextStyleIdentifiers.Bold.ToString(),
					Size = Theme.Metrics.TextHeight,
					TextColor = Theme.Colors.BlackText,
					Font = new SerializableFont(FontPool.DefaultBoldFontName),
				},
				[TextStyleIdentifiers.Regular] = new TextStyle {
					Id = TextStyleIdentifiers.Regular.ToString(),
					Size = Theme.Metrics.TextHeight,
					TextColor = Theme.Colors.GrayText,
				},
				[TextStyleIdentifiers.PropertyColor] = new TextStyle {
					Id = TextStyleIdentifiers.PropertyColor.ToString(),
					Size = Theme.Metrics.TextHeight,
					TextColor = Theme.Colors.BlackText,
					Font = new SerializableFont(FontPool.DefaultBoldFontName),
				},
				[TextStyleIdentifiers.AnimationLink] = new TextStyle {
					Id = TextStyleIdentifiers.AnimationLink.ToString(),
					Size = Theme.Metrics.TextHeight,
					TextColor = Theme.Colors.WarningBackground,
					Font = new SerializableFont(FontPool.DefaultBoldFontName),
				},
			};
	}
}
