using Lime;

namespace Tangerine.UI
{
	public static class TimelineCommands
	{
		public static readonly ICommand ScrollLeft = new Command("Scroll Left", new Shortcut(Key.Left));

		public static readonly ICommand ScrollRight = new Command("Scroll Right", new Shortcut(Key.Right));

		public static readonly ICommand FastScrollLeft =
			new Command(
				"Fast Scroll Left",
				new Shortcut(Modifiers.Alt, Key.Left)
			);

		public static readonly ICommand FastScrollRight =
			new Command(
				"Fast Scroll Right",
				new Shortcut(Modifiers.Alt, Key.Right)
			);

		public static readonly ICommand ScrollUp = new Command("Scroll Up", new Shortcut(Key.Up));

		public static readonly ICommand ScrollDown = new Command("Scroll Down", new Shortcut(Key.Down));

		public static readonly ICommand SelectNodeUp = new Command("Select Up", new Shortcut(Modifiers.Shift, Key.Up));

		public static readonly ICommand SelectNodeDown =
			new Command(
				"Select Down",
				new Shortcut(Modifiers.Shift, Key.Down)
			);

		public static readonly ICommand EnterNode =
			new Command(
				"Enter Node",
				new Shortcut(Key.Enter)
			) { Repeatable = false };

		public static readonly ICommand EnterNodeAlias =
			new Command(
				"Enter Node",
				new Shortcut(Modifiers.Alt | Modifiers.Shift, Key.Right)
			);

		public static readonly ICommand EnterNodeMouse = new Command("Enter Node", new Shortcut(Key.MouseForward));

		public static readonly ICommand ExitNode = new Command("Exit Node", new Shortcut(Key.BackSpace));

		public static readonly ICommand ExitNodeAlias =
			new Command(
				"Exit Node",
				new Shortcut(Modifiers.Alt | Modifiers.Shift, Key.Left)
			);

		public static readonly ICommand ExitNodeMouse = new Command("Exit Node", new Shortcut(Key.MouseBack));

		public static readonly ICommand RenameSceneItem = new Command("Rename Item", new Shortcut(Key.F2));

		public static readonly ICommand Expand = new Command("Expand Item", new Shortcut(Modifiers.Shift, Key.Space));

		public static readonly ICommand ExpandRecursively =
			new Command(
				"Expand Item With Children",
				new Shortcut(Modifiers.Control | Modifiers.Shift, Key.Space)
			);

		public static readonly ICommand DeleteKeyframes =
			new Command(
				"Delete Selected Keyframes",
				new Shortcut(Modifiers.Shift, Key.Delete)
			);

		public static readonly ICommand CreateMarkerPlay =
			new Command(
				"Create Play Marker",
				new Shortcut(Modifiers.Alt, Key.Number1)
			);

		public static readonly ICommand CreateMarkerStop =
			new Command(
				"Create Stop Marker",
				new Shortcut(Modifiers.Alt, Key.Number2)
			);

		public static readonly ICommand CreateMarkerJump =
			new Command(
				"Create Jump Marker",
				new Shortcut(Modifiers.Alt, Key.Number3)
			);

		public static readonly ICommand DeleteMarker =
			new Command(
				"Delete Marker",
				new Shortcut(Modifiers.Alt, Key.Number4)
			);

		public static readonly ICommand CutKeyframes =
			new Command(
				"Cut Keyframes",
				new Shortcut(Modifiers.Alt | Modifiers.Control, Key.X)
			);

		public static readonly ICommand CopyKeyframes =
			new Command(
				"Copy Keyframes",
				new Shortcut(Modifiers.Alt | Modifiers.Control, Key.C)
			);

		public static readonly ICommand PasteKeyframes =
			new Command(
				"Paste Keyframes",
				new Shortcut(Modifiers.Alt | Modifiers.Control, Key.V)
			);

		public static readonly ICommand MoveDown =
			new Command(
				"Move Down",
				new Shortcut(Modifiers.Control, Key.LBracket)
			);

		public static readonly ICommand MoveUp = new Command("Move Up", new Shortcut(Modifiers.Control, Key.RBracket));

