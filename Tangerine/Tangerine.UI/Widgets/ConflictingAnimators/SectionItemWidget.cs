using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class ConflictInfo : IEquatable<ConflictInfo>
	{
		public readonly Type NodeType;
		public readonly string RelativePath;
		public readonly string DocumentPath;
		public readonly string[] AffectedProperties;
		public readonly SortedSet<string>[] ConcurrentAnimations;
		public readonly int[] PropertyKeyframeColorIndices;
		public readonly int? NodeIndex;

		public ConflictInfo(
			Type nodeType,
			string relativePath,
			string documentPath,
			string[] affectedProperties,
			SortedSet<string>[] concurrentAnimations,
			int[] propertyKeyframeColorIndices,
			int? nodeIndex
		)
		{
			NodeType = nodeType;
			RelativePath = relativePath;
			DocumentPath = documentPath;
			AffectedProperties = affectedProperties;
			PropertyKeyframeColorIndices = propertyKeyframeColorIndices;
			ConcurrentAnimations = concurrentAnimations;
			NodeIndex = nodeIndex;
		}

		public bool Equals(ConflictInfo other) =>
			ReferenceEquals(this, other) ||
			RelativePath == other.RelativePath &&
			DocumentPath == other.DocumentPath &&
			NodeIndex == other.NodeIndex;

		public override bool Equals(object obj) => Equals(obj as ConflictInfo);

		public override int GetHashCode() =>
			HashCode.Combine(RelativePath, DocumentPath, AffectedProperties, ConcurrentAnimations);
	}

	public class SectionItemWidget : Widget
	{
		private Node cache;
		private Node Cache
		{
			get
			{
				if (cache == null) {
					// TODO:
					// Assert that both document and node exist.
					// Otherwise, mark item for deletion and notify its owner.
					cache = Document.Current.RootNode.FindNode(Info.RelativePath);
					if (Info.NodeIndex.HasValue) {
						cache = cache.Parent.Nodes[Info.NodeIndex.Value];
					}
				}

				return cache;
			}
		}

		// TODO:
		// Consider implementing specialized widget for storing info.
		// Get rid of having explicit ConflictInfo, reduce it to proxy data class as it naturally is.
		public readonly ConflictInfo Info;
		public readonly DescriptionWidget Description;
		public readonly Frame Conflicts;

		private Spacer Margin => Spacer.HSpacer(
			DescriptionWidget.IconSize +
			DescriptionWidget.IconRightPadding
		);

		public SectionItemWidget(ConflictInfo info)
		{
			Info = info;
			Layout = new VBoxLayout { Spacing = 4 };

			AddNode(Description = CreateDescription());
			AddNode(Conflicts = ParseConflicts());
		}

		private DescriptionWidget CreateDescription()
		{
			var iconTexture = NodeIconPool.GetTexture(Info.NodeType);
			var text = string.Join(
				" in ",
				Info.RelativePath
					.Split('/')
					.Reverse()
					.Select(i => $"'{i}'")
				);

			return new DescriptionWidget(text, iconTexture);
		}

		private Frame ParseConflicts()
		{
			var conflicts = new Frame {
				Layout = new VBoxLayout { Spacing = 2 },
				Padding = new Thickness(left: 5),
			};

			for (var i = 0; i < Info.AffectedProperties.Length; ++i) {
				var property = Info.AffectedProperties[i];
				var propertyColor = KeyframePalette.Colors[Info.PropertyKeyframeColorIndices[i]];
				var animations = Info.ConcurrentAnimations[i].ToList();
				conflicts.AddNode(ParseConflict(property, propertyColor, animations));
			}

			return conflicts;
		}

		private Widget ParseConflict(string property, Color4 propertyColor, List<string> animations)
		{
			var conflict = new Widget {
				Layout = new HBoxLayout { Spacing = 2 },
				Padding = new Thickness(2),
				Nodes = { Margin },
			};

			var caption = new ThemedCaption($"Target Property: {WrapProperty(property)}; Potential Conflicts: ");
			caption.PropertyStyle.TextColor = propertyColor;
			conflict.AddNode(caption);

			var links = animations.Select(i => CreateNavigationLink(property, i)).ToList();
			for (var i = 1; i < links.Count; i += 2) {
				links.Insert(i, new ThemedCaption(",", extraWidth: 1.0f));
			}
			foreach (var link in links) {
				conflict.AddNode(link);
			}

			return conflict;
		}

		private ThemedCaption CreateNavigationLink(string property, string animation)
		{
			var component = new NavigationComponent(new NavigationInfo {
				RetrieveNode = () => Cache,
				DocumentPath = Info.DocumentPath,
				AnimationId = animation == "Legacy" ? null : animation,
				TargetProperty = property,
			});
			var caption = new ThemedCaption(animation);
			caption.Components.Add(component);
			return caption;
		}

		private static string WrapProperty(string text) =>
			$"<{TextStyleIdentifiers.PropertyColor}>{text}</{TextStyleIdentifiers.PropertyColor}>";
	}
}
