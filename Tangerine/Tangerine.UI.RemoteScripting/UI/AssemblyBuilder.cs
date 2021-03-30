using System;
using System.Linq;
using Lime;
using RemoteScripting;
using Tangerine.Core;
using Tangerine.Core.Commands;
using Task = System.Threading.Tasks.Task;

namespace Tangerine.UI.RemoteScripting
{
	public class AssemblyBuilder : ExplorableItem
	{
		private static ProjectPreferences.RemoteScriptingConfiguration Configuration
		{
			get => ProjectPreferences.Instance.RemoteScriptingCurrentConfiguration;
			set => ProjectPreferences.Instance.RemoteScriptingCurrentConfiguration = value;
		}

		private readonly LimitedTextView textView;
		private bool isBuildingInProgress;

		public AssemblyBuilder(Toolbar toolbar)
		{
			Name = "Assembly";
			Content = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(textView = new LimitedTextView(maxRowCount: 1500))
				}
			};

			var configurationDropDownList = new ThemedDropDownList {
				MaxWidth = 200f
			};
			foreach (var (configurationName, configuration) in ProjectPreferences.Instance.RemoteScriptingConfigurations) {
				configurationDropDownList.Items.Add(new CommonDropDownList.Item(configurationName, configuration));
			}
			configurationDropDownList.Changed += args => {
				Configuration = (ProjectPreferences.RemoteScriptingConfiguration)args.Value;
				if (Configuration == null || !Configuration.IsValid) {
					Log("Please, select a valid remote scripting configuration!");
				}
			};
			toolbar.AddSeparator(rightOffset: 1);
			toolbar.Content.Nodes.AddRange(
				new ThemedSimpleText("Configuration:") {
					Padding = new Thickness(horizontal: 3, vertical: 2),
					ForceUncutText = true,
				},
				configurationDropDownList
			);
			toolbar.AddSeparator(leftOffset: 3, rightOffset: 1);
			if (configurationDropDownList.Items.Any()) {
				configurationDropDownList.Index = 0;
			}

			ICommand buildAssemlyCommand;
			ICommand buildGameAndAssemlyCommand;
			var buildButton = toolbar.AddMenuButton(
				"Build",
				new Menu {
					(buildAssemlyCommand = new Command("Build Assembly")),
					(buildGameAndAssemlyCommand = new Command("Build Game and Assembly")),
				}
			);
			buildAssemlyCommand.Issued += () => BuildAssembly(Configuration, requiredBuildGame: false);
			buildGameAndAssemlyCommand.Issued += () => BuildAssembly(Configuration);
			buildButton.AddChangeWatcher(
				() => !isBuildingInProgress && Configuration != null && Configuration.IsValid,
				enabled => buildButton.Enabled = enabled
			);

			Log("Please, build the assembly.");
			Log(string.Empty);
		}

#if WIN
		private void BuildAssembly(ProjectPreferences.RemoteScriptingConfiguration configuration, bool requiredBuildGame = true)
		{
			if (isBuildingInProgress) {
				return;
			}
			if (configuration == null || !configuration.IsValid) {
				Log("Please, select a valid remote scripting configuration!");
				Log(string.Empty);
				return;
			}
			BuildAssemblyAsync();

			async void BuildAssemblyAsync()
			{
				try {
					const int DelayBetweenOperations = 1000 / 20;
					isBuildingInProgress = true;
					CompiledAssembly.Instance = null;

					await Task.Delay(DelayBetweenOperations);

					if (requiredBuildGame) {
						Log("Building game...");
						var target = Orange.The.Workspace.Targets.First(t => t.Name == configuration.BuildTarget);
						var previousTarget = Orange.UserInterface.Instance.GetActiveTarget();
						Orange.UserInterface.Instance.SetActiveTarget(target);
						await OrangeBuildCommand.ExecuteAsync();
						Orange.UserInterface.Instance.SetActiveTarget(previousTarget);
						Log("Done.");
						await Task.Delay(DelayBetweenOperations);
					}

					Log("Building assembly...");
					var csAnalyzer = new CSharpAnalyzer(configuration.ScriptsProjectPath);
					var csFiles = csAnalyzer.GetCompileItems().ToList();
					await Task.Delay(DelayBetweenOperations);

					Log($"Compile code in {configuration.ScriptsProjectPath} to assembly {configuration.ScriptsAssemblyName}..");
					var compiler = new CSharpCodeCompiler {
						ProjectReferences = configuration.FrameworkReferences.Concat(configuration.ProjectReferences)
					};
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
								if (loaderException != null) {
									Log(loaderException.ToString());
								}
							}
						} catch (System.Exception exception) {
							Log("Can't load assembly due to unknown exception:");
							Log(exception.ToString());
						}
					}
					Log(success ? "Assembly was build." : "Assembly wasn't build due to errors in the code.");
					Log(string.Empty);
				} catch (System.Exception e) {
					System.Console.WriteLine(e);
					Log(e.ToString());
				} finally {
					isBuildingInProgress = false;
				}
			}
		}
#else
		private void BuildAssembly(ProjectPreferences.RemoteScriptingConfiguration configuration, bool requiredBuildGame = true) =>
			Log($"Building assembly is not supported on {Application.Platform}");
#endif // WIN

		private void Log(string message)
		{
			if (message != null) {
				textView.AppendLine(!string.IsNullOrWhiteSpace(message) ? $"[{DateTime.Now:dd.MM.yy H:mm:ss}] {message}" : message);
			}
		}
	}
}
