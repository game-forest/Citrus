using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Lime;
using Orange.Source;

namespace Orange
{
	public class OrangeInterface: UserInterface
	{
		private readonly Window window;
		private readonly WindowWidget windowWidget;
		private Widget mainVBox;
		private BundlePickerWidget bundlePickerWidget;
		private FileChooser projectPicker;
		private TargetPicker targetPicker;
		private PluginPanel pluginPanel;
		private ThemedTextView textView;
		private TextWriter textWriter;
		private Button goButton;
		private Button abortButton;
		private Widget footerSection;
		private ProgressBarField progressBarField;
		private ICommand actionsCommand;

		private Command cacheLocalAndRemote;
		private Command cacheRemote;
		private Command cacheLocal;
		private Command cacheNone;
		private Command bundlePickerCommand;

		public override void Initialize()
		{
			bundlePicker = new BundlePicker();
			bundlePickerWidget = new BundlePickerWidget(bundlePicker);
			mainInterfaceWidget.AddNode(bundlePickerWidget);
		}

		public OrangeInterface()
		{
			var windowSize = new Vector2(500, 400);
			window = new Window(new WindowOptions {
				ClientSize = windowSize,
				FixedSize = false,
				Title = "Orange",
#if WIN
				Icon = new System.Drawing.Icon(new EmbeddedResource("Orange.GUI.Orange.ico", "Orange.GUI").GetResourceStream()),
#endif // WIN
			});
			window.Closed += The.Workspace.Save;
			windowWidget = new ThemedInvalidableWindowWidget(window) {
				Id = "MainWindow",
				Layout = new HBoxLayout {
					Spacing = 6
				},
				Padding = new Thickness(6),
				Size = windowSize
			};
			mainInterfaceWidget = new Widget {
				Layout = new HBoxLayout {
					Spacing = 6
				}
			};
			mainVBox = new Widget {
				Layout = new VBoxLayout {
					Spacing = 6
				}
			};
			mainVBox.AddNode(CreateHeaderSection());
			mainVBox.AddNode(CreateTextView());
			progressBarField = new ProgressBarField();
			mainVBox.AddNode(progressBarField);
			mainVBox.AddNode(CreateFooterSection());
			mainInterfaceWidget.AddNode(mainVBox);
			CreateMenu();
		}

		private Widget mainInterfaceWidget;
		private Widget startPageWidget;

		private void CreateMenu()
		{
			// TODO Duplicates code from Tangerine.TangerineMenu.cs. Both should be presented at one file
			cacheLocalAndRemote = new Command("Local &and remote", () => UpdateCacheModeCheckboxes(AssetCacheMode.Local | AssetCacheMode.Remote));
			cacheRemote = new Command("&Remote", () => UpdateCacheModeCheckboxes(AssetCacheMode.Remote));
			cacheLocal = new Command("&Local", () => UpdateCacheModeCheckboxes(AssetCacheMode.Local));
			cacheNone = new Command("&None", () => UpdateCacheModeCheckboxes(AssetCacheMode.None));
			bundlePickerCommand = new Command("&Bundle picker", () => UpdateBundlePicker(!bundlePickerWidget.Visible));

			Application.MainMenu = new Menu {
				new Command("&File", new Menu {
					new Command("&Quit", () => { Application.Exit(); })
				}),
				new Command("&View", new Menu {
					new Command("&Panels", new Menu {
						bundlePickerCommand
					})
				}),
				(actionsCommand = new Command("&Actions", new Menu { })),
				new Command("&Cache", new Menu {
					new Command("&Actions", new Menu {
						new Command("&Upload cache to server", () => Execute(() => {
								UploadCacheToServer.UploadCacheToServerAction();
								return null;
							}
						))
					}),
					new Command("&Mode", new Menu {
						cacheLocalAndRemote,
						cacheRemote,
						cacheLocal,
						cacheNone
					})
				})
			};
		}

