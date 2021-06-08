using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Lime;
using Orange;
using Tangerine.Common.FilesDropHandlers;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.MainMenu;
using Tangerine.Panels;
using Tangerine.UI;
using Tangerine.UI.AnimeshEditor;
using Tangerine.UI.AnimeshEditor.Operations;
using Tangerine.UI.SceneView;
using Tangerine.UI.Docking;
using Tangerine.UI.Timeline;
using FileInfo = System.IO.FileInfo;

namespace Tangerine
{
	public class TangerineApp
	{
		public static TangerineApp Instance { get; private set; }
		public ToolbarView Toolbar { get; private set; }
		public readonly DockManager.State DockManagerInitialState;

		public static void Initialize(string[] args)
		{
			Instance = new TangerineApp(args);
		}

		private TangerineApp(string[] args)
		{
			ChangeTangerineSettingsFolderIfNeed();
			Orange.UserInterface.Instance = new OrangeInterface();
			Orange.UserInterface.Instance.Initialize();
			Widget.EnableViewCulling = false;
			WidgetInput.AcceptMouseBeyondWidgetByDefault = false;

			UserPreferences.Initialize();
#if WIN
			TangerineSingleInstanceKeeper.Initialize(args);
			TangerineSingleInstanceKeeper.AnotherInstanceArgsRecieved += OpenDocumentsFromArgs;
			Application.Exited += () => {
				TangerineSingleInstanceKeeper.Instance.ReleaseInstance();
			};
#endif
			switch (AppUserPreferences.Instance.ColorThemeKind) {
				case ColorTheme.ColorThemeKind.Light:
					SetColorTheme(ColorTheme.CreateLightTheme(), Theme.ColorTheme.CreateLightTheme());
					break;
				case ColorTheme.ColorThemeKind.Dark:
					SetColorTheme(ColorTheme.CreateDarkTheme(), Theme.ColorTheme.CreateDarkTheme());
					break;
				case ColorTheme.ColorThemeKind.Custom: {
					bool isDark = AppUserPreferences.Instance.ColorTheme.IsDark;
					ColorTheme theme = null;
					var flags =
						BindingFlags.Public |
						BindingFlags.GetProperty |
						BindingFlags.SetProperty |
						BindingFlags.Instance;
					foreach (var category in typeof(ColorTheme).GetProperties(flags)) {
						var categoryValue = category.GetValue(AppUserPreferences.Instance.ColorTheme);
						if (categoryValue == null) {
							if (theme == null) {
								theme = isDark ? ColorTheme.CreateDarkTheme() : ColorTheme.CreateLightTheme();
							}
							category.SetValue(AppUserPreferences.Instance.ColorTheme, category.GetValue(theme));
							category.SetValue(theme, null);
						}
					}
					SetColorTheme(AppUserPreferences.Instance.ColorTheme, AppUserPreferences.Instance.LimeColorTheme);
					break;
				}

			}
			Application.InvalidateWindows();

			LoadFont();

			DockManager.Initialize(new Vector2(1024, 768));
			DockManager.Instance.DocumentAreaDropFilesGesture.Recognized +=
				new ScenesDropHandler { ShouldCreateContextMenu = false }.Handle;
			TangerineMenu.Create();
			var mainWidget = DockManager.Instance.MainWindowWidget;
			mainWidget.Window.AllowDropFiles = true;
			mainWidget.AddChangeWatcher(() => Project.Current, _ => {
				SetupMainWindowTitle(mainWidget);
				TangerineMenu.RebuildCreateImportedTypeMenu();
			});
			mainWidget.AddChangeWatcher(
				() => Document.Current?.Container,
				_ => Document.Current?.ForceAnimationUpdate()
			);
			Application.Exiting += () => Project.Current.Close();
			Application.Exited += () => {
				AppUserPreferences.Instance.DockState = DockManager.Instance.ExportState();
				SceneUserPreferences.Instance.VisualHintsRegistry = VisualHintsRegistry.Instance;
				Core.UserPreferences.Instance.Save();
				Orange.The.Workspace.Save();
			};

			var timelinePanel = new Panel("Timeline");
			var inspectorPanel = new Panel("Inspector");
			var searchPanel = new Panel("Hierarchy");
			var animationsPanel = new Panel("Animations");
			var filesystemPanel = new Panel("Filesystem");
			var consolePanel = new Panel("Console");
			var profilerPanel = new Panel("Profiler");
			var backupHistoryPanel = new Panel("Backups");
			var documentPanel = new Panel(DockManager.DocumentAreaId, undockable: false);
			documentPanel.PanelWidget = documentPanel.ContentWidget;
			var visualHintsPanel = new Panel("Visual Hints");
			var attachmentPanel = new Panel("Model3D Attachment");
			var remoteScriptingPanel = new Panel("Remote Scripting");
			var dockManager = DockManager.Instance;
			_ = new UI.Console(consolePanel);
			_ = new UI.Profiler(profilerPanel);
			var root = dockManager.Model.WindowPlacements.First();
			var placement = new LinearPlacement(LinearPlacementDirection.Horizontal);
			dockManager.AddPanel(timelinePanel, root, DockSite.Top, 0.3f);
			dockManager.DockPlacementTo(placement, root, DockSite.Bottom, 0.6f);
			dockManager.AppendPanelTo(documentPanel, placement, 0.5f);
			var commandHandlerList = CommandHandlerList.Global;
			var commandsDictionary = new Dictionary<string, Command> {
				{ animationsPanel.Id, new Command(animationsPanel.Title) },
				{ timelinePanel.Id, new Command(timelinePanel.Title) },
				{ inspectorPanel.Id, new Command(inspectorPanel.Title) },
				{ searchPanel.Id, new Command(searchPanel.Title) },
				{ filesystemPanel.Id, new Command(filesystemPanel.Title) },
				{ consolePanel.Id, new Command(consolePanel.Title) },
				{ profilerPanel.Id, new Command(profilerPanel.Title) },
				{ backupHistoryPanel.Id, new Command(backupHistoryPanel.Title) },
				{ visualHintsPanel.Id, new Command(visualHintsPanel.Title) },
				{ attachmentPanel.Id, new Command(attachmentPanel.Title) },
				{ remoteScriptingPanel.Id, new Command(remoteScriptingPanel.Title) },
			};
			foreach (var pair in commandsDictionary) {
				commandHandlerList.Connect(pair.Value, new PanelCommandHandler(pair.Key));
				TangerineMenu.PanelsMenu.Add(pair.Value);
			}
			dockManager.AddPanel(inspectorPanel, placement, DockSite.Left);
			var filesystemPlacement = dockManager.AddPanel(filesystemPanel, placement, DockSite.Right);
			dockManager.AddPanel(searchPanel, filesystemPlacement, DockSite.Fill);
			dockManager.AddPanel(animationsPanel, filesystemPlacement, DockSite.Fill);
			dockManager.AddPanel(backupHistoryPanel, filesystemPlacement, DockSite.Fill);
			dockManager.AddPanel(consolePanel, filesystemPlacement, DockSite.Bottom, 0.3f);
			dockManager.AddPanel(profilerPanel, filesystemPlacement, DockSite.Bottom, 0.3f);
			dockManager.AddPanel(visualHintsPanel, placement, DockSite.Right, 0.3f).Hidden = true;
			dockManager.AddPanel(attachmentPanel, placement, DockSite.Bottom, 0.3f).Hidden = true;
			dockManager.AddPanel(remoteScriptingPanel, placement, DockSite.Right, 0.3f).Hidden = true;
			DockManagerInitialState = dockManager.ExportState();
			var documentViewContainer = InitializeDocumentArea(dockManager);
			documentPanel.ContentWidget.Nodes.Add(dockManager.DocumentArea);
			dockManager.ImportState(AppUserPreferences.Instance.DockState);
			Document.ShowingWarning += (doc, message) => {
				AlertDialog.Show(message);
			};
			Core.Operations.Paste.Pasted += () => {
				int removedAnimatorsCount = 0;
				foreach (var node in Document.Current.SelectedNodes()) {
					removedAnimatorsCount += node.RemoveDanglingAnimators();
				}
				if (removedAnimatorsCount != 0) {
					var message = "Your exported content has references to external animations. It's forbidden.\n";
					if (removedAnimatorsCount == 1) {
						message += "1 dangling animator has been removed!";
					} else {
						message += $"{removedAnimatorsCount} dangling animators have been removed!";
					}

					Document.Current.ShowWarning(message);
				}
			};
			Document.CloseConfirmation += doc => {
				string text = $"Save the changes to document '{doc.Path}' before closing?";
				var alert = new AlertDialog(
					text,
					"Yes",
					"No",
					"Cancel"
				);
				return alert.Show() switch {
					0 => Document.CloseAction.SaveChanges,
					1 => Document.CloseAction.DiscardChanges,
					_ => Document.CloseAction.Cancel,
				};
			};
			mainWidget.Tasks.Add(HandleMissingDocumentsTask);
			Project.HandleMissingDocuments += missingDocuments => {
				foreach (var d in missingDocuments) {
					missingDocumentsList.Add(d);
				}
			};
			Project.DocumentReloadConfirmation += doc => {
				if (doc.IsModified) {
					var modifiedAlert = new AlertDialog($"{doc.Path}\n\nThis file has been modified by another " +
						$"program and has unsaved changes.\nDo you want to reload it from disk? ", "Yes", "No");
					var res = modifiedAlert.Show();
					if (res == 1 || res == -1) {
						doc.History.ExternalModification();
						return false;
					}
					return true;
				}
				if (CoreUserPreferences.Instance.ReloadModifiedFiles) {
					return true;
				}
				var alert = new AlertDialog($"{doc.Path}\n\nThis file has been modified by another program.\n" +
					$"Do you want to reload it from disk? ", "Yes, always", "Yes", "No");
				var r = alert.Show();
				if (r == 0) {
					CoreUserPreferences.Instance.ReloadModifiedFiles = true;
					return true;
				}
				if (r == 2) {
					doc.History.ExternalModification();
					return false;
				}
				return true;
			};

			Project.TempFileLoadConfirmation += path => {
				var alert = new AlertDialog($"Do you want to load auto-saved version of '{path}'?", "Yes", "No");
				return alert.Show() == 0;
			};

			Project.OpenFileOutsideProjectAttempt += (string filePath) => {
				var projectFilePath = SearhForCitproj(filePath);
				if (projectFilePath != null && Project.Current.CitprojPath != projectFilePath) {
					var alert = new AlertDialog($"You're trying to open a document outside the project directory. " +
						$"Change the current project to '{Path.GetFileName(projectFilePath)}'?", "Yes", "No");
					if (alert.Show() == 0) {
						if (FileOpenProject.Execute(projectFilePath)) {
							Project.Current.OpenDocument(filePath, true);
						}
						return;
					}
				} else if (projectFilePath == null) {
					AlertDialog.Show("Can't open a document outside the project directory");
				}
			};
			Project.Tasks = dockManager.MainWindowWidget.Tasks;
			Project.Tasks.Add(
				new AutosaveProcessor(() => AppUserPreferences.Instance.AutosaveDelay),
				new TooltipProcessor()
			);
			BackupManager.Instance.Activate(Project.Tasks);
			Document.NodeDecorators.AddFor<Spline>(
				n => n.CompoundPostPresenter.Add(new UI.SceneView.SplinePresenter())
			);
			Document.NodeDecorators.AddFor<Viewport3D>(
				n => n.CompoundPostPresenter.Add(new UI.SceneView.Spline3DPresenter())
			);
			Document.NodeDecorators.AddFor<Viewport3D>(
				n => n.CompoundPostPresenter.Add(new UI.SceneView.Animation3DPathPresenter())
			);
			Document.NodeDecorators.AddFor<Widget>(n => {
				if (n.AsWidget.SkinningWeights == null) {
					n.AsWidget.SkinningWeights = new SkinningWeights();
				}
			});
			Document.NodeDecorators.AddFor<PointObject>(n => {
				if ((n as PointObject).SkinningWeights == null) {
					(n as PointObject).SkinningWeights = new SkinningWeights();
				}
			});
			Animation.EasingEnabledChecker = (animation) => {
				var doc = Document.Current;
				return doc == null || doc.PreviewScene || animation != doc.Animation;
			};
			if (SceneUserPreferences.Instance.VisualHintsRegistry != null) {
				VisualHintsRegistry.Instance = SceneUserPreferences.Instance.VisualHintsRegistry;
			}
			VisualHintsRegistry.Instance.RegisterDefaultHints();

			Document.NodeDecorators.AddFor<Node>(n => n.SetTangerineFlag(TangerineFlags.SceneNode, true));
			dockManager.UnhandledExceptionOccurred += ExceptionHandling.Handle;

			Document.NodeDecorators.AddFor<ParticleEmitter>(
				n => n.CompoundPostPresenter.Add(new UI.SceneView.ParticleEmitterPresenter())
			);
			DocumentHistory.AddOperationProcessorTypes(new[] {
				typeof(AnimeshModification.Animate.Processor),
				typeof(AnimeshModification.Slice.Processor),
				typeof(Core.Operations.TimelineHorizontalShift.Processor),
				typeof(Core.Operations.TimelineColumnRemove.Processor),
				typeof(Core.Operations.RemoveKeyframeRange.Processor),
				typeof(Core.Operations.RenameAnimationProcessor),
				typeof(Core.Operations.DelegateOperation.Processor),
				typeof(Core.Operations.SetProperty.Processor),
				typeof(Core.Operations.SetIndexedProperty.Processor),
				typeof(Core.Operations.RemoveKeyframe.Processor),
				typeof(Core.Operations.SetKeyframe.Processor),
				typeof(Core.Operations.AddIntoCollection<,>.Processor),
				typeof(Core.Operations.RemoveFromCollection<,>.Processor),
				typeof(Core.Operations.InsertIntoList.Processor),
				typeof(Core.Operations.RemoveFromList.Processor),
				typeof(Core.Operations.InsertIntoList<,>.Processor),
				typeof(Core.Operations.RemoveFromList<,>.Processor),
				typeof(Core.Operations.InsertIntoDictionary<,,>.Processor),
				typeof(Core.Operations.RemoveFromDictionary<,,>.Processor),
				typeof(Core.Operations.SetMarker.Processor),
				typeof(Core.Operations.DeleteMarker.Processor),
				typeof(Core.Operations.SetComponent.Processor),
				typeof(Core.Operations.DeleteComponent.Processor),
				typeof(Core.Operations.DistortionMeshProcessor),
				typeof(Core.Operations.ContentsPathProcessor),
				typeof(UI.SceneView.ResolutionPreviewOperation.Processor),
				typeof(UI.Timeline.Operations.SelectGridSpan.Processor),
				typeof(UI.Timeline.Operations.DeselectGridSpan.Processor),
				typeof(UI.Timeline.Operations.ClearGridSelection.Processor),
				typeof(UI.Timeline.Operations.ShiftGridSelection.Processor),
				typeof(UI.Timeline.Operations.SetCurrentColumn.Processor),
				typeof(UI.Timeline.Operations.SelectCurveKey.Processor),
				typeof(TriggersValidatorOnSetProperty),
				typeof(TriggersValidatorOnSetKeyframe),
				typeof(Core.Operations.DeleteRuler.Processor),
				typeof(Core.Operations.CreateRuler.Processor),
				typeof(InvalidateAnimesh.Processor)
			});
			DocumentHistory.AddOperationProcessorTypes(UI.Timeline.Timeline.GetOperationProcessorTypes());

			RegisterCommands();
			InitializeHotkeys();

			AppUserPreferences.Instance.ToolbarModel.RefreshAfterLoad();
			Toolbar = new ToolbarView(dockManager.ToolbarArea, AppUserPreferences.Instance.ToolbarModel);
			RefreshCreateNodeCommands();
			Document.AttachingViews += doc => {
				if (doc.Views.Count == 0) {
					doc.Views.AddRange(new IDocumentView[] {
						new UI.Inspector.Inspector(inspectorPanel.ContentWidget),
						new UI.Timeline.Timeline(timelinePanel),
						new UI.SceneView.SceneView(documentViewContainer),
						new Panels.HierarchyPanel(searchPanel.ContentWidget),
						new Panels.BackupHistoryPanel(backupHistoryPanel.ContentWidget),
						new Panels.AnimationsPanel(animationsPanel.ContentWidget),
						// Use VisualHintsPanel singleton because we need preserve its state between documents.
						VisualHintsPanel.Instance ?? VisualHintsPanel.Initialize(visualHintsPanel),
						new AttachmentPanel(attachmentPanel),
					});
					UI.SceneView.SceneView.ShowNodeDecorationsPanelButton.Clicked
						= () => dockManager.TogglePanel(visualHintsPanel);
				}
			};
			LoadProject();
			OpenDocumentsFromArgs(args);
			WidgetContext.Current.Root.AddChangeWatcher(
				getter: () => Project.Current,
				action: project => TangerineMenu.OnProjectChanged(project)
			);

			WidgetContext.Current.Root.AddChangeWatcher(
				getter: () => ProjectUserPreferences.Instance.RecentDocuments.Count == 0
					? null
					: ProjectUserPreferences.Instance.RecentDocuments[0],
				action: document => TangerineMenu.RebuildRecentDocumentsMenu()
			);

			WidgetContext.Current.Root.AddChangeWatcher(
				getter: () => AppUserPreferences.Instance.RecentProjects.Count == 0
					? null
					: AppUserPreferences.Instance.RecentProjects[0],
				action: document => TangerineMenu.RebuildRecentProjectsMenu()
			);

			_ = new UI.FilesystemView.FilesystemPane(filesystemPanel);
			_ = new UI.RemoteScripting.RemoteScriptingPane(remoteScriptingPanel);
			RegisterGlobalCommands();

			Documentation.Init();
			DocumentationComponent.Clicked = page => Documentation.ShowHelp(page);
		}

