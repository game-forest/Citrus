using System;
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
		public Dictionary<string, Dictionary<string, bool>> LocalInspectorExpandableEditorStates { get; set; }

		[YuzuOptional]
		public Dictionary<string, ColorPickerState> InspectorColorPickersState { get; set; }

		[YuzuOptional]
		public int LookupItemsLimit { get; set; }

		[YuzuOptional]
		public bool ExperimentalTimelineHierarchy { get; set; }

		[YuzuOptional]
		public bool LockLayout { get; set; }

		[YuzuOptional]
		public bool AnimationPanelReversedOrder { get; set; }
		
		[YuzuOptional]
		public Vector2 ConflictingAnimatorsWindowSize { get; set; }

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
			LocalInspectorExpandableEditorStates = new Dictionary<string, Dictionary<string, bool>>();
			InspectorColorPickersState = new Dictionary<string, ColorPickerState>();
			LookupItemsLimit = 30;
			ConflictingAnimatorsWindowSize = Vector2.PositiveInfinity;
		}

		public static CoreUserPreferences Instance => UserPreferences.Instance.GetOrAdd<CoreUserPreferences>();
	}

	public struct ColorPickerState
	{
		[YuzuMember]
		public EditorState HsvWheel;
		[YuzuMember]
		public EditorState AlphaSlider;
		[YuzuMember]
		public EditorState HsvSliders;
		[YuzuMember]
		public EditorState LabSliders;
		[YuzuMember]
		public EditorState RgbSliders;

		public static ColorPickerState Default => new ColorPickerState {
			HsvWheel = new EditorState { Visible = false, Position = 0 },
			AlphaSlider = new EditorState { Visible = false, Position = 1 },
			HsvSliders = new EditorState { Visible = false, Position = 2 },
			LabSliders = new EditorState { Visible = false, Position = 3 },
			RgbSliders = new EditorState { Visible = false, Position = 4 }
		};

		public ColorPickerState(EditorState[] states)
		{
			if (states.Length != 5) {
				throw new InvalidOperationException();
			}
			HsvWheel = states[0];
			AlphaSlider = states[1];
			HsvSliders = states[2];
			LabSliders = states[3];
			RgbSliders = states[4];
		}

		public IEnumerable<EditorState> Enumerate()
		{
			yield return HsvWheel;
			yield return AlphaSlider;
			yield return HsvSliders;
			yield return LabSliders;
			yield return RgbSliders;
		}

		public struct EditorState
		{
			[YuzuMember]
			public bool Visible;
			[YuzuMember]
			public int Position;
		}
	}
}
