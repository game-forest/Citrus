using EmptyProject.Dialogs;
using Lime;

namespace EmptyProject.Scripts.Common
{
	public class TaskParameters
	{
		public static readonly TaskParameters Default = new TaskParameters();
		public static readonly TaskParameters Optional = new TaskParameters { IsStrictly = false };
		public static readonly TaskParameters Immediately = new TaskParameters { Duration = 0 };
		public static readonly TaskParameters ImmediatelyAndOptional = new TaskParameters {
			IsStrictly = false,
			Duration = 0
		};
		public static readonly TaskParameters NetworkOperations = new TaskParameters {
			Duration = 300,
			UseUnscaledTime = true
		};

		public const float DefaultDuration = 10f;
		public const float DefaultPeriod = 1f / 10f;

		public bool IsStrictly { get; set; } = true;
		public float Duration { get; set; } = DefaultDuration;
		public float Period { get; set; } = DefaultPeriod;
		public bool UseUnscaledTime { get; set; }
	}

	public class WaitNodeTaskParameters : TaskParameters
	{
		public new static readonly WaitNodeTaskParameters Default = new WaitNodeTaskParameters();
		public new static readonly WaitNodeTaskParameters Optional = new WaitNodeTaskParameters { IsStrictly = false };
		public new static readonly WaitNodeTaskParameters Immediately = new WaitNodeTaskParameters { Duration = 0 };
		public new static readonly WaitNodeTaskParameters ImmediatelyAndOptional = new WaitNodeTaskParameters {
			IsStrictly = false,
			Duration = 0
		};

		public delegate bool ConditionDelegate(Node node);

		public ConditionDelegate Condition;

		public bool IsConditionMet(Node node) => Condition?.Invoke(node) ?? true;
	}

	public class WaitDialogTaskParameters : TaskParameters
	{
		public static readonly ConditionDelegate DefaultCondition = dialog => dialog.State == DialogState.Shown && dialog.IsTopDialog;
		public static readonly ConditionDelegate TopOrBackgroundDialogCondition = dialog => dialog.State == DialogState.Shown;

		public new static readonly WaitDialogTaskParameters Default = new WaitDialogTaskParameters();
		public static readonly WaitDialogTaskParameters TopOrBackground = new WaitDialogTaskParameters { Condition = TopOrBackgroundDialogCondition };
		public static readonly WaitDialogTaskParameters WithoutConditions = new WaitDialogTaskParameters { Condition = null };
		public new static readonly WaitDialogTaskParameters Optional = new WaitDialogTaskParameters { IsStrictly = false };
		public new static readonly WaitDialogTaskParameters Immediately = new WaitDialogTaskParameters { Duration = 0 };
		public new static readonly WaitDialogTaskParameters ImmediatelyAndOptional = new WaitDialogTaskParameters {
			IsStrictly = false,
			Duration = 0
		};

		public delegate bool ConditionDelegate(Dialog dialog);
		public ConditionDelegate Condition = DefaultCondition;

		public bool IsConditionMet(Dialog dialog) => Condition?.Invoke(dialog) ?? true;
	}

	public class ClickWidgetTaskParameters : TaskParameters
	{
		private static readonly ConditionDelegate defaultCondition = (widget, isInsideWindow, visible, enable) => isInsideWindow && visible && enable;

		public new static readonly ClickWidgetTaskParameters Default = new ClickWidgetTaskParameters();
		public static readonly ClickWidgetTaskParameters WithoutConditions = new ClickWidgetTaskParameters { Condition = null };
		public new static readonly ClickWidgetTaskParameters Optional = new ClickWidgetTaskParameters { IsStrictly = false };

		public delegate bool ConditionDelegate(Widget widget, bool isInsideWindow, bool visible, bool enable);
		public ConditionDelegate Condition = defaultCondition;

		public bool IsConditionMet(Widget widget, bool isInsideWindow, bool visible, bool enable) => Condition?.Invoke(widget, isInsideWindow, visible, enable) ?? true;
	}
}
