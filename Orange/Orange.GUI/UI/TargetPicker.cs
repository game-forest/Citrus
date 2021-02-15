using System.Linq;
using Lime;

namespace Orange
{
	public class TargetPicker : ThemedDropDownList
	{
		public TargetPicker()
		{
			Reload();
		}

		public void Reload()
		{
			var savedIndex = Index;
			Index = -1;
			Items.Clear();
			foreach (var target in The.Workspace.Targets) {
				if (target.Hidden) {
					continue;
				}
				Items.Add(new Item(target.Name, target));
			}
			if (savedIndex >= 0 && savedIndex < Items.Count) {
				Index = savedIndex;
			} else {
				Index = 0;
			}
		}

		public Target SelectedTarget => (Target)Items[Index].Value;
	}
}
