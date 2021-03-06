using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class TriggerSelectionDialog
	{
		private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
		private readonly Window window;
		private readonly Action<string> onSave;
		private readonly Dictionary<string, HashSet<string>> triggers;
		private readonly Dictionary<string, Dictionary<string, ThemedCheckBox>> checkBoxes;
		private readonly Dictionary<string, Queue<string>> groupSelection;
		private readonly Widget rootWidget;
		private HashSet<string> selected;
		private ThemedScrollView scrollView;
		private ThemedEditBox filter;


		public TriggerSelectionDialog(Dictionary<string, HashSet<string>> triggers, HashSet<string> selected, Action<string> onSave)
		{
			this.onSave = onSave;
			this.triggers = triggers;
			this.selected = selected;
			groupSelection = new Dictionary<string, Queue<string>>();
			checkBoxes = new Dictionary<string, Dictionary<string, ThemedCheckBox>>();
			window = new Window(new WindowOptions {
				Title = "Trigger Selection",
				ClientSize = new Vector2(300, 400),
				MinimumDecoratedSize = new Vector2(300, 400),
				FixedSize = false,
				Visible = false,
#if WIN
				Icon = new System.Drawing.Icon(new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()),
#endif // WIN
			});
			rootWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
					CreateFilter(),
					Spacer.VSpacer(5),
					CreateScrollView(),
					CreateButtonsPanel()
				}
			};
			rootWidget.FocusScope = new KeyboardFocusScope(rootWidget);
			rootWidget.LateTasks.AddLoop(() => {
				if (rootWidget.Input.ConsumeKeyPress(Key.Escape)) {
					window.Close();
				}
			});
			window.ShowModal();
		}

		private ThemedEditBox CreateFilter()
		{
			filter = new ThemedEditBox {
				LayoutCell = new LayoutCell(Alignment.Center)
			};
			filter.AddChangeWatcher(
				() => filter.Text,
				_ => ApplyFilter(_)
			);
			return filter;
		}

		private ThemedScrollView CreateScrollView()
		{
			scrollView = new ThemedScrollView();
			scrollView.Behaviour.Content.Padding = new Thickness(4);
			scrollView.Behaviour.Content.Layout = new VBoxLayout();
			scrollView.Behaviour.Content.LayoutCell = new LayoutCell(Alignment.Center);
			Rectangle calcRect(Widget w) {
				var wp = w.ParentWidget;
				var p = wp.Padding;
				return new Rectangle(
					-w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
					-w.Position + wp.Size + new Vector2(p.Right, p.Bottom)
				);
			}
			scrollView.Content.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				var rect = calcRect(w);
				Renderer.DrawRect(rect.A, rect.B, Theme.Colors.GrayBackground.Transparentify(0.9f));
			}));
			scrollView.Content.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				var rect = calcRect(w);
				Renderer.DrawRectOutline(rect.A, rect.B, Theme.Colors.ControlBorder);
			}));
			foreach (var key in triggers.Keys) {
				groupSelection[key] = new Queue<string>();
				checkBoxes[key] = new Dictionary<string, ThemedCheckBox>();
				var expandButton = new ThemedExpandButton {
					MinMaxSize = Vector2.One * 20f,
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Expanded = true
				};
				var groupLabel = new ThemedSimpleText {
					Text = key,
					VAlignment = VAlignment.Center,
					LayoutCell = new LayoutCell(Alignment.Center),
					ForceUncutText = false,
					HitTestTarget = true
				};
				var header = new Widget {
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.Center),
					Padding = new Thickness(4),
					Nodes = {
						expandButton,
						groupLabel
					}
				};
				var wrapper = new Frame {
					Padding = new Thickness(4),
					Layout = new VBoxLayout(),
					Visible = true
				};
				expandButton.Clicked += () => {
					wrapper.Visible = !wrapper.Visible;
				};
				groupLabel.Clicked += () => {
					wrapper.Visible = !wrapper.Visible;
					expandButton.Expanded = !expandButton.Expanded;
				};
				foreach (var trigger in triggers[key]) {
					wrapper.AddNode(CreateTriggerSelectionWidget(trigger, key));
				}
				scrollView.Content.AddNode(new Widget {
					Layout = new VBoxLayout(),
					LayoutCell = new LayoutCell(Alignment.Center),
					Padding = new Thickness(4),
					Nodes = {
						header,
						wrapper
					}
				});
			}
			return scrollView;
		}

		private Widget CreateTriggerSelectionWidget(string trigger, string key)
		{
			var isChecked = selected.Contains(trigger);
			var checkBox = new ThemedCheckBox {
				Checked = isChecked
			};
			if (isChecked) {
				groupSelection[key].Enqueue(trigger);
			}
			checkBoxes[key].Add(trigger, checkBox);

			var widget = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell(Alignment.Center),
				Padding = new Thickness(left: 8, right:8, top: 4, bottom: 4),
				Nodes = {
					new ThemedSimpleText {
						Text = trigger,
						LayoutCell = new LayoutCell(Alignment.Center),
						Anchors = Anchors.LeftRight,
						ForceUncutText = false
					},
					Spacer.HSpacer(5),
					checkBox
				},
				HitTestTarget = true
			};
			widget.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>(w => {
				if (w.IsMouseOverThisOrDescendant()) {
					w.PrepareRendererState();
					Renderer.DrawRect(Vector2.Zero, w.Size, Theme.Colors.SelectedBackground);
				}
			}));
			widget.LateTasks.Add(Theme.MouseHoverInvalidationTask(widget));
			widget.Clicked += checkBox.Toggle;
			checkBox.Changed += e => ToggleTriggerCheckbox(trigger, key);
			return widget;
		}

		private Widget CreateButtonsPanel()
		{
			var okButton = new ThemedButton { Text = "Ok" };
			var cancelButton = new ThemedButton { Text = "Cancel" };
			okButton.Clicked += () => {
				var value = "";
				foreach (var s in selected) {
					value += $"{s},";
				}
				if (!string.IsNullOrEmpty(value)) {
					value = value.Trim(',');
				}
				onSave.Invoke(value);
				window.Close();
			};
			cancelButton.Clicked += () => {
				window.Close();
			};
			return new Widget {
				Layout = new HBoxLayout { Spacing = 8 },
				LayoutCell = new LayoutCell { StretchY = 0 },
				Padding = new Thickness { Top = 5 },
				Nodes = {
					new Widget { MinMaxHeight = 0 },
					okButton,
					cancelButton
				},
			};
		}

		private void ToggleTriggerCheckbox(string trigger, string key)
		{
			var currentBoxes = checkBoxes[key];
			var checkBox = currentBoxes[trigger];
			if (checkBox.Checked) {
				selected.Add(trigger);
			} else {
				selected.Remove(trigger);
			}

			var currentGroup = groupSelection[key];
			if (currentGroup.Count > 0) {
				var last = currentGroup.Peek();
				if (last == trigger) {
					currentGroup.Dequeue();
				} else {
					currentBoxes[last].Toggle();
					currentGroup.Enqueue(trigger);
				}
			} else {
				currentGroup.Enqueue(trigger);
			}
		}

		private void ApplyFilter(string filter)
		{
			filter = filter.ToLower();
			scrollView.ScrollPosition = 0;
			var isShowingAll = string.IsNullOrEmpty(filter);
			foreach (var group in scrollView.Content.Nodes) {
				var header = group.Nodes[0];
				var expandButton = header.Nodes[0] as ThemedExpandButton;
				var wrapper = group.Nodes[1];
				var expanded = false;
				foreach (var node in wrapper.Nodes) {
					var trigger = (node.Nodes[0] as ThemedSimpleText).Text.ToLower();
					node.AsWidget.Visible = trigger.Contains(filter) || isShowingAll;
					if (!isShowingAll && node.AsWidget.Visible && !expanded) {
						expanded = true;
						expandButton.Expanded = true;
						wrapper.AsWidget.Visible = true;
					}
				}
			}
		}
	}
}