		// TODO Duplicates code from Tangerine.OrangeInterface.cs. Both should be presented at one file
		private void UpdateCacheModeCheckboxes(AssetCacheMode state)
		{
			cacheLocalAndRemote.Checked = state == (AssetCacheMode.Local | AssetCacheMode.Remote);
			cacheRemote.Checked = state == AssetCacheMode.Remote;
			cacheLocal.Checked = state == AssetCacheMode.Local;
			cacheNone.Checked = state == AssetCacheMode.None;
			The.Workspace.AssetCacheMode = state;
		}

		private Widget CreateHeaderSection()
		{
			var header = new Widget {
				Layout = new TableLayout {
					ColumnCount = 2,
					RowCount = 2,
					RowSpacing = 6,
					ColumnSpacing = 6,
					RowDefaults = new List<DefaultLayoutCell> {
						new DefaultLayoutCell { StretchY = 0 },
						new DefaultLayoutCell { StretchY = 0 },
					},
					ColumnDefaults = new List<DefaultLayoutCell> {
						new DefaultLayoutCell { StretchX = 0 },
						new DefaultLayoutCell(),
					}
				},
				LayoutCell = new LayoutCell { StretchY = 0 }
			};
			AddPicker(header, "Target", targetPicker = new TargetPicker());
			AddPicker(header, "Citrus Project", projectPicker = CreateProjectPicker());
			targetPicker.Changed += OnActionOrTargetChanged;
			return header;
		}

		private static void AddPicker(Node table, string name, Node picker)
		{
			var label = new ThemedSimpleText(name) {
				VAlignment = VAlignment.Center,
				HAlignment = HAlignment.Left
			};
			label.MinHeight = Theme.Metrics.DefaultButtonSize.Y;
			table.AddNode(label);
			table.AddNode(picker);
		}

		private static FileChooser CreateProjectPicker()
		{
			var picker = new FileChooser();
			picker.FileChosenByUser += The.Workspace.Open;
			return picker;
		}

		private Widget CreateTextView()
		{
			textView = new ThemedTextView();
			textWriter = new TextViewWriter(textView, Console.Out);
			Console.SetOut(textWriter);
			Console.SetError(textWriter);
			var menu = new Menu();
			var shCopy = new Shortcut(Modifiers.Control, Key.C);
			var command = new Command
			{
				Shortcut = shCopy,
				Text = "Copy All",
			};
			menu.Add(command);
			textView.Updated += (dt) => {
				if (textView.Input.WasKeyPressed(Key.Mouse1)) {
					menu.Popup();
				}
				if (command.WasIssued()) {
					command.Consume();
					Clipboard.Text = textView.Text;
				}
			};
			return textView;
		}

		private ThemedDropDownList actionPicker;

		private Widget CreateFooterSection()
		{
			footerSection = new Widget {
				Layout = new HBoxLayout {
					Spacing = 5
				},
			};

			actionPicker = new ThemedDropDownList();
			foreach (var menuItem in The.MenuController.GetVisibleAndSortedItems()) {
				actionPicker.Items.Add(new CommonDropDownList.Item(menuItem.Label, menuItem.Action));
			}
			actionPicker.Index = 0;
			actionPicker.Changed += OnActionOrTargetChanged;
			footerSection.AddNode(actionPicker);

			goButton = new ThemedButton("Go");
			goButton.Clicked += () => Execute((Func<string>) actionPicker.Value);
			footerSection.AddNode(goButton);

			abortButton = new ThemedButton("Abort") {
				Enabled = false,
				Visible = false
			};
			abortButton.Clicked += () => AssetCooker.CancelCook();
			footerSection.AddNode(abortButton);

			return footerSection;
		}

		public override void StopProgressBar()
		{
			progressBarField.HideAndClear();
		}

		public override void SetupProgressBar(int maxPosition)
		{
			progressBarField.Setup(maxPosition);
		}

		public override void IncreaseProgressBar(int amount = 1)
		{
			progressBarField.Progress(amount);
		}

		private void UpdateBundlePicker(bool value)
		{
			if (value) {
				bundlePickerWidget.Refresh();
			}
			bundlePickerWidget.Visible = value;
			bundlePickerCommand.Checked = value;
			The.Workspace.BundlePickerVisible = value;
			bundlePicker.Enabled = value;
		}