		private static void LoadProject()
		{
			if (Orange.Toolbox.TryFindCitrusProjectForExecutingAssembly(out string projectFilePath)) {
				try {
					_ = new Project(projectFilePath);
				} catch (System.Exception e) {
					AlertDialog.Show($"Can't open project '{projectFilePath}':\n{e.Message}");
				}
			}
		}

		private void ChangeTangerineSettingsFolderIfNeed()
		{
			string appdataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
			string tangerineOldPath = Path.Combine(appdataPath, "Tangerine");
			string tangerineNewPath = Path.Combine(appdataPath, "Game Forest", "Tangerine");
			// if the Tangerine folder exists in %APPDATA%\Roaming\Tangerine
			// then move it to the %APPDATA%\Roaming\Game Forest\Tangerine folder
			if (Directory.Exists(tangerineOldPath) && !Directory.Exists(tangerineNewPath)) {
				var dirSource = new DirectoryInfo(tangerineOldPath);
				var dirTarget = new DirectoryInfo(tangerineNewPath);
				CopyAllFilesRecursively(dirSource, dirTarget);
				Directory.Delete(tangerineOldPath, true);
			}
		}

		private void CopyAllFilesRecursively(DirectoryInfo source, DirectoryInfo target)
		{
			Directory.CreateDirectory(target.FullName);
			foreach (FileInfo fi in source.GetFiles()) {
				fi.MoveTo(Path.Combine(target.FullName, fi.Name));
			}
			foreach (DirectoryInfo diSourceSubDir in source.GetDirectories()) {
				DirectoryInfo nextTargetSubDir =
					target.CreateSubdirectory(diSourceSubDir.Name);
				CopyAllFilesRecursively(diSourceSubDir, nextTargetSubDir);
			}
		}