		public static readonly ICommand ReverseKeyframes = new Command("Reverse Keyframes");

		public static readonly ICommand CreatePositionKeyframe =
			new Command(
				"Create Position Keyframe",
				new Shortcut(Key.E)
			);

		public static readonly ICommand CreateRotationKeyframe =
			new Command(
				"Create Rotation Keyframe",
				new Shortcut(Key.R)
			);

		public static readonly ICommand CreateScaleKeyframe = new Command("Create Scale Keyframe", new Shortcut(Key.T));

		public static readonly ICommand SelectAllSelectedSceneItemKeyframes =
			new Command(
				"Select All Keyframes of Selected Items",
				new Shortcut(Modifiers.Control | Modifiers.Shift, Key.A)
			);

		public static readonly ICommand SelectAllKeyframes =
			new Command(
				"Select All Keyframes",
				new Shortcut(Modifiers.Control | Modifiers.Alt, Key.A)
			);

		public static readonly ICommand NumericMove = new Command("Numeric Move");

		public static readonly ICommand NumericScale = new Command("Numeric Scale");

		public static readonly ICommand CenterTimelineOnCurrentColumn =
			new Command(
				"Center Timeline on Current Column",
				new Shortcut(Modifiers.Control | Modifiers.Shift, Key.C)
			);

		public static readonly ICommand AdvanceToPreviousKeyframe =
			new Command(
				"Advance to Previous Keyframe",
				new Shortcut(Modifiers.Control, Key.Left)
			);

		public static readonly ICommand AdvanceToNextKeyframe =
			new Command(
				"Advance to Next Keyframe",
				new Shortcut(Modifiers.Control, Key.Right)
			);

		public static readonly ICommand NextInterpolation =
			new Command(
				"Set Next Interpolation Function",
				new Shortcut(Key.X)
			);
	}

	public static class InspectorCommands
	{
		public static readonly ICommand InspectRootNodeCommand = new Command("Inspect Root Node");
		public static readonly ICommand InspectEasing =
			new Command("Inspect Easing Between Markers") {
				Icon = IconPool.GetIcon("Inspector.Easing"),
			};

		public static readonly ICommand CopyAssetPath =
			new Command("Copy Asset Path") {
				Icon = IconPool.GetIcon("Inspector.CopyAssetPath"),
			};
	}

	public static class GenericCommands
	{
		public static readonly ICommand NewProject = new Command("New Project", new Shortcut(Modifiers.Command, Key.J));

		public static readonly ICommand NewScene = new Command("New Scene", new Shortcut(Modifiers.Command, Key.N));

		public static readonly ICommand NewSceneWithCustomRoot = new Command("New Scene with Custom Root", new Menu());

		public static readonly ICommand Open = new Command("Open", new Shortcut(Modifiers.Command, Key.O));

		public static readonly ICommand RecentDocuments = new Command("Recent Documents", new Menu());

		public static readonly ICommand Save = new Command("Save", new Shortcut(Modifiers.Command, Key.S));

		public static readonly ICommand SaveAs =
			new Command(
				"Save As",
				new Shortcut(Modifiers.Command | Modifiers.Shift, Key.S)
			);

		public static readonly ICommand SaveAll = new Command("Save All");

		public static readonly ICommand Revert = new Command("Revert", new Shortcut(Modifiers.Command, Key.R));

		public static readonly ICommand UpgradeDocumentFormat = new Command("Upgrade Document Format (.tan)");

		public static readonly ICommand OpenProject =
			new Command(
				"Open Project...",
				new Shortcut(Modifiers.Command | Modifiers.Shift, Key.O));

		public static readonly ICommand RecentProjects = new Command("Recent Projects", new Menu());

		public static readonly ICommand PreferencesDialog =
			new Command(
				"Preferences...",
				new Shortcut(Modifiers.Command, Key.P)
			);

		public static readonly ICommand OpenLookupDialog =
			new Command(
				"Open Lookup Dialog",
				new Shortcut(Modifiers.Control | Modifiers.Shift, Key.P)
			);

		public static readonly ICommand LookupCommands = new Command("Lookup Commands");

