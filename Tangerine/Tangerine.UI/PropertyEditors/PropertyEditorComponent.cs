namespace Tangerine.UI
{
	public class PropertyEditorComponent : Lime.NodeComponent
	{
		public IPropertyEditor PropertyEditor { get; set; }

		public PropertyEditorComponent(IPropertyEditor propertyEditor)
		{
			PropertyEditor = propertyEditor;
		}
	}
}
