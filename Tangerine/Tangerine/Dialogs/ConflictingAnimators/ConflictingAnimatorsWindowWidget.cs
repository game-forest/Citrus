using System;
using System.Collections.Generic;
using System.Threading;
using Lime;
using Orange;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;
using Console = System.Console;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public class ConflictingAnimatorsWindowWidget : ThemedInvalidableWindowWidget
	{
		// TODO:
		// Encapsulate data in a specialized view widget.
		private readonly ITexture sectionTexture = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;
		private readonly Dictionary<string, SectionWidget> sections = new Dictionary<string, SectionWidget>();
		private readonly Queue<ConflictInfo> pending = new Queue<ConflictInfo>();

		private CancellationTokenSource enumeratingNodesCancellationSource;

		public readonly ThemedScrollView SearchResultsView;
		public readonly ThemedButton SearchButton;
		public readonly ThemedButton CancelButton;
		public readonly ThemedCheckBox GlobalCheckBox;
		public readonly ThemedCheckBox ExternalScenesCheckBox;

		public ConflictingAnimatorsWindowWidget(Window window) : base(window)
		{
			Layout = new VBoxLayout { Spacing = 8 };
			Padding = new Thickness(8);
			FocusScope = new KeyboardFocusScope(this);

			SearchButton = new ThemedButton { Text = "Search" };
			CancelButton = new ThemedButton { Text = "Cancel" };
			GlobalCheckBox = new ThemedCheckBox();
			ExternalScenesCheckBox = new ThemedCheckBox();
			SearchButton.Clicked += OnSearchIssued;
			CancelButton.Clicked += OnSearchCancelled;
			GlobalCheckBox.Changed += OnGlobalSearchToggled;
			ExternalScenesCheckBox.Changed += OnExternalScenesTraversionToggled;

			AddNode(SearchResultsView = CreateScrollView());
			AddNode(CreateSearchControlsBar());
			Tasks.Add(PendingInfoProcessor);
		}

		private ThemedScrollView CreateScrollView()
		{
			var scrollView = new ThemedScrollView {
				Content = {
					Layout = new VBoxLayout { Spacing = 16 },
					Padding = new Thickness(8),
				},
			};
			scrollView.Content.CompoundPresenter.AddRange(new[] {
				new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = CalcRect(w);
					Renderer.DrawRect(rect.A, rect.B, Theme.Colors.WhiteBackground);
				})
			});
			scrollView.Content.CompoundPostPresenter.AddRange(new[] {
				new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = CalcRect(w);
					Renderer.DrawRectOutline(rect.A, rect.B, Theme.Colors.ControlBorder);
				})
			});
			return scrollView;

			static Rectangle CalcRect(Widget w)
			{
				var wp = w.ParentWidget;
				var p = wp.Padding;
				return new Rectangle(
					-w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
					-w.Position + wp.Size + new Vector2(p.Right, p.Bottom)
				);
			}
		}

		private Widget CreateSearchControlsBar()
		{
			var sceneCaption = new ThemedCaption();
			sceneCaption.Tasks.AddLoop(() => {
				sceneCaption.Visible = Document.Current != null;
				sceneCaption.Text = $"Observed Document: {ThemedCaption.Stylize(Document.Current?.DisplayName, TextStyleIdentifiers.Bold)}";
				sceneCaption.AdjustWidthToText();
			});

			// TODO:
			// Make progress bar that shows up
			// once search has been issued.
			var spacer = Spacer.HFill();
			spacer.Height = spacer.MaxHeight = 0.0f;

			return new Widget {
				Layout = new HBoxLayout { Spacing = 8 },
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Nodes = {
					SearchButton,
					CancelButton,
					AddCaptionToCheckBox(GlobalCheckBox, "Global"),
					AddCaptionToCheckBox(ExternalScenesCheckBox, "External Scenes"),
					spacer,
					sceneCaption,
				},
			};
		}

		private IEnumerator<object> PendingInfoProcessor()
		{
			while (true) {
				for (var i = 0; i < 100; ++i) {
					if (pending.Count == 0) break;

					var info = pending.Dequeue();
					if (!sections.TryGetValue(info.DocumentPath, out var section)) {
						section = new SectionWidget(info.DocumentPath, sectionTexture);
						sections[info.DocumentPath] = section;
						SearchResultsView.Content.AddNode(section);
					}
					section.AddItem(new SectionItemWidget(info));
				}
				yield return null;
			}
		}

		private void OnSearchIssued()
		{
			FillItemsCancel();
			SearchResultsView.Content.Nodes.Clear();
			ConflictingAnimatorsInfoProvider.Invalidate();
			FillItemsAsync();
		}

		private void OnSearchCancelled() => FillItemsCancel();

		private void FillItemsCancel()
		{
			enumeratingNodesCancellationSource?.Cancel();
			enumeratingNodesCancellationSource = null;
		}

		private async void FillItemsAsync()
		{
			sections.Clear();
			pending.Clear();
			var isGlobal = GlobalCheckBox.Checked;
			var external = ExternalScenesCheckBox.Checked;
			var path = isGlobal ? null : Document.Current.Path;
			var shouldTraverseExternal = external && !isGlobal;
			enumeratingNodesCancellationSource = new CancellationTokenSource();
			try {
				var cancellationToken = enumeratingNodesCancellationSource.Token;
				await System.Threading.Tasks.Task.Run(
					() => {
						AssetBundle.Current = new TangerineAssetBundle(The.Workspace.AssetsDirectory);
						foreach (var info in ConflictingAnimatorsInfoProvider.Get(path, shouldTraverseExternal, cancellationToken)) {
							cancellationToken.ThrowIfCancellationRequested();
							pending.Enqueue(info);
						}
					},
					cancellationToken
				);
				enumeratingNodesCancellationSource = null;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (System.Exception exception) {
				Console.WriteLine(exception);
				enumeratingNodesCancellationSource = null;
			}
		}

		private void OnGlobalSearchToggled(CheckBox.ChangedEventArgs args)
		{
			var enabled = !args.Value;
			ExternalScenesCheckBox.Enabled = enabled;
			ExternalScenesCheckBox.HitTestTarget = enabled;
		}

		private void OnExternalScenesTraversionToggled(CheckBox.ChangedEventArgs args) { }

		private static Widget AddCaptionToCheckBox(ThemedCheckBox checkBox, string text)
		{
			var caption = new ThemedCaption(text);
			return new Frame {
				Layout = new HBoxLayout { Spacing = 2 },
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Nodes = {
					checkBox,
					caption,
				}
			};
		}
	}
}