		public static readonly ICommand LookupFiles = new Command("Lookup Files");

		public static readonly ICommand LookupNodes = new Command("Lookup Nodes");

		public static readonly ICommand LookupAnimationMarkers = new Command("Lookup Animation's Markers");

		public static readonly ICommand LookupDocumentMarkers = new Command("Lookup Document's Markers");

		public static readonly ICommand LookupAnimationFrames = new Command("Lookup Animation's Frames");

		public static readonly ICommand LookupNodeAnimations = new Command("Lookup Node's Animations");

		public static readonly ICommand LookupDocumentAnimations = new Command("Lookup Document's Animations");

		public static readonly ICommand LookupComponents = new Command("Lookup Components");

		public static readonly ICommand Close = new Command("Close");

		public static readonly ICommand CloseAll = new Command("Close All");

		public static readonly ICommand CloseAllButCurrent = new Command("Close All but Current");

		public static readonly ICommand Group = new Command("Group", new Shortcut(Modifiers.Command, Key.G));

		public static readonly ICommand Ungroup =
			new Command(
				"Ungroup",
				new Shortcut(Modifiers.Command | Modifiers.Shift, Key.G)
			);

		public static readonly ICommand InsertTimelineColumn =
			new Command(
				"Insert Timeline Column",
				new Shortcut(Modifiers.Command, Key.Q)
			);

		public static readonly ICommand RemoveTimelineColumn =
			new Command(
				"Remove Timeline Column",
				new Shortcut(Modifiers.Command, Key.W)
			);

		public static readonly ICommand NextDocument =
			new Command(
				"Next Document",
				new Shortcut(Modifiers.Control, Key.Tab)
			);

		public static readonly ICommand PreviousDocument =
			new Command(
				"Previous Document",
				new Shortcut(Modifiers.Control | Modifiers.Shift, Key.Tab)
			);

		public static readonly ICommand CloseProject = new Command("Close Project", new Shortcut(Modifiers.Alt, Key.Q));

#if MAC
		public static readonly ICommand Quit = new Command("Quit", new Shortcut(Modifiers.Command, Key.Q));
#else
		public static readonly ICommand Quit = new Command("Quit", new Shortcut(Modifiers.Alt, Key.F4));
#endif
		public static readonly ICommand DefaultLayout = new Command("Default Layout");

		public static readonly ICommand ExportScene = new Command("Export Scene...");

		public static readonly ICommand InlineExternalScene = new Command("Inline external scene");

		public static readonly ICommand UpsampleAnimationTwice = new Command("Upsample Animation Twice");

		public static readonly ICommand ViewHelp = new Command("View Help", new Shortcut(Modifiers.Control, Key.F1));

		public static readonly ICommand HelpMode = new Command("Help Mode", Key.F1);

		public static readonly ICommand About = new Command("About");

		public static readonly ICommand ViewChangelog = new Command("View Changelog");

		public static readonly ICommand LockLayout = new Command("Lock layout");

		public static readonly ICommand SaveLayout = new Command("Save layout");

		public static readonly ICommand LoadLayout = new Command("Load layout");

		public static readonly ICommand ConvertTo = new Command("Convert to");

		public static readonly ICommand ClearCache = new Command("Clear cache");

		public static readonly ICommand PurgeBackups = new Command("Purge Backups");

		public static readonly ICommand ResetGlobalSettings = new Command("Reset global settings");

		public static readonly ICommand Overlay1 =
			new Command(
				"Overlay 1",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number1)
			);

