using System;
using System.Linq;
using Lime;
using RemoteScripting;
using Tangerine.Core;
using Tangerine.Core.Commands;

namespace Tangerine.UI.RemoteScripting
{
	public class AssemblyBuilder : ExplorableItem
	{
		private static ProjectPreferences.RemoteScriptingConfiguration Configuration
		{
			get => ProjectPreferences.Instance.RemoteScriptingCurrentConfiguration;
			set => ProjectPreferences.Instance.RemoteScriptingCurrentConfiguration = value;
		}

		private readonly ToolbarButton buildAssemblyButton;
		private readonly ToolbarButton buildGameAndAssemblyButton;
		private readonly ThemedDropDownList configurationDropDownList;
		private readonly LimitedTextView textView;

		public AssemblyBuilder(Toolbar toolbar)
		{
			Name = "Assembly";
			Content = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(textView = new LimitedTextView(maxRowCount: 1500))
				}
			};

			toolbar.Content.Nodes.AddRange(
				buildAssemblyButton = new ToolbarButton("Build Assembly") { Clicked = () => BuildAssembly(Configuration, requiredBuildGame: false) },
				buildGameAndAssemblyButton = new ToolbarButton("Build Game and Assembly") { Clicked = () => BuildAssembly(Configuration) }
			);

			configurationDropDownList = new ThemedDropDownList {
				MaxWidth = 200f
			};
			foreach (var (configurationName, configuration) in ProjectPreferences.Instance.RemoteScriptingConfigurations) {
				configurationDropDownList.Items.Add(new CommonDropDownList.Item(configurationName, configuration));
			}
			toolbar.Content.AddNode(configurationDropDownList);
			configurationDropDownList.Changed += args => {
				Configuration = (ProjectPreferences.RemoteScriptingConfiguration)args.Value;
				var arePreferencesCorrect =
					!string.IsNullOrEmpty(Configuration?.ScriptsProjectPath) &&
					!string.IsNullOrEmpty(Configuration?.ScriptsAssemblyName) &&
					!string.IsNullOrEmpty(Configuration?.RemoteStoragePath);
				buildAssemblyButton.Enabled = arePreferencesCorrect;
				buildGameAndAssemblyButton.Enabled = arePreferencesCorrect;
				if (!arePreferencesCorrect) {
					Log("Please, select a valid remote scripting configuration!");
				}
			};
			if (configurationDropDownList.Items.Any()) {
				configurationDropDownList.Index = 0;
			}

			Log("Please, compile the assembly.");
		}

#if WIN
		private void BuildAssembly(ProjectPreferences.RemoteScriptingConfiguration configuration, bool requiredBuildGame = true)
		{
			if (configuration == null) {
				Log("Please, select a valid remote scripting configuration!");
				return;
			}
			BuildAssemblyAsync();

			async void BuildAssemblyAsync()
			{
				buildAssemblyButton.Enabled = false;
				buildGameAndAssemblyButton.Enabled = false;
				configurationDropDownList.Enabled = false;

				try {
					if (requiredBuildGame) {
						Log("Building game...");
						var target = Orange.The.Workspace.Targets.First(t => t.Name == configuration.BuildTarget);
						var previousTarget = Orange.UserInterface.Instance.GetActiveTarget();
						Orange.UserInterface.Instance.SetActiveTarget(target);
						await OrangeBuildCommand.ExecuteAsync();
						Orange.UserInterface.Instance.SetActiveTarget(previousTarget);
						Log("Done.");
					}

					Log("Building assembly...");
					var csAnalyzer = new CSharpAnalyzer(configuration.ScriptsProjectPath);
					var csFiles = csAnalyzer.GetCompileItems().ToList();

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
								Log(loaderException.ToString());
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
					buildAssemblyButton.Enabled = true;
					buildGameAndAssemblyButton.Enabled = true;
					configurationDropDownList.Enabled = true;
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
