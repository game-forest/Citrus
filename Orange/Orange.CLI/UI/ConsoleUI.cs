using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Orange.Source;
using System.Linq;

namespace Orange
{
	public class TerminateException : Exception
	{
		public int Code;

		public TerminateException(int code)
		{
			Code = code;
		}
	}

	public class ConsoleUI : UserInterface
	{
		public override void Initialize()
		{
			base.Initialize();
#if MAC
			AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
#else
			AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
#endif
			CreateMenuItems(); 
			OpenWorkspace();
			RunCommand(Toolbox.GetCommandLineArg("--command"));
		}

		private static void OpenWorkspace()
		{
			var args = System.Environment.GetCommandLineArgs();
			if (args.Length >= 3 && !args[1].StartsWith("-")) {
				var projectFile = Path.Combine(Directory.GetCurrentDirectory(), args[1]);
				if (!File.Exists(projectFile)) {
					throw new FileNotFoundException($"Project file '{projectFile}' does not exist");
				}
				// TODO: consider if Load or Open should really be called.
				// Also, this is somehow chained with getting rid of "The" namespace
				The.Workspace.Load(projectFile);
			}
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			if (e.ExceptionObject is TerminateException te) {
				System.Environment.Exit(te.Code);
			}
		}
		
		void CurrentDomain_FirstChanceException(object sender, System.Runtime.ExceptionServices.FirstChanceExceptionEventArgs e)
		{
			if (e.Exception is TerminateException te) {
				System.Environment.Exit(te.Code);
			}
		}

		private void RunCommand(string command)
		{
			if (command == null) {
				WriteHelpAndExit();
			}
			var commands = The.MenuController.GetVisibleAndSortedItems();
			var commandObj = commands.Find(i => i.Label == command);
			if (commandObj == null) {
				Console.WriteLine("Unknown command: '{0}'", command);
				WriteHelpAndExit();
				return;
			}

			if (!OrangeActionsHelper.ExecuteOrangeActionInstantly(
				commandObj.Action, () => { }, () => { }
			)) {
				throw new TerminateException(1);
			}
		}

		private static void WriteHelpAndExit()
		{
			Console.WriteLine($"Orange.CLI [citrus_project]" +
			                  $" --target:[Win|Mac|ios|android|uc]" +
			                  $" --command:command" +
			                  $" [--autoupdate]" +
			                  $" [{Actions.ConsoleCommandPassArguments}:\"--statfile:<statistics.tsv> --testscript:<testscript.txt>\"]"
			);
			var commands = The.MenuController.GetVisibleAndSortedItems();
			if (commands.Count > 0) {
				Console.WriteLine("Available commands are:");
				foreach (var item in commands) {
					Console.WriteLine("\"" + item.Label + "\"");
				}
			}
			throw new TerminateException(1);
		}

		private static void CreateMenuItems()
		{
			var assembly = System.Reflection.Assembly.GetExecutingAssembly();
			The.MenuController.CreateAssemblyMenuItems();
		}

		public override bool AskConfirmation(string text)
		{
			Console.WriteLine(text + " (Y/N)");
			var ch = Console.Read();
			if (ch == 'Y' || ch == 'y') {
				return true;
			}
			return false;
		}

		public override bool AskChoice(string text, out bool yes)
		{
			Console.WriteLine(text + " (Y/N/C)");
			var ch = Console.Read();
			if (ch == 'Y' || ch == 'y') {
				yes = true;
				return true;
			}
			yes = false;
			if (ch == 'N' || ch == 'n') {
				return true;
			}
			return false;
		}

		public override void ShowError(string message)
		{
			Console.WriteLine(message);
		}

		public override Target GetActiveTarget()
		{
			var specifiedTarget = Toolbox.GetCommandLineArg("--target");
			if (specifiedTarget == null) {
	#if MAC
				specifiedTarget = "Mac";
	#else
				specifiedTarget = "Win";
	#endif
			}
			foreach (var target in The.Workspace.Targets) {
				if (string.Equals(specifiedTarget, target.Name, StringComparison.OrdinalIgnoreCase)) {
					return target;
				}
			}
			var validTargetsText = string.Join(", ", The.Workspace.Targets.Select(t => $"\"{t.Name}\""));
			throw new System.ArgumentException($"target with name \"{specifiedTarget}\" not found. Valid targets are: {validTargetsText}", "--target");
		}

		public override EnvironmentType GetEnvironmentType()
		{
			return EnvironmentType.Orange;
		}

		public override void ScrollLogToEnd()
		{
		}

		public override void OnWorkspaceOpened()
		{
		}

		public override void RefreshMenu()
		{
		}

		public override void ClearLog()
		{
		}

		public override void ExitWithErrorIfPossible()
		{
			Environment.Exit(1);
		}

		public override IPluginUIBuilder GetPluginUIBuilder()
		{
			return new PluginUIBuilder();
		}

		public override void CreatePluginUI(IPluginUIBuilder builder)
		{
		}

		public override void DestroyPluginUI()
		{
		}

		public override void SetupProgressBar(int maxPosition) { }
		public override void StopProgressBar() { }
		public override void IncreaseProgressBar(int amount = 1) { }

		private class PluginUIBuilder : IPluginUIBuilder
		{
			public IPluginPanel SidePanel { get; } = new PluginPanel();
		}

		private class PluginPanel : IPluginPanel
		{
			public bool Enabled { get; set; }

			public string Title { get; set; }

			public ICheckBox AddCheckBox(string label)
			{
				return new CheckBox();
			}
		}

		private class CheckBox : ICheckBox
		{
			public event EventHandler Toggled;

			public bool Active { get; set; }
		}
	}
}
