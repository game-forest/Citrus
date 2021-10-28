using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Lime;
using Orange;
using Tangerine.Core;
using Tangerine.Core.Commands;
using Tangerine.Core.Operations;
using Tangerine.MainMenu;
using Tangerine.UI;
using Tangerine.UI.SceneView;

namespace Tangerine
{
	static class TangerineMenu
	{
		public static readonly List<ICommand> CreateNodeCommands = new List<ICommand>();
		public static IMenu PanelsMenu;
		public static Menu overlaysMenu;
		public static Menu rulerMenu;
		private static IMenu resolution;
		private static ICommand customNodes;
		private static IMenu create;
		private static Menu localizationMenu;
		private static Menu orangeMenu;
		private static readonly List<ICommand> orangeCommands = new List<ICommand>();
		private static ICommand orangeMenuCommand;

		static TangerineMenu()
		{
			PanelsMenu = new Menu();
		}

		public static void Create()
		{
			resolution = new Menu();
			overlaysMenu = new Menu();
			rulerMenu = new Menu();
			orangeMenu = new Menu();
			orangeMenuCommand = new Command("Orange", orangeMenu);
			RebuildOrangeMenu(null);
			CreateMainMenu();
			CreateResolutionMenu();
		}

		private static void CreateResolutionMenu()
		{
			foreach (var orientation in DisplayResolutions.Items) {
				resolution.Add(new Command(orientation.Name, () => DisplayResolutions.SetResolution(orientation)));
			}
		}

		public static void RebuildProjectDependentMenus()
		{
			var menus = new[] { customNodes.Menu, GenericCommands.NewSceneWithCustomRoot.Menu, create };
			foreach (var menu in menus) {
				foreach (var command in menu) {
					CommandHandlerList.Global.Disconnect(command);
				}
			}
			CreateNodeCommands.Clear();
			customNodes.Menu.Clear();
			GenericCommands.NewSceneWithCustomRoot.Menu.Clear();
			create.Clear();
			create.Add(customNodes = new Command("Custom Nodes", new Menu()));
			var registeredNodeTypes =
				Project.Current == Project.Null
				? Project.GetNodeTypesOrdered("Lime")
				: Project.Current.RegisteredNodeTypes;
			GenericCommands.ConvertTo.Menu = GenerateConvertToMenu(registeredNodeTypes);
			foreach (var type in registeredNodeTypes) {
				var tooltipText = ClassAttributes<TangerineTooltipAttribute>.Get(type, true)?.Text;
				var cmd = new Command("Create " + type.Name) {
					TooltipText = tooltipText
				};
				bool isProjectSpecificType = type.Namespace != "Lime";
				if (NodeIconPool.TryGetIcon(type, out var icon)) {
					cmd.Icon = icon;
				} else {
					cmd.Icon = NodeIconPool.DefaultIcon;
					NodeIconPool.GenerateIcon(type, newIcon => cmd.Icon = newIcon);
				}
				var menuPath = ClassAttributes<TangerineMenuPathAttribute>.Get(type, true)?.Path;
				if (menuPath != null) {
					create.InsertCommandAlongPath(cmd, menuPath);
					if (!menuPath.EndsWith("/")) {
						cmd.Text = "Create " + cmd.Text;
					}
				} else {
					if (isProjectSpecificType) {
						customNodes.Menu.Add(cmd);
					} else {
						create.Add(cmd);
					}
				}
				CreateNodeCommands.Add(cmd);
				CommandRegistry.Register(cmd, "CreateCommands", "Create" + type.Name, @override: true, isProjectSpecificType);
				CommandHandlerList.Global.Connect(cmd, new CreateNode(type, cmd));
				if (IsNodeTypeCanBeRoot(type)) {
					var newFileCmd = new Command(type.Name);
					var format = typeof(Node3D).IsAssignableFrom(type) ? DocumentFormat.T3D : DocumentFormat.Tan;
					CommandHandlerList.Global.Connect(newFileCmd, new FileNew(format, type));
					GenericCommands.NewSceneWithCustomRoot.Menu.Add(newFileCmd);
				}
			}
			customNodes.Enabled = customNodes.Menu.Count > 0;
			GenericCommands.NewSceneWithCustomRoot.Enabled = GenericCommands.NewSceneWithCustomRoot.Menu.Count > 0;
			HotkeyRegistry.CurrentProjectName = Project.Current == null
				? null
				: System.IO.Path.GetFileNameWithoutExtension(Project.Current.CitprojPath);
			TangerineApp.Instance?.RefreshCreateNodeCommands();
		}

