using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using Microsoft.Win32;
using System.Text;
using System.Reflection;
using System.Threading;

namespace Launcher
{
	public class Builder
	{
		private string citrusDirectory;
		public bool NeedRunExecutable = true;
		public string SolutionPath;
		public string ExecutablePath;
		public string ExecutableArgs;

		public event Action<string> OnBuildStatusChange;
		public event Action OnBuildSuccess;

		public event Action OnBuildFail;
		public Builder(string citrusDirectory)
		{
			this.citrusDirectory = citrusDirectory;
		}

		private void RunExecutable()
		{
			Console.WriteLine($"Starting '{ExecutablePath}'.");
			var process = new Process {
				StartInfo = {
					FileName = ExecutablePath,
					Arguments = ExecutableArgs
				}
			};
			process.Start();
		}

		private void RestoreNuget()
		{
			if (Orange.Nuget.Restore (SolutionPath, BuilderPath) != 0) {
				throw new Exception ("Unable to restore nugets!");
			}
		}

		private void SynchronizeAllProjects()
		{
			var csprojSyncList = Path.Combine(citrusDirectory, "Orange", "Launcher", "csproj_sync_list.txt");
			using (var stream = File.OpenRead(csprojSyncList)) {
				using (var reader = new StreamReader(stream)) {
					string line = null;
					while ((line = reader.ReadLine ()) != null) {
						Sync (line);
					}
				}
			}

			void Sync(string csprojPath) => Orange.CsprojSynchronization.SynchronizeProject(citrusDirectory + "/" + csprojPath);
		}

		public Task Start()
		{
			var task = new Task(() => {
				try {
					RestoreNuget();
					SynchronizeAllProjects();
					BuildAndRun();
				} catch (Exception e) {
					Console.WriteLine(e.Message);
					SetFailedBuildStatus(e.Message);
				}
			});
			task.Start();
			return task;
		}

		private void BuildAndRun()
		{
			Environment.CurrentDirectory = Path.GetDirectoryName(SolutionPath);
			ClearObjFolder(citrusDirectory);
			OnBuildStatusChange?.Invoke("Building");
			if (AreRequirementsMet() && Build(SolutionPath)) {
				ClearObjFolder(citrusDirectory);
				if (NeedRunExecutable) {
					RunExecutable();
				}
				OnBuildSuccess?.Invoke();
			}
		}

		private static void ClearObjFolder(string citrusDirectory)
		{
			// Mac-specific bug: while building Lime.iOS mdtool reuses obj folder after Lime.MonoMac build,
			// which results in invalid Lime.iOS assembly (missing classes, etc.).
			// Solution: remove obj folder after Orange build (and before, just in case).
			var path = Path.Combine(citrusDirectory, "Lime", "obj");
			if (Directory.Exists(path)) {
				// https://stackoverflow.com/a/1703799
				foreach (var dir in Directory.EnumerateDirectories(path)) {
					ForceDeleteDirectory(dir);
				}
				ForceDeleteDirectory(path);
			}
		}

		private static void ForceDeleteDirectory(string path)
		{
			try {
				Directory.Delete(path, true);
			} catch (IOException) {
				Thread.Sleep(100);
				Directory.Delete(path, true);
			} catch (UnauthorizedAccessException) {
				Thread.Sleep(100);
				Directory.Delete(path, true);
			}
		}

		private bool Build(string solutionPath)
		{
			var exitCode = Orange.Process.Start(BuilderPath, "-t:Build -p:Configuration=Release " + solutionPath);
			if (exitCode == 0) {
				return true;
			}
			SetFailedBuildStatus($"Process exited with code {exitCode}");
			return false;
		}

		private bool AreRequirementsMet()
		{
#if WIN
			if (BuilderPath != null) {
				return true;
			}
			Process.Start(@"https://visualstudio.microsoft.com/ru/thank-you-downloading-visual-studio/?sku=BuildTools&rel=16");
			SetFailedBuildStatus("Please install Microsoft Build Tools 2019");
			return false;
#else
			return true;
#endif // WIN
		}

		private void DecorateBuildProcess(Process process, string solutionPath)
		{
#if WIN
			process.StartInfo.Arguments =
				$"\"{solutionPath}\" /t:Build /p:Configuration=Release /p:Platform=\"Any CPU\" /verbosity:minimal";
			var cp = Encoding.Default.CodePage;
			if (cp == 1251)
				cp = 866;
			process.StartInfo.StandardOutputEncoding = Encoding.GetEncoding(cp);
			process.StartInfo.StandardErrorEncoding = Encoding.GetEncoding(cp);
#elif MAC
			// Perhaps we should refuse mdtool and vstool.
			process.StartInfo.Arguments = $"build \"{solutionPath}\" -t:Build -c:Release";
#endif // WIN
		}

		private void SetFailedBuildStatus(string details = null)
		{
			if (string.IsNullOrEmpty(details)) {
				details = "Send this text to our developers.";
			}
			OnBuildStatusChange?.Invoke($"Build failed. {details}");
			OnBuildFail?.Invoke();
		}

#if WIN
		private static string BuilderPath => "dotnet";
#elif MAC
		private string BuilderPath => "/Library/Frameworks/Mono.framework/Versions/Current/Commands/msbuild";
#endif // WIN
	}
}
