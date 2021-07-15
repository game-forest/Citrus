using System;
using System.Linq;
using System.Collections.Generic;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(CanBeRoot = true, Order = 1)]
	[TangerineNodeBuilder("BuildForTangerine")]
	[TangerineAllowedChildrenTypes(typeof(Node))]
	[TangerineVisualHintGroup("/All/Nodes/Containers")]
	public class Button : Widget
	{
		private TextPresentersFeeder textPresentersFeeder;
		private IEnumerator<int> stateHandler;
		private ClickGesture clickGesture;
		private bool isChangingState;
		private bool isDisabledState;
		private bool awoken;

		private string text;

		public BitSet32 EnableMask = BitSet32.Full;

		[YuzuMember]
		[TangerineKeyframeColor(9)]
		public override string Text
		{
			get => text;
			set
			{
				if (text != value) {
					text = value;
					textPresentersFeeder?.Update();
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(18)]
		public override bool Enabled
		{
			get => EnableMask[0];
			set {
				if (EnableMask[0] != value) {
					EnableMask[0] = value;
					PropagateDirtyFlags(DirtyFlags.Enabled);
				}
			}
		}

		public override Action Clicked { get; set; }

		public Button()
		{
			HitTestTarget = true;
			Input.AcceptMouseBeyondWidget = false;
			Components.Add(new ButtonBehavior());
		}

		private void SetState(IEnumerator<int> newState)
		{
			stateHandler = newState;
			if (!isChangingState) {
				isChangingState = true;
				stateHandler.MoveNext();
				isChangingState = false;
			}
		}

		private IEnumerator<int> InitialState()
		{
			yield return 0;
			SetState(NormalState());
		}

		public override bool WasClicked() => clickGesture?.WasRecognized() ?? false;

		internal void Awake()
		{
			if (!awoken) {
				OnAwake();
				awoken = true;
			}
		}

		protected virtual void OnAwake()
		{
			SetState(InitialState());
			textPresentersFeeder = new TextPresentersFeeder(this);
			clickGesture = new ClickGesture();
			Gestures.Add(clickGesture);
			OnUpdate(0);
		}

		private IEnumerator<int> NormalState()
		{
			TryRunAnimation("Normal");
			while (true) {
#if WIN || MAC
				if (IsMouseOverThisOrDescendant()) {
					SetState(HoveredState());
				}
#else
				if (clickGesture.WasBegan()) {
					SetState(PressedState());
				}
#endif
				yield return 0;
			}
		}

		private IEnumerator<int> HoveredState()
		{
			TryRunAnimation("Focus");
			while (true) {
				if (!IsMouseOverThisOrDescendant()) {
					SetState(NormalState());
				} else if (clickGesture.WasBegan()) {
					SetState(PressedState());
				}
				yield return 0;
			}
		}

		private IEnumerator<int> PressedState()
		{
			TryRunAnimation("Press");
			var wasMouseOver = true;
			while (true) {
				if (GloballyEnabled && EnableMask.All() && clickGesture.WasRecognized()) {
					Clicked?.Invoke();
					// buz: don't play release animation
					// if button's parent became invisible due to
					// button press (or it will be played when
					// parent is visible again)
					if (!GloballyVisible) {
#if WIN || MAC
						SetState(HoveredState());
#else
						SetState(NormalState());
#endif
					} else {
						SetState(ReleaseState());
					}
				} else if (clickGesture.WasCanceled()) {
					if (CurrentAnimation == "Press") {
						TryRunAnimation("Release");
						while (DefaultAnimation.IsRunning) {
							yield return 0;
						}
					}
					SetState(NormalState());
				}
				var mouseOver = IsMouseOverThisOrDescendant();
				if (wasMouseOver && !mouseOver) {
					if (CurrentAnimation == "Press") {
						TryRunAnimation("Release");
					}
				} else if (!wasMouseOver && mouseOver) {
					TryRunAnimation("Press");
				}
				wasMouseOver = mouseOver;
				yield return 0;
			}
		}

		private IEnumerator<int> ReleaseState()
		{
			if (CurrentAnimation != "Release") {
				if (TryRunAnimation("Release")) {
					while (DefaultAnimation.IsRunning) {
						yield return 0;
					}
				}
			}
#if WIN || MAC
			SetState(HoveredState());
#else
			SetState(NormalState());
#endif
		}

		private IEnumerator<int> DisabledState()
		{
			isDisabledState = true;
			if (CurrentAnimation == "Release") {
				// The release animation should be played if we disable the button
				// right after click on it.
				while (DefaultAnimation.IsRunning) {
					yield return 0;
				}
			}
			TryRunAnimation("Disable");
			while (DefaultAnimation.IsRunning) {
				yield return 0;
			}
			while (!EnableMask.All() || !GloballyEnabled) {
				yield return 0;
			}
			TryRunAnimation("Enable");
			while (DefaultAnimation.IsRunning) {
				yield return 0;
			}
			isDisabledState = false;
			SetState(NormalState());
		}

		public virtual void OnUpdate(float delta)
		{
			if (GloballyVisible) {
				stateHandler.MoveNext();
			}
			textPresentersFeeder.Update();
			if ((!EnableMask.All() || !GloballyEnabled) && !isDisabledState) {
				SetState(DisabledState());
			}
#if WIN || MAC
			if (EnableMask.All() && GloballyEnabled) {
				if (Input.ConsumeKeyPress(Key.Space) || Input.ConsumeKeyPress(Key.Enter)) {
					Clicked?.Invoke();
				}
			}
#endif
		}

		private void BuildForTangerine()
		{
			int[] markerFrames = { 0, 10, 20, 30, 40};
			string[] makerIds = { "Normal", "Focus", "Press", "Release", "Disable" };
			for (var i = 0; i < 5; i++) {
				DefaultAnimation.Markers.Add(new Marker(makerIds[i], markerFrames[i], MarkerAction.Stop));
			}
		}
	}

	[NodeComponentDontSerialize]
	[UpdateStage(typeof(EarlyUpdateStage))]
	[UpdateAfterBehavior(typeof(LegacyEarlyBehaviorContainer))]
	public class ButtonBehavior : BehaviorComponent
	{
		private Button button;

		protected internal override void Start()
		{
			button = (Button)Owner;
			button.Awake();
		}

		protected internal override void Update(float delta)
		{
			button.OnUpdate(delta);
		}
	}

	internal class TextPresentersFeeder
	{
		private Widget widget;
		private List<Widget> textPresenters;
		public const string TextPresenterId = "TextPresenter";

		public TextPresentersFeeder(Widget widget)
		{
			this.widget = widget;
		}

		public void Update()
		{
#if TANGERINE
			// TextPresenters in Tangerine can be added or deleted by artists that's
			// why we have to somehow update list of text presenters.
			// There is a way to use NodeManager.HierarchyChanged and also track SetProperty operation.
			if (textPresenters == null) {
				textPresenters = new List<Widget>();
			}
			textPresenters.Clear();
			textPresenters.AddRange(widget.Descendants.OfType<Widget>().Where(i => i.Id == TextPresenterId));
#else
			textPresenters = textPresenters ?? widget.Descendants.OfType<Widget>().Where(i => i.Id == "TextPresenter").ToList();
#endif

			foreach (var i in textPresenters) {
				i.Text = widget.Text;
			}
		}
	}
}
