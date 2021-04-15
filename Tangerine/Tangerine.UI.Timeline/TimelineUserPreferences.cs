using System;
using System.Collections.Generic;
using Lime;
using Yuzu;

namespace Tangerine.UI.Timeline
{
	public class TimelineUserPreferences : Component
	{
		[YuzuOptional]
		public float ColWidth { get; set; }

		[YuzuOptional]
		public bool EditCurves { get; set; }

		[YuzuOptional]
		public List<float> TimelineVSplitterStretches;

		[YuzuOptional]
		public List<float> TimelineHSplitterStretches;

		[YuzuOptional]
		public bool AnimationStretchMode;

		[YuzuOptional]
		public bool SlowMotionMode;

		public TimelineUserPreferences()
		{
			ResetToDefaults();
		}

		public void ResetToDefaults()
		{
			ColWidth = 15;
			EditCurves = true;
			TimelineVSplitterStretches = new List<float>();
			TimelineHSplitterStretches = new List<float>();
		}

		private static TimelineUserPreferences instance;

		public static TimelineUserPreferences Instance => instance ?? (instance = Core.UserPreferences.Instance.GetOrAdd<TimelineUserPreferences>());
	}
}
