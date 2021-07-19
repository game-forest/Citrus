using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine.UI
{
	public class ConflictingAnimatorsItem : Frame
	{
		private Node cachedNode;

		protected const float IconSize = 16;
		protected const float IconRightPadding = 5;

		public readonly ConflictingAnimatorsInfo Info;

		public ConflictingAnimatorsItem(ConflictingAnimatorsInfo info)
		{
			Info = info;

			Layout = new VBoxLayout { Spacing = 4 };
			foreach (var widget in CreateContent()) {
				AddNode(widget);
			}

			HitTestTarget = true;
			Gestures.Add(new DoubleClickGesture(() => {
				try {
					Project.Current.OpenDocument(info.DocumentPath);
					if (cachedNode == null) {
						cachedNode = Document.Current.RootNode.FindNode(info.RelativePath);
						if (info.IndexForPathCollisions.HasValue) {
							cachedNode = cachedNode.Parent.Nodes[info.IndexForPathCollisions.Value];
						}
					}
					NavigateToNode.Perform(cachedNode, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
				} catch (System.Exception e) {
					new AlertDialog($"Couldn't navigate to node {info.RelativePath} in {info.DocumentPath}");
					Logger.Write(e.Message);
				}
			}));

			CompoundPresenter.Add(new ItemPresenter(this));
			Tasks.Add(Theme.MouseHoverInvalidationTask(this));
		}

		protected IEnumerable<Widget> CreateContent()
		{
			// Display affected scene.
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
						Text = Info.DocumentPath,
					},
				},
			};

			// Display affected widget.
			var iconTexture = NodeIconPool.GetTexture(Info.NodeType);
			var relativePathVerbose = string.Join(" in ", Info.RelativePath.Split('/').Reverse().Select(i => $"'{i}'"));
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
						Text = relativePathVerbose,
					},
				},
			};

			// Display affected properties and corresponding animations.
			var conflicts = new Widget {
				Layout = new VBoxLayout { Spacing = 2 },
				Padding = new Thickness(left: 5),
			};
			for (var i = 0; i < Info.AffectedProperties.Length; ++i) {
				conflicts.AddNode(new Widget {
					Layout = new HBoxLayout(),
					Padding = new Thickness(left: 5),
					Nodes = {
						Spacer.HSpacer(IconSize + IconRightPadding),
						new ThemedSimpleText {
							Text = $"Target Property: {Info.AffectedProperties[i]}; Potential Conflicts: {string.Join(", ", Info.ConcurrentAnimations[i])}",
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
		public readonly Type NodeType;
		public readonly string RelativePath;
		public readonly string DocumentPath;
		public readonly string[] AffectedProperties;
		public readonly SortedSet<string>[] ConcurrentAnimations;
		public readonly int? IndexForPathCollisions;

		public ConflictingAnimatorsInfo(
			Type nodeType,
			string relativePath,
			string documentPath,
			string[] affectedProperties,
			SortedSet<string>[] concurrentAnimations,
			int? indexForPathCollisions
		) {
			NodeType = nodeType;
			RelativePath = relativePath;
			DocumentPath = documentPath;
			AffectedProperties = affectedProperties;
			ConcurrentAnimations = concurrentAnimations;
			IndexForPathCollisions = indexForPathCollisions;
		}

		public bool Equals(ConflictingAnimatorsInfo other) =>
			ReferenceEquals(this, other) ||
			RelativePath == other.RelativePath &&
			DocumentPath == other.DocumentPath &&
			IndexForPathCollisions == other.IndexForPathCollisions;

		public override bool Equals(object obj) => Equals(obj as ConflictingAnimatorsInfo);
		public override int GetHashCode() => HashCode.Combine(RelativePath, DocumentPath, AffectedProperties, ConcurrentAnimations);
	}
}