		public static readonly ICommand Overlay2 =
			new Command(
				"Overlay 2",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number2)
			);

		public static readonly ICommand Overlay3 =
			new Command(
				"Overlay 3",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number3)
			);

		public static readonly ICommand Overlay4 =
			new Command(
				"Overlay 4",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number4)
			);

		public static readonly ICommand Overlay5 =
			new Command(
				"Overlay 5",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number5)
			);

		public static readonly ICommand Overlay6 =
			new Command(
				"Overlay 6",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number6)
			);

		public static readonly ICommand Overlay7 =
			new Command(
				"Overlay 7",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number7)
			);

		public static readonly ICommand Overlay8 =
			new Command(
				"Overlay 8",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number8)
			);

		public static readonly ICommand Overlay9 =
			new Command(
				"Overlay 9",
				new Shortcut(Modifiers.Shift | Modifiers.Control, Key.Number9)
			);
	}

	public static class SceneViewCommands
	{
		public static readonly ICommand PreviewOrStopAnimation = new Command("Preview Animation", new Shortcut(Key.F5));

		public static readonly ICommand PreviewOrPauseAnimation =
			new Command(
				"Preview Animation Once",
				new Shortcut(Key.F6)
			);

		public static readonly ICommand ResolutionChanger =
			new Command(
				"Preview Next Resolution",
				new Shortcut(Key.F11)
			);

		public static readonly ICommand ResolutionReverceChanger =
			new Command(
				"Preview Previous Resolution",
				new Shortcut(Modifiers.Shift, Key.F11)
			);

		public static readonly ICommand ResolutionOrientation =
			new Command(
				"Preview Next Orientation",
				new Shortcut(Key.F12)
			);

		public static readonly ICommand ShowBone3DVisualHint =
			new Command(
				"Bone (3D)",
				new Shortcut(Modifiers.Command, Key.B)
			);

		public static readonly ICommand ShowAllVisualHints =
			new Command(
				"Display All",
				new Shortcut(Modifiers.Command, Key.M)
			);

		public static readonly ICommand ShowVisualHintsForInvisibleNodes =
			new Command(
				"Display Invisible",
				new Shortcut(Modifiers.Command | Modifiers.Alt, Key.M)
			);

		public static readonly ICommand TieWidgetsWithBones =
			new Command(
				"Tie Widgets with Bones",
				new Shortcut(Modifiers.Command, Key.T)
			);

		public static readonly ICommand UntieWidgetsFromBones =
			new Command(
				"Untie Widgets from Bones",
				new Shortcut(Modifiers.Command | Modifiers.Shift, Key.T)
			);

		public static readonly ICommand Duplicate =
			new Command(
				"Duplicate",
				new Shortcut(Modifiers.Command, Key.D)
			);

		public static readonly ICommand ToggleDisplayRuler =
			new Command(
				"Toggle Display",
				new Shortcut(Modifiers.Command | Modifiers.Alt, Key.R)
			);

		public static readonly ICommand SaveCurrentRuler = new Command("Save Ruler");

		public static readonly ICommand ClearActiveRuler = new Command("Clear Active Ruler");

		public static readonly ICommand SnapWidgetPivotToRuler = new Command("Snap Widget Pivot to Ruler");

		public static readonly ICommand SnapWidgetBorderToRuler = new Command("Snap Widget to Ruler");

		public static readonly ICommand SnapRulerLinesToWidgets = new Command("Snap Ruler Lines to Widgets");

		public static readonly ICommand CreateRulerGrid = new Command("Create Ruler Grid");

		public static readonly ICommand ManageRulers = new Command("Manage Rulers");

		public static readonly ICommand GeneratePreview = new Command("Generate Preview");

		public static readonly ICommand PasteAtOldPosition =
			new Command(
				"Paste at Old Position",
				new Shortcut(Modifiers.Command | Modifiers.Shift, Key.V)
			);

		public static readonly ICommand ToggleOverdrawMode = new Command("Toggle Overdraw Mode");

		public static readonly ICommand AddComponentToSelection = new Command("Add Component", new Menu());
	}

	public static class Tools
	{
		public static readonly ICommand AlignLeft =
			new Command("Align Left") {
				Icon = IconPool.GetIcon("Tools.AlignLeft"),
			};

		public static readonly ICommand AlignRight =
			new Command("Align Right") {
				Icon = IconPool.GetIcon("Tools.AlignRight"),
			};

		public static readonly ICommand AlignTop =
			new Command("Align Top") {
				Icon = IconPool.GetIcon("Tools.AlignTop"),
			};

		public static readonly ICommand AlignBottom =
			new Command("Align Bottom") {
				Icon = IconPool.GetIcon("Tools.AlignBottom"),
			};

		public static readonly ICommand CenterHorizontally =
			new Command("Center Horizontally") {
				Icon = IconPool.GetIcon("Tools.CenterH"),
			};

		public static readonly ICommand CenterVertically =
			new Command("Center Vertically") {
				Icon = IconPool.GetIcon("Tools.CenterV"),
			};

		public static readonly ICommand CenterAlignTo =
			new Command("Center to Parent") {
				Icon = IconPool.GetIcon("Tools.Parent"),
			};

		public static readonly ICommand AlignCentersHorizontally =
			new Command("Align Centers Horizontally") {
				Icon = IconPool.GetIcon("Tools.AlignCentersHorizontally"),
			};

		public static readonly ICommand AlignCentersVertically =
			new Command("Align Centers Vertically") {
				Icon = IconPool.GetIcon("Tools.AlignCentersVertically"),
			};

		public static readonly ICommand DistributeLeft =
			new Command("Distribute Left") {
				Icon = IconPool.GetIcon("Tools.DistributeLeft"),
			};

		public static readonly ICommand DistributeHorizontally =
			new Command("Distribute Horizontally") {
				Icon = IconPool.GetIcon("Tools.DistributeCentersHorizontally"),
			};

		public static readonly ICommand DistributeRight =
			new Command("Distribute Right") {
				Icon = IconPool.GetIcon("Tools.DistributeRight"),
			};

		public static readonly ICommand DistributeTop =
			new Command("Distribute Top") {
				Icon = IconPool.GetIcon("Tools.DistributeTop"),
			};

		public static readonly ICommand DistributeVertically =
			new Command("Distribute Vertically") {
				Icon = IconPool.GetIcon("Tools.DistributeCentersVertically"),
			};

		public static readonly ICommand DistributeBottom =
			new Command("Distribute Bottom") {
				Icon = IconPool.GetIcon("Tools.DistributeBottom"),
			};

		public static readonly ICommand AlignTo =
			new Command("Align to Selection") {
				Icon = IconPool.GetIcon("Tools.Selection"),
			};

		public static readonly ICommand RestoreOriginalSize =
			new Command("Restore Original Size") {
				Icon = IconPool.GetIcon("Tools.RestoreOriginalSize"),
			};

		public static readonly ICommand ResetScale =
			new Command("Reset Scale") {
				Icon = IconPool.GetIcon("Tools.SetUnitScale"),
			};

		public static readonly ICommand ResetRotation =
			new Command("Reset Rotation") {
				Icon = IconPool.GetIcon("Tools.SetZeroRotation"),
			};

		public static readonly ICommand FlipX =
			new Command("Flip Horizontally") {
				Icon = IconPool.GetIcon("Tools.FlipH"),
			};

		public static readonly ICommand FlipY =
			new Command("Flip Vertically") {
				Icon = IconPool.GetIcon("Tools.FlipV"),
			};

		public static readonly ICommand FitToContainer =
			new Command("Fit to Container") {
				Icon = IconPool.GetIcon("Tools.FitToContainer"),
			};

		public static readonly ICommand FitToContent =
			new Command("Fit to Content") {
				Icon = IconPool.GetIcon("Tools.FitToContent"),
			};

		public static readonly ICommand CenterView =
			new Command("Center View") {
				Icon = IconPool.GetIcon("Tools.ToolsCenterView"),
			};

		public static readonly ICommand AnimeshAnimate =
			new Command("Animate Mesh") {
				Icon = IconPool.GetIcon("Tools.ToolsCenterView"),
			};

		public static readonly ICommand AnimeshModify =
			new Command("Modify Mesh Triangulation") {
				Icon = IconPool.GetIcon("Tools.DistributeBottom"),
			};

		public static readonly ICommand AnimeshCreate =
			new Command("Create Mesh Vertex or Constraint") {
				Icon = IconPool.GetIcon("Tools.FlipV"),
			};

		public static readonly ICommand AnimeshRemove =
			new Command("Remove Mesh Vertex") {
				Icon = IconPool.GetIcon("Tools.RestoreOriginalSize"),
			};

		public static readonly ICommand AnimeshTransform =
			new Command("Transform Mesh") {
				Icon = IconPool.GetIcon("Cursors.DragHandOpen"),
			};
	}

	public static class ToolsCommands
	{
		public static readonly ICommand RenderToPngSequence = new Command("Render to PNG sequence");

		public static readonly ICommand OpenConflictingAnimatorsDialog =
			new Command("Open Conflicting Animators Dialog");
	}

	public static class GitCommands
	{
		public static readonly ICommand ForceUpdate = new Command("Force update");

		public static readonly ICommand Update = new Command("Update");
	}

	public static class FilesystemCommands
	{
		public static readonly ICommand Left = new Command(Key.Left);

		public static readonly ICommand Right = new Command(Key.Right);

		public static readonly ICommand Up = new Command(Key.Up);

		public static readonly ICommand Down = new Command(Key.Down);

		public static readonly ICommand PageUp = new Command(Key.PageUp);

		public static readonly ICommand PageDown = new Command(Key.PageDown);

		public static readonly ICommand Home = new Command(Key.Home);

		public static readonly ICommand End = new Command(Key.End);

		public static readonly ICommand SelectLeft = new Command(Modifiers.Shift, Key.Left);

		public static readonly ICommand SelectRight = new Command(Modifiers.Shift, Key.Right);

		public static readonly ICommand SelectUp = new Command(Modifiers.Shift, Key.Up);

		public static readonly ICommand SelectDown = new Command(Modifiers.Shift, Key.Down);

		public static readonly ICommand SelectPageUp = new Command(Modifiers.Shift, Key.PageUp);

		public static readonly ICommand SelectPageDown = new Command(Modifiers.Shift, Key.PageDown);

		public static readonly ICommand SelectHome = new Command(Modifiers.Shift, Key.Home);

		public static readonly ICommand SelectEnd = new Command(Modifiers.Shift, Key.End);

		public static readonly ICommand ToggleLeft = new Command(Modifiers.Command, Key.Left);

		public static readonly ICommand ToggleRight = new Command(Modifiers.Command, Key.Right);

		public static readonly ICommand ToggleUp = new Command(Modifiers.Command, Key.Up);

		public static readonly ICommand ToggleDown = new Command(Modifiers.Command, Key.Down);

		public static readonly ICommand TogglePageUp = new Command(Modifiers.Command, Key.PageUp);

		public static readonly ICommand TogglePageDown = new Command(Modifiers.Command, Key.PageDown);

		public static readonly ICommand ToggleHome = new Command(Modifiers.Command, Key.Home);

		public static readonly ICommand ToggleEnd = new Command(Modifiers.Command, Key.End);

		public static readonly ICommand Cancel = new Command(Key.Escape);

		public static readonly ICommand Enter = new Command(Key.Enter);

		public static readonly ICommand EnterSpecial = new Command(Modifiers.Command, Key.Enter);

		public static readonly ICommand GoUp = new Command(Key.BackSpace);

		// Also go up on Alt + Up
		public static readonly ICommand GoUpAlso = new Command(Modifiers.Alt, Key.Up);

		public static readonly ICommand GoBack = new Command(Modifiers.Alt, Key.Left);

		public static readonly ICommand GoForward = new Command(Modifiers.Alt, Key.Right);

		public static readonly ICommand ToggleSelection = new Command(Modifiers.Command, Key.Space);

		public static readonly ICommand NavigateTo = new Command("Navigate to");

#if WIN
		private const string OpenInSystemFileManagerDescription = "Show in Explorer";
#elif MAC
		private const string OpenInSystemFileManagerDescription = "Show in Finder";
#endif
		public static readonly ICommand OpenInSystemFileManager = new Command(OpenInSystemFileManagerDescription);
	}

	public static class OrangeCommands
	{
		public static readonly ICommand Run = new Command("Build and Run", new Shortcut(Key.F9));

		public static readonly ICommand Build = new Command("Build");

		public static readonly ICommand RunConfig = new Command("Run Config", new Shortcut(Modifiers.Control, Key.F9));

		public static readonly ICommand CookGameAssets =
			new Command(
				"Cook Game Assets",
				new Shortcut(Modifiers.Alt, Key.F9)
			);
	}
}
