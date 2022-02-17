using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Cmds = Tangerine.UI.FilesystemCommands;

namespace Tangerine.UI.FilesystemView
{
	// Single instance of view on filesystem, cooking rules editor and preview
	// Created and managed by FilesystemPane
	public class FilesystemView
	{
		private enum DragState
		{
			None,
			WaitingForSelecting,
			WaitingForDragging,
			Selecting,
			Dragging,
		}

		public Widget RootWidget { get; private set; }
		private ThemedScrollView scrollView;
		private FilesystemToolbar toolbar;
		private FilesystemModel filesystemModel;
		private readonly FilesystemSelection filesystemSelection = new FilesystemSelection();
		private Lime.FileSystemWatcher fsWatcher;
		private CookingRulesEditor crEditor;
		private Preview preview;
		private List<(string Path, FilesystemSelection Selection)> navigationHistory =
			new List<(string, FilesystemSelection)>();
		private int navigationHistoryIndex = -1;
		private NodeToggler toggleCookingRules;
		private NodeToggler togglePreview;
		private DragState dragState;
		private ThemedHSplitter cookingRulesSplitter;
		private ThemedVSplitter selectionPreviewSplitter;
		private FilesystemItem lastKeyboardSelectedFilesystemItem;
		private FilesystemItem lastKeyboardRangeSelectionEndFilesystemItem;
		private Vector2 dragStartPosition;
		private Vector2 dragEndPosition;
		private FilesystemSelection savedFilesystemSelection;

		private SortType sortType = SortType.Name;
		public SortType SortType
		{
			get
			{
				return sortType;
			}
		}

		private OrderType orderType = OrderType.Ascending;
		public OrderType OrderType
		{
			get
			{
				return orderType;
			}
		}

		public void Split(SplitterType type)
		{
			FilesystemPane.Instance.Split(this, type);
		}

		public void Close()
		{
			FilesystemPane.Instance.Close(this);
		}

		public void GoBackward()
		{
			filesystemSelection.Clear();
			var newIndex = navigationHistoryIndex - 1;
			(string Path, FilesystemSelection Selection) i;
			do {
				if (newIndex < 0 || newIndex >= navigationHistory.Count) {
					return;
				}
				i = navigationHistory[newIndex];
				if (!Directory.Exists(i.Path)) {
					navigationHistory.RemoveAt(newIndex);
					newIndex--;
				} else {
					break;
				}
			} while (true);
			GoTo(i.Path);
			foreach (var s in i.Selection) {
				filesystemSelection.Select(s);
			}
			navigationHistoryIndex = newIndex;
		}

		public void GoForward()
		{
			filesystemSelection.Clear();
			var newIndex = navigationHistoryIndex + 1;
			(string Path, FilesystemSelection Selection) i;
			do {
				if (newIndex >= navigationHistory.Count) {
					return;
				}
				i = navigationHistory[newIndex];
				if (!Directory.Exists(i.Path)) {
					navigationHistory.RemoveAt(newIndex);
				} else {
					break;
				}
			} while (true);
			GoTo(i.Path);
			foreach (var s in i.Selection) {
				filesystemSelection.Select(s);
			}
			navigationHistoryIndex = newIndex;
		}

		private void AddToNavHystory(string path)
		{
			if (navigationHistory.Count > 0 && navigationHistory[navigationHistoryIndex].Path == path) {
				return;
			}
			var i = (path, filesystemSelection.Clone());
			navigationHistory.Add(i);
			int newIndex = navigationHistoryIndex + 1;
			navigationHistory.RemoveRange(newIndex, navigationHistory.Count - newIndex - 1);
			navigationHistoryIndex = newIndex;
		}

		public void SortByType(SortType sortType, OrderType orderType)
		{
			this.sortType = sortType;
			this.orderType = orderType;
			InvalidateView(filesystemModel.CurrentPath, sortType, orderType);
		}

		private Task fsWatcherInvalidationTask;

		private void InvalidateFSWatcher(string path)
		{
			if (fsWatcherInvalidationTask != null) {
				RootWidget.Tasks.Remove(fsWatcherInvalidationTask);
				fsWatcherInvalidationTask = null;
			}
			fsWatcher?.Dispose();
			fsWatcher = new Lime.FileSystemWatcher(path, includeSubdirectories: false);
			Action<string> onFsWatcherChanged = p => {
				if (fsWatcherInvalidationTask != null) {
					RootWidget.Tasks.Remove(fsWatcherInvalidationTask);
				}
				RootWidget.Tasks.Add(fsWatcherInvalidationTask = new Task(Task.Delay(0.2f, () => {
					InvalidateView(filesystemModel.CurrentPath);
					preview.ClearTextureCache(p);
					fsWatcherInvalidationTask = null;
				})));
			};
			fsWatcher.Deleted += p => {
				filesystemSelection.Deselect(p);
				onFsWatcherChanged(p);
			};
			fsWatcher.Created += onFsWatcherChanged;
			fsWatcher.Renamed += (prevFullPath, fullPath) => onFsWatcherChanged(fullPath);
			fsWatcher.Changed += onFsWatcherChanged;
		}

