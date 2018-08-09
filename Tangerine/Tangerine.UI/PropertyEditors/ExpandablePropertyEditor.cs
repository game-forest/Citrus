using Lime;

namespace Tangerine.UI
{
	// Used to unify generic descendants of ExpandableProperty for type checking
	public interface IExpandablePropertyEditor
	{ }

	public class ExpandablePropertyEditor<T> : CommonPropertyEditor<T>, IExpandablePropertyEditor
	{
		private bool expanded;
		public bool Expanded
		{
			get { return expanded; }
			set
			{
				expanded = value;
				ExpandButton.Expanded = value;
				ExpandableContent.Visible = value;
			}
		}
		public Widget ExpandableContent { get; }
		private ThemedExpandButton ExpandButton { get; }

		public ExpandablePropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			ExpandableContent = new ThemedFrame {
				Padding = new Thickness(4),
				Layout = new VBoxLayout(),
				Visible = false
			};
			ExpandButton = new ThemedExpandButton {
				Anchors = Anchors.Left,
				MinMaxSize = Vector2.One * 20f,
			};
			ExpandButton.Clicked += () => Expanded = !Expanded;
			editorParams.InspectorPane.AddNode(ExpandableContent);
			ContainerWidget.Nodes.Insert(0, ExpandButton);
		}
	}
}