		private static IMenu GenerateConvertToMenu(IEnumerable<Type> registeredNodeTypes)
		{
			IMenu menu = new Menu();
			foreach (var type in registeredNodeTypes) {
				var tooltipText = type.GetCustomAttribute<TangerineTooltipAttribute>()?.Text;
				var menuPath = type.GetCustomAttribute<TangerineMenuPathAttribute>()?.Path;
				ICommand command = new Command(CamelCaseToLabel(type.Name), () => {
					Core.Document.Current.History.DoTransaction(() => {
						var rowToParentCount = Document.Current.SelectedRows()
							.Where(r => r.GetNode() != null)
							.ToDictionary(k => k, v => 0);
						foreach (var (row, _) in rowToParentCount) {
							var parent = row.Parent;
							while (parent != null) {
								if (rowToParentCount.ContainsKey(parent)) {
									rowToParentCount[row]++;
								}
								parent = parent.Parent;
							}
						}
						var sortedRows = rowToParentCount.OrderBy(i => -i.Value).ToList();
						foreach (var kv in sortedRows) {
							var row = kv.Key;
							try {
								NodeTypeConvert.Perform(row, type, typeof(Node));
							} catch (InvalidOperationException e) {
								AlertDialog.Show(e.Message);
								Document.Current.History.RollbackTransaction();
								return;
							}
						}
					});
				}) {
					TooltipText = tooltipText
				};
				if (menuPath != null) {
					menu.InsertCommandAlongPath(command, menuPath);
				} else {
					menu.Add(command);
				}
			}
			return menu;
		}