		public FilesystemView()
		{
			RootWidget = new Widget() { Id = "FSRoot" };
			RootWidget.FocusScope = new KeyboardFocusScope(RootWidget);
			scrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
				TabTravesable = new TabTraversable(),
			};
			crEditor = new CookingRulesEditor(NavigateAndSelect);
			crEditor.RootWidget.TabTravesable = new TabTraversable();
			preview = new Preview();
			preview.RootWidget.TabTravesable = new TabTraversable();
		}

		// Component with user preferences should be added to rootWidget at this moment
		public void Initialize()
		{
			var up = RootWidget.Components.Get<ViewNodeComponent>().ViewNode as FSViewNode;
			filesystemModel = new FilesystemModel(up.Path);
			toolbar = new FilesystemToolbar(this, filesystemModel);
			toolbar.TabTravesable = new TabTraversable();
			InitializeWidgets();
			selectionPreviewSplitter.Stretches = Splitter.GetStretchesList(up.SelectionPreviewSplitterStretches, 1, 1);
			cookingRulesSplitter.Stretches = Splitter.GetStretchesList(up.CookingRulesSplitterStretches, 1, 1);
			toggleCookingRules = new NodeToggler(
				crEditor.RootWidget, () => { crEditor.Invalidate(filesystemSelection); }
			);
			togglePreview = new NodeToggler(preview.RootWidget, () => { preview.Invalidate(filesystemSelection); });
			if (!up.ShowCookingRulesEditor) {
				toggleCookingRules.Toggle();
			}
			if (!up.ShowSelectionPreview) {
				togglePreview.Toggle();
			}
			foreach (var n in RootWidget.Descendants) {
				var w = n.AsWidget;
				if (w.TabTravesable != null) {
					w.HitTestTarget = true;
				}
			}
			RootWidget.Updating += (float delta) => {
				if (
					RootWidget.Input.IsKeyPressed(Key.Control) &&
					RootWidget.Input.WasKeyReleased(Key.L)
				) {
					toolbar.AddressBar.SetFocusOnEditor();
				}
			};
		}

		private void NavigateAndSelect(string filename)
		{
			GoTo(Path.GetDirectoryName(filename));
			filesystemSelection.Clear();
			filesystemSelection.Select(filename);
		}

