using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace Tangerine.UI
{
	public class FuzzyStringSearch
	{
		private readonly ThreadLocal<Data> threadLocalData = new ThreadLocal<Data>(() => new Data());

		public bool DoesTextMatch(string text, string pattern, ICollection<int> matches, out int distance, out int gapCount)
		{
			distance = 0;
			gapCount = 0;
			if (string.IsNullOrEmpty(text)) {
				return string.IsNullOrEmpty(pattern);
			}
			if (string.IsNullOrEmpty(pattern)) {
				return true;
			}
			var data = threadLocalData.Value;
			data.EnsureCapacity(text.Length, pattern.Length);
			data.Clear();
			var patternMatching = 0;
			for (var ti = 0; ti < text.Length; ti++) {
				for (var pi = 0; pi < pattern.Length && pi <= patternMatching; pi++) {
					if (!AreCharsEquals(pattern[pi], text[ti])) {
						continue;
					}
					if (pi == 0) {
						data.AddMatch(out var index);
						data.AppendMatch(index, ti, pi);
					} else {
						for (var tpi = 0; tpi < pi; tpi++) {
							data.TempPattern[tpi] = -1;
						}
						var currentMatchesCount = data.Count;
						for (var i = 0; i < currentMatchesCount; i++) {
							if (data.Matches[i, pi - 1] >= ti) {
								continue;
							}
							// data.Matches can be unsorted array in some cases. Need to rework calculation of isNewMatch variable
							var isNewMatch = false;
							for (var tpi = 0; tpi < pi; tpi++) {
								if (isNewMatch || data.TempPattern[tpi] < data.Matches[i, tpi]) {
									isNewMatch = true;
									data.TempPattern[tpi] = data.Matches[i, tpi];
								}
							}
							if (!isNewMatch) {
								continue;
							}

							if (data.Lengths[i] == pi) {
								data.AppendMatch(i, ti, pi);
							} else if (data.Lengths[i] > pi) {
								data.AddMatch(out var index);
								for (var pj = 0; pj < pi; pj++) {
									data.AppendMatch(index, data.Matches[i, pj], pj);
								}
								data.AppendMatch(index, ti, pi);
							}
						}
					}
					if (pi == patternMatching) {
						patternMatching++;
						break;
					}
				}
			}
			if (patternMatching != pattern.Length) {
				return false;
			}
			var matchIndex = -1;
			var minGapCount = int.MaxValue;
			var minDistance = int.MaxValue;
			for (var i = 0; i < data.Count; i++) {
				if (
					data.Lengths[i] == pattern.Length &&
					(minGapCount > data.Gaps[i] || minGapCount == data.Gaps[i] && minDistance > data.Distances[i])
				) {
					matchIndex = i;
					minGapCount = data.Gaps[i];
					minDistance = data.Distances[i];
				}
			}
			distance = minDistance;
			gapCount = minGapCount;
			for (var pi = 0; pi < pattern.Length; pi++) {
				matches.Add(data.Matches[matchIndex, pi]);
			}
			return true;
		}

		private static bool AreCharsEquals(char lhs, char rhs)
		{
			if (lhs == '\\' || lhs == '/') {
				return rhs == '\\' || rhs == '/';
			}
			if (!char.IsLetter(lhs)) {
				return lhs == rhs;
			}
			return char.ToLowerInvariant(lhs) == char.ToLowerInvariant(rhs);
		}

		[DebuggerDisplay("{" + nameof(DebuggerDisplay) + "(), nq}")]
		private class Data
		{
			private const int TextDefaultCapacity = 256;
			private const int PatternDefaultCapacity = 16;

			private int textCapacity;
			private int patternCapacity;

			public int Count { get; private set; }
			public int[,] Matches { get; private set; }
			public int[] Lengths { get; private set; }
			public int[] Gaps { get; private set; }
			public int[] Distances { get; private set; }

			public int[] TempPattern;

			public void EnsureCapacity(int textLength, int patternLength, bool recreate = true)
			{
				if (textCapacity < textLength || patternCapacity < patternLength) {
					textCapacity = CalculateCapacity(textCapacity, textLength, TextDefaultCapacity);
					patternCapacity = CalculateCapacity(patternCapacity, patternLength, PatternDefaultCapacity);
					if (recreate) {
						Matches = new int[textCapacity, patternCapacity];
					} else {
						var a = new int[textCapacity, patternCapacity];
						for (var i = 0; i < Count; i++) {
							for (var j = 0; j < Lengths[i]; j++) {
								a[i, j] = Matches[i, j];
							}
						}
						Matches = a;
					}
					if (Lengths == null || Lengths.Length < textCapacity) {
						if (recreate) {
							Lengths = new int[textCapacity];
							Gaps = new int[textCapacity];
							Distances = new int[textCapacity];
						} else {
							var a = new int[textCapacity];
							Array.Copy(Lengths, a, Count);
							Lengths = a;
							a = new int[textCapacity];
							Array.Copy(Gaps, a, Count);
							Gaps = a;
							a = new int[textCapacity];
							Array.Copy(Distances, a, Count);
							Distances = a;
						}
					}
					if (TempPattern == null || TempPattern.Length < patternCapacity) {
						if (recreate) {
							TempPattern = new int[patternCapacity];
						} else {
							Array.Resize(ref TempPattern, patternCapacity);
						}
					}
				}

				int CalculateCapacity(int current, int min, int @default)
				{
					if (current <= 0) {
						current = @default;
					}
					while (current < min) {
						current *= 2;
						// Maximum object size allowed in the GC Heap at 2GB, even on the 64-bit version of the runtime
						// https://docs.microsoft.com/en-us/archive/blogs/joshwil/bigarrayt-getting-around-the-2gb-array-size-limit
						if (current > 2146435071) {
							return 2146435071;
						}
					}
					return current;
				}
			}

			public void AddMatch(out int index)
			{
				if (Count + 1 > textCapacity) {
					EnsureCapacity(Count + 1, Matches.GetLength(1), recreate: false);
				}
				index = Count++;
				Distances[index] = 0;
				Gaps[index] = 0;
				Lengths[index] = 0;
			}

			public void AppendMatch(int index, int ti, int pi)
			{
				Matches[index, pi] = ti;
				Lengths[index]++;
				if (pi > 0) {
					var d = ti - Matches[index, pi - 1] - 1;
					Distances[index] += d;
					Gaps[index] += d > 0 ? 1 : 0;
				}
			}

			public void Clear() => Count = 0;

			private string DebuggerDisplay()
			{
				var sb = new StringBuilder();
				sb.Append($"{{Count = {Count}}} \n");
				for (var i = 0; i < Count; i++) {
					sb.Append("Matches:");
					for (var j = 0; j < Lengths[i]; j++) {
						sb.Append($" {Matches[i, j]}");
					}
					sb.Append($"; Gaps: {Gaps[i]}; Distances: {Distances[i]}. \n");
				}
				return sb.ToString();
			}
		}
	}
}
