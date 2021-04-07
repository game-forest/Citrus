using System.Collections.Generic;
using Lime;
using Yuzu;

namespace Tangerine.Core
{
	public class CoreUserPreferences : Component
	{
		[YuzuOptional]
		public bool AutoKeyframes { get; set; }

		[YuzuOptional]
		public bool InspectEasing { get; set; }

		[YuzuOptional]
		public KeyFunction DefaultKeyFunction { get; set; }

		[YuzuOptional]
		public bool ReloadModifiedFiles { get; set; }

		[YuzuOptional]
		public bool StopAnimationOnCurrentFrame { get; set; }

		[YuzuOptional]
		public bool ShowSceneThumbnail  { get; set; }

		[YuzuOptional]
		public bool ShowSplinesGlobally { get; set; }

		[YuzuOptional]
		public bool DontPasteAtMouse { get; set; }

		[YuzuOptional]
		public bool InverseShiftKeyframeDrag { get; set; }

		[YuzuOptional]
		public bool SwapMouseButtonsForKeyframeSwitch { get; set; }

		[YuzuOptional]
		public bool ShowFrameProgression { get; set; }

		[YuzuOptional]
		public bool LockTimelineCursor { get; set; }

		[YuzuOptional]
		public Dictionary<string, bool> InspectorExpandableEditorsState { get; set; }

		[YuzuOptional]
		public int LookupItemsLimit { get; set; }

		[YuzuOptional]
		public bool ExperimentalTimelineHierarchy { get; set; }

		public CoreUserPreferences()
		{
			ResetToDefaults();
		}

		public void ResetToDefaults()
		{
			AutoKeyframes = false;
			DefaultKeyFunction = KeyFunction.Linear;
			StopAnimationOnCurrentFrame = false;
			ShowSceneThumbnail = true;
			ShowSplinesGlobally = false;
			InspectorExpandableEditorsState = new Dictionary<string, bool>();
			LookupItemsLimit = 30;
		}

		public static CoreUserPreferences Instance => UserPreferences.Instance.GetOrAdd<CoreUserPreferences>();
	}
}
