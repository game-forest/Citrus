using System.Collections.Generic;
using Lime;
using RemoteScripting;
using Tangerine.Core;

namespace Tangerine.UI.RemoteScripting
{
	public class RemoteProcedureCallScrollView : ThemedScrollView
	{
		private readonly List<ToolbarButton> buttons = new List<ToolbarButton>();

		public delegate void RemoteProcedureCalledDelegate(CompiledAssembly assembly, PortableEntryPoint entryPoint);
		public RemoteProcedureCalledDelegate RemoteProcedureCalled;

		public bool ItemsEnabled { get; set; }

		public RemoteProcedureCallScrollView()
		{
			Rebuild(CompiledAssembly.Instance);
			this.AddChangeWatcher(() => ItemsEnabled, SetItemsEnabled);
			this.AddChangeWatcher(() => CompiledAssembly.Instance, Rebuild);

			void SetItemsEnabled(bool value)
			{
				foreach (var button in buttons) {
					button.Enabled = value;
				}
			}
		}

		private void Rebuild(CompiledAssembly assembly)
		{
			buttons.Clear();
			Content.Nodes.Clear();
			if (assembly == null) {
				return;
			}

			foreach (var entryPoint in assembly.PortableAssembly.EntryPoints) {
				var button = new ToolbarButton($"{entryPoint.Summary}") {
					Enabled = ItemsEnabled,
					Clicked = () => {
						RemoteProcedureCalled?.Invoke(assembly, entryPoint);
					}
				};
				Content.AddNode(button);
				buttons.Add(button);
			}
		}
	}
}