		private readonly List<Document> missingDocumentsList = new List<Document>();

		private IEnumerator<object> HandleMissingDocumentsTask()
		{
			while (true) {
				while (missingDocumentsList.Count == 0) {
					yield return null;
				}
				var missingDocuments = missingDocumentsList.Where(
					d => !Project.Current.GetFullPath(d.Path, out string fullPath) &&
					Project.Current.Documents.Contains(d)
				);
				while (missingDocuments.Any()) {
					var nextDocument = missingDocuments.First();
					bool loaded = nextDocument.Loaded;
					Document.SetCurrent(nextDocument);
					yield return null;
					string path = nextDocument.FullPath.Replace('\\', '/');
					var choices = new[] { "Save", "Save All", "Close", "Close All" };
					var alert = new AlertDialog($"Document {path} has been moved or deleted.", choices);
					var r = alert.Show();
					switch (r) {
						case 0: {
							// Save
							nextDocument.Save();
							missingDocumentsList.Remove(nextDocument);
							break;
						}
						case 1: {
							// Save All
							while (missingDocuments.Any()) {
								nextDocument = missingDocuments.First();
								Document.SetCurrent(nextDocument);
								nextDocument.Save();
								missingDocumentsList.Remove(nextDocument);
							}
							break;
						}
						case 2: {
							// Close
							Project.Current.CloseDocument(nextDocument);
							missingDocumentsList.Remove(nextDocument);
							break;
						}
						case 3: {
							// Close All
							while (missingDocuments.Any()) {
								Project.Current.CloseDocument(nextDocument = missingDocuments.First());
								missingDocumentsList.Remove(nextDocument);
							}
							break;
						}
					}
				}
			}
		}

