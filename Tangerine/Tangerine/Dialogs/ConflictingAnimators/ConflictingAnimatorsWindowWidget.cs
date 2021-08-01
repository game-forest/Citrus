using Lime;
using System.Collections.Generic;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine
{
	public class ConflictingAnimatorsWindowWidget : ThemedInvalidableWindowWidget
	{
		private const string BoldStyleTag = "b";

		public ThemedScrollView SearchResultsView { get; protected set; }
		public ThemedButton SearchButton { get; protected set; }
		public ThemedCheckBox GlobalCheckBox { get; protected set; }
		public ThemedCheckBox ExternalScenesCheckBox { get; protected set; }

		public ConflictingAnimatorsWindowWidget(Window window) : base(window)
		{
			Layout = new VBoxLayout { Spacing = 8 };
			Padding = new Thickness(8);
			FocusScope = new KeyboardFocusScope(this);
			CreateContent();
		}

		private void CreateContent()
		{
			AddNode(SearchResultsView = CreateScrollView());
			AddNode(CreateSearchControlsBar());
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
			SearchButton = new ThemedButton {
				Text = "Search",
				Clicked = OnSearchIssued,
			};

			var labeledGlobalCheckBox = CreateLabeledCheckBox(
				GlobalCheckBox = new ThemedCheckBox {
					Checked = false,
				},
				text: "Global"
			);
			GlobalCheckBox.Changed += OnGlobalSearchToggled;

			var labeledExternalScenesCheckBox = CreateLabeledCheckBox(
				ExternalScenesCheckBox = new ThemedCheckBox {
					Checked = false,
				},
				text: "External Scenes"
			);
			ExternalScenesCheckBox.Changed += OnExternalScenesTraversionToggled;

			var observedSceneLabel = CreateLabel();
			observedSceneLabel.Tasks.AddLoop(() => {
				observedSceneLabel.Visible = Document.Current != null;
				observedSceneLabel.Text = $"Observed Document: <{BoldStyleTag}>{Document.Current?.DisplayName}</{BoldStyleTag}>";
				AdjustLabelWidth(observedSceneLabel);
			});

			var spacer = Spacer.HFill();
			spacer.Height = spacer.MaxHeight = 0.0f;

			return new Widget {
				Layout = new HBoxLayout { Spacing = 8 },
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Nodes = {
					SearchButton,
					labeledGlobalCheckBox,
					labeledExternalScenesCheckBox,
					spacer,
					observedSceneLabel,
				},
			};
		}

		private void OnSearchIssued()
		{
			SearchResultsView.Content.Nodes.Clear();
			ConflictingAnimatorsInfoProvider.Invalidate();
			var sections = new Dictionary<string, SectionWidget>();
			var isGlobal = GlobalCheckBox.Checked;
			var external = ExternalScenesCheckBox.Checked;
			var path = isGlobal ? null : Document.Current.Path;
			var iconTexture = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;
			foreach (var info in ConflictingAnimatorsInfoProvider.Get(path, external && !isGlobal)) {
				if (info != null) {
					if (!sections.TryGetValue(info.DocumentPath, out var section)) {
						section = new SectionWidget(info.DocumentPath, iconTexture);
						sections[info.DocumentPath] = section;
						SearchResultsView.Content.AddNode(section);
					}
					section.AddItem(new SectionItemWidget(info));
				}
			}
		}

		private void OnGlobalSearchToggled(CheckBox.ChangedEventArgs args)
		{
			var enabled = !args.Value;
			ExternalScenesCheckBox.Enabled = enabled;
			ExternalScenesCheckBox.HitTestTarget = enabled;
		}

		private void OnExternalScenesTraversionToggled(CheckBox.ChangedEventArgs args) { }

		private static RichText CreateLabel(string text = null) {
			text ??= string.Empty;
			var label = new RichText {
				Text = text,
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Padding = new Thickness(left: 5.0f),
				MinMaxHeight = Theme.Metrics.TextHeight,
				Localizable = false,
				Color = Color4.White,
				HAlignment = HAlignment.Left,
				VAlignment = VAlignment.Center,
				OverflowMode = TextOverflowMode.Ellipsis,
				TrimWhitespaces = true,
				Nodes = {
					new TextStyle {
						Size = Theme.Metrics.TextHeight,
						TextColor = Theme.Colors.BlackText,
					},
					new TextStyle {
						Id = BoldStyleTag,
						Size = Theme.Metrics.TextHeight,
						TextColor = Theme.Colors.BlackText,
						Font = new SerializableFont(FontPool.DefaultBoldFontName),
					},
				},
			};
			AdjustLabelWidth(label);
			return label;
		}

		private static void AdjustLabelWidth(RichText label)
		{
			// TODO: Reconsider after uniform TextWidget is merged.
			label.Width = 1024.0f;
			label.MinMaxWidth = label.Width = label.MeasureText().Width;
		}

		private static Widget CreateLabeledCheckBox(ThemedCheckBox checkBox, string text)
		{
			var label = CreateLabel(text);
			return new Frame {
				Layout = new HBoxLayout { Spacing = 2 },
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Nodes = {
					checkBox,
					label,
				}
			};
		}
	}
}
