using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Tangerine.Core
{
	public class AssetsDatabase : IEnumerable<KeyValuePair<string, AssetsDatabase.Entry>>
	{
		private IReadOnlyDictionary<string, Entry> items;
		private CancellationTokenSource scanningCancellationSource;

		public AssetsDatabase()
		{
			items = GetAssetsDictionary(CancellationToken.None);
		}

		public async void RescanAsync()
		{
			scanningCancellationSource?.Cancel();
			scanningCancellationSource = new CancellationTokenSource();
			try {
				var cancellationToken = scanningCancellationSource.Token;
				items = await System.Threading.Tasks.Task.Run(() => GetAssetsDictionary(cancellationToken), cancellationToken);
				scanningCancellationSource = null;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (Exception exception) {
				Console.WriteLine(exception);
				scanningCancellationSource = null;
			}
		}

		private static SortedList<string, Entry> GetAssetsDictionary(CancellationToken cancellationToken)
		{
			var assetsDirectory = Project.Current.AssetsDirectory;
			var assets = new SortedList<string, Entry>();
			GetFilesRecursively(assetsDirectory);
			return assets;

			void GetFilesRecursively(string directory)
			{
				cancellationToken.ThrowIfCancellationRequested();
				foreach (var filePath in Directory.GetFiles(directory)) {
					cancellationToken.ThrowIfCancellationRequested();
					assets.Add(filePath, new Entry(assetsDirectory, filePath));
				}
				foreach (var d in Directory.GetDirectories(directory)) {
					GetFilesRecursively(d);
				}
			}
		}

		public IEnumerator<KeyValuePair<string, Entry>> GetEnumerator() => items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public class Entry
		{
			public readonly string Path;
			public readonly string Type;

			public Entry(string assetsDirectory, string filePath)
			{
				var relativePath = filePath.Substring(assetsDirectory.Length + 1);
				relativePath = Lime.AssetPath.CorrectSlashes(relativePath);
				var assetType = System.IO.Path.GetExtension(relativePath)?.ToLower();
				var assetPath =
					string.IsNullOrEmpty(assetType) ?
					relativePath :
					relativePath.Substring(0, relativePath.Length - assetType.Length);
				Path = assetPath;
				Type = assetType;
			}
		}
	}
}