		private void OpenDocumentsFromArgs(string[] args)
		{
			foreach (var arg in args) {
				if (Path.GetExtension(arg) == ".citproj") {
					FileOpenProject.Execute(arg);
				} else if (!arg.StartsWith('-')) {
					Project.Current.OpenDocument(arg, pathIsAbsolute: true);
				}
			}
		}

		private static string SearhForCitproj(string filePath)
		{
			var path = filePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			path[0] += Path.DirectorySeparatorChar;
			for (int i = path.Length - 2; i >= 0; i--) {
				var ppp = Path.Combine(path.Take(i + 1).ToArray());
				var projectFileCandidates = Directory.GetFiles(ppp, "*.citproj", SearchOption.TopDirectoryOnly);
				if (projectFileCandidates.Length > 0) {
					return projectFileCandidates[0];
				}
			}
			return null;
		}

		private static void SetupMainWindowTitle(WindowWidget windowWidget)
		{
			var title = "Tangerine";
			if (Project.Current != Project.Null) {
				var citProjName = System.IO.Path.GetFileNameWithoutExtension(Project.Current.CitprojPath);
				title = $"{citProjName} - Tangerine";
			}
			windowWidget.Window.Title = title;
		}

		private static void SetColorTheme(ColorTheme theme, Theme.ColorTheme limeTheme)
		{
			AppUserPreferences.Instance.LimeColorTheme = Theme.Colors = limeTheme.Clone();
			AppUserPreferences.Instance.ColorTheme = ColorTheme.Current = theme.Clone();
		}

