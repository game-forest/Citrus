using System;
using System.Linq;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;

namespace Tangerine.UI.Inspector
{
	public class KeyframeButton : Button
	{
		private readonly Image image;
		private readonly Image fillImage;
		private readonly Image outlineImage;
		private static readonly string[] iconNames = new[] { "Linear", "Step", "Catmullrom", "Loop" };
		private readonly List<ITexture> fillTextures = new List<ITexture>();
		private readonly List<ITexture> outlineTextures = new List<ITexture>();
		private ClickGesture rightClickGesture;
		private KeyFunction function;
		private bool @checked;

		public Color4 KeyColor { get; set; }
		public KeyFunction[] AllowedKeyFunctions { get; set; }

		public bool Checked
		{
			get => @checked;
			set
			{
				@checked = value;
				fillImage.Visible = value;
				outlineImage.Visible = value;
				fillImage.Color = KeyColor;
				outlineImage.Color = ColorTheme.Current.Inspector.BorderAroundKeyframeColorbox;
			}
		}

		public void SetKeyFunction(KeyFunction function)
		{
			this.function = function;
			fillImage.Texture = fillTextures[(int)function];
			outlineImage.Texture = outlineTextures[(int)function];
		}

		private static void Awake(Node owner)
		{
			var kfb = (KeyframeButton)owner;
			kfb.rightClickGesture = new ClickGesture(1);
			kfb.Gestures.Add(kfb.rightClickGesture);
		}

		public bool WasRightClicked() => rightClickGesture?.WasRecognized() ?? false;

		public KeyframeButton()
		{
			foreach (var v in Enum.GetValues(typeof(KeyFunction))) {
				fillTextures.Add(IconPool.GetTexture("Inspector." + iconNames[(byte)v] + "Fill"));
				outlineTextures.Add(IconPool.GetTexture("Inspector." + iconNames[(byte)v] + "Outline"));
			}
			Nodes.Clear();
			Size = MinMaxSize = new Vector2(22, 22);
			image = new Image { Size = Size, Shader = ShaderId.Silhuette, Texture = new SerializableTexture(), Color = Theme.Colors.WhiteBackground };
			fillImage = new Image { Size = Size, Visible = false };
			outlineImage = new Image { Size = Size, Visible = false };
			Nodes.Add(outlineImage);
			Nodes.Add(fillImage);
			Nodes.Add(image);
			Layout = new StackLayout();
			PostPresenter = new WidgetBoundsPresenter(ColorTheme.Current.Inspector.BorderAroundKeyframeColorbox);
			Awoke += Awake;
		}
	}

	internal class KeyframeButtonBinding : ITaskProvider
	{
		private readonly IPropertyEditorParams editorParams;
		private readonly KeyframeButton button;
		private readonly IPropertyEditor editor;

		public KeyframeButtonBinding(IPropertyEditorParams editorParams, KeyframeButton button, IPropertyEditor editor)
		{
			this.editorParams = editorParams;
			this.button = button;
			this.editor = editor;
		}

		public IEnumerator<object> Task()
		{
			var keyFunctionFlow = KeyframeDataflow.GetProvider(editorParams, i => i?.Function).DistinctUntilChanged().GetDataflow();
			while (true) {
				keyFunctionFlow.Poll(out var kf);
				button.Checked = kf.HasValue;
				if (kf.HasValue) {
					button.SetKeyFunction(kf.Value);
				}
				bool wasClicked = button.WasClicked();
				bool wasRightClicked = button.WasRightClicked();
				if (CoreUserPreferences.Instance.SwapMouseButtonsForKeyframeSwitch) {
					Toolbox.Swap(ref wasClicked, ref wasRightClicked);
				}
				if (editor.Enabled && button.GloballyEnabled && wasClicked) {
					Document.Current.History.DoTransaction(() => {
						SetKeyframe(!kf.HasValue);
						if (!kf.HasValue) {
							keyFunctionFlow.Poll(out kf);
							SetKeyFunction(kf ?? GetDefaultKeyFunction());
						}
					});
				}
				if (button.GloballyEnabled && wasRightClicked) {
					if (kf.HasValue) {
						var nextKeyFunction = GetNextKeyFunction(kf.GetValueOrDefault());
						Document.Current.History.DoTransaction(() => {
							SetKeyFunction(nextKeyFunction);
						});
					} else {
						Document.Current.History.DoTransaction(() => {
							SetKeyframe(true);
							SetKeyFunction(GetDefaultKeyFunction());
						});
					}
				}
				yield return null;
			}
		}

