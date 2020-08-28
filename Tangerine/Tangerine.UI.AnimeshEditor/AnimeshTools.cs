using System.Linq;
using Lime;
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
			Transformation,
			Modification,
			Creation,
			Removal,
		}

		private static readonly int stateCount = typeof(ModificationState).GetEnumValues().Length;

		public static ModificationState NextState(this ModificationState state) =>
			(ModificationState)(((int)state + 1) % stateCount);

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
					case ModificationState.Modification:
					case ModificationState.Creation:
					case ModificationState.Removal:
					case ModificationState.Transformation:
						Mode = ModificationMode.Setup;
						break;
				}
			}
		}

		public static ModificationState StateBeforeAnimationPreview { get; set; }

		public class ChangeState : DocumentCommandHandler
		{
			private readonly ModificationState toState;

			public ChangeState(ModificationState toState)
			{
				this.toState = toState;
			}

			public override void ExecuteTransaction()
			{
				State = toState;
			}
		}
	}
}
