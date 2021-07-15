using Lime;
using System;
using System.Collections.Generic;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI
{
	public class ConflictingAnimatorsItem : Frame
	{
		protected const float IconSize = 16;
		protected const float IconRightPadding = 5;

		protected readonly ConflictingAnimatorsInfo info;

		public ConflictingAnimatorsItem(ConflictingAnimatorsInfo info)
		{
			this.info = info;

			Layout = new VBoxLayout { Spacing = 4 };
			foreach (var widget in CreateContent()) {
				AddNode(widget);
			}

			HitTestTarget = true;
			Gestures.Add(new DoubleClickGesture(() => {
				try {
					Project.Current.OpenDocument(info.DocumentPath);
					NavigateToNode.Perform(info.Node, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
				} catch (System.Exception e) {
					Logger.Write(e.Message);
				}
			}));

			CompoundPresenter.Add(new ItemPresenter(this));
			Tasks.Add(Theme.MouseHoverInvalidationTask(this));
		}

		protected IEnumerable<Widget> CreateContent()
		{
			// Display affected scene.
			if (!string.IsNullOrEmpty(info.DocumentPath)) {
				var sceneTexture = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;
				yield return new Widget {
					Layout = new HBoxLayout(),
					Padding = new Thickness(left: 5),
					Nodes = {
						new Image {
							LayoutCell = new LayoutCell {
								Stretch = Vector2.Zero,
								Alignment = new Alignment { X = HAlignment.Center, Y = VAlignment.Center }
							},
							Padding = new Thickness(right: IconRightPadding),
							MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
							Texture = sceneTexture,
						},
						new ThemedSimpleText {
							Text = info.DocumentPath,
						},
					},
				};
			}

			// Display affected widget.
			var iconTexture = NodeIconPool.GetTexture(info.Node);
			yield return new Widget {
				Layout = new HBoxLayout(),
				Padding = new Thickness(left: 5),
				Nodes = {
					new Image {
						LayoutCell = new LayoutCell {
							Stretch = Vector2.Zero,
							Alignment = new Alignment { X = HAlignment.Center, Y = VAlignment.Center }
						},
						Padding = new Thickness(right: IconRightPadding),
						MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
						Texture = iconTexture,
					},
					new ThemedSimpleText {
						Text = info.Node.ToString(),
					},
				},
			};

			// Display affected properties and corresponding animations.
			var conflicts = new Widget {
				Layout = new VBoxLayout { Spacing = 2 },
				Padding = new Thickness(left: 5),
			};
			for (var i = 0; i < info.AffectedProperties.Length; ++i) {
				conflicts.AddNode(new Widget {
					Layout = new HBoxLayout(),
					Padding = new Thickness(left: 5),
					Nodes = {
						Spacer.HSpacer(IconSize + IconRightPadding),
						new ThemedSimpleText {
							Text = $"{info.AffectedProperties[i]}: {string.Join(", ", info.ConcurrentAnimations[i])}",
						},
					},
				});
			}
			yield return conflicts;
		}

		private class ItemPresenter : IPresenter
		{
			private readonly ConflictingAnimatorsItem item;

			public ItemPresenter(ConflictingAnimatorsItem item) => this.item = item;

			public Lime.RenderObject GetRenderObject(Node node)
			{
				var widget = (Widget)node;
				if (widget.GloballyEnabled) {
					var isMouseOverThisOrDescendant = widget.IsMouseOverThisOrDescendant();
					if (isMouseOverThisOrDescendant) {
						var ro = RenderObjectPool<RenderObject>.Acquire();
						ro.CaptureRenderState(widget);
						ro.Size = widget.Size;
						ro.SelectedColor = Theme.Colors.WhiteBackground;
						ro.HoveredColor = Theme.Colors.SelectedBorder;
						ro.IsSelected = false;
						ro.IsHovered = isMouseOverThisOrDescendant;
						return ro;
					}
				}
				return null;
			}

			public bool PartialHitTest(Node node, ref HitTestArgs args) => false;

			private class RenderObject : WidgetRenderObject
			{
				public bool IsSelected;
				public bool IsHovered;
				public Vector2 Size;
				public Color4 SelectedColor;
				public Color4 HoveredColor;

				public override void Render()
				{
					var offset = new Vector2(3.0f);
					PrepareRenderState();
					if (IsSelected) {
						Renderer.DrawRect(-offset, Size + offset, SelectedColor);
					}
					if (IsHovered) {
						Renderer.DrawRectOutline(-offset, Size + offset, HoveredColor);
					}
				}
			}
		}
	}

	public class ConflictingAnimatorsInfo : IEquatable<ConflictingAnimatorsInfo>
	{
		public readonly Node Node;
		public readonly string DocumentPath;
		public readonly string[] AffectedProperties;
		public readonly HashSet<string>[] ConcurrentAnimations;

		public ConflictingAnimatorsInfo(Node node, string documentPath, string[] affectedProperties, HashSet<string>[] concurrentAnimations)
		{
			Node = node;
			DocumentPath = documentPath;
			AffectedProperties = affectedProperties;
			ConcurrentAnimations = concurrentAnimations;
		}

		public bool Equals(ConflictingAnimatorsInfo other) =>
			ReferenceEquals(this, other) ||
			DocumentPath == other.DocumentPath &&
			Node.GetRelativePath() == other.Node.GetRelativePath();

		public override bool Equals(object obj) => Equals(obj as ConflictingAnimatorsInfo);
		public override int GetHashCode() => HashCode.Combine(Node, DocumentPath, AffectedProperties, ConcurrentAnimations);
	}
}
