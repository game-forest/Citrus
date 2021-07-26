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
		private const string BoldStyleTag = "b";
		private const string PropertyColorStyleTag = "pc";

		private Node cachedNode;

		protected const float IconSize = 16;
		protected const float IconRightPadding = 5;

		public readonly ThemedScrollView Container;
		public readonly ConflictingAnimatorsInfo Info;

		public ConflictingAnimatorsItem(ConflictingAnimatorsInfo info, ThemedScrollView container)
		{
			Info = info;
			Container = container;

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
						if (info.NodeIndexForPathCollisions.HasValue) {
							cachedNode = cachedNode.Parent.Nodes[info.NodeIndexForPathCollisions.Value];
						}
					}
					NavigateToNode.Perform(cachedNode, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
					using (Document.Current.History.BeginTransaction()) {
						SelectNode.Perform(cachedNode);
						Document.Current.History.CommitTransaction();
					}
				} catch (System.Exception e) {
					new AlertDialog($"Couldn't navigate to node {info.RelativePath} in {info.DocumentPath}", "Ok").Show();
					Logger.Write(e.Message);
				}
			}));

			CompoundPresenter.Add(new ItemPresenter(this));
			Tasks.Add(Theme.MouseHoverInvalidationTask(this));
		}

		protected IEnumerable<Widget> CreateContent()
		{
			// Display affected widget.
			var iconTexture = NodeIconPool.GetTexture(Info.NodeType);
			var relativePathVerbose = string.Join(" in ", Info.RelativePath.Split('/').Reverse().Select(i => $"'{i}'"));
			yield return new Widget {
				Layout = new HBoxLayout { Spacing = 2 },
				Padding = new Thickness(2),
				Nodes = {
					Spacer.HSpacer(IconSize + IconRightPadding + Theme.Metrics.CloseButtonSize.X),
					new Image {
						LayoutCell = new LayoutCell {
							Stretch = Vector2.Zero,
							Alignment = new Alignment { X = HAlignment.Center, Y = VAlignment.Center }
						},
						Padding = new Thickness(right: IconRightPadding),
						MinMaxSize = new Vector2(IconSize + IconRightPadding, IconSize),
						Texture = iconTexture,
					},
					new RichText {
						Text = relativePathVerbose,
						Padding = new Thickness(left: 5.0f),
						MinHeight = Theme.Metrics.TextHeight,
						Localizable = false,
						Color = Color4.White,
						HAlignment = HAlignment.Left,
						VAlignment = VAlignment.Center,
						OverflowMode = TextOverflowMode.Ellipsis,
						TrimWhitespaces = true,
						Nodes = {
							new TextStyle {
								Size = Theme.Metrics.TextHeight,
								TextColor = Theme.Colors.GrayText,
							}
						},
					},
				},
			};

			// Display affected properties and corresponding animations.
			var conflicts = new Widget {
				Layout = new VBoxLayout { Spacing = 2 },
				Padding = new Thickness(left: 5),
			};
			for (var i = 0; i < Info.AffectedProperties.Length; ++i) {
				var propertyColor = KeyframePalette.Colors[Info.PropertyKeyframeColorIndices[i]];
				conflicts.AddNode(new Widget {
					Layout = new HBoxLayout { Spacing = 2 },
					Padding = new Thickness(2),
					Nodes = {
						Spacer.HSpacer(2 * IconSize + IconRightPadding + 4 + Theme.Metrics.CloseButtonSize.X),
						new RichText {
							Text = $"Target Property: <{PropertyColorStyleTag}>{Info.AffectedProperties[i]}</{PropertyColorStyleTag}>; " +
							       $"Potential Conflicts: {string.Join(", ", Info.ConcurrentAnimations[i].Select(j => $"<{BoldStyleTag}>{j}</{BoldStyleTag}>"))}",
							Padding = new Thickness(left: 5.0f),
							MinHeight = Theme.Metrics.TextHeight,
							Localizable = false,
							Color = Color4.White,
							HAlignment = HAlignment.Left,
							VAlignment = VAlignment.Center,
							OverflowMode = TextOverflowMode.Ellipsis,
							TrimWhitespaces = true,
							Nodes = {
								new TextStyle {
									Size = Theme.Metrics.TextHeight,
									TextColor = Theme.Colors.GrayText,
								},
								new TextStyle {
									Id = BoldStyleTag,
									Size = Theme.Metrics.TextHeight,
									TextColor = Theme.Colors.BlackText,
									Font = new SerializableFont(FontPool.DefaultBoldFontName),
								},
								new TextStyle {
									Id = PropertyColorStyleTag,
									Size = Theme.Metrics.TextHeight,
									TextColor = propertyColor,
									Font = new SerializableFont(FontPool.DefaultBoldFontName),
								}
							},
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
				var scrollView = (ScrollViewWithSlider)item.Container.Behaviour;

				if (widget.GloballyEnabled) {
					var isMouseOverThisOrDescendant = widget.IsMouseOverThisOrDescendant();
					if (isMouseOverThisOrDescendant) {
						var margin = new Vector2(3.0f);
						var ro = RenderObjectPool<RenderObject>.Acquire();
						ro.CaptureRenderState(widget);
						ro.A = -margin;
						ro.B = new Vector2(widget.Width - scrollView.SliderVisibleWidth, widget.Height) + margin;
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
				public Vector2 A;
				public Vector2 B;
				public bool IsSelected;
				public bool IsHovered;
				public Color4 SelectedColor;
				public Color4 HoveredColor;

				public override void Render()
				{
					PrepareRenderState();
					if (IsSelected) {
						Renderer.DrawRect(A, B, SelectedColor);
					}
					if (IsHovered) {
						Renderer.DrawRectOutline(A, B, HoveredColor);
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
		public readonly int[] PropertyKeyframeColorIndices;
		public readonly int? NodeIndexForPathCollisions;

		public ConflictingAnimatorsInfo(
			Type nodeType,
			string relativePath,
			string documentPath,
			string[] affectedProperties,
			SortedSet<string>[] concurrentAnimations,
			int[] propertyKeyframeColorIndices,
			int? nodeIndexForPathCollisions
		) {
			NodeType = nodeType;
			RelativePath = relativePath;
			DocumentPath = documentPath;
			AffectedProperties = affectedProperties;
			PropertyKeyframeColorIndices = propertyKeyframeColorIndices;
			ConcurrentAnimations = concurrentAnimations;
			NodeIndexForPathCollisions = nodeIndexForPathCollisions;
		}

		public bool Equals(ConflictingAnimatorsInfo other) =>
			ReferenceEquals(this, other) ||
			RelativePath == other.RelativePath &&
			DocumentPath == other.DocumentPath &&
			NodeIndexForPathCollisions == other.NodeIndexForPathCollisions;

		public override bool Equals(object obj) => Equals(obj as ConflictingAnimatorsInfo);
		public override int GetHashCode() => HashCode.Combine(RelativePath, DocumentPath, AffectedProperties, ConcurrentAnimations);
	}
}
