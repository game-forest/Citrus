using Lime;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class LinkIndicatorButton : ToolbarButton
	{
		private static readonly Vector2 defaultMinMaxSize = new Vector2(21, 16);
		private static readonly Thickness defaultPadding = new Thickness { Left = 5 };
		private readonly List<Node> linkedNodes = new List<Node>();

		public LinkIndicatorButton(ITexture texture)
		{
			Highlightable = false;
			MinMaxSize = defaultMinMaxSize;
			Padding = defaultPadding;
			LayoutCell = new LayoutCell(Alignment.RightCenter);
			Texture = texture;
			Clicked += () => {
				if (linkedNodes.Count > 0) {
					LinkIndicationContextMenu.Create(linkedNodes);
				}
			};
		}

		public void AddLinkedNode(Node node)
		{
			linkedNodes.Add(node);
		}

		private static class LinkIndicationContextMenu
        {
            public static void Create(List<Node> nodes)
            {
                var menu = new Menu();
                var isSameParent = true;
                var parent = nodes.FirstOrDefault()?.Parent;
                foreach (var node in nodes) {
                	menu.Add(new Command(node.Id, new ShowLinkedNodes(node).Execute));
                	isSameParent &= node.Parent == parent;
                }
                if (nodes.Count > 0) {
                	if (isSameParent && nodes.Count() > 1) {
                		menu.Add(Command.MenuSeparator);
                		menu.Add(new Command("Show All", new ShowLinkedNodes(nodes.ToArray()).Execute));
                	}
                	menu.Popup();
                }
            }

            private class ShowLinkedNodes : CommandHandler
            {
                private readonly Node[] nodes;

                public ShowLinkedNodes(params Node[] nodes)
                {
                	this.nodes = nodes;
                }

                public override void Execute()
                {
                	Document.Current.History.DoTransaction(() => {
                		var parent = nodes.First().Parent;
                		if (parent != Document.Current.Container) {
                			Core.Operations.EnterNode.Perform(parent, false);
                		}
                		Core.Operations.ClearSceneItemSelection.Perform();
                		foreach (var node in nodes) {
                			Core.Operations.SelectNode.Perform(node);
                		}
                	});
                }
            }
        }
	}

	public class LinkIndicatorButtonContainer
	{
		public readonly Widget Container = new Widget { Layout = new HBoxLayout() };

		public LinkIndicatorButtonContainer()
		{
			Container.Layout = new HBoxLayout();
		}

		public void Clear()
		{
			Container.Nodes.Clear();
		}

		public TLinkIndicatorButton GetOrAdd<TLinkIndicatorButton>() where TLinkIndicatorButton : LinkIndicatorButton, new()
		{
			foreach (var node in Container.Nodes) {
				if (node is TLinkIndicatorButton button) {
					return button;
				}
			}
			var newButton = new TLinkIndicatorButton();
			Container.Nodes.Add(newButton);
			return newButton;
		}
	}
}
