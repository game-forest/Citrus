using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lime
{
	public static class StringExtensions
	{
		/// <summary>
		/// Функция, аналогичная String.Format, но сделанная в виде функции-расширения
		/// </summary>
		public static string Format(this string format, params object[] args)
		{
			return string.Format(format, args);
		}

		/// <summary>
		/// Локализует строку для текущего языка (с качестве ключа для словаря используется вся строка)
		/// </summary>
		public static string Localize(this string text)
		{
			return Pluralizer.Pluralize(Localization.GetString(text));
		}

		/// <summary>
		/// Локализует строку для текущего языка (с качестве ключа для словаря используется вся строка)
		/// </summary>
		public static string Localize(this string format, params object[] args)
		{
			return Localization.GetString(format, args);
		}

		public static bool HasJapaneseChineseSymbols(this string text, int start = 0, int length = -1)
		{
			int end = (length < 0) ? text.Length : Math.Min(text.Length, start + length);
			for (int i = start; i < end; i++) {
				char c = text[i];
				if (
					/* Hiragana */
					(c >= 0x3040 && c <= 0x309f)
					/* Katakana */
					|| (c >= 0x30a0 && c <= 0x30ff)
					/* Kanji */
					|| (c >= 0x4e00 && c <= 0x9faf)
					/* CJK Unified Ideographs: */
					|| (c >= 0x4e00 && c <= 0x62ff)
					|| (c >= 0x6300 && c <= 0x77ff)
					|| (c >= 0x7800 && c <= 0x8cff)
					|| (c >= 0x8d00 && c <= 0x9fff)
					|| (c >= 0x3400 && c <= 0x4dbf)
					|| (c >= 0x20000 && c <= 0x215ff)
					|| (c >= 0x21600 && c <= 0x230ff)
					|| (c >= 0x23100 && c <= 0x245ff)
					|| (c >= 0x24600 && c <= 0x260ff)
					|| (c >= 0x26100 && c <= 0x275ff)
					|| (c >= 0x27600 && c <= 0x290ff)
					|| (c >= 0x29100 && c <= 0x2a6df)
					|| (c >= 0x2a700 && c <= 0x2b73f)
					|| (c >= 0x2b740 && c <= 0x2b81f)
					|| (c >= 0x2b820 && c <= 0x2ceaf)
					|| (c >= 0x2ceb0 && c <= 0x2ebef)
					|| (c >= 0xf900 && c <= 0xfaff)
				) {
					return true;
				}
			}
			return false;
		}
	}
}
