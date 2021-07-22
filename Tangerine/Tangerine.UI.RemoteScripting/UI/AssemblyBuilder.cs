using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lime;
using RemoteScripting;
using Tangerine.Core;
using Tangerine.Core.Commands;
using FileSystemWatcher = Lime.FileSystemWatcher;
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
		private AssemblyBuilderIcon icon = AssemblyBuilderDefaultIcon.Instance;
		private bool isAssemblyRequireGameBuild = true;
		private bool autoRebuildAssembly;
		private bool autoRebuildAssemblyAllowed;
		private FileSystemWatcher fileSystemWatcher;
		private float? timeSinceAssemblyWasModified;

		private float? TimeSinceAssemblyWasModified
		{
			get => timeSinceAssemblyWasModified;
			set
			{
				var wasNameChanged =
					timeSinceAssemblyWasModified.HasValue && !value.HasValue ||
					!timeSinceAssemblyWasModified.HasValue && value.HasValue;
				timeSinceAssemblyWasModified = value;
				if (wasNameChanged) {
					NameChanged();
				}
			}
		}

		public bool AutoRebuildAssembly
		{
			get => autoRebuildAssembly;
			set
			{
				autoRebuildAssembly = value;
				AutoRebuildAssemblyChanged();
			}
		}

		public delegate void AssemblyBuiltDelegate(CompiledAssembly assembly);
		public event AssemblyBuiltDelegate AssemblyBuilt;

		public event Action AssemblyBuildFailed;

		public AssemblyBuilder(Toolbar toolbar)
		{
			IconPresenter = new SyncDelegatePresenter<Widget>(RenderIcon);
			Content = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(textView = new LimitedTextView(maxRowCount: 1500))
				}
			};
			Updating += UpdateIconState;
			Updating += AutoRebuildAssemblyMonitoring;
			NameChanged();

			var configurationDropDownList = new ThemedDropDownList {
				MaxWidth = 200f
			};
			foreach (var (configurationName, configuration) in ProjectPreferences.Instance.RemoteScriptingConfigurations) {
				configurationDropDownList.Items.Add(new CommonDropDownList.Item(configurationName, configuration));
			}
			configurationDropDownList.Changed += args => {
				Configuration = (ProjectPreferences.RemoteScriptingConfiguration)args.Value;
				ConfigurationChanged();
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

			if (isAssemblyRequireGameBuild && !requiredBuildGame) {
				var buttons = new[] {
					"Assembly",
					"Game and Assembly",
					"Cancel"
				};
				var result = AlertDialog.Show(
					"You are trying to build assembly for the first time.\n" +
					"Assembly may require game binaries and it strongly recommended to build the game before building the scripts assembly.\n" +
					"Please select the most relevant option.",
					buttons
				);
				if (result == 2) {
					return;
				}
				if (result == 1) {
					requiredBuildGame = true;
				}
			}
			isAssemblyRequireGameBuild = false;

			BuildAssemblyAsync();

			async void BuildAssemblyAsync()
			{
				var success = false;
				try {
					const int DelayBetweenOperations = 1000 / 20;
					isBuildingInProgress = true;
					autoRebuildAssemblyAllowed = true;
					TransitIconStateTo(new AssemblyBuilderBuildingIcon());
					AssemblyBuilt?.Invoke(null);
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
					var csAnalyzer = await CSharpAnalyzer.CreateAsync(configuration.ScriptsProjectPath);
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
					if (result.Success) {
						Log($"Assembly length in bytes: {result.AssemblyRawBytes.Length}");
						try {
							var portableAssembly = await Task<PortableAssembly>.Factory.StartNew(() =>
								new PortableAssembly(result.AssemblyRawBytes, result.PdbRawBytes)
							);
							var compiledAssembly = new CompiledAssembly {
								RawBytes = result.AssemblyRawBytes,
								PdbRawBytes = result.PdbRawBytes,
								PortableAssembly = portableAssembly
							};
							AssemblyBuilt?.Invoke(compiledAssembly);
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
				} catch (System.Exception e) {
					System.Console.WriteLine(e);
					Log(e.ToString());
				} finally {
					isBuildingInProgress = false;
					TimeSinceAssemblyWasModified = null;
					if (!success) {
						autoRebuildAssemblyAllowed = false;
						AssemblyBuildFailed?.Invoke();
					}
					TransitIconStateTo(success ? (AssemblyBuilderIcon)AssemblyBuilderBuildSucceededIcon.Instance : AssemblyBuilderBuildFailedIcon.Instance);
					Log(success ? "Assembly was build." : "Assembly wasn't build due to errors in the code.");
					Log(string.Empty);
				}
			}
		}
#else
		private void BuildAssembly(ProjectPreferences.RemoteScriptingConfiguration configuration, bool requiredBuildGame = true) =>
			Log($"Building assembly is not supported on {Application.Platform}");
#endif // WIN

		private void NameChanged()
		{
			Name = TimeSinceAssemblyWasModified.HasValue ? "Assembly*" : "Assembly";
		}

		private void AutoRebuildAssemblyChanged()
		{
			autoRebuildAssemblyAllowed = false;
		}

		private void ConfigurationChanged()
		{
			autoRebuildAssemblyAllowed = false;
			TimeSinceAssemblyWasModified = 0;
			fileSystemWatcher?.Dispose();
			fileSystemWatcher = null;
			if (Configuration == null || !Configuration.IsValid) {
				return;
			}
			var directory = Path.GetDirectoryName(Configuration.ScriptsProjectPath);
			if (string.IsNullOrEmpty(directory)) {
				return;
			}
			fileSystemWatcher = new FileSystemWatcher(directory, includeSubdirectories: true);
			fileSystemWatcher.Changed += HandleFileSystemWatcherEvent;
			fileSystemWatcher.Created += HandleFileSystemWatcherEvent;
			fileSystemWatcher.Deleted += HandleFileSystemWatcherEvent;
			fileSystemWatcher.Renamed += (previousPath, newPath) => {
				// simulating rename as pairs of deleted / created events
				HandleFileSystemWatcherEvent(previousPath);
				HandleFileSystemWatcherEvent(newPath);
			};

			void HandleFileSystemWatcherEvent(string path)
			{
				var extension = Path.GetExtension(path);
				if (extension == ".csproj" || extension == ".cs") {
					TimeSinceAssemblyWasModified = 0;
				}
			}
		}

		private void AutoRebuildAssemblyMonitoring(float delta)
		{
			const float ModificationsAggregationDuration = 0.5f;
			if (TimeSinceAssemblyWasModified.HasValue) {
				TimeSinceAssemblyWasModified += delta;
			}
			if (
				AutoRebuildAssembly &&
				autoRebuildAssemblyAllowed &&
				!isBuildingInProgress &&
				TimeSinceAssemblyWasModified > ModificationsAggregationDuration &&
				Application.Windows.Any(window => window.Active)
			) {
				BuildAssembly(Configuration, requiredBuildGame: false);
			}
		}

		private void Log(string message)
		{
			if (message != null) {
				textView.AppendLine(!string.IsNullOrWhiteSpace(message) ? $"[{DateTime.Now:dd.MM.yy H:mm:ss}] {message}" : message);
			}
		}

		private void RenderIcon(Widget widget)
		{
			widget.PrepareRendererState();
			var cornerWidth = widget.Width * 0.375f;
			var cornerHeight = widget.Height * 0.125f;
			DrawCorner(Vector2.One, 1, 1, icon.TopLeftColor);
			DrawCorner(new Vector2(widget.Width - 1, 1), -1, 1, icon.TopRightColor);
			DrawCorner(widget.Size - Vector2.One, -1, -1, icon.BottomRightColor);
			DrawCorner(new Vector2(1, widget.Height - 1), 1, -1, icon.BottomLeftColor);

			void DrawCorner(Vector2 position, int h, int v, Color4 color)
			{
				Renderer.DrawRect(position, position + new Vector2(cornerWidth * h, cornerHeight * v), color);
				Renderer.DrawRect(position, position + new Vector2(cornerHeight * h, cornerWidth * v), color);
			}
		}

		private void TransitIconStateTo(AssemblyBuilderIcon destinationIcon)
		{
			icon = new AssemblyBuilderTransitionIcon(icon, destinationIcon);
		}

		private void UpdateIconState(float delta)
		{
			delta = Mathf.Min(delta, 1f / 30);
			icon.Update(delta);
			if (icon.IsDynamic) {
				Window.Current.Invalidate();
			}
			if (icon is AssemblyBuilderTransitionIcon { IsFinished: true } iconTransitionState) {
				icon = iconTransitionState.DestinationIcon;
			}
		}
	}
}
