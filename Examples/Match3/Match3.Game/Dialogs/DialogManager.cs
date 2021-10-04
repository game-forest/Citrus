using System;
using System.Collections.Generic;
using System.Linq;

namespace Match3.Dialogs
{
	public class DialogManager
	{
		public static DialogManager Instance { get; } = new DialogManager();

		public DialogManager()
		{
			var baseType = typeof(Dialog);
			foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies()) {
				foreach (var type in assembly.GetTypes()) {
					if (typeof(Dialog).IsAssignableFrom(type)) {
						var atrs = type.GetCustomAttributes(typeof(ScenePathAttribute), false);
						if (atrs.Length > 0) {
							var path = (atrs[0] as ScenePathAttribute).ScenePath;
							Overrides.Add(path, () => (Dialog)Activator.CreateInstance(type));
						}
					}
				}
			}
		}

		public List<Dialog> ActiveDialogs { get; } = new List<Dialog>();
		public Dialog Top => ActiveDialogs.FirstOrDefault();

		public Dictionary<string, Func<Dialog>> Overrides { get; } = new Dictionary<string, Func<Dialog>>();

		public Dialog Open(string scenePath)
		{
			if (scenePath.EndsWith(".tan", StringComparison.OrdinalIgnoreCase)) {
				scenePath = scenePath[0..^4];
			}
			if (!Lime.AssetBundle.Current.FileExists(System.IO.Path.ChangeExtension(scenePath, ".tan"))) {
				Open(new AlertDialog($"Can't find {scenePath}"));
				return null;
			}
			var dialog = Overrides.ContainsKey(scenePath) ? Overrides[scenePath]() : new Dialog(scenePath);
			return Open(dialog);
		}

		public Dialog Open(Dialog dialog)
		{
			dialog.Attach(The.World);
			ActiveDialogs.Insert(0, dialog);
			return dialog;
		}

		public void Remove(Dialog dialog)
		{
			ActiveDialogs.Remove(dialog);
		}

		public Dialog GetActiveDialog() => Top;

		public void CloseDialog(Dialog dialog)
		{
			dialog.Close();
		}
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public class ScenePathAttribute : Attribute
	{
		public string ScenePath { get; set; }
		public ScenePathAttribute(string scenePath) { ScenePath = scenePath; }
	}
}