		public void RefreshCreateNodeCommands()
		{
			Toolbar.Rebuild();
			HotkeyRegistry.InitDefaultShortcuts();
			HotkeyRegistry.UpdateProfiles();
			UI.SceneView.VisualHintsPanel.Refresh();
		}

		private static void RegisterCommands()
		{
			RegisterCommands(typeof(TimelineCommands));
			RegisterCommands(typeof(InspectorCommands));
			RegisterCommands(typeof(GenericCommands));
			RegisterCommands(typeof(SceneViewCommands));
			RegisterCommands(typeof(Tools));
			RegisterCommands(typeof(OrangeCommands));
			RegisterCommands(typeof(FilesystemCommands));
			CommandRegistry.Register(
				command: Command.Undo,
				categoryId: "GenericCommands",
				commandId: "Undo",
				@override: false,
				isProjectSpecific: false
			);
			CommandRegistry.Register(
				command: Command.Redo,
				categoryId: "GenericCommands",
				commandId: "Redo",
				@override: false,
				isProjectSpecific: false
			);
		}

		private static void RegisterCommands(Type type)
		{
			var fields = type.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
			foreach (var field in fields) {
				var fieldType = field.FieldType;
				if (!(fieldType == typeof(ICommand) || fieldType.IsSubclassOf(typeof(ICommand)))) {
					continue;
				}
				CommandRegistry.Register(
					command: (ICommand)field.GetValue(null),
					categoryId: type.Name,
					commandId: field.Name,
					@override: false,
					isProjectSpecific: false
				);
			}
		}

		private static Frame InitializeDocumentArea(DockManager dockManager)
		{
			var tabBar = new ThemedTabBar { LayoutCell = new LayoutCell { StretchY = 0 } };
			var documentViewContainer = new Frame {
				ClipChildren = ClipMethod.ScissorTest,
				Layout = new StackLayout(),
				HitTestTarget = true
			};
			_ = new DocumentTabsProcessor(tabBar);
			var docArea = dockManager.DocumentArea;
			docArea.Layout = new VBoxLayout();
			docArea.AddNode(tabBar);
			docArea.AddNode(documentViewContainer);
			return documentViewContainer;
		}

		private class DocumentTabsProcessor
		{
			private readonly TabBar tabBar;

			public DocumentTabsProcessor(TabBar tabBar)
			{
				this.tabBar = tabBar;
				var g = new DropFilesGesture();
				g.Recognized += new ScenesDropHandler { ShouldCreateContextMenu = false }.Handle;
				tabBar.Gestures.Add(g);
				tabBar.AllowReordering = true;
				RebuildTabs();
				tabBar.OnReordered += args => Project.Current.ReorderDocument(Document.Current, args.IndexTo);
				tabBar.AddChangeWatcher(() => Project.Current.Documents.Version, _ => RebuildTabs());
				tabBar.AddChangeWatcher(() => Project.Current, _ => RebuildTabs());
			}

