using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Orange
{
	public struct FileInfo
	{
		public readonly string SrcPath;
		public readonly string DstPath;
		public DateTime LastWriteTime;

		public FileInfo(string srcPath, string dstPath, DateTime lastWriteTime)
		{
			SrcPath = srcPath;
			DstPath = dstPath;
			LastWriteTime = lastWriteTime;
		}
	}

	public class AliasedFileEnumerator : IFileEnumerator
	{
		private readonly IFileEnumerator sourceFileEnumerator;
		private readonly List<FileInfo> files = new List<FileInfo>();

		private bool isScanned;

		public AliasedFileEnumerator(IFileEnumerator fileEnumerator)
		{
			sourceFileEnumerator = fileEnumerator;
		}
		public string Directory { get { return sourceFileEnumerator.Directory; } }

		public Predicate<FileInfo> EnumerationFilter
		{
			get => sourceFileEnumerator.EnumerationFilter;
			set => sourceFileEnumerator.EnumerationFilter = value;
		}

		public IEnumerable<FileInfo> Enumerate(string extension = null)
		{
			if (!isScanned) {
				Rescan();
			}
			return files.Where(file => extension == null || file.DstPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
		}

		public void Rescan()
		{
			isScanned = true;

			sourceFileEnumerator.Rescan();

			files.Clear();
			var presentDstPaths = new HashSet<string>();

			foreach (var file in sourceFileEnumerator.Enumerate()) {
				string aliasedPath = GetAliasedPath(file.SrcPath);

				var newFileInfo = new FileInfo(file.SrcPath, aliasedPath, file.LastWriteTime);

				int index = -1;
				if (!presentDstPaths.Add(newFileInfo.DstPath)) {
					index = files.FindIndex(f => f.DstPath == newFileInfo.DstPath);
				}

				if (index >= 0) {
					if (files[index].SrcPath == files[index].DstPath) {
						files[index] = newFileInfo;
					}
				} else {
					files.Add(newFileInfo);
				}
			}
		}

		private static string GetAliasedPath(string filePath)
		{
			string rulePath = filePath + ".txt";
			string validatedPath = filePath;
			string alias = string.Empty;

			if (!File.Exists(rulePath)) {
				return validatedPath;
			}

			foreach (string line in File.ReadLines(rulePath)) {
				if (!line.Contains("Alias")) {
					continue;
				}

				var words = line.Split(' ');

				if (words.Length <= 0) {
					continue;
				}

				alias = words[1];
				break;
			}

			if (!string.IsNullOrEmpty(alias)) {
				validatedPath = Path.GetDirectoryName(filePath) + "/" + alias;
			}

			return validatedPath;
		}
	}

	public class FilteredFileEnumerator : IFileEnumerator
	{
		private IFileEnumerator sourceFileEnumerator;
		private List<FileInfo> files = new List<FileInfo>();
		public FilteredFileEnumerator(IFileEnumerator fileEnumerator, Predicate<FileInfo> predicate)
		{
			sourceFileEnumerator = fileEnumerator;
			sourceFileEnumerator.EnumerationFilter = predicate;
			sourceFileEnumerator.Rescan();
			files = sourceFileEnumerator.Enumerate().ToList();
			sourceFileEnumerator.EnumerationFilter = null;
		}
		public string Directory { get { return sourceFileEnumerator.Directory; } }

		public Predicate<FileInfo> EnumerationFilter
		{
			get => sourceFileEnumerator.EnumerationFilter;
			set => sourceFileEnumerator.EnumerationFilter = value;
		}

		public IEnumerable<FileInfo> Enumerate(string extension = null)
		{
			return files.Where(file => extension == null || file.SrcPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase));
		}

		public void Rescan()
		{
			sourceFileEnumerator.Rescan();
		}
	}

	public class FileEnumerator : IFileEnumerator
	{
		public string Directory { get; }
		public Predicate<FileInfo> EnumerationFilter { get; set; }
		readonly List<FileInfo> files = new List<FileInfo>();

		public FileEnumerator(string directory)
		{
			Directory = directory;
			Rescan();
		}

		public void Rescan()
		{
			files.Clear();
			var dirInfo = new DirectoryInfo(Directory);

			foreach (var fileInfo in dirInfo.GetFiles("*.*", SearchOption.AllDirectories)) {
				var file = fileInfo.FullName;
				if (file.Contains(".svn"))
					continue;

				file = file.Remove(0, dirInfo.FullName.Length + 1);
				file = CsprojSynchronization.ToUnixSlashes(file);

				files.Add(new FileInfo(file, file, fileInfo.LastWriteTime));
			}
#if MAC
			// Mono 6.0 breaks files order
			files.Sort((a, b) => string.Compare(a.Path, b.Path));
#endif
		}

		public IEnumerable<FileInfo> Enumerate(string extension = null)
		{
			if (extension == null && EnumerationFilter == null) {
				return files;
			}
			return files
				.Where(file => extension == null || file.DstPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				.Where(file => EnumerationFilter == null || EnumerationFilter(file));
		}
	}

	/// <summary>
	/// File enumerator optimized for scaning large data sets with some unwanted sub-directories
	/// </summary>
	public class ScanOptimizedFileEnumerator : IFileEnumerator
	{
		private readonly Predicate<DirectoryInfo> scanFilter;
		private readonly List<FileInfo> files = new List<FileInfo>();
		private readonly bool cutDirectoryPrefix = true;

		public ScanOptimizedFileEnumerator(string directory, Predicate<DirectoryInfo> scanFilter, bool cutDirectoryPrefix = true)
		{
			this.scanFilter = scanFilter;
			this.cutDirectoryPrefix = cutDirectoryPrefix;
			Directory = directory;
			Rescan();
		}

		public string Directory { get; }
		public Predicate<FileInfo> EnumerationFilter { get; set; }

		public void Rescan()
		{
			files.Clear();
			var dirInfo = new DirectoryInfo(Directory);
			var queue = new Queue<DirectoryInfo>();
			queue.Enqueue(new DirectoryInfo(Directory));
			while (queue.Count != 0) {
				var rootDirectoryInfo = queue.Dequeue();
				foreach (var fileInfo in rootDirectoryInfo.EnumerateFiles()) {
					var file = fileInfo.FullName;
					if (cutDirectoryPrefix) {
						file = file.Remove(0, dirInfo.FullName.Length + 1);
					}
					file = CsprojSynchronization.ToUnixSlashes(file);
					files.Add(new FileInfo(file, file, fileInfo.LastWriteTime));
				}
				foreach (var directoryInfo in rootDirectoryInfo.EnumerateDirectories()) {
					if (scanFilter?.Invoke(directoryInfo) ?? true) {
						queue.Enqueue(directoryInfo);
					}
				}
			}
#if MAC
			// Mono 6.0 breaks files order
			files.Sort ((a, b) => string.Compare (a.Path, b.Path));
#endif
		}

		public IEnumerable<FileInfo> Enumerate(string extension = null)
		{
			if (extension == null && EnumerationFilter == null) {
				return files;
			}
			return files
				.Where(file => extension == null || file.SrcPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
				.Where(file => EnumerationFilter == null || EnumerationFilter(file));
		}
	}
}
