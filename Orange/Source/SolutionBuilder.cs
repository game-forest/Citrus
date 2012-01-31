using System;
using System.IO;

namespace Orange
{
	public class SolutionBuilder
	{
		private CitrusProject project;
		private TargetPlatform platform;
		
		public SolutionBuilder(CitrusProject project, TargetPlatform platform)
		{
			this.project = project;
			this.platform = platform;
		}
		
		public static void CopyFile(string srcDir, string dstDir, string fileName)
		{
			string srcFile = Path.Combine(srcDir, fileName);
			string dstFile = Path.Combine(dstDir, fileName);
			Console.WriteLine("Copying: {0}", dstFile);
			System.IO.File.Copy(srcFile, dstFile, true);
		}

		public bool Build()
		{
			Console.WriteLine("------------- Building Game Application -------------");
			string app, args, slnFile;
#if MAC
			app = "/Applications/MonoDevelop.app/Contents/MacOS/mdtool";
			if (platform == TargetPlatform.iOS) {
				slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".iOS", project.Title + ".iOS.sln");
				args = String.Format("build \"{0}\" -t:Build -c:\"Release|iPhone\"", slnFile);
			} else {
				slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".Mac", project.Title + ".Mac.sln");
				args = String.Format("build \"{0}\" -t:Build -c:\"Release|x86\"", slnFile);
			}
#elif WIN
			// Uncomment follow block if you would like to use mdtool instead of MSBuild
			/*
			app = @"C:\Program Files(x86)\MonoDevelop\bin\mdtool.exe";
			slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".Win", project.Title + ".Win.sln");
			args = String.Format("build \"{0}\" -t:Build -c:\"Release|x86\"", slnFile);
			*/

			app = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "MSBuild.exe");
			slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".Win", project.Title + ".Win.sln");
			args = String.Format("\"{0}\" /verbosity:minimal /p:Configuration=Release", slnFile);
#endif
			if (Helpers.StartProcess(app, args) != 0) {
				return false;
			}
#if MAC
			if (platform == TargetPlatform.Desktop) {
				string appName = Path.GetFileName(project.ProjectDirectory);
				string src = "/Applications/MonoDevelop.app/Contents/MacOS/lib/monodevelop/Addins";
				string dst = Path.Combine(project.ProjectDirectory, appName + ".Mac", "bin/Release", appName + ".app", "Contents/Resources");
				CopyFile(src, dst, "MonoMac.dll");
			}
#endif
			return true;
		}

		public bool Clean()
		{
			Console.WriteLine("------------- Cleanup Game Application -------------");
			string app, args, slnFile;
#if MAC
			app = "/Applications/MonoDevelop.app/Contents/MacOS/mdtool";
			if (platform == TargetPlatform.iOS) {
				slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".iOS", project.Title + ".iOS.sln");
				args = String.Format("build \"{0}\" -t:Clean -c:\"Release|iPhone\"", slnFile);
			} else {
				slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".Mac", project.Title + ".Mac.sln");
				args = String.Format("build \"{0}\" -t:Clean -c:\"Release|x86\"", slnFile);
			}
#elif WIN
			// Uncomment follow block if you would like to use mdtool instead of MSBuild
			/*
			app = @"C:\Program Files(x86)\MonoDevelop\bin\mdtool.exe";
			slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".Win", project.Title + ".Win.sln");
			args = String.Format("build \"{0}\" -t:Clean -c:\"Release|x86\"", slnFile);
			*/

			app = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "MSBuild.exe");
			slnFile = Path.Combine(project.ProjectDirectory, project.Title + ".Win", project.Title + ".Win.sln");
			args = String.Format("\"{0}\" /t:Clean /p:Configuration=Release", slnFile);
#endif
			if (Helpers.StartProcess(app, args) != 0) {
				return false;
			}
			return true;
		}
		
		public void Run()
		{
			Console.WriteLine("------------- Starting Game -------------");
			string app, dir;
#if MAC
			if (platform == TargetPlatform.Desktop) {
				app = Path.Combine(project.ProjectDirectory, project.Title + ".Mac", "bin/Release", project.Title + ".app", "Contents/MacOS", project.Title);
				dir = Path.GetDirectoryName(app);
			} else {
				throw new NotImplementedException();
			}
#elif WIN
			app = Path.Combine(project.ProjectDirectory, project.Title + ".Win", "bin/Release", project.Title + ".exe");
			dir = Path.GetDirectoryName(app);
#endif
			using(new DirectoryChanger(dir)) {
				Helpers.StartProcess(app, "");
			}
		}
	}
}