			private void RebuildTabs()
			{
				tabBar.Nodes.Clear();
				foreach (var doc in Project.Current.Documents) {
					var tab = new ThemedTab { Closable = true };
					tab.Components.Add(new TooltipComponent(() => doc.FullPath.Replace('/', '\\')));
					tab.AddChangeWatcher(() => Document.Current, _ => {
						if (doc == Document.Current) {
							tabBar.ActivateTab(tab);
						}
					});
					tab.Gestures.Add(new ClickGesture(1, () => {
						DocumentTabContextMenu.Create(doc);
					}));
					tab.AddChangeWatcher(() => doc.DisplayName, _ => tab.Text = doc.DisplayName);
					tab.Clicked += doc.MakeCurrent;
					tab.Closing += () => Project.Current.CloseDocument(doc);
					tabBar.AddNode(tab);
				}
				tabBar.AddNode(new Widget { LayoutCell = new LayoutCell { StretchX = 0 } });
			}
		}

		public static void LoadFont()
		{
			var asianFont = LoadFont("Tangerine.Resources.NotoSansCJKtc-Regular.ttf");
			FontPool.Instance.AddFont(
				name: FontPool.DefaultFontName,
				font: new CompoundFont(LoadFont("Tangerine.Resources.SegoeUI.ttf"), asianFont)
			);
			FontPool.Instance.AddFont(
				name: FontPool.DefaultBoldFontName,
				font: new CompoundFont(
					LoadFont("Tangerine.Resources.SegoeUI-Bold.ttf"),
					// Use the same Asian font for the bold one,
					// since we don't need Asian bold for now and it is too heavy.
					asianFont
				)
			);
			static IFont LoadFont(string resource)
			{
				return new DynamicFont(new UI.EmbeddedResource(resource, "Tangerine")
					.GetResourceBytes());
			}
		}