		private static string CamelCaseToLabel(string text)
		{
			return Regex.Replace(Regex.Replace(text, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
		}

		private static bool IsNodeTypeCanBeRoot(Type type)
		{
			return ClassAttributes<TangerineRegisterNodeAttribute>.Get(type).CanBeRoot;
		}

		private static void CreateMainMenu()
		{
			Menu viewMenu;
			Application.MainMenu = new Menu {
#if MAC
				new Command("Application", new Menu {
					GenericCommands.PreferencesDialog,
					Command.MenuSeparator,
					GenericCommands.Quit,
				}),
#endif
				new Command("File", new Menu {
					new Command("New", new Menu {
						GenericCommands.NewScene,
						GenericCommands.NewSceneWithCustomRoot,
					}),
					GenericCommands.NewProject,
					Command.MenuSeparator,
					GenericCommands.Open,
					GenericCommands.OpenProject,
					GenericCommands.CloseProject,
					Command.MenuSeparator,
					GenericCommands.RecentDocuments,
					GenericCommands.RecentProjects,
					Command.MenuSeparator,
					GenericCommands.Save,
					GenericCommands.SaveAs,
					GenericCommands.SaveAll,
					GenericCommands.Revert,
					GenericCommands.UpgradeDocumentFormat,
					Command.MenuSeparator,
#if !MAC
					GenericCommands.PreferencesDialog,
					GenericCommands.OpenLookupDialog,
					Command.MenuSeparator,
#endif
					GenericCommands.Close,
					GenericCommands.CloseAll,
					GenericCommands.CloseAllButCurrent,
#if !MAC
					GenericCommands.Quit,
#endif
				}),
				new Command("Edit", new Menu {
					Command.Undo,
					Command.Redo,
					Command.MenuSeparator,
					Command.Cut,
					Command.Copy,
					Command.Paste,
					Command.Delete,
					SceneViewCommands.Duplicate,
					TimelineCommands.DeleteKeyframes,
					TimelineCommands.CreateMarkerPlay,
					TimelineCommands.CreateMarkerStop,
					TimelineCommands.CreateMarkerJump,
					TimelineCommands.DeleteMarker,
					Command.MenuSeparator,
					Command.SelectAll,
					Command.MenuSeparator,
					GenericCommands.Group,
					GenericCommands.Ungroup,
					GenericCommands.InsertTimelineColumn,
					GenericCommands.RemoveTimelineColumn,
					Command.MenuSeparator,
					SceneViewCommands.TieWidgetsWithBones,
					SceneViewCommands.UntieWidgetsFromBones,
					GenericCommands.ExportScene,
					GenericCommands.UpsampleAnimationTwice,
					GenericCommands.ConvertTo,
					SceneViewCommands.GeneratePreview,
					SceneViewCommands.AddComponentToSelection
				}),
				new Command("Create", (create = new Menu())),
				new Command("Tools", new Menu {
					ToolsCommands.RenderToPngSequence,
					ToolsCommands.OpenConflictingAnimatorsDialog,
				}),
				new Command("View", (viewMenu = new Menu {
					new Command("Layouts", (new Menu {
						GenericCommands.LockLayout,
						GenericCommands.SaveLayout,
						GenericCommands.LoadLayout,
						GenericCommands.DefaultLayout,
					})),
					new Command("Panels", PanelsMenu),
					new Command("Resolution", resolution),
					Command.MenuSeparator,
					new Command("Overlays", overlaysMenu),
					new Command("Rulers", rulerMenu),
					SceneViewCommands.SnapWidgetBorderToRuler,
					SceneViewCommands.SnapWidgetPivotToRuler,
					SceneViewCommands.SnapRulerLinesToWidgets,
					SceneViewCommands.ResolutionChanger,
					SceneViewCommands.ResolutionReverceChanger,
					SceneViewCommands.ResolutionOrientation,
					TimelineCommands.CenterTimelineOnCurrentColumn,
					new Command("Locale", localizationMenu = new Menu()),
				})),
				new Command("Window", new Menu {
					GenericCommands.NextDocument,
					GenericCommands.PreviousDocument
				}),
				orangeMenuCommand,
				new Command("Git", new Menu {
					GitCommands.ForceUpdate,
					GitCommands.Update,
				}),
				new Command("Help", new Menu {
					GenericCommands.ViewHelp,
					GenericCommands.HelpMode,
					GenericCommands.ViewChangelog,
					GenericCommands.About
				}),
				new Command("System", new Menu {
					GenericCommands.ResetGlobalSettings,
					GenericCommands.ClearCache,
					GenericCommands.PurgeBackups,
				}),
			};
			create.Add(customNodes = new Command("Custom Nodes", new Menu()));
			foreach (var t in Project.GetNodeTypesOrdered("Lime")) {
				var cmd = new Command(t.Name);
				if (NodeIconPool.TryGetIcon(t, out var icon)) {
					cmd.Icon = icon;
				} else {
					cmd.Icon = NodeIconPool.DefaultIcon;
					NodeIconPool.GenerateIcon(t, newIcon => cmd.Icon = newIcon);
				}
				CommandHandlerList.Global.Connect(cmd, new CreateNode(t, cmd));
				create.Add(cmd);
				CreateNodeCommands.Add(cmd);
			}
			Command.Undo.Icon = IconPool.GetIcon("Tools.Undo");
			Command.Redo.Icon = IconPool.GetIcon("Tools.Redo");
			GenericCommands.Revert.Icon = IconPool.GetIcon("Tools.Revert");
		}

		private static void RebuildOrangeMenu(string citprojPath)
		{
			orangeMenu.Clear();
			foreach (var command in orangeCommands) {
				CommandHandlerList.Global.Disconnect(command);
			}
			orangeCommands.Clear();
			OrangeBuildCommand.Instance = null;
			orangeMenuCommand.Enabled = citprojPath != null;
			if (!orangeMenuCommand.Enabled) {
				return;
			}

			void AddOrangeCommand(ICommand command, CommandHandler handler)
			{
				orangeMenu.Add(command);
				CommandHandlerList.Global.Connect(command, handler);
			}
			void OnOrangeCommandExecuting()
			{
				UI.Console.Instance.Show();
			}
			var orangeMenuItems = Orange.MenuController.Instance.GetVisibleAndSortedItems();
			const string BuildAndRunLabel = "Build and Run";
			const string BuildLabel = "Build";
			const string CookGameAssetsLabel = "Cook Game Assets";
			var blacklist = new HashSet<string> { "Run Tangerine", BuildAndRunLabel, BuildLabel, CookGameAssetsLabel };
			var buildAndRun = orangeMenuItems.First(item => item.Label == BuildAndRunLabel);
			var build = orangeMenuItems.First(item => item.Label == BuildLabel);
			var cookGameAssets = orangeMenuItems.First(item => item.Label == CookGameAssetsLabel);
			AddOrangeCommand(
				OrangeCommands.Run,
				new OrangeCommand(() => buildAndRun.Action()) { Executing = OnOrangeCommandExecuting }
			);
			AddOrangeCommand(
				OrangeCommands.Build,
				OrangeBuildCommand.Instance = new OrangeBuildCommand(build.Action) {
					Executing = OnOrangeCommandExecuting
				}
			);
			AddOrangeCommand(OrangeCommands.RunConfig, new OrangePluginOptionsCommand());
			AddOrangeCommand(
				OrangeCommands.CookGameAssets,
				new OrangeCommand(() => cookGameAssets.Action()) { Executing = OnOrangeCommandExecuting }
			);
			foreach (var menuItem in orangeMenuItems) {
				if (blacklist.Contains(menuItem.Label)) {
					continue;
				}
				AddOrangeCommand(
					new Command(menuItem.Label),
					new OrangeCommand(() => menuItem.Action()) { Executing = OnOrangeCommandExecuting }
				);
			}

			// TODO Duplicates code from Orange.GUI.OrangeInterface.cs. Both should be presented at one file
			var orangeInterfaceInstance = (OrangeInterface) Orange.UserInterface.Instance;
			var updateAction = new Action<AssetCacheMode>(
				mode => orangeInterfaceInstance.UpdateCacheModeCheckboxes(mode)
			);
			orangeInterfaceInstance.CacheLocalAndRemote = new Command(
				text: "Local &and remote",
				execute: () => updateAction(Orange.AssetCacheMode.Local | Orange.AssetCacheMode.Remote)
			);
			orangeInterfaceInstance.CacheRemote = new Command(
				text: "&Remote",
				execute: () => updateAction(Orange.AssetCacheMode.Remote)
			);
			orangeInterfaceInstance.CacheLocal = new Command(
				text: "&Local",
				execute: () => updateAction(Orange.AssetCacheMode.Local)
			);
			orangeInterfaceInstance.CacheNone = new Command(
				text: "&None",
				execute: () => updateAction(Orange.AssetCacheMode.None)
			);

			var uploadCacheToServerCommand = new Command("&Upload cache to server");
			CommandHandlerList.Global.Connect(
				uploadCacheToServerCommand,
				new OrangeCommand(UploadCacheToServer.UploadCacheToServerAction) {
					Executing = OnOrangeCommandExecuting
				}
			);

			orangeMenu.Add(new Command("Cache", new Menu {
				new Command("&Actions", new Menu {
					uploadCacheToServerCommand
				}),
				new Command("Mode", new Menu{
					orangeInterfaceInstance.CacheLocalAndRemote,
					orangeInterfaceInstance.CacheRemote,
					orangeInterfaceInstance.CacheLocal,
					orangeInterfaceInstance.CacheNone,
				})
			}));
			var config = Orange.WorkspaceConfig.Load();
			Orange.The.UI.LoadFromWorkspaceConfig(config, config.GetProjectConfig(citprojPath));
			Orange.The.Workspace.LoadCacheSettings();
		}

		public static void OnProjectChanged(Project proj)
		{
			foreach (var item in overlaysMenu) {
				CommandHandlerList.Global.Disconnect(item);
			}
			overlaysMenu.Clear();
			RebuildOrangeMenu(proj.CitprojPath);
			LocalizationMenuFactory.Rebuild(localizationMenu);
			if (proj == Project.Null)
				return;
			proj.UserPreferences.Rulers.CollectionChanged += OnRulersCollectionChanged;
			AddRulersCommands(proj.UserPreferences.DefaultRulers);
			AddRulersCommands(proj.UserPreferences.Rulers);
			RebuildRulerMenu();
			AddOverlaysCommands(proj.Overlays);
		}

		private static void RebuildRulerMenu()
		{
			rulerMenu.Clear();
			rulerMenu.Add(SceneViewCommands.ToggleDisplayRuler);
			rulerMenu.Add(SceneViewCommands.ClearActiveRuler);
			rulerMenu.Add(SceneViewCommands.SaveCurrentRuler);
			rulerMenu.Add(SceneViewCommands.ManageRulers);
			rulerMenu.Add(SceneViewCommands.CreateRulerGrid);
			rulerMenu.Add(Command.MenuSeparator);
			foreach (var ruler in ProjectUserPreferences.Instance.DefaultRulers) {
				rulerMenu.Add(ruler.Components.Get<CommandComponent>().Command);
			}
			if (ProjectUserPreferences.Instance.Rulers.Count > 0) {
				rulerMenu.Add(Command.MenuSeparator);
			}
			foreach (var ruler in ProjectUserPreferences.Instance.Rulers) {
				rulerMenu.Add(ruler.Components.Get<CommandComponent>().Command);
			}
		}

		public static void OnRulersCollectionChanged(
			object sender,
			System.Collections.Specialized.NotifyCollectionChangedEventArgs e
		) {
			// Invoke handler at the next update to avoid collection changed exceptions while
			// command handler iterates the commands list.
			UpdateHandler handler = null;
			handler = delta => {
				AddRulersCommands(e.NewItems, true);
				RemoveRulersCommands(e.OldItems);
				RebuildRulerMenu();
				UI.Docking.DockManager.Instance.MainWindowWidget.Updated -= handler;
			};
			UI.Docking.DockManager.Instance.MainWindowWidget.Updated += handler;
		}

		public static void AddRulersCommands(IEnumerable rulers, bool issueCommands = false)
		{
			if (rulers == null)
				return;
			foreach (Ruler ruler in rulers) {
				Command c;
				ruler.Components.Add(new CommandComponent {
					Command = (c = new Command(ruler.Name))
				});
				CommandHandlerList.Global.Connect(c, new RulerToggleCommandHandler(ruler.Name));
				if (issueCommands) {
					c.Issue();
				}
			}
		}

		public static void RemoveRulersCommands(IEnumerable rulers)
		{
			if (rulers == null)
				return;
			foreach (Ruler ruler in rulers) {
				CommandHandlerList.Global.Disconnect(ruler.Components.Get<CommandComponent>().Command);
				ProjectUserPreferences.Instance.DisplayedRulers.Remove(ruler.Name);
				ruler.Components.Clear();
			}
		}

		private static void AddOverlaysCommands(SortedDictionary<string, Widget> overlays)
		{
			var commands = new List<ICommand>(overlays.Count) {
				GenericCommands.Overlay1, GenericCommands.Overlay2, GenericCommands.Overlay3,
				GenericCommands.Overlay4, GenericCommands.Overlay5, GenericCommands.Overlay6,
				GenericCommands.Overlay7, GenericCommands.Overlay8, GenericCommands.Overlay9
			};
			int lastIndex = 0;
			foreach (var overlayPair in overlays) {
				var command = lastIndex < 9 ? commands[lastIndex] : new Command(overlayPair.Key);
				command.Text = overlayPair.Key;
				overlayPair.Value.Components.Add(new NodeCommandComponent { Command = command });
				overlaysMenu.Add(command);
				var commandHandler = new OverlayToggleCommandHandler(overlayPair.Key);
				CommandHandlerList.Global.Connect(command, commandHandler);
				lastIndex++;
			}
		}

		public static void RebuildRecentDocumentsMenu()
		{
			var recentDocuments = ProjectUserPreferences.Instance.RecentDocuments;
			int recentDocumentCount = CoreUserPreferences.Instance.RecentDocumentCount;
			if (recentDocuments.Count > recentDocumentCount) {
				recentDocuments.RemoveTail(startIndex: recentDocumentCount);
			}
			var menu = new Menu();
			int counter = 1;
			foreach (var i in recentDocuments) {
				string name = System.String.Format("{0}. {1}", counter++, i);
				menu.Add(new Command(name, () => Project.Current.OpenDocument(i) ));
			}
			GenericCommands.RecentDocuments.Menu = menu;
			GenericCommands.RecentDocuments.Enabled = recentDocuments.Count > 0;
		}

		public static void RebuildRecentProjectsMenu()
		{
			var recentProjects = AppUserPreferences.Instance.RecentProjects;
			int recentProjectCount = AppUserPreferences.Instance.RecentProjectCount;
			if (recentProjects.Count > recentProjectCount) {
				recentProjects.RemoveTail(startIndex: recentProjectCount);
			}
			var menu = new Menu();
			int counter = 1;
			foreach (var i in recentProjects) {
				string name = System.String.Format("{0}. {1} ({2})", counter++, System.IO.Path.GetFileName(i),
					System.IO.Path.GetDirectoryName(i));
				menu.Add(new Command(name, () =>  {
					if (Project.Current.Close()) {
						_ = new Project(i);
						FileOpenProject.AddRecentProject(i);
					}
				}));
			}
			GenericCommands.RecentProjects.Menu = menu;
			GenericCommands.RecentProjects.Enabled = recentProjects.Count > 0;
		}

		private class OverlayToggleCommandHandler : DocumentCommandHandler
		{
			private readonly string overlayName;

			public override bool GetChecked()
			{
				return ProjectUserPreferences.Instance.DisplayedOverlays.Contains(overlayName);
			}

			public OverlayToggleCommandHandler(string overlayName) => this.overlayName = overlayName;

			public override void ExecuteTransaction()
			{
				var prefs = ProjectUserPreferences.Instance;
				if (prefs.DisplayedOverlays.Contains(overlayName)) {
					prefs.DisplayedOverlays.Remove(overlayName);
				} else {
					prefs.DisplayedOverlays.Add(overlayName);
				}
				Application.InvalidateWindows();
			}
		}

		private class RulerToggleCommandHandler : DocumentCommandHandler
		{
			private readonly string rulerName;

			public override bool GetChecked() => ProjectUserPreferences.Instance
				.DisplayedRulers.Contains(rulerName);

			public RulerToggleCommandHandler(string rulerName)
			{
				this.rulerName = rulerName;
			}

			public override void ExecuteTransaction()
			{
				var prefs = ProjectUserPreferences.Instance;
				if (prefs.DisplayedRulers.Contains(rulerName)) {
					prefs.DisplayedRulers.Remove(rulerName);
				} else {
					prefs.DisplayedRulers.Add(rulerName);
				}
				Application.InvalidateWindows();
			}
		}
	}
}
