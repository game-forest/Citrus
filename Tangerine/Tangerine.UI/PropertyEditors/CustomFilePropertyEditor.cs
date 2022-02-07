using Lime;

namespace Tangerine.UI
{
	public sealed class CustomFilePropertyEditor<T> : FilePropertyEditor<T>
	{
		private TangerineFilePropertyAttribute filePropertyAttribute;
		protected override bool SaveFileExtension { get; set; }

		public CustomFilePropertyEditor(IPropertyEditorParams editorParams, TangerineFilePropertyAttribute attribute)
			: base(editorParams, attribute.AllowedFileTypes)
		{
			this.filePropertyAttribute = attribute;
			this.SaveFileExtension = filePropertyAttribute.SaveFileExtension;
		}

		protected override string ValueToStringConverter(T value)
		{
			return filePropertyAttribute?.ValueToStringConverter(EditorParams.Type, value)
				?? value?.ToString()
				?? string.Empty;
		}

		protected override T StringToValueConverter(string path)
		{
			return filePropertyAttribute != null
				? filePropertyAttribute.StringToValueConverter<T>(EditorParams.Type, path)
				: (T)(object)path;
		}

		protected override void AssignAsset(string path)
		{
			SetProperty(StringToValueConverter(path));
		}
	}
}