		private void OnActionOrTargetChanged(CommonDropDownList.ChangedEventArgs args)
		{
			var target = GetActiveTarget();
			var action = GetActiveAction();
			if (action == null || target == null) {
				return;
			}
			if (action.ApplicableToBundleSubset) {
				EnableChildren(bundlePickerWidget, true);
				bundlePicker.Enabled = true;
				bundlePickerWidget.SetInfoText(false);
			} else {
				bundlePickerWidget.SetSelectionForAll(!action.UsesTargetBundles || !target.Bundles.Any());
				EnableChildren(bundlePickerWidget, false);
				bundlePicker.Enabled = false;
				bundlePickerWidget.SetInfoText(true);
			}
			bundlePickerWidget.ClearBundleLocks();
			if (action.UsesTargetBundles) {
				foreach (var bundle in target.Bundles) {
					bundlePickerWidget.SetBundleLock(bundle, true);
				}
				if (action.ApplicableToBundleSubset) {
					foreach (var bundle in target.Bundles) {
						bundlePickerWidget.SetBundleSelection(bundle, true);
					}
				}
			}
		}

		private void Execute(Func<string> action)
		{
			windowWidget.Tasks.Add(ExecuteTask(action));
		}

		private IEnumerator<object> ExecuteTask(Func<string> action)
		{
			var task = OrangeActionsHelper.ExecuteOrangeAction(
				action, () => {
					The.Workspace.Save();
					EnableControls(false);
					textView.Clear();
				}, () => {
					EnableControls(true);
					if (textView.ScrollPosition == textView.MaxScrollPosition) {
						The.UI.ScrollLogToEnd();
					}
				},
				true
			);
			while (!task.IsCompleted && !task.IsCanceled && !task.IsFaulted) {
				yield return null;
			}
		}

		private void EnableControls(bool value)
		{
			// Nested nodes can't be enabled when their parent is disabled
			// so we have to enable top-level nodes or all UI elements will look like they are in 'disabled' state
			goButton.Visible = value;
			abortButton.Visible = !value;
			EnableChildren(windowWidget, value);
			mainInterfaceWidget.Enabled = true;
			mainVBox.Enabled = true;
			EnableChildren(mainVBox, value);
			footerSection.Enabled = true;
			EnableChildren(footerSection, value);
			abortButton.Enabled = !value;
			textView.Enabled = true;
			progressBarField.Enabled = true;
			bundlePickerWidget.Enabled = true;
			EnableChildren(bundlePickerWidget, value);
		}

		private void EnableChildren(Widget widget, bool value)
		{
			foreach (var node in widget.Nodes) {
				(node as Widget).Enabled = value;
			}
		}

		public override void OnWorkspaceOpened()
		{
			windowWidget.Nodes.Clear();
			windowWidget.Nodes.Add(mainInterfaceWidget);
			targetPicker.Reload();
			AssetCooker.BeginCookBundles += () => abortButton.Enabled = true;
			AssetCooker.EndCookBundles += () => abortButton.Enabled = false;
		}

		public override void ReloadBundlePicker()
		{
			base.ReloadBundlePicker();
			bundlePicker.Setup();
			bundlePickerWidget.CreateBundlesList();
			UpdateBundlePicker(The.Workspace.BundlePickerVisible);
		}

		public override void ClearLog()
		{
			textView.Clear();
		}

