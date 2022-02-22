using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public abstract class FilePropertyEditor<T> : ExpandablePropertyEditor<T>
	{
		private class PrefixData
		{
			public string Prefix { get; set; }
		}

		protected readonly EditBox editor;
		protected static string lastOpenedDirectory = Path.GetDirectoryName(Document.Current.FullPath);
		protected readonly string[] allowedFileTypes;

		private readonly PrefixData prefix = new PrefixData();

		public bool ShowPrefix { get; set; } = true;
		protected bool TrimExtension { get; set; } = true;

		protected FilePropertyEditor(IPropertyEditorParams editorParams, string[] allowedFileTypes)
			: base(editorParams)
		{
			ThemedButton button;
			this.allowedFileTypes = allowedFileTypes;
			EditorContainer.AddNode(new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					(editor = editorParams.EditBoxFactory()),
					Spacer.HSpacer(4),
					(button = new ThemedButton {
						Text = "...",
						MinMaxWidth = 20,
						LayoutCell = new LayoutCell(Alignment.Center),
					}),
				},
			});
			editor.LayoutCell = new LayoutCell(Alignment.Center);
			editor.Submitted += text => SetComponent(text);
			button.Clicked += OnSelectClicked;
			ExpandableContent.Padding = new Thickness(24, 10, 2, 2);
			var prefixEditor = new StringPropertyEditor(
				new PropertyEditorParams(prefix, nameof(PrefixData.Prefix)) { LabelWidth = 180 }
			);
			ExpandableContent.AddNode(prefixEditor.ContainerWidget);
			prefix.Prefix = GetLongestCommonPrefix(GetPaths());
			ContainerWidget.AddChangeWatcher(() => prefix.Prefix, v => {
				string oldPrefix = GetLongestCommonPrefix(GetPaths());
				if (oldPrefix == v) {
					return;
				}
				SetPathPrefix(oldPrefix, v);
				prefix.Prefix = v.Trim('/');
			});
			ContainerWidget.AddChangeWatcher(() => ShowPrefix, show => {
				Expanded = show && Expanded;
				ExpandButton.Visible = show;
			});
			var current = CoalescedPropertyValue();
			editor.AddLateChangeWatcher(current, v => editor.Text = ValueToStringConverter(v.Value) ?? string.Empty);
			ManageManyValuesOnFocusChange(editor, current);
		}

		protected override void FillContextMenuItems(Menu menu)
		{
			base.FillContextMenuItems(menu);
			if (EditorParams.Objects.Skip(1).Any()) {
				return;
			}
			var path = GetPaths().First();
			if (!string.IsNullOrEmpty(path)) {
				path = Path.Combine(Project.Current.AssetsDirectory, path);
				FilesystemCommands.NavigateTo.UserData = path;
				menu.Insert(0, FilesystemCommands.NavigateTo);
				FilesystemCommands.OpenInSystemFileManager.UserData = path;
				menu.Insert(0, FilesystemCommands.OpenInSystemFileManager);
			}
		}

		private List<string> GetPaths()
		{
			var result = new List<string>();
			foreach (var o in EditorParams.Objects) {
				result.Add(ValueToStringConverter(PropertyValue(o).GetValue()));
			}
			return result;
		}

		private void SetPathPrefix(string oldPrefix, string prefix)
		{
			SetProperty<T>(
				current => StringToValueConverter(
					AssetPath.CorrectSlashes(
						Path.Combine(prefix, ValueToStringConverter(current)[oldPrefix.Length..].TrimStart('/'))
					)
				)
			);
		}

		protected abstract string ValueToStringConverter(T obj);

		protected abstract T StringToValueConverter(string path);

		public void SetComponent(string text) => SetFilePath(text);

		public override void Submit() => SetFilePath(editor.Text);

		private void SetFilePath(string path)
		{
			if (
				Utils.ExtractAssetPathOrShowAlert(
					path,
					out string asset,
					out string type,
					trimExtension: TrimExtension
				) && Utils.AssertCurrentDocument(asset, type)
			) {
				if (!IsValid(asset)) {
					AlertDialog.Show(
						$"Asset '{asset}' missing or path contains characters other than Latin letters and digits."
					);
				} else {
					AssignAsset(AssetPath.CorrectSlashes(asset));
				}
			}
		}

		public override void DropFiles(IEnumerable<string> files)
		{
			var nodeUnderMouse = WidgetContext.Current.NodeUnderMouse;
			if (nodeUnderMouse != null && nodeUnderMouse.SameOrDescendantOf(editor) && files.Any()) {
				SetFilePath(files.First());
			}
		}

		protected override void Copy() => Clipboard.Text = editor.Text;

		protected override void Paste()
		{
			try {
				AssignAsset(AssetPath.CorrectSlashes(Clipboard.Text));
			} catch (System.Exception) {
			}
		}

		protected abstract void AssignAsset(string path);

		protected virtual bool IsValid(string path)
		{
			foreach (var o in EditorParams.Objects) {
				var validatedValues = PropertyValidator.ValidateValue(o, path, EditorParams.PropertyInfo);
				if (validatedValues.Any(
						value => value.Result != ValidationResult.Ok && value.Result != ValidationResult.Info
					)
				) {
					return false;
				}
			}
			return true;
		}

		public string GetLongestCommonPrefix(List<string> paths)
		{
			if (paths == null || paths.Count == 0) {
				return string.Empty;
			}
			const char Separator = '/';
			var directoryParts = new List<string>(paths[0].Split(Separator));
			for (int index = 1; index < paths.Count; index++) {
				var first = directoryParts;
				var second = paths[index].Split(Separator);
				int maxPrefixLength = Math.Min(first.Count, second.Length);
				var tempDirectoryParts = new List<string>(maxPrefixLength);
				for (int part = 0; part < maxPrefixLength; part++) {
					if (first[part] == second[part]) {
						tempDirectoryParts.Add(first[part]);
					}
				}
				directoryParts = tempDirectoryParts;
			}
			return string.Join(Separator.ToString(), directoryParts);
		}

		public bool TryGetClosestAvailableDirectory(string path, out string directory)
		{
			directory = path;
			while (!Directory.Exists(directory)) {
				directory = Path.GetDirectoryName(directory);
				if (string.IsNullOrEmpty(directory)) {
					return false;
				}
			}
			return true;
		}

		protected virtual void OnSelectClicked()
		{
			var initialDirectory = Project.Current.AssetsDirectory;
			if (Directory.Exists(lastOpenedDirectory)) {
				initialDirectory = lastOpenedDirectory;
			}
			var current = CoalescedPropertyValue().GetDataflow();
			current.Poll();
			var value = current.Value;
			var path = ValueToStringConverter(value.Value);
			if (
				current.GotValue
				&& value.IsDefined
				&& !string.IsNullOrEmpty(path)
				&& TryGetClosestAvailableDirectory(
					AssetPath.Combine(Project.Current.AssetsDirectory, path), out var dir
				)
			) {
				initialDirectory = dir;
			}
			var dlg = new FileDialog {
				AllowedFileTypes = allowedFileTypes,
				Mode = FileDialogMode.Open,
				InitialDirectory = initialDirectory,
			};
			if (dlg.RunModal()) {
				SetFilePath(dlg.FileName);
				lastOpenedDirectory = Path.GetDirectoryName(dlg.FileName);
			}
		}
	}
}
