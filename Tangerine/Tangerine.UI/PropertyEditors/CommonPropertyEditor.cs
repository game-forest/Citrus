using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Lime;
using Lime.RenderOptimizer;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class CommonPropertyEditor<T> : IPropertyEditor
	{
		protected const string ManyValuesText = "<many values>";

		private Dictionary<ValidationResult, List<string>> warnings;
		private bool areWarningsUpdated;

		public IPropertyEditorParams EditorParams { get; private set; }
		public Widget ContainerWidget { get; private set; }
		public SimpleText PropertyLabel { get; private set; }
		public Widget LabelContainer { get; private set; }
		public Widget EditorContainer { get; private set; }
		public Widget WarningCounters { get; private set; }
		public Widget WarningsContainer { get; private set; }
		public Widget PropertyContainerWidget { get; private set; }

		public bool Enabled
		{
			get => PropertyContainerWidget.Enabled;
			set => PropertyContainerWidget.Enabled = value;
		}

		public bool IsMultiselection => EditorParams.Objects.Skip(1).Any();

		public CommonPropertyEditor(IPropertyEditorParams editorParams)
		{
			EditorParams = editorParams;
			ContainerWidget = new Widget {
				Layout = new VBoxLayout(),
				LayoutCell = new LayoutCell { StretchY = 0f },
			};
			ContainerWidget.Components.Add(new PropertyEditorComponent(this));

			PropertyContainerWidget = new Widget {
				Layout = new HBoxLayout { IgnoreHidden = false },
				LayoutCell = new LayoutCell { StretchY = 0f },
			};

			ContainerWidget.AddNode(PropertyContainerWidget);
			if (editorParams.ShowLabel) {
				LabelContainer = new Widget {
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell { StretchX = 1.0f },
					Nodes = {
						(PropertyLabel = new ThemedSimpleText {
							Text = editorParams.DisplayName ?? editorParams.PropertyName,
							VAlignment = VAlignment.Center,
							LayoutCell = new LayoutCell(Alignment.LeftCenter),
							ForceUncutText = false,
							Padding = new Thickness(left: 5.0f),
							HitTestTarget = true,
							AutoMaxSize = true,
							TabTravesable = new TabTraversable(),
						}),
						Spacer.HStretch(),
					},
				};
				PropertyLabel.Tasks.Add(ManageLabelTask());
				var tooltip = PropertyAttributes<TangerineTooltipAttribute>.Get(
					editorParams.PropertyInfo
				)?.Text ?? PropertyLabel.Text;
				PropertyLabel.Components.Add(new TooltipComponent(() => tooltip));
				PropertyContainerWidget.AddNode(LabelContainer);
				EditorContainer = new Widget {
					Enabled = PropertyAttributes<TangerineReadOnlyAttribute>.Get(
						editorParams.PropertyInfo) == null &&
						!editorParams.Objects.Any(o =>
							ClassAttributes<TangerineReadOnlyPropertiesAttribute>.Get(
								o.GetType()
							)?.Contains(editorParams.PropertyName) ?? false),
					Layout = new HBoxLayout(),
					LayoutCell = new LayoutCell { StretchX = 2.0f },
				};
				PropertyContainerWidget.AddNode(EditorContainer);
			} else {
				LabelContainer = EditorContainer = PropertyContainerWidget;
			}
			WarningsContainer = new Widget {
				Layout = new VBoxLayout(),
				LayoutCell = new LayoutCell(),
			};
			WarningCounters = new Widget {
				Layout = new HBoxLayout(),
				LayoutCell = new LayoutCell { StretchX = 0f },
			};
			warnings = new Dictionary<ValidationResult, List<string>>();
			foreach (var result in (ValidationResult[])Enum.GetValues(typeof(ValidationResult))) {
				if (result == ValidationResult.Ok) {
					continue;
				}
				warnings.Add(result, new List<string>());
			}
			WarningsContainer.Tasks.Add(UpdateWarningsTask());
			ContainerWidget.AddNode(WarningsContainer);
			EditorContainer.AddChangeWatcher(CoalescedPropertyValue(), v => ClearWarningsAndValidate());
		}

		protected void AddWarning(string message, ValidationResult validationResult)
		{
			if (message.IsNullOrWhiteSpace() || validationResult == ValidationResult.Ok) {
				return;
			}
			if (!IsMultiselection) {
				WarningsContainer.AddNode(CommonPropertyEditor.CreateWarning(message, validationResult));
			}
			warnings[validationResult].Add(message);
			areWarningsUpdated = true;
		}

		private void ClearWarnings()
		{
			WarningsContainer.Nodes.Clear();
			foreach (var key in warnings.Keys) {
				warnings[key].Clear();
			}
			areWarningsUpdated = true;
		}

		private void UpdateWarningCounters()
		{
			WarningCounters.Nodes.Clear();
			if (IsMultiselection) {
				Widget counterContainer;
				const int MaxTooltipLength = 8;
				foreach (var result in (ValidationResult[])Enum.GetValues(typeof(ValidationResult))) {
					if (result == ValidationResult.Ok || warnings[result].Count == 0) {
						continue;
					}
					WarningCounters.AddNode(counterContainer = new Widget {
						Layout = new HBoxLayout(),
						LayoutCell = new LayoutCell(Alignment.LeftCenter),
						HitTestTarget = true,
						Nodes = {
							CommonPropertyEditor.CreateWarningIcon(result),
							CommonPropertyEditor.CreateWarningText(
								text: $"({warnings[result].Count})",
								autoMinSize: true
							),
						},
					});
					var builder = new StringBuilder(warnings[result][0]);
					for (int i = 1; i < Math.Min(warnings[result].Count, MaxTooltipLength); i++) {
						builder.Append('\n');
						builder.Append(warnings[result][i]);
					}
					if (warnings[result].Count > MaxTooltipLength) {
						builder.Append("...");
					}
					string tooltip = builder.ToString();
					counterContainer.Components.Add(new TooltipComponent(() => tooltip));
				}
			}
			if (WarningCounters.Nodes.Count > 0 && WarningCounters.Parent == null) {
				PropertyContainerWidget.AddNode(WarningCounters);
			} else if (WarningCounters.Nodes.Count == 0 && WarningCounters.Parent != null) {
				WarningCounters.Unlink();
			}
		}

		private IEnumerator<object> UpdateWarningsTask()
		{
			while (true) {
				if (areWarningsUpdated) {
					UpdateWarningCounters();
					areWarningsUpdated = false;
				}
				yield return null;
			}
		}

		private IEnumerator<object> ManageLabelTask()
		{
			var clickGesture0 = new ClickGesture(0, () => {
				PropertyLabel.SetFocus();
			});
			var clickGesture1 = new ClickGesture(1, () => {
				PropertyLabel.SetFocus();
				ShowPropertyContextMenu();
			});
			var clickGesture2 = new ClickGesture(2, () => {
				PropertyLabel.SetFocus();
				resetToDefault.Consume();
				var defaultValue = EditorParams.DefaultValueGetter();
				if (defaultValue != null) {
					SetProperty(defaultValue);
				}
			});
			PropertyLabel.Gestures.Add(clickGesture0);
			PropertyLabel.Gestures.Add(clickGesture1);
			PropertyLabel.Gestures.Add(clickGesture2);
			while (true) {
				PropertyLabel.Color = PropertyLabel.IsFocused()
					? Theme.Colors.KeyboardFocusBorder
					: Theme.Colors.BlackText;
				if (PropertyLabel.IsFocused()) {
					if (Command.Copy.WasIssued()) {
						Command.Copy.Consume();
						Copy();
					}
					if (Command.Paste.WasIssued()) {
						Command.Paste.Consume();
						Paste();
					}
					if (resetToDefault.WasIssued()) {
						resetToDefault.Consume();
						var defaultValue = EditorParams.DefaultValueGetter();
						if (defaultValue != null) {
							SetProperty(defaultValue);
						}
					}
				}
				PropertyLabel.Text = EditorParams.DisplayName ?? EditorParams.PropertyName;
				yield return null;
			}
		}

		private static readonly Yuzu.Json.JsonSerializer serializer = new Yuzu.Json.JsonSerializer {
			JsonOptions = new Yuzu.Json.JsonSerializeOptions {
				FieldSeparator = " ",
				Indent = string.Empty,
				EnumAsString = true,
				SaveRootClass = true,
			},
		};

		private static readonly Yuzu.Json.JsonDeserializer deserializer = new Yuzu.Json.JsonDeserializer {
			JsonOptions = new Yuzu.Json.JsonSerializeOptions { EnumAsString = true },
		};

		protected virtual void Copy()
		{
			var v = CoalescedPropertyValue().GetValue();
			try {
				Clipboard.Text = Serialize(v.Value);
			} catch (System.Exception) {
			}
		}

		protected virtual void Paste()
		{
			try {
				var v = Deserialize(Clipboard.Text);
				SetProperty(v);
			} catch (System.Exception) {
			}
		}

		protected virtual string Serialize(T value) => serializer.ToString(value);
		protected virtual T Deserialize(string source) => deserializer.FromString<T>(source + ' ');

		protected void DoTransaction(Action block)
		{
			if (EditorParams.History != null) {
				using (EditorParams.History.BeginTransaction()) {
					block();
					EditorParams.History.CommitTransaction();
				}
			} else {
				block();
			}
		}

		private readonly ICommand resetToDefault = new Command("Reset To Default");

		protected virtual void FillContextMenuItems(Menu menu)
		{
			var commands = new List<ICommand>();
			if (EditorParams.DefaultValueGetter != null && Enabled) {
				commands.Insert(0, resetToDefault);
			}
			commands.Add(Command.Copy);
			if (Enabled) {
				commands.Add(Command.Paste);
			}
			menu.AddRange(commands);
		}

		private void ShowPropertyContextMenu()
		{
			var menu = new Menu { };
			FillContextMenuItems(menu);
			menu.Popup();
		}

		public virtual void DropFiles(IEnumerable<string> files) { }

		protected IDataflowProvider<T> PropertyValue(object o)
		{
			var indexParameters = EditorParams.PropertyInfo.GetIndexParameters();
			switch (indexParameters.Length)
			{
				case 0:
					return new PropertyDataflowProvider<T>(o, EditorParams.PropertyName);
				case 1 when indexParameters[0].ParameterType == typeof(int):
					return new IndexedPropertyDataflowProvider<T>(
						o, EditorParams.PropertyName, EditorParams.IndexInList
					);
				default:
					throw new NotSupportedException();
			}
		}

		protected IDataflowProvider<CoalescedValue<T>> CoalescedPropertyValue(
			T defaultValue = default,
			Func<T, T, bool> comparator = null
		) {
			var indexParameters = EditorParams.PropertyInfo.GetIndexParameters();
			var dataflow = new CoalescedDataflow<T>(defaultValue, comparator);
			switch (indexParameters.Length) {
				case 0:
					foreach (var o in EditorParams.Objects) {
						dataflow.AddDataflow(new PropertyDataflowProvider<T>(o, EditorParams.PropertyName));
					}
					return new DataflowProvider<CoalescedValue<T>>(() => dataflow);
				case 1 when indexParameters[0].ParameterType == typeof(int):
					foreach (var o in EditorParams.Objects) {
						dataflow.AddDataflow(
							new IndexedPropertyDataflowProvider<T>(
								o, EditorParams.PropertyName, EditorParams.IndexInList
							)
						);
					}
					return new DataflowProvider<CoalescedValue<T>>(() => dataflow);
				default:
					throw new NotSupportedException();
			}
		}

		protected IDataflowProvider<CoalescedValue<ComponentValue>> CoalescedPropertyComponentValue<ComponentValue>(
			Func<T, ComponentValue> selector,
			ComponentValue defaultValue = default
		) {
			var indexParameters = EditorParams.PropertyInfo.GetIndexParameters();
			var dataflow = new CoalescedDataflow<ComponentValue>(defaultValue);
			switch (indexParameters.Length) {
				case 0:
					foreach (var o in EditorParams.Objects) {
						dataflow.AddDataflow(
							new PropertyDataflowProvider<T>(o, EditorParams.PropertyName).Select(selector))
						;
					}
					return new DataflowProvider<CoalescedValue<ComponentValue>>(() => dataflow);
				case 1 when indexParameters[0].ParameterType == typeof(int):
					foreach (var o in EditorParams.Objects) {
						dataflow.AddDataflow(
							new IndexedPropertyDataflowProvider<T>(
								o,
								EditorParams.PropertyName,
								EditorParams.IndexInList
							).Select(selector)
						);
					}
					return new DataflowProvider<CoalescedValue<ComponentValue>>(() => dataflow);
				default:
					throw new NotSupportedException();
			}
		}

		protected IDataflowProvider<ComponentType> PropertyComponentValue<ComponentType>(
			object o,
			Func<T, ComponentType> selector
		) {
			var indexParameters = EditorParams.PropertyInfo.GetIndexParameters();
			switch (indexParameters.Length) {
				case 0:
					return new PropertyDataflowProvider<T>(o, EditorParams.PropertyName).Select(selector);
				case 1 when indexParameters[0].ParameterType == typeof(int):
					return new IndexedPropertyDataflowProvider<T>(
						o,
						EditorParams.PropertyName,
						EditorParams.IndexInList
					).Select(selector);
				default:
					throw new NotSupportedException();
			}
		}

		protected void SetProperty(object value)
		{
			if (IsSetterPrivate()) {
				ShowPrivateSetterAlert();
				return;
			}
			ClearWarnings();
			void ValidateAndApply(object o, object next)
			{
				var result = PropertyValidator.ValidateValue(o, next, EditorParams.PropertyInfo);
				bool errorExist = false;
				foreach (var (validationResult, message) in result) {
					if (validationResult != ValidationResult.Ok) {
						AddWarning(message, validationResult);
						errorExist = validationResult == ValidationResult.Error;
					}
				}
				if (errorExist) {
					return;
				}
				((IPropertyEditorParamsInternal)EditorParams).PropertySetter(
					o,
					EditorParams.IsAnimable ? EditorParams.PropertyPath : EditorParams.PropertyName,
					next
				);
			}
			DoTransaction(() => {
				if (EditorParams.IsAnimable) {
					foreach (var o in EditorParams.RootObjects) {
						ValidateAndApply(o, value);
					}
				} else {
					foreach (var o in EditorParams.Objects) {
						ValidateAndApply(o, value);
					}
				}
			});
		}

		protected void SetProperty<ValueType>(Func<ValueType, object> valueProducer)
		{
			if (IsSetterPrivate()) {
				ShowPrivateSetterAlert();
				return;
			}
			ClearWarnings();
			void ValidateAndApply(object o, ValueType current)
			{
				var next = valueProducer(current);
				var result = PropertyValidator.ValidateValue(o, next, EditorParams.PropertyInfo);
				bool errorExist = false;
				foreach (var (validationResult, message) in result) {
					if (validationResult != ValidationResult.Ok) {
						var messageCopy = message;
						if (!messageCopy.IsNullOrWhiteSpace() && o is Node node) {
							messageCopy = $"{node.Id}: {messageCopy}";
						}
						AddWarning(messageCopy, validationResult);
						errorExist = validationResult == ValidationResult.Error;
					}
				}
				if (errorExist) {
					return;
				}
				((IPropertyEditorParamsInternal)EditorParams).PropertySetter(
					o,
					EditorParams.IsAnimable
						? EditorParams.PropertyPath
						: EditorParams.PropertyName,
					next
				);
			}
			DoTransaction(() => {
				if (EditorParams.IsAnimable) {
					foreach (var o in EditorParams.RootObjects) {
						var (p, a, i) = AnimationUtils.GetPropertyByPath((IAnimationHost)o, EditorParams.PropertyPath);
						var current = i == -1 ? p.Info.GetValue(a) : p.Info.GetValue(a, new object[] { i });
						ValidateAndApply(o, (ValueType)current);
					}
				} else {
					foreach (var o in EditorParams.Objects) {
						var current = EditorParams.IndexInList != -1
							? new IndexedProperty(o, EditorParams.PropertyName, EditorParams.IndexInList).Value
							: new Property(o, EditorParams.PropertyName).Value;
						ValidateAndApply(o, (ValueType)current);
					}
				}
			});
		}

		protected bool SameValues()
		{
			if (!EditorParams.Objects.Any()) {
				return false;
			}
			if (!EditorParams.Objects.Skip(1).Any()) {
				return true;
			}
			var first = PropertyValue(EditorParams.Objects.First()).GetValue();
			return EditorParams.Objects.Aggregate(
				true,
				(current, o) => current && EqualityComparer<T>.Default.Equals(first, PropertyValue(o).GetValue())
			);
		}

		protected bool SameComponentValues<ComponentType>(Func<T, ComponentType> selector)
		{
			if (!EditorParams.Objects.Any()) {
				return false;
			}
			var first = PropertyComponentValue(EditorParams.Objects.First(), selector).GetValue();
			return EditorParams.Objects.Aggregate(
				true,
				(current, o) => current
					&& EqualityComparer<ComponentType>.Default
						.Equals(first, PropertyComponentValue(o, selector).GetValue())
			);
		}

		protected void ManageManyValuesOnFocusChange<U>(
			CommonEditBox editBox,
			IDataflowProvider<CoalescedValue<U>> current
		) {
			editBox.TextWidget.TextProcessor += (ref string text, Widget widget) => {
				if (!editBox.IsFocused() && !current.GetValue().IsDefined) {
					text = ManyValuesText;
				}
			};

			editBox.AddLateChangeWatcher(editBox.IsFocused, focused => {
				if (!focused && !current.GetValue().IsDefined) {
					editBox.Editor.Text.Invalidate();
				} else if (focused && !current.GetValue().IsDefined) {
					editBox.Text = string.Empty;
					editBox.Editor.Text.Invalidate();
				}
			});
		}

		public virtual void Submit()
		{ }

		protected void ClearWarningsAndValidate()
		{
			ClearWarnings();
			_ = EditorParams.IsAnimable ? EditorParams.RootObjects : EditorParams.Objects;
			foreach (var o in EditorParams.Objects) {
				var result = PropertyValidator.ValidateValue(
					owner: o,
					value: PropertyValue(o).GetValue(),
					propertyInfo: EditorParams.PropertyInfo
				);
				foreach (var (validationResult, message) in result) {
					if (validationResult != ValidationResult.Ok) {
						var messageCopy = message;
						if (!messageCopy.IsNullOrWhiteSpace() && o is Node node) {
							messageCopy = $"{node.Id}: {messageCopy}";
						}
						AddWarning(messageCopy, validationResult);
					}
				}
			}
		}

		protected bool IsSetterPrivate() => EditorParams.PropertyInfo.SetMethod.IsPrivate;

		protected void ShowPrivateSetterAlert()
		{
			new AlertDialog(
				$"Can't assign value to property '{EditorParams.PropertyInfo.Name}' because it's setter is private. " +
				$"Either make setter public or make sure property is initialized."
			).Show();
		}
	}

	public static class CommonPropertyEditor
	{
		public static Widget CreateWarning(string message, ValidationResult validationResult)
		{
			return new Widget {
				Layout = new HBoxLayout(),
				Nodes = {
					CreateWarningIcon(validationResult),
					CreateWarningText(message),
				},
			};
		}

		public static Widget CreateWarningIcon(ValidationResult validationResult)
		{
			return new Image(IconPool.GetTexture($"Inspector.{validationResult}")) {
				MinMaxSize = new Vector2(16, 16),
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
			};
		}

		public static Widget CreateWarningText(string text, bool autoMinSize = false)
		{
			return new ThemedSimpleText {
				Text = text,
				VAlignment = VAlignment.Center,
				LayoutCell = new LayoutCell(Alignment.LeftCenter),
				ForceUncutText = false,
				AutoMinSize = autoMinSize,
				Padding = new Thickness(left: 5.0f),
				TabTravesable = new TabTraversable(),
			};
		}
	}
}
