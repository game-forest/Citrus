using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.RemoteScripting
{
	internal class RemoteScriptingWidgets
	{
		public class TabbedWidget : ThemedTabbedWidget
		{
			private readonly List<TabbedWidgetPage> pages;

			public TabbedWidget(IEnumerable<TabbedWidgetPage> pages)
			{
				TabBar = new ThemedTabBar();
				var isFirstPage = true;
				this.pages = pages.ToList();
				foreach (var page in this.pages) {
					page.Initialize();
					AddTab(page.Tab, page.Content, isFirstPage);
					isFirstPage = false;
				}
			}

			public override void Dispose()
			{
				base.Dispose();
				foreach (var page in pages) {
					page.OnDispose();
				}
			}
		}

		public abstract class TabbedWidgetPage
		{
			public ThemedTab Tab { get; protected set; }
			public Widget Content { get; protected set; }

			public abstract void Initialize();
			public virtual void OnDispose() { }
		}

		public class Toolbar : Widget
		{
			public Widget Content { get; }

			public Toolbar()
			{
				Padding = new Thickness(4);
				MinMaxHeight = Metrics.ToolbarHeight;
				MinWidth = 50;
				Presenter = new SyncDelegatePresenter<Widget>(Render);
				Layout = new HBoxLayout {
					Spacing = 2,
					DefaultCell = new DefaultLayoutCell(Alignment.Center)
				};
				Nodes.AddRange(
					new Widget {
						Layout = new VBoxLayout(),
						Nodes = {
							(Content = new Widget {
								Layout = new HBoxLayout { Spacing = 2 }
							})
						}
					}
				);
			}

			private static void Render(Widget widget)
			{
				widget.PrepareRendererState();
				Renderer.DrawRect(Vector2.Zero, widget.Size, ColorTheme.Current.Toolbar.Background);
			}
		}

		public class TextView : ThemedTextView
		{
			private static readonly object scrollToEndTaskTag = new object();
			private readonly ICommand viewInExternalEditorCommand = new Command("View in External Editor");
			private readonly ICommand commandCopy = new Command("Copy");
			private readonly ICommand commandClear = new Command("Clear");
			private readonly int maxRowCount;
			private readonly int removeRowCount;

			private string filePath;
			private StreamWriter file;
			private int fileBufferSize;

			public string FilePath
			{
				get => filePath;
				set
				{
					CloseFile();
					filePath = value;
					try {
						file =
							!string.IsNullOrEmpty(filePath) ?
							new StreamWriter(File.Open(filePath, FileMode.Append, FileAccess.Write, FileShare.Read)) :
							null;
					} catch (System.Exception exception) {
						System.Console.WriteLine(exception);
						CloseFile();
						filePath = null;
					}
				}
			}

			public TextView(int maxRowCount = 500, int removeRowCount = 250)
			{
				this.maxRowCount = maxRowCount;
				this.removeRowCount = removeRowCount;
				TrimWhitespaces = false;
				var menu = new Menu {
					viewInExternalEditorCommand,
					Command.MenuSeparator,
					commandCopy,
					commandClear
				};
				Updated += dt => {
					if (Input.WasKeyPressed(Key.Mouse0) || Input.WasKeyPressed(Key.Mouse1)) {
						SetFocus();
						Window.Current.Activate();
					}
					if (Input.WasKeyPressed(Key.Mouse1)) {
						viewInExternalEditorCommand.Enabled = !string.IsNullOrEmpty(FilePath);
						menu.Popup();
					}
					if (viewInExternalEditorCommand.WasIssued()) {
						viewInExternalEditorCommand.Consume();
						if (file != null && fileBufferSize > 0) {
							try {
								file.Flush();
								fileBufferSize = 0;
							} catch (System.Exception exception) {
								System.Console.WriteLine(exception);
								CloseFile();
							}
						}
						if (!string.IsNullOrEmpty(FilePath)) {
							System.Diagnostics.Process.Start(FilePath);
						}
					}
					if (commandCopy.WasIssued()) {
						commandCopy.Consume();
						Clipboard.Text = Text;
					}
					if (commandClear.WasIssued()) {
						commandClear.Consume();
						Clear();
					}
				};
			}

			public void AppendLine(string text)
			{
				if (text == null) {
					return;
				}
				var isScrolledToEnd = Mathf.Abs(Behaviour.ScrollPosition - Behaviour.MaxScrollPosition) < Mathf.ZeroTolerance;
				if (text.Length == 0 || text[text.Length - 1] != '\n') {
					text += '\n';
				}
				Append(text);
				if (file != null) {
					try {
						file.Write(text);
						fileBufferSize += text.Length;
						const int FileBufferSizeLimit = 2048;
						if (fileBufferSize >= FileBufferSizeLimit) {
							file.Flush();
							fileBufferSize = 0;
						}
					} catch (System.Exception exception) {
						System.Console.WriteLine(exception);
						CloseFile();
					}
				}
				if (Content.Nodes.Count >= maxRowCount) {
					Content.Nodes.RemoveRange(0, removeRowCount);
				}
				if (isScrolledToEnd || Behaviour.Content.LateTasks.AnyTagged(scrollToEndTaskTag)) {
					Behaviour.Content.LateTasks.StopByTag(scrollToEndTaskTag);
					IEnumerator<object> ScrollToEnd()
					{
						yield return null;
						this.ScrollToEnd();
					}
					Behaviour.Content.LateTasks.Add(ScrollToEnd, scrollToEndTaskTag);
				}
			}

			public void CloseFile()
			{
				try {
					file?.Close();
				} catch (Exception exception) {
					System.Console.WriteLine(exception);
				}
				fileBufferSize = 0;
				file = null;
			}
		}
	}
}
