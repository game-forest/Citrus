using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class ContentsPathPropertyEditor : FilePropertyEditor<string>
	{
		public ContentsPathPropertyEditor(IPropertyEditorParams editorParams)
			: base(editorParams, Document.AllowedFileTypes)
		{ }

		protected override bool IsValid(string path)
		{
			if (string.IsNullOrEmpty(path)) {
				return true;
			}
			if (base.IsValid(path)) {
				var resolvedPath = Node.ResolveScenePath(path);
				if (resolvedPath == null || !AssetBundle.Current.FileExists(resolvedPath)) {
					return false;
				}
				return Utils.ExtractAssetPathOrShowAlert(path, out string assetPath, out string assetType)
					&& Utils.AssertCurrentDocument(assetPath, assetType);
			}
			return false;
		}

		protected override void AssignAsset(string path)
		{
			if (IsValid(path)) {
				DoTransaction(() => {
					SetProperty(path);
					try {
						Document.Current.RefreshExternalScenes();
					} catch (System.Exception e) {
						AlertDialog.Show(e.Message);
						EditorParams.History.RollbackTransaction();
					}
				});
			} else {
				var value = CoalescedPropertyValue().GetValue();
				editor.Text = value.IsDefined ? value.Value : ManyValuesText;
			}
		}

		protected override string ValueToStringConverter(string obj) => obj ?? "";

		protected override string StringToValueConverter(string path) => path;
	}
}
