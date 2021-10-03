using System.Collections.Generic;
using Lime;
using Yuzu;
using System.Linq;
using System;
using System.Reflection;

#if TANGERINE
using System.ComponentModel.Composition;
using Tangerine.Core;
using Tangerine.UI;
#endif // TANGERINE

namespace EmptyProject
{
	[TangerineRegisterComponent]
	[UpdateStage(typeof(EarlyUpdateStage))]
	[AllowedComponentOwnerTypes(typeof(Node))]
	public class RunAnimationBehavior : NodeBehavior
	{
#if TANGERINE
		[Types.AnimationString(nameof(NodeProvider))]
#endif // TANGERINE
		[YuzuMember]
		public string Animation { get; set; }

		public Node NodeProvider()
		{
			return Owner;
		}

		public RunAnimationBehavior()
		{ }

		public override int Order => -1000100;

		public bool IsAwoken { get; private set; }

		protected override void OnRegister()
		{
			base.OnRegister();
			if (IsAwoken) {
				return;
			}
			RunAnimationFromTriggerString(Owner, Animation);
			IsAwoken = true;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			IsAwoken = false;
		}

		private static List<Animation> RunAnimationFromTriggerString(Node node, string trigger)
		{
			List<Animation> runAnimations = new List<Animation>();
			if (string.IsNullOrEmpty(trigger)) {
				return runAnimations;
			}
			TriggerMultipleAnimations(trigger);
			return runAnimations;

			void TriggerMultipleAnimations(string trigger)
			{
				if (trigger.Contains(',')) {
					foreach (var s in trigger.Split(',')) {
						TriggerAnimation(s.Trim());
					}
				} else {
					TriggerAnimation(trigger);
				}
			}

			void TriggerAnimation(string markerWithOptionalAnimationId)
			{
				if (markerWithOptionalAnimationId.Contains('@')) {
					var s = markerWithOptionalAnimationId.Split('@');
					if (s.Length == 2) {
						var markerId = s[0];
						var animationId = s[1];
						if (node.Animations.TryFind(animationId, out var animation)) {
							if (animation.TryRun(markerId)) {
								runAnimations.Add(animation);
							}
						}
					}
				} else {
					if (node.TryRunAnimation(markerWithOptionalAnimationId, null)) {
						runAnimations.Add(node.DefaultAnimation);
					}
				}
			}
		}
	}

}

namespace EmptyProject.Types
{
#if TANGERINE
	internal class AnimationStringAttribute : Attribute
	{
		private readonly string methodName;
		private MethodInfo method;

		public AnimationStringAttribute(string nodeProviderMethodName)
		{
			this.methodName = nodeProviderMethodName;
		}

		public Node GetNode(object o)
		{
			if (method == null) {
				var type = o.GetType();
				method = type.GetMethod(methodName);
			}
			return (Node)method.Invoke(o, new object [] { });
		}
	}

	public static class TangerinePlugin
	{
		[Export(nameof(Orange.OrangePlugin.Initialize))]
		public static void Initialize()
		{
			var editorRegister = Tangerine.UI.Inspector.InspectorPropertyRegistry.Instance;
			editorRegister.Items.Insert(0, new Tangerine.UI.Inspector.InspectorPropertyRegistry.RegistryItem(
				(c) => PropertyAttributes<AnimationStringAttribute>.Get(c.PropertyInfo) != null,
				(c) => {
					return new AnimationStringPropertyEditor(
						c,
						(o) => PropertyAttributes<AnimationStringAttribute>.Get(c.PropertyInfo).GetNode(o)
					);
				}
			));
		}
	}

	public class AnimationStringPropertyEditor : CommonPropertyEditor<string>
	{
		private readonly EditBox editBox;
		private readonly Node node;

		public AnimationStringPropertyEditor(
			IPropertyEditorParams editorParams,
			Func<Object, Node> nodeProvider,
			bool multiline = false
		) : base(editorParams) {
			if (EditorParams.Objects.Skip(1).Any()) {
				EditorContainer.AddNode(CreateWarning("Edit of triggers isn't supported for multiple selection."));
				return;
			}
			node = nodeProvider(editorParams.Objects.First());
			var button = new ThemedButton {
				Text = "...",
				MinMaxWidth = 20,
				LayoutCell = new LayoutCell(Alignment.Center)
			};
			EditorContainer.AddNode(editBox = editorParams.EditBoxFactory());
			EditorContainer.AddNode(Spacer.HSpacer(4));
			EditorContainer.AddNode(button);
			EditorContainer.AddNode(Spacer.HStretch());
			editBox.Submitted += text => {
				var newValue = FilterTriggers(text);
				editBox.Text = newValue;
				SetProperty(newValue);
			};
			button.Clicked += () => {
				var value = CoalescedPropertyValue().GetValue().Value;
				var currentTriggers = string.IsNullOrEmpty(value) ?
					new HashSet<string>() :
					value.Split(',').Select(el => el.Trim()).ToHashSet();
				var window = new TriggerSelectionDialog(
					node,
					currentTriggers,
					s => {
						s = FilterTriggers(s);
						SetProperty(s);
						editBox.Text = s;
					}
				);
			};
			editBox.AddLateChangeWatcher(CoalescedPropertyValue(), v => editBox.Text = v.Value);
			Invalidate();
		}

		public void Invalidate()
		{
			var value = CoalescedPropertyValue().GetValue().Value;
			if (node != null && editBox != null) {
				editBox.Text = FilterTriggers(value);
			}
		}

		private Dictionary<string, HashSet<string>> GetAvailableTriggers()
		{
			var triggers = new Dictionary<string, HashSet<string>>();
			foreach (var a in node.Animations) {
				foreach (var m in a.Markers.Where(i => !string.IsNullOrEmpty(i.Id))) {
					var id = a.Id != null ? m.Id + '@' + a.Id : m.Id;
					var key = a.Id ?? "";
					if (!triggers.Keys.Contains(key)) {
						triggers[key] = new HashSet<string>();
					}
					if (!triggers[key].Contains(id)) {
						triggers[key].Add(id);
					}
				}
			}
			return triggers;
		}

		private Widget CreateWarning(string message)
		{
			return new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					new ThemedSimpleText {
						Text = message,
						Padding = Theme.Metrics.ControlsPadding,
						LayoutCell = new LayoutCell(Alignment.Center),
						VAlignment = VAlignment.Center,
						ForceUncutText = false
					}
				},
				Presenter = new WidgetFlatFillPresenter(Theme.Colors.WarningBackground)
			};
		}

		private string FilterTriggers(string text)
		{
			var newValue = "";
			if (!string.IsNullOrEmpty(text)) {
				var triggersToSet = text.Split(',').ToList();
				var triggers = GetAvailableTriggers();
				foreach (var key in triggers.Keys) {
					foreach (var trigger in triggersToSet) {
						if (triggers[key].Contains(trigger.Trim(' '))) {
							newValue += trigger.Trim(' ') + ',';
							break;
						}
					}
				}
				if (!string.IsNullOrEmpty(newValue)) {
					newValue = newValue.Trim(',');
				}
			}
			return newValue;
		}
	}
#endif // TANGERINE
}
