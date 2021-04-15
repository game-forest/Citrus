using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Operations;

namespace Tangerine.UI.Timeline
{
	public class NumericScaleDialog
	{
		private readonly Window window;
		private readonly WindowWidget rootWidget;
		private readonly Button okButton;
		private readonly Button cancelButton;
		private readonly Widget container;
		public float Scale { get; set; }

		public NumericScaleDialog()
		{
			window = new Window(new WindowOptions {
				ClientSize = new Vector2(250, 70),
				FixedSize = true,
				Title = "Numeric Scale",
				Visible = false,
			});
			rootWidget = new ThemedInvalidableWindowWidget(window) {
				Padding = new Thickness(8),
				Layout = new VBoxLayout(),
				Nodes = {
					(container = new Widget {
						Layout = new VBoxLayout(),
					}),
					new Widget {
						Layout = new HBoxLayout { Spacing = 8 },
						LayoutCell = new LayoutCell {
							StretchY = 0
						},
						Padding = new Thickness { Top = 5 },
						Nodes = {
							new Widget { MinMaxHeight = 0 },
							(okButton = new ThemedButton { Text = "Ok" }),
							(cancelButton = new ThemedButton { Text = "Cancel" }),
						},
					}
				}
			};
			rootWidget.FocusScope = new KeyboardFocusScope(rootWidget);
			Scale = 1;
			var editor = new FloatPropertyEditor(new PropertyEditorParams(container, this, nameof(Scale), "Scale"));
			cancelButton.Clicked += () => {
				window.Close();
			};
			okButton.Clicked += () => {
				editor.Submit();
				if (Scale < Mathf.ZeroTolerance) {
					AlertDialog.Show("Scale value too small");
					window.Close();
					return;
				}
				Document.Current.History.DoTransaction(ScaleKeyframes);
				window.Close();
			};
			cancelButton.SetFocus();
			window.ShowModal();
		}

		private void ScaleKeyframes()
		{
			if (GridSelection.GetSelectionBoundaries(out var boundaries) && Scale > Mathf.ZeroTolerance) {
				var processed = new HashSet<IAnimator>();
				var saved = new List<IKeyframe>();
				foreach (var animable in GridSelection.EnumerateAnimators(boundaries)) {
					foreach (var animator in animable.Animators) {
						if (animator.AnimationId != Document.Current.AnimationId || processed.Contains(animator)) {
							continue;
						}
						processed.Add(animator);
						saved.Clear();
						IEnumerable<IKeyframe> keys = animator.ReadonlyKeys.Where(k =>
							k.Frame >= boundaries.Left && k.Frame < boundaries.Right
						).ToList();
						if (Scale < 1) {
							keys = keys.Reverse().ToList();
						}
						foreach (var key in keys) {
							saved.Add(key);
							RemoveKeyframe.Perform(animator, key.Frame);
						}
						foreach (var key in saved) {
							// The formula should behave similiar to stretching animation with mouse
							int newFrame = (int)(
								boundaries.Left +
								(key.Frame - boundaries.Left) *
								(1 + (boundaries.Left - boundaries.Right) * Scale) /
								(1 + boundaries.Left - boundaries.Right)
							);
							var newKey = key.Clone();
							newKey.Frame = newFrame;
							SetAnimableProperty.Perform(
								animable.Host, animator.TargetPropertyPath, newKey.Value,
								createAnimatorIfNeeded: true,
								createInitialKeyframeForNewAnimator: false,
								newKey.Frame
							);
							SetKeyframe.Perform(animable.Host, animator.TargetPropertyPath, Document.Current.Animation, newKey);
						}
					}
				}
				ClearGridSelection.Perform();
				for (int i = boundaries.Top; i <= boundaries.Bottom; ++i) {
					SelectGridSpan.Perform(i, boundaries.Left, (int)(boundaries.Left + (boundaries.Right - boundaries.Left) * Scale));
				}
			} else {
				Document.Current.History.RollbackTransaction();
			}
		}
	}
}