		public override void RefreshMenu()
		{
			actionPicker.Items.Clear();
			actionsCommand.Menu.Clear();
			var letterUsedCount = new Dictionary<char, int>();
			foreach (var menuItem in The.MenuController.GetVisibleAndSortedItems()) {
				actionPicker.Items.Add(new CommonDropDownList.Item(menuItem.Label, menuItem.Action));
				// Arrange win-specific hotkey ampersands, minimizing conflicts
				var label = menuItem.Label.ToLower();
				bool wordStart = true;
				var insertionPoints = new List<KeyValuePair<char, int>>();
				for (int i = 0; i < label.Length; i++) {
					if (label[i] == ' ') {
						continue;
					}
					if (wordStart) {
						var key = label[i];
						if (!letterUsedCount.ContainsKey(key)) {
							letterUsedCount.Add(key, 0);
						}
						insertionPoints.Add(new KeyValuePair<char, int>(key, i));
					}
					wordStart = false;
					if (i < label.Length - 1 && label[i + 1] == ' ') {
						wordStart = true;
					}
				}
				insertionPoints.Sort((a, b) => {
					if (!letterUsedCount.ContainsKey(a.Key)) {
						letterUsedCount.Add(a.Key, 0);
					}
					if (!letterUsedCount.ContainsKey(b.Key)) {
						letterUsedCount.Add(b.Key, 0);
					}
					return letterUsedCount[a.Key] - letterUsedCount[b.Key];
				});
				var labelWithAmpersand = menuItem.Label.Insert(insertionPoints[0].Value, "&");
				letterUsedCount[insertionPoints[0].Key]++;
				actionsCommand.Menu.Add(new Command(labelWithAmpersand, () => { Execute(menuItem.Action); }));
			}
			OnActionOrTargetChanged(null);
		}

		public override bool AskConfirmation(string text)
		{
			bool? result = null;
			Application.InvokeOnMainThread(() => result = ConfirmationDialog.Show(text));
			while (result == null) {
				Thread.Sleep(1);
			}
			return result.Value;
		}

		public override bool AskChoice(string text, out bool yes)
		{
			yes = true;
			return true;
		}

		public override void ShowError(string message)
		{
			Application.InvokeOnMainThread(() => AlertDialog.Show(message));
		}

		public override MenuItem GetActiveAction()
		{
			return The.MenuController.Items.Find(i => i.Label == actionPicker.Text);
		}

		public override Target GetActiveTarget()
		{
			return targetPicker.SelectedTarget ?? The.Workspace.Targets.FirstOrDefault();
		}

		public override void SetActiveTarget(Target target)
		{
			throw new NotImplementedException();
		}

		public override EnvironmentType GetEnvironmentType()
		{
			return EnvironmentType.Orange;
		}

		public override IPluginUIBuilder GetPluginUIBuilder()
		{
			return new PluginUIBuidler();
		}

		public override void CreatePluginUI(IPluginUIBuilder builder)
		{
			if (!builder.SidePanel.Enabled) {
				return;
			}
			pluginPanel = builder.SidePanel as PluginPanel;
			mainInterfaceWidget.AddNode(pluginPanel);
			window.ClientSize = new Vector2(window.ClientSize.X + 150, window.ClientSize.Y);
		}

		public override void DestroyPluginUI()
		{
			mainInterfaceWidget.Nodes.Remove(pluginPanel);
			if (pluginPanel != null) {
				window.ClientSize = new Vector2(window.ClientSize.X - 150, window.ClientSize.Y);
				pluginPanel = null;
			}
		}

		public override void SaveToWorkspaceConfig(ref WorkspaceConfig config, ProjectConfig projectConfig)
		{
			if (projectConfig != null) {
				projectConfig.ActiveTargetIndex = targetPicker.Index;
				projectConfig.BundlePickerVisible = bundlePickerWidget.Visible;
			}
			if (window.State != WindowState.Minimized) {
				config.ClientPosition = window.ClientPosition;
				config.ClientSize = window.ClientSize;
				config.WindowState = window.State;
			}
		}

		public override void UpdateOpenedProjectPath(string projectPath) => projectPicker.ChosenFile = projectPath;