		private void InitializeWidgets()
		{
			RootWidget.AddChangeWatcher(() => filesystemSelection.Version, Selection_Changed);
			scrollView.Content.Layout = new FlowLayout(LayoutDirection.TopToBottom) { Spacing = 1.0f };
			scrollView.Content.Padding = new Thickness(5.0f);
			scrollView.Content.CompoundPostPresenter.Insert(
				0, new SyncDelegatePresenter<Widget>(RenderFilesWidgetRectSelection)
			);
			scrollView.Updating += ScrollViewUpdating;
			scrollView.Content.Presenter = new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				var wp = w.ParentWidget;
				var p = wp.Padding;
				Renderer.DrawRect(
					a: -w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
					b: -w.Position + wp.Size + new Vector2(p.Right, p.Bottom),
					color: Theme.Colors.WhiteBackground
				);
			});
			RootWidget.AddChangeWatcher(() => dragState, (ds) => Window.Current.Invalidate());
			RootWidget.AddChangeWatcher(() => dragEndPosition, WhenSelectionRectChanged);
			RootWidget.AddChangeWatcher(() => WidgetContext.Current.NodeUnderMouse, (value) => {
				if (value != null && scrollView.Content == value.Parent) {
					Window.Current.Invalidate();
				}
			});
			RootWidget.AddChangeWatcher(() => filesystemModel.CurrentPath, (p) => {
				var up = RootWidget.Components.Get<ViewNodeComponent>().ViewNode as FSViewNode;
				up.Path = p;
				AddToNavHystory(p);
				// In case of preventing the unselecting file that selected via "Navigate to"
				if (!Directory.GetFiles(filesystemModel.CurrentPath).Any(x => filesystemSelection.Contains(x))) {
					filesystemSelection.Clear();
				}
				InvalidateView(p);
				InvalidateFSWatcher(p);
				preview.ClearTextureCache();
				lastKeyboardSelectedFilesystemItem = scrollView.Content.FirstChild as FilesystemItem;
			});
			RootWidget.Layout = new VBoxLayout();
			RootWidget.AddNode(
				cookingRulesSplitter = new ThemedHSplitter {
					Nodes = {
						new Widget {
							Layout = new VBoxLayout(),
							Nodes = {
								toolbar,
								(selectionPreviewSplitter = new ThemedVSplitter {
									Nodes = {
										scrollView,
										preview.RootWidget,
									},
								}),
							},
						},
						crEditor.RootWidget,
					},
				}
			);
		}

		private void Selection_Changed(int version)
		{
			crEditor.Invalidate(filesystemSelection);
			preview.Invalidate(filesystemSelection);
			Window.Current.Invalidate();
		}

		private void InvalidateView(string path, SortType sortType, OrderType orderType)
		{
			scrollView.Content.Nodes.Clear();
			foreach (var item in filesystemModel.EnumerateItems(sortType, orderType)) {
				var fsItem = new FilesystemItem(item);
				scrollView.Content.AddNode(fsItem);
				fsItem.CompoundPresenter.Insert(0, new SyncDelegatePresenter<FilesystemItem>(RenderFSItemSelection));
			}
		}

		private void InvalidateView(string path)
		{
			InvalidateView(path, sortType, orderType);
		}

		public bool Open(string path)
		{
			try {
				var attr = File.GetAttributes(path);
				if ((attr & FileAttributes.Directory) == FileAttributes.Directory) {
					GoTo(path);
				} else {
					if (path.EndsWith(".tan", StringComparison.OrdinalIgnoreCase)) {
						Project.Current.OpenDocument(path, true);
					}
				}
				return true;
			} catch (ArgumentException) {
				AlertDialog.Show("The path is empty, contains only white spaces, or contains invalid characters.");
			} catch (PathTooLongException) {
				AlertDialog.Show("The specified path, file name, or both exceed the system-defined maximum length.");
			} catch (NotSupportedException) {
				AlertDialog.Show("The path is in an invalid format.");
			} catch (FileNotFoundException) {
				AlertDialog.Show($"Tangerine can not find \"{path}\".\nCheck the spelling and try again.");
			} catch (DirectoryNotFoundException) {
				AlertDialog.Show(
					"The path represents a directory and is invalid, such as being on an unmapped drive, " +
					"or the directory cannot be found."
				);
			} catch (IOException) {
				AlertDialog.Show("This file is being used by another process.");
			} catch (UnauthorizedAccessException) {
				AlertDialog.Show("Tangerine does not have the required permission.");
			}
			return false;
		}

		private void OpenSpecial(string path)
		{
			System.Diagnostics.Process.Start(path);
		}

		private void RenderFSItemSelection(FilesystemItem filesystemItem)
		{
			if (filesystemSelection.Contains(filesystemItem.FilesystemPath)) {
				filesystemItem.PrepareRendererState();
				Renderer.DrawRect(Vector2.Zero, filesystemItem.Size, Theme.Colors.SelectedBackground);
			} else if (filesystemItem.IsMouseOverThisOrDescendant()) {
				filesystemItem.PrepareRendererState();
				Renderer.DrawRect(
					Vector2.Zero,
					filesystemItem.Size,
					Theme.Colors.HoveredBackground);
			}
			if (filesystemItem == lastKeyboardRangeSelectionEndFilesystemItem) {
				filesystemItem.PrepareRendererState();
				Renderer.DrawRectOutline(Vector2.Zero, filesystemItem.Size, Theme.Colors.SelectedBorder);
			}
		}

		private void WhenSelectionRectChanged(Vector2 value)
		{
			if (dragState != DragState.Selecting) {
				return;
			}
			var p0 = dragStartPosition;
			var p1 = dragEndPosition;
			var r0 = new Rectangle(
				new Vector2(Mathf.Min(p0.X, p1.X), Mathf.Min(p0.Y, p1.Y)),
				new Vector2(Mathf.Max(p0.X, p1.X), Mathf.Max(p0.Y, p1.Y)));
			foreach (var n in scrollView.Content.Nodes) {
				var ic = n as FilesystemItem;
				var r1 = new Rectangle(ic.Position, ic.Position + ic.Size);
				if (Rectangle.Intersect(r0, r1) != Rectangle.Empty) {
					if (savedFilesystemSelection != null) {
						if (savedFilesystemSelection.Contains(ic.FilesystemPath)) {
							filesystemSelection.Deselect(ic.FilesystemPath);
						} else {
							filesystemSelection.Select(ic.FilesystemPath);
						}
					} else {
						filesystemSelection.Select(ic.FilesystemPath);
					}
				} else {
					if (savedFilesystemSelection != null) {
						if (savedFilesystemSelection.Contains(ic.FilesystemPath)) {
							filesystemSelection.Select(ic.FilesystemPath);
						} else {
							filesystemSelection.Deselect(ic.FilesystemPath);
						}
					} else if (
						filesystemSelection.Contains(ic.FilesystemPath)
						&& !scrollView.Input.IsKeyPressed(Key.Shift)
					) {
						filesystemSelection.Deselect(ic.FilesystemPath);
					}
				}
			}
		}

		private const float TypeNavigationInterval = 0.5f;
		private float typeNavigationTimeout = 0.0f;
		private string typeNavigationPrefix = string.Empty;

		private void ScrollViewUpdating(float dt)
		{
			ProcessInputOverFSItem();
			ProcessDragState(dt);
			ProcessChangeViewMode();
			typeNavigationTimeout -= dt;
			if (scrollView.IsFocused()) {
				ProcessTypingNavigation();
				ProcessOtherCommands();
				ProcessSelectionCommands();
				foreach (var c in printableKeysCommands) {
					c.Consume();
				}
			}
		}

		private void ProcessChangeViewMode()
		{
			if (
				scrollView.Input.IsKeyPressed(Key.Control) &&
				(scrollView.Input.WasKeyPressed(Key.MouseWheelDown) || scrollView.Input.WasKeyPressed(Key.MouseWheelUp))
			) {
				scrollView.Unlink();
				if (scrollView.Direction == ScrollDirection.Horizontal) {
					scrollView = new ThemedScrollView(ScrollDirection.Vertical) {
						TabTravesable = new TabTraversable(),
					};
					scrollView.Content.Layout = new FlowLayout(LayoutDirection.LeftToRight) { Spacing = 1.0f };
				} else {
					scrollView = new ThemedScrollView(ScrollDirection.Horizontal) {
						TabTravesable = new TabTraversable(),
					};
					scrollView.Content.Layout = new FlowLayout(LayoutDirection.TopToBottom) { Spacing = 1.0f };
				}

				scrollView.Content.Padding = new Thickness(5.0f);
				scrollView.Content.CompoundPostPresenter.Insert(
					0, new SyncDelegatePresenter<Widget>(RenderFilesWidgetRectSelection)
				);
				scrollView.Updated += ScrollViewUpdating;
				scrollView.Content.Presenter = new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var wp = w.ParentWidget;
					var p = wp.Padding;
					Renderer.DrawRect(
						a: -w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
						b: -w.Position + wp.Size + new Vector2(p.Right, p.Bottom),
						color: Theme.Colors.WhiteBackground
					);
				});

				InvalidateView(filesystemModel.CurrentPath);
				lastKeyboardSelectedFilesystemItem = scrollView.Content.FirstChild as FilesystemItem;

				selectionPreviewSplitter.Nodes.Insert(0, scrollView);
			}
		}

		private static readonly List<Command> printableKeysCommands = Key.Enumerate()
			.Where(k => k.IsPrintable())
			.Select(i => new Command(i))
			.Union(
				Key.Enumerate()
					.Where(k => k.IsPrintable())
					.Select(i => new Command(new Shortcut(Modifiers.Shift, i)))
			).ToList();

		private void ProcessOtherCommands()
		{
			if (!Command.SelectAll.IsConsumed()) {
				Command.SelectAll.Enabled = true;
			}

			if (Cmds.Cancel.Consume()) {
				typeNavigationTimeout = TypeNavigationInterval;
				typeNavigationPrefix = string.Empty;
			} else if (Window.Current.Input.WasKeyReleased(Key.Menu)) {
				if (!filesystemSelection.Empty) {
					Window.Current.Input.ConsumeKey(Key.Menu);
					SystemShellContextMenu.Instance.Show(
						filesystemSelection.ToArray(), lastKeyboardSelectedFilesystemItem.GlobalPosition
					);
				}
			} else if (Cmds.GoBack.Consume()) {
				GoBackward();
			} else if (Cmds.GoForward.Consume()) {
				GoForward();
			} else if (Cmds.GoUp.Consume() || Cmds.GoUpAlso.Consume()) {
				GoUp();
			} else if (Cmds.Enter.Consume()) {
				if (lastKeyboardSelectedFilesystemItem != null) {
					Open(lastKeyboardSelectedFilesystemItem.FilesystemPath);
				}
			} else if (Cmds.EnterSpecial.Consume()) {
				if (lastKeyboardSelectedFilesystemItem != null) {
					OpenSpecial(lastKeyboardSelectedFilesystemItem.FilesystemPath);
				}
			} else if (Command.SelectAll.Consume()) {
				filesystemSelection.Clear();
				filesystemSelection.SelectRange(
					scrollView.Content.Nodes.Select(n => (n as FilesystemItem).FilesystemPath)
				);
			} else if (Cmds.ToggleSelection.Consume()) {
				if (lastKeyboardRangeSelectionEndFilesystemItem != null) {
					var path = lastKeyboardRangeSelectionEndFilesystemItem.FilesystemPath;
					if (filesystemSelection.Contains(path)) {
						filesystemSelection.Deselect(path);
					} else {
						filesystemSelection.Select(path);
					}
				}
			}
		}

		private void ProcessDragState(float dt)
		{
			var input = scrollView.Input;

			switch (dragState) {
				case DragState.None: {
						if (scrollView.IsMouseOver()) {
							if (input.ConsumeKeyPress(Key.Mouse0)) {
								dragEndPosition = dragStartPosition = scrollView.Content.LocalMousePosition();
								dragState = DragState.WaitingForSelecting;
							}
							if (input.ConsumeKeyRelease(Key.Mouse1)) {
								dragState = DragState.None;
								filesystemSelection.Clear();
								SystemShellContextMenu.Instance.Show(filesystemModel.CurrentPath);
							}
						}
						break;
					}
				case DragState.Selecting: {
						if (Application.Input.WasKeyReleased(Key.Mouse0)) {
							Application.Input.ConsumeKey(Key.Mouse0);
							scrollView.SetFocus();
							dragState = DragState.None;
						}
						dragEndPosition = scrollView.Content.LocalMousePosition();
						var scrollOffset = 0.0f;
						var pos = scrollView.LocalMousePosition();
						if (scrollView.Direction == ScrollDirection.Vertical) {
							if (pos.Y < 0) {
								scrollOffset = pos.Y;
							} else if (pos.Y > scrollView.Height) {
								scrollOffset = pos.Y - scrollView.Height;
							}
						} else if (scrollView.Direction == ScrollDirection.Horizontal) {
							if (pos.X < 0) {
								scrollOffset = pos.X;
							} else if (pos.X > scrollView.Width) {
								scrollOffset = pos.X - scrollView.Width;
							}
						}
						scrollView.ScrollPosition += Math.Sign(scrollOffset) * Mathf.Sqr(scrollOffset) * 0.1f * dt;
						scrollView.ScrollPosition = Mathf.Clamp(
							scrollView.ScrollPosition,
							scrollView.MinScrollPosition,
							scrollView.MaxScrollPosition
						);
						Window.Current.Invalidate();
					}
					break;
				case DragState.WaitingForDragging:
					if ((dragStartPosition - Window.Current.Input.MousePosition).Length > 5.0f) {
						dragState = DragState.Dragging;
						CommonWindow.Current.DragFiles(filesystemSelection.ToArray());
					}
					if (
						Window.Current.Input.WasKeyReleased(Key.Mouse0) ||
						Window.Current.Input.WasKeyReleased(Key.Mouse0DoubleClick)
					) {
						dragState = DragState.None;
						Window.Current.Input.ConsumeKey(Key.Mouse0);
					}
					break;
				case DragState.WaitingForSelecting:
					if (input.ConsumeKeyRelease(Key.Mouse0)) {
						dragState = DragState.None;
						if (!input.IsKeyPressed(Key.Control) && !input.IsKeyPressed(Key.Shift)) {
							filesystemSelection.Clear();
						}
					} else if (input.IsKeyPressed(Key.Mouse0)) {
						if ((scrollView.Content.LocalMousePosition() - dragStartPosition).Length > 6.0f) {
							dragState = DragState.Selecting;
							if (input.IsKeyPressed(Key.Control)) {
								savedFilesystemSelection = filesystemSelection.Clone();
							} else {
								savedFilesystemSelection = null;
							}
						}
					}
					break;
				case DragState.Dragging:
					if (Window.Current.Input.WasKeyReleased(Key.Mouse0)) {
						Window.Current.Input.ConsumeKey(Key.Mouse0);
						dragState = DragState.None;
					}
					break;
			}
		}

		private string lastSelected;

		private void ProcessInputOverFSItem()
		{
			// TODO: Ctrl + Shift clicks
			var nodeUnderMouse = WidgetContext.Current.NodeUnderMouse;
			if (
				nodeUnderMouse == null ||
				!(
					nodeUnderMouse is FilesystemItem &&
					nodeUnderMouse.Parent == scrollView.Content ||
					nodeUnderMouse.Parent is FilesystemItem &&
					nodeUnderMouse.Parent.Parent == scrollView.Content
				)
			) {
				return;
			}
			var fsItem = nodeUnderMouse as FilesystemItem ?? nodeUnderMouse.Parent as FilesystemItem;
			var path = fsItem.FilesystemPath;
			var input = fsItem.Input;
			if (input.ConsumeKeyPress(Key.Mouse0DoubleClick)) {
				scrollView.SetFocus();
				Open(path);
				// it's important to return here since Open(path) may take a lot of time
				// and since it's synchronous we want to cease Mouse0 down handling below
				// or it will lead to drag behaviour. i.e. if there's a
				// doubleclick event -- dont handle click event, they come both at same frame.
				return;
			}
			if (fsItem.Input.ConsumeKeyRelease(Key.Mouse1)) {
				scrollView.SetFocus();
				if (!filesystemSelection.Contains(path)) {
					filesystemSelection.Clear();
					filesystemSelection.Select(path);
				}
				SystemShellContextMenu.Instance.Show(filesystemSelection);
			}
			if (fsItem.Input.ConsumeKeyRelease(Key.Mouse0)) {
				scrollView.SetFocus();
				if (!fsItem.IsMouseOver() || filesystemSelection.Contains(path)) {
					if (
						dragState != DragState.Selecting &&
						dragState != DragState.Dragging &&
						!fsItem.Input.IsKeyPressed(Key.Control) &&
						!fsItem.Input.IsKeyPressed(Key.Shift)
					) {
						filesystemSelection.Clear();
					}
					filesystemSelection.Select(path);
					lastKeyboardSelectedFilesystemItem = fsItem;
				}
				dragState = DragState.None;
			}
			if (fsItem.Input.WasKeyPressed(Key.Mouse0)) {
				scrollView.SetFocus();
				input.ConsumeKey(Key.Mouse0);
				if (input.IsKeyPressed(Key.Control) && !input.IsKeyPressed(Key.Shift)) {
					input.ConsumeKey(Key.Control);
					if (filesystemSelection.Contains(path)) {
						filesystemSelection.Deselect(path);
					} else {
						filesystemSelection.Select(path);
						lastSelected = path;
					}
				} else if (!input.IsKeyPressed(Key.Control) && input.IsKeyPressed(Key.Shift)) {
					input.ConsumeKey(Key.Shift);
					var items = filesystemModel.EnumerateItems(sortType, orderType).ToList();
					var currentIndex = items.IndexOf(path);
					int prevIndex;
					if (lastSelected == default) {
						prevIndex = items.FindIndex(i => filesystemSelection.Contains(i));
					} else {
						prevIndex = items.IndexOf(lastSelected);
					}
					filesystemSelection.Clear();
					if (prevIndex == -1) {
						filesystemSelection.Select(path);
						lastSelected = path;
						return;
					}
					lastSelected = items[prevIndex];
					for (int i = Math.Min(currentIndex, prevIndex); i <= Math.Max(currentIndex, prevIndex); ++i) {
						filesystemSelection.Select(items[i]);
					}
					fsItem.Input.ConsumeKeyRelease(Key.Mouse0);
				} else {
					if (filesystemSelection.Contains(path)) {
						dragState = DragState.WaitingForDragging;
						dragStartPosition = Window.Current.Input.MousePosition;
						lastKeyboardSelectedFilesystemItem = fsItem;
					} else {
						if (!fsItem.IsMouseOver()) {
							dragState = DragState.WaitingForSelecting;
							dragStartPosition = scrollView.Content.LocalMousePosition();
						} else {
							if (!filesystemSelection.Contains(path)) {
								filesystemSelection.Clear();
								filesystemSelection.Select(path);
								lastSelected = path;
							}
							dragState = DragState.WaitingForDragging;
							dragStartPosition = Window.Current.Input.MousePosition;
							lastKeyboardSelectedFilesystemItem = fsItem;
						}
					}
				}
				Window.Current?.Invalidate();
			}
		}

		private void ProcessTypingNavigation()
		{
			var input = scrollView.Input;
			if (string.IsNullOrEmpty(input.TextInput)) {
				return;
			}
			if (typeNavigationTimeout <= 0.0f) {
				typeNavigationPrefix = string.Empty;
			}
			typeNavigationTimeout = TypeNavigationInterval;
			var prevPrefix = typeNavigationPrefix;
			bool offset = false;
			if (prevPrefix == input.TextInput) {
				offset = true;
			} else {
				typeNavigationPrefix += input.TextInput;
			}
			var matches = scrollView.Content.Nodes
				.Select(i => i as FilesystemItem)
				.Where(i => {
					var a = Path.GetFileName(i.FilesystemPath);
					var b = typeNavigationPrefix;
					return a.StartsWith(b, true, CultureInfo.CurrentCulture);
				})
				.ToList();
			if (matches.Count != 0) {
				var index = matches.IndexOf(lastKeyboardSelectedFilesystemItem);
				if (index == -1) {
					index = 0;
				}
				if (offset) {
					index = (index + 1) % matches.Count;
				}
				filesystemSelection.Clear();
				filesystemSelection.Select(matches[index].FilesystemPath);
				lastKeyboardSelectedFilesystemItem = matches[index];
				EnsureFSItemVisible(lastKeyboardSelectedFilesystemItem);
			}
		}

		private void ProcessSelectionCommands()
		{
			int indexDelta = 0;
			bool select = false;
			bool toggle = false;
			var index = 0;
			var maxIndex = scrollView.Content.Nodes.Count - 1;
			if (lastKeyboardSelectedFilesystemItem != null) {
				index = scrollView.Content.Nodes.IndexOf(lastKeyboardSelectedFilesystemItem);
			}
			int rangeSelectionIndex = index;
			if (lastKeyboardRangeSelectionEndFilesystemItem != null) {
				rangeSelectionIndex = scrollView.Content.Nodes.IndexOf(lastKeyboardRangeSelectionEndFilesystemItem);
			}
			var flowLayout = scrollView.Content.Layout as FlowLayout;
			int columnCount = flowLayout.ColumnCount(0);
			int rowCount = flowLayout.RowCount(0);
			float rowHeight = FilesystemItem.ItemPadding * 2 + FilesystemItem.IconSize;
			for (int navType = 0; navType < navCommands.Count; navType++) {
				for (int navOffset = 0; navOffset < navCommands[navType].Count; navOffset++) {
					var cmd = navCommands[navType][navOffset];
					if (cmd.Consume()) {
						select = navType == 1;
						toggle = navType == 2;
						var sign = navOffset % 2 == 0 ? -1 : 1;
						if (scrollView.Direction == ScrollDirection.Vertical) {
							switch (navOffset) {
								// Left, Right
								case 0: case 1: indexDelta = sign * 1; break;
								// Up,  Down
								case 2: case 3: indexDelta = sign * columnCount; break;
								// PageUp, PageDown
								case 4:
								case 5:
									int currentColumn = index % columnCount;
									int count = scrollView.Content.Nodes.Count;
									bool lastRow = currentColumn < count % columnCount;
									indexDelta =
										(
											sign
											* columnCount
											* ((int)(scrollView.Size.Y / (rowHeight + flowLayout.Spacing)) - 1)
										).Clamp(
											currentColumn - index,
											currentColumn
											+ columnCount * (count / columnCount - (lastRow ? 0 : 1))
											- index
										);
									if (indexDelta == 0) {
										indexDelta = sign < 0 ? -index : count - index - 1;
									}
									break;
								// Home
								case 6: indexDelta = -rangeSelectionIndex; break;
								// End
								case 7: indexDelta = maxIndex - rangeSelectionIndex; break;
							}
						} else if (scrollView.Direction == ScrollDirection.Horizontal) {
							switch (navOffset) {
								// Left, Right
								case 0: case 1: indexDelta = sign * rowCount; break;
								// Up,  Down
								case 2: case 3: indexDelta = sign * 1; break;
								// PageUp, PageDown
								case 4:
								case 5:
									indexDelta = (sign * rowCount)
										.Clamp(-index, scrollView.Content.Nodes.Count - index - 1);
									break;
								// Home
								case 6: indexDelta = -rangeSelectionIndex; break;
								// End
								case 7: indexDelta = maxIndex - rangeSelectionIndex; break;
							}
						}
					}
				}
			}
			if (indexDelta != 0) {
				if (select) {
					int selectionEndIndex = lastKeyboardRangeSelectionEndFilesystemItem != null
						? scrollView.Content.Nodes.IndexOf(lastKeyboardRangeSelectionEndFilesystemItem)
						: index;
					int newIndex = selectionEndIndex + indexDelta;
					if (newIndex >= 0 && newIndex <= maxIndex) {
						filesystemSelection.Clear();
						for (int i = Math.Min(index, newIndex); i <= Math.Max(index, newIndex); i++) {
							var path = (scrollView.Content.Nodes[i] as FilesystemItem).FilesystemPath;
							filesystemSelection.Select(path);
						}
						lastKeyboardRangeSelectionEndFilesystemItem =
							scrollView.Content.Nodes[newIndex] as FilesystemItem;
						EnsureFSItemVisible(lastKeyboardRangeSelectionEndFilesystemItem);
					}
				} else {
					if (!toggle) {
						int newIndex = index + indexDelta;
						if (newIndex >= 0 && newIndex <= maxIndex) {
							lastKeyboardSelectedFilesystemItem = scrollView.Content.Nodes[newIndex] as FilesystemItem;
							var path = lastKeyboardSelectedFilesystemItem.FilesystemPath;
							filesystemSelection.Clear();
							filesystemSelection.Select(path);
							lastKeyboardRangeSelectionEndFilesystemItem = null;
							EnsureFSItemVisible(lastKeyboardSelectedFilesystemItem);
						}
					} else {
						int selectionEndIndex = lastKeyboardRangeSelectionEndFilesystemItem != null
							? scrollView.Content.Nodes.IndexOf(lastKeyboardRangeSelectionEndFilesystemItem)
							: index;
						int newIndex = selectionEndIndex + indexDelta;
						if (newIndex >= 0 && newIndex <= maxIndex) {
							lastKeyboardRangeSelectionEndFilesystemItem =
								scrollView.Content.Nodes[newIndex] as FilesystemItem;
							EnsureFSItemVisible(lastKeyboardRangeSelectionEndFilesystemItem);
							Window.Current.Invalidate();
						}
					}
				}
			}
		}

		private void EnsureFSItemVisible(FilesystemItem fsItem)
		{
			float min = 0;
			float offset = 0;
			var pos = fsItem.CalcPositionInSpaceOf(scrollView);
			if (scrollView.Direction == ScrollDirection.Vertical) {
				min = pos.Y;
				offset = min + fsItem.Height - scrollView.Height;
			} else if (scrollView.Direction == ScrollDirection.Horizontal) {
				min = pos.X;
				offset = min + fsItem.Width - scrollView.Width;
			}
			EnsureRangeVisible(min, offset);
		}

		private void EnsureSelectionVisible()
		{
			float min = float.MaxValue;
			float offset = float.MinValue;
			foreach (var n in scrollView.Content.Nodes) {
				var fsItem = n as FilesystemItem;
				if (!filesystemSelection.Contains(fsItem.FilesystemPath)) {
					continue;
				}
				var pos = fsItem.CalcPositionInSpaceOf(scrollView);
				if (scrollView.Direction == ScrollDirection.Vertical) {
					min = Mathf.Min(min, pos.Y);
					offset = Mathf.Max(offset, pos.Y + fsItem.Height - scrollView.Height);
				} else if (scrollView.Direction == ScrollDirection.Horizontal) {
					min = Mathf.Min(min, pos.X);
					offset = Mathf.Max(offset, pos.X + fsItem.Width - scrollView.Width);
				}
			}
			EnsureRangeVisible(min, offset);
		}

		private void EnsureRangeVisible(float min, float offset)
		{
			if (offset > 0.0f) {
				scrollView.ScrollPosition += offset;
			}
			if (min < 0.0f) {
				scrollView.ScrollPosition += min;
			}
			scrollView.ScrollPosition = Mathf.Clamp(
				scrollView.ScrollPosition, scrollView.MinScrollPosition, scrollView.MaxScrollPosition
			);
		}

		private static readonly List<List<ICommand>> navCommands = new List<List<ICommand>> {
			// simple navigation
			new List<ICommand> {
				Cmds.Left,
				Cmds.Right,
				Cmds.Up,
				Cmds.Down,
				Cmds.PageUp,
				Cmds.PageDown,
				Cmds.Home,
				Cmds.End,
			},
			// Range-select (shift) navigation
			new List<ICommand> {
				Cmds.SelectLeft,
				Cmds.SelectRight,
				Cmds.SelectUp,
				Cmds.SelectDown,
				Cmds.SelectPageUp,
				Cmds.SelectPageDown,
				Cmds.SelectHome,
				Cmds.SelectEnd,
			},
			// Toggle-select (hold ctrl, navigate, toggle with space)
			new List<ICommand> {
				Cmds.ToggleLeft,
				Cmds.ToggleRight,
				Cmds.ToggleUp,
				Cmds.ToggleDown,
				Cmds.TogglePageUp,
				Cmds.TogglePageDown,
				Cmds.ToggleHome,
				Cmds.ToggleEnd,
			},
		};

		private void RenderFilesWidgetRectSelection(Widget canvas)
		{
			if (dragState != DragState.Selecting) {
				return;
			}
			canvas.PrepareRendererState();
			Renderer.DrawRect(dragStartPosition, dragEndPosition, new Color4(150, 180, 230, 128));
			Renderer.DrawRectOutline(dragStartPosition, dragEndPosition, Theme.Colors.KeyboardFocusBorder);
		}

		public void GoUp()
		{
			filesystemModel.GoUp();
		}

		public void GoTo(string path)
		{
			filesystemModel.GoTo(path);
		}

		private class NodeToggler
		{
			private Node savedParent;
			private int savedIndex;
			private Node node;
			private Action invalidator;
			public NodeToggler(Node n, Action invalidator)
			{
				node = n;
				this.invalidator = invalidator;
			}
			public void Toggle()
			{
				if (node.Parent != null) {
					savedParent = node.Parent;
					savedIndex = savedParent.Nodes.IndexOf(node);
					node.Unlink();
				} else {
					savedParent.Nodes.Insert(Mathf.Clamp(savedIndex, 0, savedParent.Nodes.Count), node);
					invalidator?.Invoke();
				}
			}
		}

		public void TogglePreview()
		{
			togglePreview.Toggle();
		}

		public void ToggleCookingRules()
		{
			toggleCookingRules.Toggle();
		}

		public void SelectAsset(string path)
		{
			var dir = Path.GetDirectoryName(path);
			path = path.Replace('/', '\\');
			filesystemSelection.Clear();
			foreach (string f in Directory.GetFiles(dir)) {
				if (Path.ChangeExtension(f, null).EndsWith(path)) {
					filesystemSelection.Select(f);
					lastSelected = f;
				}
			}
			EnsureSelectionVisible();
		}

		// TODO: probably remove the function and either
		// 1. make it so input goes through toolbar to fsview and you don't have to set focus manually.
		// 2. invoke set focus when path changes
		public void SetFocus()
		{
			scrollView.Content.SetFocus();
		}
	}
}
