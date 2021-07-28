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
		private const string AnimationLinkStyleTag = "al";

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
				var conflict = new Widget {
					Layout = new HBoxLayout { Spacing = 2 },
					Padding = new Thickness(2),
					Nodes = {
						Spacer.HSpacer(2 * IconSize + IconRightPadding + 4 + Theme.Metrics.CloseButtonSize.X),
					},
				};
				var label = new RichText {
					Text = $"Target Property: <{PropertyColorStyleTag}>{Info.AffectedProperties[i]}</{PropertyColorStyleTag}>; " +
						   $"Potential Conflicts: ",
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
							Id = PropertyColorStyleTag,
							Size = Theme.Metrics.TextHeight,
							TextColor = propertyColor,
							Font = new SerializableFont(FontPool.DefaultBoldFontName),
						}
					},
				};
				label.Width = 1024.0f;
				label.MinMaxWidth = label.Width = label.MeasureText().Width;
				conflict.AddNode(label);
				var conflictingAnimationsLinks = Info.ConcurrentAnimations[i].Select(j => CreateAnimationLink(j, Info.AffectedProperties[i])).ToList();
				for (var j = 0; j < conflictingAnimationsLinks.Count; ++j) {
					var link = conflictingAnimationsLinks[j];
					conflict.AddNode(link);
					if (j! != conflictingAnimationsLinks.Count - 1) {
						var sep = new RichText {
							Text = ",",
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
							}
						};
						sep.Width = 1024.0f;
						sep.MinMaxWidth = sep.Width = sep.MeasureText().Width + 1;
						conflict.AddNode(sep);
					}
				}
				conflicts.AddNode(conflict);
			}
			yield return conflicts;
		}

		private RichText CreateAnimationLink(string text, string property)
		{
			var label = new RichText {
				Text = text,
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				Padding = new Thickness(left: 5.0f),
				MinHeight = Theme.Metrics.TextHeight,
				Localizable = false,
				Color = Color4.White,
				HAlignment = HAlignment.Left,
				VAlignment = VAlignment.Center,
				OverflowMode = TextOverflowMode.Ellipsis,
				TrimWhitespaces = true,
				HitTestTarget = true,
				Nodes = {
					new TextStyle {
						Id = BoldStyleTag,
						Size = Theme.Metrics.TextHeight,
						TextColor = Theme.Colors.BlackText,
						Font = new SerializableFont(FontPool.DefaultBoldFontName),
					},
					new TextStyle {
						Id = AnimationLinkStyleTag,
						Size = Theme.Metrics.TextHeight,
						TextColor = Theme.Colors.GrayText,
						Font = new SerializableFont(FontPool.DefaultBoldFontName),
					},
				},
			};
			label.Width = 1024.0f;
			label.MinMaxWidth = label.Width = label.MeasureText().Width;
			label.Clicked += () => {
				if (label.IsMouseOver()) {
					try {
						Project.Current.OpenDocument(Info.DocumentPath);
						if (cachedNode == null) {
							cachedNode = Document.Current.RootNode.FindNode(Info.RelativePath);
							if (Info.NodeIndexForPathCollisions.HasValue) {
								cachedNode = cachedNode.Parent.Nodes[Info.NodeIndexForPathCollisions.Value];
							}
						}
						NavigateToNode.Perform(cachedNode, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
						var animationId = text == "Legacy" ? null : text;
						foreach (var a in cachedNode.Ancestors) {
							if (a.Animations.TryFind(animationId, out var animation)) {
								NavigateToAnimation.Perform(animation);
								break;
							}
						}
						var sceneItem = Document.Current.GetSceneItemForObject(cachedNode);
						var timelineItemState = sceneItem.GetTimelineItemState();
						var row = sceneItem.Rows
							.Where(i => i.Id == property)
							.First(i => i.Components.Get<Core.Components.CommonPropertyRowData>().Animator.AnimationId == animationId);
						using (Document.Current.History.BeginTransaction()) {
							timelineItemState.ShowAnimators = true;
							timelineItemState.Expanded = true;
							SelectRow.Perform(row);
							Document.Current.History.CommitTransaction();
						}
					} catch (System.Exception e) {
						new AlertDialog($"Couldn't navigate to node {Info.RelativePath} in {Info.DocumentPath}\n\n{e.Message}", "Ok").Show();
						Logger.Write(e.Message);
					};
				}
			};
			label.Tasks.Add(Theme.MouseHoverInvalidationTask(this));
			label.Tasks.Add(MouseHoverTask(label));
			return label;
		}

		private IEnumerator<object> MouseHoverTask(RichText label)
		{
			while (true) {
				if (label.IsMouseOver()) {
					var style = label.FindNode(AnimationLinkStyleTag);
					style.Unlink();
					style.PushToNode(label);
					while (label.IsMouseOver()) {
						WidgetContext.Current.MouseCursor = MouseCursor.Hand;
						yield return null;
					}
					style = label.FindNode(BoldStyleTag);
					style.Unlink();
					style.PushToNode(label);
				}
				yield return null;
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
