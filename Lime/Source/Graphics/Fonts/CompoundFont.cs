using System.Collections.Generic;
using Yuzu;

namespace Lime
{
	/// <summary>
	/// Font class which combines different fonts into one. You can use collection of <see cref="IFont"/>
	/// in order of priority to use (if first font doesn't contain <see cref="FontChar"/> it will look for it
	/// in the next font and so on).
	/// </summary>
	public class CompoundFont : IFont
	{
		/// <summary>
		/// Allow to render a PlaceHolderCharacter when character is missing (used in QA testing)
		/// </summary>
		public static bool UsePlaceholders = false;

		/// <summary>
		/// Character that is used as a placeholder for missing character (used in QA testing)
		/// </summary>
		public static char PlaceHolderCharacter = '�';

		private readonly CompoundFontCharSource chars;
		private readonly List<IFont> fonts = new List<IFont>();

		public List<IFont> Fonts => fonts;

		public CompoundFont()
		{
			chars = new CompoundFontCharSource(fonts);
		}

		public CompoundFont(IEnumerable<IFont> fonts) : this()
		{
			this.fonts.AddRange(fonts);
		}

		public CompoundFont(params IFont[] fonts)
			: this((IEnumerable<IFont>)fonts)
		{
		}

		/// <summary>
		/// Legacy interface property
		/// </summary>
		public string About
		{
			get { return string.Empty; }
		}

		public float Spacing { get; }

		public IFontCharSource CharSource
		{
			get { return chars; }
		}

		public void ClearCache()
		{
			chars.ClearCache();
		}

		public bool RoundCoordinates { get; } = true;

		public void Dispose()
		{
			chars.Dispose();
		}

		private class CompoundFontCharSource : IFontCharSource
		{
			private readonly List<IFont> fonts;

			public CompoundFontCharSource(List<IFont> fonts)
			{
				this.fonts = fonts;
			}

			public FontChar Get(char code, float heightHint)
			{
				var fc = TryGet(code, heightHint);
				return fc == FontChar.Null && UsePlaceholders ? TryGet(PlaceHolderCharacter, heightHint) : fc;
			}

			private FontChar TryGet(char code, float heightHint)
			{
				foreach (var font in fonts) {
					var c = font.CharSource.Get(code, heightHint);
					if (c != FontChar.Null) {
						return c;
					}
				}
				return FontChar.Null;
			}

			public bool Contains(char code)
			{
				foreach (var font in fonts) {
					if (font.CharSource.Contains(code)) {
						return true;
					}
				}
				return false;
			}

			public void ClearCache()
			{
				foreach (var font in fonts) {
					font.ClearCache();
				}
			}

			public void Dispose()
			{
				foreach (var font in fonts) {
					font.Dispose();
				}
				fonts.Clear();
			}
		}
	}

	public class SerializableCompoundFont : IFont
	{
		[YuzuMember]
		public List<string> FontNames { get; private set; } = new List<string>();

		private CompoundFont font;

		public string About => (font ?? (font = CreateFont())).About;

		public float Spacing => (font ?? (font = CreateFont())).Spacing;

		public IFontCharSource CharSource => (font ?? (font = CreateFont())).CharSource;

		public bool RoundCoordinates => (font ?? (font = CreateFont())).RoundCoordinates;

		private CompoundFont CreateFont()
		{
			var font = new CompoundFont();
			foreach (var name in FontNames) {
				var f = FontPool.Instance[name];
				if (f != FontPool.Instance.Null && !font.Fonts.Contains(f)) {
					font.Fonts.Add(f);
				}
			}
			return font;
		}

		public void ClearCache()
		{
			Dispose();
		}

		public void Dispose()
		{
			font?.Dispose();
			font = null;
		}
	}
}