		private void RegisterGlobalCommands()
		{
			UI.Inspector.Inspector.RegisterGlobalCommands();
			UI.Timeline.CommandBindings.Bind();
			UI.SceneView.SceneView.RegisterGlobalCommands();

			var h = CommandHandlerList.Global;
			h.Connect(GenericCommands.NewProject, new ProjectNew());
			h.Connect(GenericCommands.NewTan, new FileNew());
			h.Connect(GenericCommands.Open, new FileOpen());
			h.Connect(GenericCommands.OpenProject, new FileOpenProject());
			h.Connect(GenericCommands.Save, new FileSave());
			h.Connect(GenericCommands.CloseProject, new FileCloseProject());
			h.Connect(GenericCommands.SaveAs, new FileSaveAs());
			h.Connect(GenericCommands.SaveAll, new FileSaveAll());
			h.Connect(GenericCommands.Revert, new FileRevert());
			h.Connect(GenericCommands.Close, new FileClose());
			h.Connect(GenericCommands.CloseAll, new FileCloseAll());
			h.Connect(GenericCommands.CloseAllButCurrent, new FileCloseAllButCurrent());
			h.Connect(GenericCommands.Quit, Application.Exit);
			h.Connect(GenericCommands.PreferencesDialog, () => new PreferencesDialog());
			h.Connect(GenericCommands.OpenLookupDialog, () => new LookupDialog());
			h.Connect(GenericCommands.LookupCommands, () => new LookupDialog(LookupSections.SectionType.Commands));
			h.Connect(GenericCommands.LookupFiles, () => new LookupDialog(LookupSections.SectionType.Files));
			h.Connect(GenericCommands.LookupNodes, () => new LookupDialog(LookupSections.SectionType.Nodes));
			h.Connect(
				GenericCommands.LookupAnimationMarkers,
				() => new LookupDialog(LookupSections.SectionType.AnimationMarkers)
			);
			h.Connect(
				GenericCommands.LookupDocumentMarkers,
				() => new LookupDialog(LookupSections.SectionType.DocumentMarkers)
			);
			h.Connect(
				GenericCommands.LookupAnimationFrames,
				() => new LookupDialog(LookupSections.SectionType.AnimationFrames)
			);
			h.Connect(
				GenericCommands.LookupNodeAnimations,
				() => new LookupDialog(LookupSections.SectionType.NodeAnimations)
			);
			h.Connect(
				GenericCommands.LookupDocumentAnimations,
				() => new LookupDialog(LookupSections.SectionType.DocumentAnimations)
			);
			h.Connect(GenericCommands.LookupComponents, () => new LookupDialog(LookupSections.SectionType.Components));
			h.Connect(GenericCommands.Group, new GroupNodes());
			h.Connect(GenericCommands.Ungroup, new UngroupNodes());
			h.Connect(GenericCommands.InsertTimelineColumn, new InsertTimelineColumn());
			h.Connect(GenericCommands.RemoveTimelineColumn, new RemoveTimelineColumn());
			h.Connect(GenericCommands.NextDocument, new SetNextDocument());
			h.Connect(GenericCommands.PreviousDocument, new SetPreviousDocument());
			h.Connect(GenericCommands.DefaultLayout, new ViewDefaultLayout());
			h.Connect(GenericCommands.SaveLayout, new SaveLayout());
			h.Connect(GenericCommands.LoadLayout, new LoadLayout());
			h.Connect(GenericCommands.ExportScene, new ExportScene());
			h.Connect(GenericCommands.InlineExternalScene, new InlineExternalScene());
			h.Connect(GenericCommands.UpsampleAnimationTwice, new UpsampleAnimationTwice());
			h.Connect(GenericCommands.ViewHelp, () => Documentation.ShowHelp(Documentation.StartPageName));
			h.Connect(GenericCommands.HelpMode, () => Documentation.IsHelpModeOn = !Documentation.IsHelpModeOn);
			h.Connect(GenericCommands.ViewChangelog, () => Documentation.ShowHelp(Documentation.ChangelogPageName));
			h.Connect(GenericCommands.ConvertToButton, new ConvertToButton());
			h.Connect(Tools.AlignLeft, new AlignLeft());
			h.Connect(Tools.AlignRight, new AlignRight());
			h.Connect(Tools.AlignTop, new AlignTop());
			h.Connect(Tools.AlignBottom, new AlignBottom());
			h.Connect(Tools.CenterHorizontally, new CenterHorizontally());
			h.Connect(Tools.CenterVertically, new CenterVertically());
			h.Connect(Tools.DistributeLeft, new DistributeLeft());
			h.Connect(Tools.DistributeHorizontally, new DistributeCenterHorizontally());
			h.Connect(Tools.DistributeRight, new DistributeRight());
			h.Connect(Tools.DistributeTop, new DistributeTop());
			h.Connect(Tools.DistributeVertically, new DistributeCenterVertically());
			h.Connect(Tools.DistributeBottom, new DistributeBottom());
			h.Connect(Tools.CenterVertically, new DistributeCenterVertically());
			h.Connect(Tools.AlignCentersHorizontally, new AlignCentersHorizontally());
			h.Connect(Tools.AlignCentersVertically, new AlignCentersVertically());
			h.Connect(Tools.AlignTo, new AlignAndDistributeToHandler(Tools.AlignTo));
			h.Connect(Tools.CenterAlignTo, new CenterToHandler(Tools.CenterAlignTo));
			h.Connect(Tools.RestoreOriginalSize, new RestoreOriginalSize());
			h.Connect(Tools.ResetScale, new ResetScale());
			h.Connect(Tools.ResetRotation, new ResetRotation());
			h.Connect(Tools.FitToContainer, new FitToContainer());
			h.Connect(Tools.FitToContent, new FitToContent());
			h.Connect(Tools.FlipX, new FlipX());
			h.Connect(Tools.FlipY, new FlipY());
			h.Connect(Tools.CenterView, new CenterView());
			h.Connect(Tools.AnimeshAnimate, new AnimeshTools.ChangeState(AnimeshTools.ModificationState.Animation));
			h.Connect(Tools.AnimeshModify, new AnimeshTools.ChangeState(AnimeshTools.ModificationState.Modification));
			h.Connect(Tools.AnimeshCreate, new AnimeshTools.ChangeState(AnimeshTools.ModificationState.Creation));
			h.Connect(Tools.AnimeshRemove, new AnimeshTools.ChangeState(AnimeshTools.ModificationState.Removal));
			h.Connect(
				Tools.AnimeshTransform,
				new AnimeshTools.ChangeState(AnimeshTools.ModificationState.Transformation))
			;
			h.Connect(Command.Copy, Core.Operations.Copy.CopyToClipboard);
			h.Connect(Command.Cut, new DocumentDelegateCommandHandler(Core.Operations.Cut.Perform));
			h.Connect(Command.Paste, new DocumentDelegateCommandHandler(() => Paste(), Document.HasCurrent));
			h.Connect(Command.Delete, new DocumentDelegateCommandHandler(Core.Operations.Delete.Perform));
			h.Connect(Command.SelectAll, new DocumentDelegateCommandHandler(() => {
				foreach (var row in Document.Current.Rows) {
					Core.Operations.SelectRow.Perform(row, true);
				}
			}, () => Document.Current?.Rows.Count > 0));
			h.Connect(
				Command.Undo,
				() => Document.Current.History.Undo(),
				() => Document.Current?.History.CanUndo() ?? false
			);
			h.Connect(
				Command.Redo,
				() => Document.Current.History.Redo(),
				() => Document.Current?.History.CanRedo() ?? false
			);
			h.Connect(
				SceneViewCommands.PasteAtOldPosition,
				new DocumentDelegateCommandHandler(() => Paste(pasteAtMouse: false), Document.HasCurrent)
			);
			h.Connect(SceneViewCommands.SnapWidgetBorderToRuler, new SnapWidgetBorderCommandHandler());
			h.Connect(SceneViewCommands.SnapWidgetPivotToRuler, new SnapWidgetPivotCommandHandler());
			h.Connect(SceneViewCommands.SnapRulerLinesToWidgets, new SnapRulerLinesToWidgetCommandHandler());
			h.Connect(SceneViewCommands.ClearActiveRuler, new DocumentDelegateCommandHandler(ClearActiveRuler));
			h.Connect(SceneViewCommands.ManageRulers, new ManageRulers());
			h.Connect(SceneViewCommands.CreateRulerGrid, new CreateRulerGrid());
			h.Connect(SceneViewCommands.GeneratePreview, new GeneratePreview());
			h.Connect(TimelineCommands.CutKeyframes, UI.Timeline.Operations.CutKeyframes.Perform);
			h.Connect(TimelineCommands.CopyKeyframes, UI.Timeline.Operations.CopyKeyframes.Perform);
			h.Connect(TimelineCommands.PasteKeyframes, UI.Timeline.Operations.PasteKeyframes.Perform);
			h.Connect(TimelineCommands.ReverseKeyframes, UI.Timeline.Operations.ReverseKeyframes.Perform);
			h.Connect(TimelineCommands.CreatePositionKeyframe, UI.Timeline.Operations.TogglePositionKeyframe.Perform);
			h.Connect(TimelineCommands.CreateRotationKeyframe, UI.Timeline.Operations.ToggleRotationKeyframe.Perform);
			h.Connect(TimelineCommands.CreateScaleKeyframe, UI.Timeline.Operations.ToggleScaleKeyframe.Perform);
			h.Connect(
				TimelineCommands.CenterTimelineOnCurrentColumn,
				new DocumentDelegateCommandHandler(UI.Timeline.Operations.CenterTimelineOnCurrentColumn.Perform)
			);
			h.Connect(SceneViewCommands.ToggleDisplayRuler, new DisplayRuler());
			h.Connect(SceneViewCommands.SaveCurrentRuler, new SaveRuler());
			h.Connect(TimelineCommands.NumericMove, () => new NumericMoveDialog());
			h.Connect(TimelineCommands.NumericScale, () => new NumericScaleDialog());
			h.Connect(ToolsCommands.RenderToPngSequence, new RenderToPngSequence());
			h.Connect(GitCommands.ForceUpdate, new ForceUpdate());
			h.Connect(GitCommands.Update, new Update());
			h.Connect(GenericCommands.ClearCache, new ClearCache());
			h.Connect(GenericCommands.ResetGlobalSettings, new ResetGlobalSettings());
			h.Connect(GenericCommands.PurgeBackups, new PurgeBackUps());
		}

