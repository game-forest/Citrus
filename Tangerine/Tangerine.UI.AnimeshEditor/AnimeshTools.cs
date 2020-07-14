using System.Linq;
using Lime.Widgets.Animesh;
using Tangerine.Core;

namespace Tangerine.UI.AnimeshEditor
{
	public static class AnimeshTools
	{
		public enum ModificationMode
		{
			Animation,
			Setup
		}

		public enum ModificationState
		{
			Animation,
			Triangulation,
			Creation,
			Removal,
		}

		public static ModificationState NextState(this ModificationState state) =>
			(ModificationState)(((int)state + 1) % 4);

		private static ModificationMode mode;
		public static ModificationMode Mode
		{
			get => mode;
			set
			{
				mode = value;
				foreach (var mesh in Document.Current.Nodes().OfType<Animesh>().ToList()) {
					mesh.TangerineAnimationModeEnabled = mode == ModificationMode.Animation;
				}
			}
		}

		private static ModificationState state;
		public static ModificationState State
		{
			get => state;
			set
			{
				state = value;
				switch (value) {
					case ModificationState.Animation:
						Mode = ModificationMode.Animation;
						break;
					case ModificationState.Triangulation:
					case ModificationState.Creation:
					case ModificationState.Removal:
						Mode = ModificationMode.Setup;
						break;
				}
			}
		}

		public static ModificationState StateBeforeAnimationPreview { get; set; }

		public class Animate : DocumentCommandHandler
		{
			public override void ExecuteTransaction() => State = ModificationState.Animation;
		}

		public class Triangulate : DocumentCommandHandler
		{
			public override void ExecuteTransaction() => State = ModificationState.Triangulation;
		}

		public class Create : DocumentCommandHandler
		{
			public override void ExecuteTransaction() => State = ModificationState.Creation;
		}

		public class Remove : DocumentCommandHandler
		{
			public override void ExecuteTransaction() => State = ModificationState.Removal;
		}
	}
}