		public override void LoadFromWorkspaceConfig(WorkspaceConfig config, ProjectConfig projectConfig)
		{
			if (config.ClientPosition.X < 0) {
				config.ClientPosition.X = 0;
			}
			if (config.ClientPosition.Y < 0) {
				config.ClientPosition.Y = 0;
			}
			if (config.ClientPosition != Vector2.Zero) {
				window.ClientPosition = config.ClientPosition;
			}
			if (config.ClientSize != Vector2.Zero) {
				window.ClientSize = config.ClientSize;
			}
			if (config.WindowState != WindowState.Minimized) {
				window.State = config.WindowState;
			}
			if (projectConfig != null) {
				var newIndex = projectConfig.ActiveTargetIndex;
				if (newIndex < 0 || newIndex >= targetPicker.Items.Count) {
					newIndex = 0;
				}
				targetPicker.Index = newIndex;
				UpdateCacheModeCheckboxes(projectConfig.AssetCacheMode);
				UpdateBundlePicker(projectConfig.BundlePickerVisible);
			} else {
				UpdateBundlePicker(false);
				startPageWidget = ProduceStartPage(config.RecentProjects);
				windowWidget.Nodes.Clear();
				windowWidget.AddNode(startPageWidget);
			}
		}

		private class BundlePickerWidget : Widget
		{
			private readonly ThemedEditBox filter;
			private readonly ThemedScrollView scrollView;
			private readonly ThemedButton selectButton;
			private readonly ThemedButton refreshButton;
			private readonly Dictionary<string, ThemedCheckBox> checkboxes;
			private readonly Dictionary<string, Widget> lines;
			private readonly ThemedSimpleText infoText;
			private readonly BundlePicker bundlePicker;

			/// <summary>
			/// Creates basic UI elements, but lefts bundle list empty. To fill it, call <see cref="CreateBundlesList"/>
			/// </summary>
			public BundlePickerWidget(BundlePicker bundlePicker)
			{
				this.bundlePicker = bundlePicker;

				Layout = new VBoxLayout {
					Spacing = 6
				};
				MaxWidth = 250f;

				checkboxes = new Dictionary<string, ThemedCheckBox>();
				lines = new Dictionary<string, Widget>();
				scrollView = new ThemedScrollView();
				scrollView.CompoundPostPresenter.Add(new WidgetBoundsPresenter(Lime.Theme.Colors.ControlBorder));
				scrollView.Content.Layout = new VBoxLayout {
					Spacing = 6
				};
				scrollView.Content.Padding = new Thickness(6);
				scrollView.CompoundPresenter.Add(new WidgetFlatFillPresenter(Color4.White));

				selectButton = new ThemedButton {
					Text = "Select all",
					Clicked = SelectButtonClickHandler
				};

				refreshButton = new ThemedButton {
					Text = "Refresh",
					Clicked = Refresh,
				};

				filter = new ThemedEditBox();
				filter.Tasks.Add(FilterBundlesTask);

				infoText = new ThemedSimpleText("Selected action sets bundles by itself.") {
					Color = Theme.Colors.BlackText,
					MinMaxHeight = Theme.Metrics.DefaultEditBoxSize.Y,
					Visible = false,
					VAlignment = VAlignment.Center,
				};

				AddNode(filter);
				AddNode(infoText);
				AddNode(scrollView);

				var buttonLine = new Widget {
					Layout = new HBoxLayout {
						Spacing = 6
					}
				};
				AddNode(buttonLine);
				buttonLine.AddNode(new Widget { LayoutCell = new LayoutCell { StretchX = float.MaxValue }, MaxHeight = 0 });
				buttonLine.AddNode(refreshButton);
				buttonLine.AddNode(selectButton);
				selectButton.Tasks.Add(UpdateTextOfSelectButtonTask());
			}

			/// <summary>
			/// Fills bundle list with bundles for current active project
			/// </summary>
			public void CreateBundlesList()
			{
				if (scrollView.Content.Nodes.Count > 0) {
					scrollView.Content.Nodes.Clear();
				}
				foreach (var bundle in bundlePicker.GetListOfBundles(refresh: true)) {
					AddBundleToList(bundle);
				}
			}