		private KeyFunction GetNextKeyFunction(KeyFunction value)
		{
			if (button.AllowedKeyFunctions == null) {
				return nextKeyFunction[(int)value];
			}
			while (true) {
				value = nextKeyFunction[(int)value];
				if (button.AllowedKeyFunctions.Contains<KeyFunction>(value)) {
					return value;
				}
			}
		}

		private KeyFunction GetDefaultKeyFunction() => button.AllowedKeyFunctions?[0] ?? KeyFunction.Linear;

		private static readonly KeyFunction[] nextKeyFunction = {
			KeyFunction.Spline, KeyFunction.ClosedSpline,
			KeyFunction.Steep, KeyFunction.Linear
		};

		internal void SetKeyFunction(KeyFunction value)
		{
			foreach (var animable in editorParams.RootObjects.OfType<IAnimationHost>()) {
				if (animable.Animators.TryFind(editorParams.PropertyPath, out var animator, Document.Current.AnimationId)) {
					var spans = GetAnimableSpans(animable, animator);
					var keyframeClones = animator
						.ReadonlyKeys
						.Where(i => spans.Any(j => j.Contains(i.Frame)))
						.Select(k => k.Clone())
						.ToList();
					foreach (var keyframe in keyframeClones) {
						keyframe.Function = value;
						Core.Operations.SetKeyframe.Perform(animable, editorParams.PropertyPath, Document.Current.AnimationId, keyframe);
					}
				}
			}
		}

		private void SetKeyframe(bool value)
		{
			int currentFrame = Document.Current.AnimationFrame;

			foreach (var (animable, owner) in editorParams.RootObjects.Zip(editorParams.Objects, (ro, o) => (ro as IAnimationHost, o))) {
				bool hasKey = false;
				if (animable.Animators.TryFind(editorParams.PropertyPath, out IAnimator animator, Document.Current.AnimationId)) {
					hasKey = animator.ReadonlyKeys.Any(i => i.Frame == currentFrame);
					if (hasKey && !value) {
						Core.Operations.RemoveKeyframe.Perform(animator, currentFrame);
					}
				}

				if (!hasKey && value) {
					var propValue = editorParams.IndexInList == -1
						? new Property(owner, editorParams.PropertyName).Getter()
						: new IndexedProperty(owner, editorParams.PropertyName, editorParams.IndexInList).Getter();
					var keyFunction = animator?.Keys.LastOrDefault(k => k.Frame <= currentFrame)?.Function ??
						CoreUserPreferences.Instance.DefaultKeyFunction;
					IKeyframe keyframe = Keyframe.CreateForType(editorParams.PropertyInfo.PropertyType, currentFrame, propValue, keyFunction);
					Core.Operations.SetKeyframe.Perform(animable, editorParams.PropertyPath, Document.Current.AnimationId, keyframe);
				}
			}
		}

		private static GridSpanList GetAnimableSpans(IAnimationHost animable, IAnimator animator)
		{
			var rows = Document.Current
				.SelectedRows()
				.Where(r => animable == r.Components.Get<NodeRow>()?.Node || animator != null && r.Components.Get<PropertyRow>()?.Animator == animator)
				.ToList();
			var spans = new GridSpanList {
				new GridSpan(Document.Current.AnimationFrame, Document.Current.AnimationFrame + 1)
			};
			foreach (var row in rows) {
				var rowSpans = row.Components.Get<GridSpanListComponent>()?.Spans;
				if (rowSpans != null && rowSpans.Count > 0) {
					spans.AddRange(rowSpans);
				}
			}
			return spans.GetNonOverlappedSpans();
		}
	}
}
