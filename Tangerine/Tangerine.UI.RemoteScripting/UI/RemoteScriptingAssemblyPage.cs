using System;
using System.IO;
using System.Linq;
using Lime;
using RemoteScripting;
using Tangerine.Core;
using Tangerine.Core.Commands;
using Exception = Lime.Exception;

namespace Tangerine.UI.RemoteScripting
{
	internal class RemoteScriptingAssemblyPage : RemoteScriptingWidgets.TabbedWidgetPage
	{
		private readonly RemoteScriptingStatusBar statusBar;
		private RemoteScriptingWidgets.TextView assemblyBuilderLog;
		private ToolbarButton buildGameAndAssemblyButton;
		private ToolbarButton buildAssemblyButton;

		public RemoteScriptingAssemblyPage(RemoteScriptingStatusBar statusBar)
		{
			this.statusBar = statusBar;
		}

		public override void Initialize()
		{
			Tab = new ThemedTab { Text = "Assembly" };
			RemoteScriptingWidgets.Toolbar toolbar;
			Content = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(toolbar = new RemoteScriptingWidgets.Toolbar()),
					(assemblyBuilderLog = new RemoteScriptingWidgets.TextView())
				}
			};
			var configurationDropDownList = new ThemedDropDownList();
			foreach (var p in ProjectPreferences.Instance.RemoteScriptingConfigurations) {
				configurationDropDownList.Items.Add(new CommonDropDownList.Item(p.Key, p.Value));
			}
			var configuration = RemoteScriptingPane.Instance.SelectedRemoteScriptingConfiguration;
			configurationDropDownList.Changed += (eventArgs) => {
				configuration = RemoteScriptingPane.Instance.SelectedRemoteScriptingConfiguration
					= (ProjectPreferences.RemoteScriptingConfiguration)eventArgs.Value;
				var preferences = ProjectPreferences.Instance;
				var arePreferencesCorrect =
					!string.IsNullOrEmpty(configuration.ScriptsPath) &&
					!string.IsNullOrEmpty(configuration.ScriptsAssemblyName) &&
					!string.IsNullOrEmpty(configuration.RemoteStoragePath);
				if (!arePreferencesCorrect) {
					buildAssemblyButton.Enabled = false;
					buildGameAndAssemblyButton.Enabled = false;
					SetStatusAndLog("Preferences are invalid.");
				}
			};
			toolbar.Content.Nodes.AddRange(
				buildAssemblyButton = new ToolbarButton("Build Assembly") { Clicked = () => BuildAssembly(configuration, requiredBuildGame: false) },
				buildGameAndAssemblyButton = new ToolbarButton("Build Game and Assembly") { Clicked = () => BuildAssembly(configuration) },
				configurationDropDownList
			);
			if (configurationDropDownList.Items.Any()) {
				configurationDropDownList.Index = 0;
			}
		}

		private void BuildAssembly(ProjectPreferences.RemoteScriptingConfiguration configuration, bool requiredBuildGame = true)
		{
			async void BuildAssemblyAsync()
			{
				buildAssemblyButton.Enabled = false;
				buildGameAndAssemblyButton.Enabled = false;

				try {
					if (requiredBuildGame) {
						SetStatusAndLog("Building game...");
						var target = Orange.The.Workspace.Targets.Where(t => t.Name == configuration.BuildTarget).First();
						var previousTarget = Orange.UserInterface.Instance.GetActiveTarget();
						Orange.UserInterface.Instance.SetActiveTarget(target);
						await OrangeBuildCommand.ExecuteAsync();
						Orange.UserInterface.Instance.SetActiveTarget(previousTarget);
						Log("Done.");
					}

					SetStatusAndLog("Building assembly...");

					var compiler = new CSharpCodeCompiler {
						ProjectReferences = configuration.FrameworkReferences.Concat(configuration.ProjectReferences)
					};
					var csFiles = Directory.EnumerateFiles(configuration.ScriptsPath, "*.cs", SearchOption.AllDirectories);
					SetStatusAndLog($"Compile code in {configuration.ScriptsPath} to assembly {configuration.ScriptsAssemblyName}..");
					var result = await compiler.CompileAssemblyToRawBytesAsync(configuration.ScriptsAssemblyName, csFiles);
					foreach (var diagnostic in result.Diagnostics) {
						Log(diagnostic.ToString());
					}
					var success = false;
					if (result.Success) {
						Log($"Assembly length in bytes: {result.AssemblyRawBytes.Length}");
						try {
							var portableAssembly = new PortableAssembly(result.AssemblyRawBytes, result.PdbRawBytes, configuration.EntryPointsClass);
							var compiledAssembly = new CompiledAssembly {
								RawBytes = result.AssemblyRawBytes,
								PdbRawBytes = result.PdbRawBytes,
								PortableAssembly = portableAssembly
							};
							CompiledAssembly.Instance = compiledAssembly;
							success = true;
						} catch (System.Reflection.ReflectionTypeLoadException exception) {
							Log(exception.ToString());
							Log("Can't load assembly due to type load exceptions:");
							foreach (var loaderException in exception.LoaderExceptions) {
								Log(loaderException.ToString());
							}
						} catch (Exception exception) {
							Log("Can't load assembly due to unknown exception:");
							Log(exception.ToString());
						}
					}
					SetStatusAndLog(success ? "Assembly was build." : "Assembly wasn't build due to errors in the code.");
					Log(string.Empty);
				} catch (System.Exception e) {
					System.Console.WriteLine(e);
				} finally {
					buildAssemblyButton.Enabled = true;
					buildGameAndAssemblyButton.Enabled = true;
				}
			}

			BuildAssemblyAsync();
		}

		public void Log(string text)
		{
			if (text != null) {
				assemblyBuilderLog.AppendLine(!string.IsNullOrWhiteSpace(text) ? $"[{DateTime.Now:dd.MM.yy H:mm:ss}] {text}" : text);
			}
		}

		private void SetStatusAndLog(string message)
		{
			statusBar.Text = message;
			Log(message);
		}
	}
}