			private void AddBundleToList(string bundle)
			{
				checkboxes[bundle] = new ThemedCheckBox();
				lines[bundle] = new Widget {
					Layout = new HBoxLayout {
						Spacing = 8
					},
					Nodes = {
						checkboxes[bundle],
						new ThemedSimpleText(bundle) {
							HitTestTarget = true,
							Clicked = () => {
								if (checkboxes[bundle].GloballyEnabled) {
									checkboxes[bundle].Checked =
										bundlePicker.SetBundleSelection(bundle, !checkboxes[bundle].Checked);
								}
							}
						}
					}
				};
				scrollView.Content.AddNode(lines[bundle]);
				checkboxes[bundle].Checked = true;
				checkboxes[bundle].Changed += args => bundlePicker.SetBundleSelection(bundle, checkboxes[bundle].Checked);
			}

			/// <summary>
			/// Sets info text params.
			/// </summary>
			public void SetInfoText(bool visibility, string text = null)
			{
				infoText.Visible = visibility;
				if (text != null) {
					infoText.Text = text;
				}
			}

			/// <summary>
			/// Marks all bundles as selected / deselected.
			/// </summary>
			public void SetSelectionForAll(bool state)
			{
				foreach (var bundle in checkboxes.Keys) {
					SetBundleSelection(bundle, state);
				}
			}

			/// <summary>
			/// Sets bundle state.
			/// </summary>
			public void SetBundleSelection(string bundle, bool state)
			{
				checkboxes[bundle].Checked = bundlePicker.SetBundleSelection(bundle, state);
			}

			/// <summary>
			/// Unlocks bundle's checkboxes.
			/// </summary>
			public void ClearBundleLocks()
			{
				foreach (var bundle in lines.Keys) {
					SetBundleLock(bundle, false);
				}
			}

			/// <summary>
			/// Locks / unlocks bundle's checkbox.
			/// </summary>
			public void SetBundleLock(string bundle, bool state)
			{
				lines[bundle].Enabled = !state;
				checkboxes[bundle].Enabled = !state;
			}

			/// <summary>
			/// Updates bundle list.
			/// </summary>
			public void Refresh()
			{
				var changed = The.UI.RefreshBundlesList();
				foreach (var bundle in changed) {
					if (checkboxes.ContainsKey(bundle)) {
						scrollView.Content.Nodes.Remove(lines[bundle]);
						checkboxes.Remove(bundle);
					} else {
						AddBundleToList(bundle);
					}
				}
			}

			private void SelectButtonClickHandler()
			{
				var deselect = true;
				foreach (var bundle in checkboxes.Keys) {
					if (!checkboxes[bundle].Checked) {
						deselect = false;
					}
				}
				foreach (var bundle in checkboxes.Keys) {
					SetBundleSelection(bundle, !deselect);
				}
			}

			private IEnumerator<object> UpdateTextOfSelectButtonTask()
			{
				while (true) {
					var allChecked = true;
					foreach (var bundle in checkboxes.Keys) {
						if (!checkboxes[bundle].Checked) {
							allChecked = false;
						}
					}
					if (allChecked) {
						selectButton.Text = "Deselect all";
					} else {
						selectButton.Text = "Select all";
					}
					yield return null;
				}
			}

			private IEnumerator<object> FilterBundlesTask()
			{
				var lastText = string.Empty;
				while (true) {
					var text = filter.Text;
					if (text == lastText) {
						yield return null;
					}
					lastText = text;
					foreach (var bundle in lines.Keys) {
						lines[bundle].Visible = bundle.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
					}
					yield return null;
				}
			}
		}