		private static void InitializeHotkeys()
		{
			string dir = HotkeyRegistry.ProfilesDirectory;
			Directory.CreateDirectory(dir);
			HotkeyRegistry.InitDefaultShortcuts();
			var defaultProfile = HotkeyRegistry.CreateProfile(HotkeyRegistry.DefaultProfileName);
			if (File.Exists(defaultProfile.FilePath)) {
				defaultProfile.Load();
			} else {
				defaultProfile.Save();
			}
			HotkeyRegistry.Profiles.Add(defaultProfile);
			foreach (string file in Directory.EnumerateFiles(dir)) {
				string name = Path.GetFileName(file);
				if (name == HotkeyRegistry.DefaultProfileName) {
					continue;
				}
				var profile = HotkeyRegistry.CreateProfile(name);
				profile.Load();
				HotkeyRegistry.Profiles.Add(profile);
			}
			var currentProfile = HotkeyRegistry.Profiles.FirstOrDefault(
				i => i.Name == AppUserPreferences.Instance.CurrentHotkeyProfile
			);
			if (currentProfile != null) {
				HotkeyRegistry.CurrentProfile = currentProfile;
			} else {
				HotkeyRegistry.CurrentProfile = defaultProfile;
			}
		}

		private void ClearActiveRuler()
		{
			if (new AlertDialog("Are you sure you want to clear active ruler?", "Yes", "No").Show() == 0) {
				using (Document.Current.History.BeginTransaction()) {
					foreach (var line in ProjectUserPreferences.Instance.ActiveRuler.Lines.ToList()) {
						Core.Operations.DeleteRuler.Perform(ProjectUserPreferences.Instance.ActiveRuler, line);
					}
					Document.Current.History.CommitTransaction();
				}
			}
		}

		private static void Paste(bool pasteAtMouse = true)
		{
			try {
				Core.Operations.Paste.Perform(out var pastedItems);
				if (
					SceneView.Instance.InputArea.IsMouseOverThisOrDescendant() &&
					pasteAtMouse &&
				    !CoreUserPreferences.Instance.DontPasteAtMouse
				) {
					var mousePosition =
						SceneView.Instance.Scene.LocalMousePosition() *
						Document.Current.Container.AsWidget.LocalToWorldTransform.CalcInversed();
					var widgets = pastedItems.Select(i => i.GetNode()).OfType<Widget>().ToList();
					if (widgets.Count > 0) {
						Utils.CalcHullAndPivot(widgets, out _, out var pivot);
						pivot *= Document.Current.Container.AsWidget.LocalToWorldTransform.CalcInversed();
						DragWidgetsProcessor.DragWidgets(widgets, mousePosition, pivot);
					}
				}
			} catch (InvalidOperationException e) {
				Document.Current.History.RollbackTransaction();
				AlertDialog.Show(e.Message);
			}
		}
	}
}
