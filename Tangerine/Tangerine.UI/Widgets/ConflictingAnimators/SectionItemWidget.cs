using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class SectionItemWidget : Widget
	{
		private readonly ConflictInfo conflict;
		
		private Node cache;
		
		private Node Cache
		{
			get
			{
				if (cache == null) {
					Project.Current.OpenDocument(conflict.DocumentPath);
					cache = Document.Current.RootNode;
					var segments = conflict.RelativeIndexedPath;
					for (int i = segments.Count - 1; i >= 0; --i) {
						cache = cache.Nodes[segments[i]];
					}
				}
				return cache;
			}
		}

		private Spacer Margin => Spacer.HSpacer(
			DescriptionWidget.IconSize +
			DescriptionWidget.IconRightPadding
		);

		public SectionItemWidget(ConflictInfo conflict)
		{
			this.conflict = conflict;
			Layout = new VBoxLayout { Spacing = 4 };

			AddNode(CreateDescriptionWidget());
			AddNode(ParseConflicts());
		}

		private DescriptionWidget CreateDescriptionWidget()
		{
			var iconTexture = NodeIconPool.GetTexture(conflict.NodeType);
			var text = string.Join(
				" in ",
				conflict.RelativeTextPath
					.Split('/')
					.Reverse()
					.Select(i => $"'{i}'")
			);
			return new DescriptionWidget(text, iconTexture);
		}

		private Widget ParseConflicts()
		{
			var conflictsWidget = new Widget {
				Layout = new VBoxLayout { Spacing = 0 },
				Padding = new Thickness(left: 5),
			};
			for (var i = 0; i < conflict.AffectedProperties.Length; ++i) {
				var property = conflict.AffectedProperties[i];
				var propertyColor = KeyframePalette.Colors[property.KeyframeColorIndex];
				var animations = conflict.ConcurrentAnimations[i].ToList();
				conflictsWidget.AddNode(ParseConflict(property.Path, propertyColor, animations));
			}
			return conflictsWidget;
		}

		private Widget ParseConflict(string property, Color4 propertyColor, List<string> animations)
		{
			var conflictWidget = new Widget {
				Layout = new HBoxLayout { Spacing = 2 },
				Padding = new Thickness(horizontal: 2, vertical: 0),
				Nodes = { Margin },
			};
			var caption = new ThemedCaption($"Property: {StylizeProperty(property)}; Potential Conflicts: ");
			caption.PropertyStyle.TextColor = propertyColor;
			conflictWidget.AddNode(caption);
			var links = animations.Select(i => CreateNavigationLink(property, i)).ToList();
			for (var i = 1; i < links.Count; i += 2) {
				links.Insert(i, new ThemedCaption(",", extraWidth: 1.0f));
			}
			foreach (var link in links) {
				conflictWidget.AddNode(link);
			}

			return conflictWidget;
		}

		private ThemedCaption CreateNavigationLink(string property, string animation)
		{
			var component = new NavigationComponent(new NavigationInfo {
				RetrieveNode = () => Cache,
				DocumentPath = conflict.DocumentPath,
				AnimationId = animation == "Legacy" ? null : animation,
				TargetProperty = property,
			});
			var caption = new ThemedCaption(animation);
			caption.Components.Add(component);
			return caption;
		}

		private static string StylizeProperty(string text) => ThemedCaption.Stylize(text, TextStyleIdentifiers.PropertyColor);
	}
}