		public static Widget ProduceStartPage(IEnumerable<string> recentProjects)
		{
			ThemedButton openButton = null;
			var root = new Frame {
				Layout = new LinearLayout(LayoutDirection.TopToBottom),
				Nodes = {
					(openButton = new ThemedButton("Open") {
						Clicked = () => FileChooser.ShowOpenCitrusProjectDialog(The.Workspace.Open, recentProjects.FirstOrDefault()),
					})
				},
			};
			DecorateButton(openButton);
			foreach (var projectPath in recentProjects) {
				var projectPathBound = projectPath;
				Frame f = null;
				var projectName = Path.GetFileNameWithoutExtension(projectPath);
				root.AddNode(
					f = new Frame {
						Layout = new LinearLayout(LayoutDirection.LeftToRight),
						Nodes = {
							(openButton = new ThemedButton($"{projectName}\n at \"{projectPathBound}\"") {
								// Uses InvokeOnNextUpdate because Workspace.Load will remove StartPage (clear window widget nodes)
								// which leads to incorrect behavior update order. E.g. if plugin side panel will be enabled by plugin
								// it will crash 50% of times.
								Clicked = () => Application.InvokeOnNextUpdate(() => The.Workspace.Load(projectPathBound)),
							}),
							new ThemedButton("X") {
								Clicked = () => {
									The.Workspace.RemoveRecentProject(projectPathBound);
									root.Nodes.Remove(f);
								},
							}
						}

					}
				);
				DecorateButton(openButton);
			}
			return root;

			void DecorateButton(ThemedButton b)
			{
				b.MinSize = Vector2.Zero;
				b.MaxSize = Vector2.PositiveInfinity;
				var buttonText = (b["TextPresenter"] as SimpleText);
				buttonText.FontHeight = 100;
				buttonText.OverflowMode = TextOverflowMode.Minify;
			}
		}

		private class TextViewWriter : TextWriter
		{
			private readonly ThemedTextView textView;
			private readonly TextWriter consoleOutput;

			public TextViewWriter(ThemedTextView textView, TextWriter consoleOutput)
			{
				this.consoleOutput = consoleOutput;
				this.textView = textView;
				this.textView.Behaviour.Content.Gestures.Add(new DragGesture());
			}

			public override void WriteLine(string value)
			{
				Write(value + '\n');
			}

			private bool autoscrollEnabled = false;

			public override void Write(string value)
			{
				Application.InvokeOnMainThread(() => {
#if DEBUG
					System.Diagnostics.Debug.Write(value);
#endif // DEBUG
					if (autoscrollEnabled && !textView.Behaviour.IsScrolling() && !(textView.Behaviour as ScrollViewWithSlider).SliderIsDragging) {
						textView.ScrollToEnd();
					}
					autoscrollEnabled = textView.ScrollPosition == textView.MaxScrollPosition;
					consoleOutput.Write(value);
					textView.Append(value);
				});
			}

			public override Encoding Encoding { get; }
		}

		private class ProgressBarField : Widget
		{
			public int CurrentPosition;
			public int MaxPosition;

			private ThemedSimpleText textFieldA;
			private ThemedSimpleText textFieldB;

			public ProgressBarField()
			{
				Layout = new HBoxLayout { Spacing = 6 };
				MinMaxHeight = Theme.Metrics.DefaultButtonSize.Y;

				var bar = new ThemedFrame();
				var rect = new Widget();
				rect.CompoundPresenter.Add(new WidgetFlatFillPresenter(Lime.Theme.Colors.SelectedBorder));
				rect.Tasks.AddLoop(() => {
					rect.Size = new Vector2(bar.Width * Mathf.Clamp((float)CurrentPosition / MaxPosition, 0, 1), bar.ContentHeight);
				});
				bar.AddNode(rect);

				textFieldA = new ThemedSimpleText {
					VAlignment = VAlignment.Center,
					HAlignment = HAlignment.Center,
				};
				textFieldB = new ThemedSimpleText {
					VAlignment = VAlignment.Center,
					HAlignment = HAlignment.Center,
				};

				AddNode(bar);
				AddNode(textFieldA);
				AddNode(textFieldB);

				HideAndClear();
			}

			public void Progress(int amount = 1)
			{
				CurrentPosition += amount;
				Application.InvokeOnMainThread(() => {
					textFieldA.Text = (int)((float)CurrentPosition / MaxPosition * 100) + "%";
					textFieldB.Text = CurrentPosition + " / " + MaxPosition;
				});
			}

			public void Setup(int maxPosition)
			{
				CurrentPosition = 0;
				MaxPosition = maxPosition;
				Progress(0);
				Visible = true;
			}

			public void HideAndClear()
			{
				CurrentPosition = 100;
				MaxPosition = 100;
				Visible = false;
			}
		}
	}
}
