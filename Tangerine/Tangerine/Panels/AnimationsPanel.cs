using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.Panels
{
	public class AnimationsPanel : IDocumentView
	{
		private const string legacyAnimationId = "[Legacy]";
		private readonly Widget panelWidget;
		private readonly Frame rootWidget;
		private readonly ThemedScrollView scrollView;
		private readonly float rowHeight = Theme.Metrics.DefaultEditBoxSize.Y;

		public static AnimationsPanel Instance { get; private set; }

		static class Commands
		{
			public static readonly ICommand Up = new Command(Key.Up);
			public static readonly ICommand Down = new Command(Key.Down);
			public static readonly ICommand Delete = Command.Delete;
		}

		public AnimationsPanel(Widget panelWidget)
		{
			Instance = this;
			this.panelWidget = panelWidget;
			scrollView = new ThemedScrollView { TabTravesable = new TabTraversable() };
			this.rootWidget = new Frame {
				Id = "AnimationsPanel",
				Padding = new Thickness(4),
				Layout = new VBoxLayout { Spacing = 4 },
				Nodes = { scrollView }
			};
			scrollView.Content.CompoundPresenter.Insert(0, new SyncDelegatePresenter<Widget>(w => {
				w.PrepareRendererState();
				var selectedIndex = GetSelectedAnimationIndex();
				Renderer.DrawRect(
					0, rowHeight * selectedIndex,
					w.Width, rowHeight * (selectedIndex + 1),
					scrollView.IsFocused() ? Theme.Colors.SelectedBackground : Theme.Colors.SelectedInactiveBackground);
			}));
			var dragGesture = new DragGesture(0);
			dragGesture.Recognized += () => {
				var index = (scrollView.Content.LocalMousePosition().Y / rowHeight).Floor();
				if (index >= 0 && index < GetAnimations().Count) {
					var animation = GetAnimations()[index];
					if (!animation.IsLegacy && animation != Document.Current.Animation) {
						// Dirty hack: using a file drag&drop mechanics for dropping animation clips on the timeline grid.
						var encodedAnimationId = Convert.ToBase64String(Encoding.UTF8.GetBytes(animation.Id));
						// DragFiles in Winforms blocks execution, call it on the next update
						Application.InvokeOnNextUpdate(() => {
							Window.Current.DragFiles(new[] { encodedAnimationId });
						});
					}
				}
			};
			var mouseDownGesture = new ClickGesture(0);
			mouseDownGesture.Recognized += SelectAnimationBasedOnMousePosition;
			scrollView.Gestures.Add(dragGesture);
			scrollView.Gestures.Add(new ClickGesture(1, ShowContextMenu));
			scrollView.Gestures.Add(mouseDownGesture);
			scrollView.Gestures.Add(new DoubleClickGesture(0, RenameAnimation));
			scrollView.Tasks.Add(ProcessCommandsTask);
			scrollView.AddChangeWatcher(CalcAnimationsHashCode, _ => Refresh());
		}

		private void RenameAnimation()
		{
			var index = GetSelectedAnimationIndex();
			var animation = GetAnimations()[index];
			var item = (Widget)scrollView.Content.Nodes[index];
			var label = item["Label"];
			label.Visible = false;
			var editor = new ThemedEditBox();
			label.Parent.Nodes.Insert(label.CollectionIndex(), editor);
			editor.Text = animation.Id;
			editor.SetFocus();
			editor.AddChangeWatcher(() => editor.IsFocused(), focused => {
				if (!focused) {
					var newId = editor.Text.Trim();
					if (!animation.Owner.Animations.Any(a => a.Id == newId)) {
						RenameAnimationHelper(newId);
					}
					editor.Unlink();
					label.Visible = true;
				}
			});
			editor.Submitted += s => {
				RenameAnimationHelper(s.Trim());
				editor.Unlink();
				label.Visible = true;
			};

			void RenameAnimationHelper(string newId)
			{
				string error = null;
				if (animation.IsLegacy) {
					error = "Can't rename legacy animation";
				} else if (animation.Id == Animation.ZeroPoseId) {
					error = "Can't rename zero pose animation";
				} else if (newId.IsNullOrWhiteSpace() || newId == Animation.ZeroPoseId) {
					error = "Invalid animation id";
				} else if (TangerineDefaultCharsetAttribute.IsValid(newId, out var message) != ValidationResult.Ok) {
					error = message;
				} else if (animation.Owner.Animations.Any(a => a.Id == newId)) {
					error = $"An animation '{newId}' already exists";
				}
				if (error != null) {
					UI.AlertDialog.Show(error, "Ok");
					return;
				}
				Document.Current.History.DoTransaction(() => {
					var oldId = animation.Id;
					Core.Operations.SetProperty.Perform(animation, nameof(Animation.Id), newId);
					foreach (var a in animation.Owner.Animations) {
						foreach (var track in a.Tracks) {
							foreach (var animator in track.Animators) {
								if (animator.AnimationId == oldId) {
									Core.Operations.SetProperty.Perform(animator, nameof(IAnimator.AnimationId), newId);
								}
							}
						}
					}
					ChangeAnimatorsAnimationId(animation.OwnerNode);

					void ChangeAnimatorsAnimationId(Node node)
					{
						foreach (var child in node.Nodes) {
							foreach (var animator in child.Animators) {
								if (animator.AnimationId == oldId) {
									Core.Operations.SetProperty.Perform(animator, nameof(IAnimator.AnimationId), newId);
								}
							}
							if (child.ContentsPath != null) {
								continue;
							}
							if (child.Animations.Any(i => i.Id == oldId)) {
								continue;
							}
							ChangeAnimatorsAnimationId(child);
						}
					}
				});
			}
		}

		private void DuplicateAnimation()
		{
			var index = GetSelectedAnimationIndex();
			var sourceAnimation = GetAnimations()[index];
			var owner = sourceAnimation.Owner;

			Document.Current.History.DoTransaction(() => {
				var animation = Cloner.Clone(sourceAnimation);
				animation.Id = GenerateAnimationId(sourceAnimation.Id + "Copy");
				foreach (var track in animation.Tracks) {
					foreach (var animator in track.Animators) {
						animator.AnimationId = animation.Id;
					}
				}
				Core.Operations.InsertIntoList.Perform(owner.Animations, owner.Animations.Count, animation);
				SelectAnimation(GetAnimations().IndexOf(animation));
				DuplicateAnimators(animation.OwnerNode);

				void DuplicateAnimators(Node node)
				{
					foreach (var child in node.Nodes) {
						foreach (var animator in child.Animators.ToList()) {
							if (animator.AnimationId == sourceAnimation.Id) {
								var clone = Cloner.Clone(animator);
								clone.AnimationId = animation.Id;
								Core.Operations.AddIntoCollection<AnimatorCollection, IAnimator>.Perform(child.Animators, clone);
							}
						}
						if (child.ContentsPath != null) {
							continue;
						}
						if (child.Animations.Any(i => i.Id == sourceAnimation.Id)) {
							continue;
						}
						DuplicateAnimators(child);
					}
				}
			});
			// Schedule animation rename on the next update, since the widgets are not built yet
			panelWidget.Tasks.Add(DelayedRenameAnimation());

			IEnumerator<object> DelayedRenameAnimation()
			{
				yield return null;
				RenameAnimation();
			}
		}

		private string GenerateAnimationId(string prefix)
		{
			for (int i = 1; ; i++) {
				var id = prefix + (i > 1 ? i.ToString() : "");
				if (!GetAnimations().Any(a => a.Id == id)) {
					return id;
				}
			}
			throw new System.Exception();
		}

		private void SelectAnimationBasedOnMousePosition()
		{
			scrollView.SetFocus();
			var index = (scrollView.Content.LocalMousePosition().Y / rowHeight).Floor();
			if (index < GetAnimations().Count) {
				SelectAnimation(index);
			}
		}

		private void ShowContextMenu()
		{
			SelectAnimationBasedOnMousePosition();
			var menu = new Menu();
			var rootNode = Document.Current.RootNode;
			menu.Add(new Command("Add", () => AddAnimation(rootNode, false)));
			menu.Add(new Command("Add Compound", () => AddAnimation(rootNode, true)));
			menu.Add(new Command("Add ZeroPose", () => AddZeroPoseAnimation(rootNode)) {
				Enabled = !rootNode.Animations.TryFind(Animation.ZeroPoseId, out _)
			});
			var path = GetNodePath(Document.Current.Container);
			if (!string.IsNullOrEmpty(path)) {
				var container = Document.Current.Container;
				menu.Add(new Command($"Add To '{path}'", () => AddAnimation(container, false)));
				menu.Add(new Command($"Add Compound To '{path}'", () => AddAnimation(container, true)));
				menu.Add(new Command($"Add ZeroPose To '{path}'", () => AddZeroPoseAnimation(container)) {
					Enabled = !container.Animations.TryFind(Animation.ZeroPoseId, out _)
				});
			}
			menu.Add(Command.MenuSeparator);
			menu.Add(new Command("Rename", RenameAnimation));
			menu.Add(new Command("Duplicate", DuplicateAnimation));
			menu.Add(Command.Delete);
			menu.Popup();

			void AddAnimation(Node node, bool compound)
			{
				Document.Current.History.DoTransaction(() => {
					var animation = new Animation { Id = GenerateAnimationId("NewAnimation"), IsCompound = compound };
					InsertIntoList.Perform(node.Animations, node.Animations.Count, animation);
					SelectAnimation(GetAnimations().IndexOf(animation));
					if (compound) {
						var track = new AnimationTrack { Id = "Track1" };
						var item = LinkSceneItem.Perform(Document.Current.GetSceneItemForObject(animation), 0, track);
						SelectRow.Perform(item);
					}
				});
				// Schedule animation rename on the next update, since the widgets are not built yet
				panelWidget.Tasks.Add(DelayedRenameAnimation());
			}

			void AddZeroPoseAnimation(Node node)
			{
				Document.Current.History.DoTransaction(() => {
					var animation = new Animation { Id = Animation.ZeroPoseId };
					InsertIntoList.Perform(node.Animations, node.Animations.Count, animation);
					foreach (var a in node.Descendants.SelectMany(n => n.Animators).ToList()) {
						var (propertyData, animable, index) =
							AnimationUtils.GetPropertyByPath(a.Owner, a.TargetPropertyPath);
						var zeroPoseKey = Keyframe.CreateForType(propertyData.Info.PropertyType);
						zeroPoseKey.Value = index == -1
							? propertyData.Info.GetValue(animable)
							: propertyData.Info.GetValue(animable, new object[]{index});
						zeroPoseKey.Function = KeyFunction.Steep;
						SetKeyframe.Perform(a.Owner, a.TargetPropertyPath, Animation.ZeroPoseId, zeroPoseKey);
					}
					SelectAnimation(GetAnimations().IndexOf(animation));
				});
			}

			IEnumerator<object> DelayedRenameAnimation()
			{
				yield return null;
				RenameAnimation();
			}
		}

		private void Delete()
		{
			Document.Current.History.DoTransaction(() => {
				var index = GetSelectedAnimationIndex();
				if (index > 0) {
					SelectAnimation(index - 1);
				} else if (index + 1 < GetAnimations().Count) {
					SelectAnimation(index + 1);
				}
				var animation = GetAnimations()[index];
				DeleteAnimators(animation.OwnerNode);
				Core.Operations.RemoveFromList.Perform(animation.Owner.Animations, animation.Owner.Animations.IndexOf(animation));

				void DeleteAnimators(Node node)
				{
					foreach (var child in node.Nodes) {
						foreach (var animator in child.Animators.ToList()) {
							if (animator.AnimationId == animation.Id) {
								Core.Operations.RemoveFromCollection<AnimatorCollection, IAnimator>.Perform(child.Animators, animator);
							}
						}
						if (child.ContentsPath != null) {
							continue;
						}
						if (child.Animations.Any(i => i.Id == animation.Id)) {
							continue;
						}
						DeleteAnimators(child);
					}
				}
			});
		}

		private static string GetNodePath(Node node)
		{
			var t = "";
			for (var n = node; n != Document.Current.RootNode; n = n.Parent) {
				var id = string.IsNullOrEmpty(n.Id) ? "?" : n.Id;
				t = id + ((t != "") ? ": " + t : t);
			}
			return t;
		}

		private int GetSelectedAnimationIndex() => GetAnimations().IndexOf(Document.Current.Animation);

		private List<Animation> animationsStorage = new List<Animation>();
		private List<Animation> GetAnimations()
		{
			animationsStorage.Clear();
			Document.Current.GetAnimations(animationsStorage);
			return animationsStorage;
		}

		private long CalcAnimationsHashCode()
		{
			var h = new Hasher();
			h.Begin();
			foreach (var a in GetAnimations()) {
				h.Write(a.Id ?? string.Empty);
				h.Write(a.Owner.GetHashCode());
				h.Write(a.IsCompound);
				h.Write(a.IsLegacy);
			}
			return h.End();
		}

		IEnumerator<object> ProcessCommandsTask()
		{
			while (true) {
				yield return null;
				if (!scrollView.IsFocused()) {
					continue;
				}
				if (Commands.Down.Consume()) {
					SelectAnimation(GetSelectedAnimationIndex() + 1);
				}
				if (Commands.Up.Consume()) {
					scrollView.SetFocus();
					SelectAnimation(GetSelectedAnimationIndex() - 1);
				}
				if (!Commands.Delete.IsConsumed()) {
					Command.Delete.Enabled = !Document.Current.Animation.IsLegacy;
					if (Commands.Delete.Consume()) {
						Delete();
					}
				}
			}
		}

		private void SelectAnimation(int index)
		{
			var a = GetAnimations();
			index = index.Clamp(0, a.Count - 1);
			EnsureRowVisible(index);
			Window.Current.Invalidate();
			var document = Document.Current;
			document.History.DoTransaction(() => {
				SetProperty.Perform(document, nameof(Document.SelectedAnimation), a[index], isChangingDocument: false);
			});
		}

		private void EnsureRowVisible(int row)
		{
			while ((row + 1) * rowHeight > scrollView.ScrollPosition + scrollView.Height) {
				scrollView.ScrollPosition++;
			}
			while (row * rowHeight < scrollView.ScrollPosition) {
				scrollView.ScrollPosition--;
			}
		}

		private void Refresh()
		{
			int index = GetSelectedAnimationIndex();
			var content = scrollView.Content;
			content.Nodes.Clear();
			var animations = GetAnimations();
			content.Layout = new TableLayout {
				ColumnCount = 1,
				ColumnSpacing = 8,
				RowCount = animations.Count,
				ColumnDefaults = new List<DefaultLayoutCell> { new DefaultLayoutCell { StretchY = 0 } }
			};
			foreach (var a in animations) {
				var label = a.IsLegacy ? legacyAnimationId : a.Id;
				if (a.IsCompound) {
					label += " [Compound]";
				}
				var path = GetNodePath(a.OwnerNode);
				if (!a.IsLegacy && !string.IsNullOrEmpty(path)) {
					label += " (" + path + ')';
				}
				var item = new Widget {
					MinHeight = rowHeight,
					Padding = new Thickness(2, 10, 0, 0),
					Layout = new HBoxLayout(),
					Nodes = {
						new ThemedSimpleText(label) {
							Id = "Label", LayoutCell = new LayoutCell { VerticalAlignment = VAlignment.Center }
						},
					}
				};
				content.Nodes.Add(item);
			}
			SelectAnimation(index);
		}

		public void Attach()
		{
			panelWidget.PushNode(rootWidget);
			Refresh();
		}

		public void Detach()
		{
			rootWidget.Unlink();
		}
	}
}
