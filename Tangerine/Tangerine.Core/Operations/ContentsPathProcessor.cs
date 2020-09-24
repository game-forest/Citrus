using Lime;

namespace Tangerine.Core.Operations
{
	public class ContentsPathProcessor : IOperationProcessor
	{
		public void Do(IOperation op)
		{
			RefreshSceneTreeIfNeeded(op);
		}

		private void RefreshSceneTreeIfNeeded(IOperation op)
		{
			if (op is SetProperty setProperty) {
				if (setProperty.Obj is Node && setProperty.Property.Name == nameof(Node.ContentsPath)) {
					Document.Current.RefreshSceneTree();
				}
			}
		}

		public void Redo(IOperation op) => RefreshSceneTreeIfNeeded(op);
		public void Undo(IOperation op) => RefreshSceneTreeIfNeeded(op);
	}
}
