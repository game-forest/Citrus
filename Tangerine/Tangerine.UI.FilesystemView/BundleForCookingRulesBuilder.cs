using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Orange;
using FileInfo = Lime.FileInfo;

namespace Tangerine.UI.FilesystemView
{
	public class BundleForCookingRulesBuilder : WrappedAssetBundle
	{
		private readonly string rootDirectory;
		private readonly string targetDirectory;

		public BundleForCookingRulesBuilder(AssetBundle bundle, string rootDirectory, string targetDirectory)
			: base(bundle)
		{
			this.rootDirectory = new DirectoryInfo(rootDirectory).FullName;
			this.targetDirectory = targetDirectory;
		}

		/// <summary>
		/// Enumerates all parent directories from target directory to root directory
		/// also enumerating all #CookingRules.txt files by the way
		/// afterwards enumerates top level files and folders in target directory
		/// for each folder in target directory also enumerates folder/#CookingRules.txt if present
		/// so it can be applied to this folder
		/// </summary>
		public override IEnumerable<FileInfo> EnumerateFileInfos(string path = null, string extension = null)
		{
			var dirInfoRoot = new DirectoryInfo(rootDirectory);
			var dirInfoTarget = new DirectoryInfo(targetDirectory);
			var trunc = new List<DirectoryInfo>();
			var di = dirInfoTarget;
			trunc.Add(di);
			while (di.FullName != dirInfoRoot.FullName) {
				di = di.Parent;
				trunc.Add(di);
			}
			trunc.Reverse();
			foreach (var di2 in trunc) {
				yield return new Lime.FileInfo { Path = ProcessPath(di2.FullName), LastWriteTime = di2.LastWriteTime };
				if (TryGetCookingRulesForDirectory(di2.FullName, out var fi)) {
					yield return fi;
				}
			}
			foreach (var fi in dirInfoTarget.GetFileSystemInfos("*", SearchOption.TopDirectoryOnly)) {
				var file = fi.FullName;
				if (file.EndsWith(CookingRulesBuilder.CookingRulesFilename)) {
					continue;
				}
				yield return new FileInfo { Path = ProcessPath(file), LastWriteTime = fi.LastWriteTime };
				if (fi.Attributes == FileAttributes.Directory && TryGetCookingRulesForDirectory(fi.FullName, out var fi2)) {
					yield return fi2;
				}
			}
		}

		private bool TryGetCookingRulesForDirectory(string FullName, out FileInfo fileInfo)
		{
			var cookingRulesPath = Path.Combine(FullName, CookingRulesBuilder.CookingRulesFilename);
			if (File.Exists(cookingRulesPath)) {
				fileInfo = new FileInfo {
					Path = ProcessPath(cookingRulesPath),
					LastWriteTime = File.GetLastWriteTime(cookingRulesPath)
				};
				return true;
			}
			fileInfo = default;
			return false;
		}

		private string ProcessPath(string path)
		{
			var r = path;
			r = r.Remove(0, rootDirectory.Length);
			r = CsprojSynchronization.ToUnixSlashes(r);
			if (r.StartsWith("/")) {
				r = r.Substring(1);
			}
			return r;
		}
	}
}
