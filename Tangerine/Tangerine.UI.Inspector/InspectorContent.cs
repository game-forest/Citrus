using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using System.Collections;
using System.Collections.ObjectModel;
using Tangerine.UI.PropertyEditors;

namespace Tangerine.UI.Inspector
{
	public class InspectorContent
	{
		private readonly List<IPropertyEditor> editors;
		private readonly Widget widget;
		private int row = 1;
		private int totalObjectCount;
		public event Action<NodeComponent> OnComponentRemove;
		public DocumentHistory History { get; set; }
		public Widget Footer { get; set; }
		public readonly IReadOnlyList<IPropertyEditor> ReadonlyEditors;

		public event Action<IMenu> CreatedAddComponentsMenu;

		public static event Action CreateLookupForAddComponent;

		private bool enabled = true;
		public bool Enabled
		{
			get => enabled;
			set
			{
				if (enabled != value) {
					enabled = value;
					editors.ForEach(e => e.Enabled = enabled);
				}
			}
		}

		public InspectorContent(Widget widget)
		{
			this.widget = widget;
			editors = new List<IPropertyEditor>();
			ReadonlyEditors = new ReadOnlyCollection<IPropertyEditor>(editors);
		}

		public void BuildForObjects(IEnumerable<object> objects)
		{
			SaveExpandedStates();
			totalObjectCount = objects.Count();
			if (Widget.Focused != null && Widget.Focused.DescendantOf(widget)) {
				widget.SetFocus();
			}
			Clear();
			if (CoreUserPreferences.Instance.InspectEasing) {
				AddEasingEditor();
			}
			foreach (var _ in BuildForObjectsHelper(objects)) {
			}
			if (objects.Any() && objects.All(o => o is Node)) {
				var nodes = objects.Cast<Node>().ToList();
				foreach (var t in GetComponentsTypes(nodes)) {
					var components = new List<NodeComponent>();
					var nodesWithComponent = new List<Node>();
					foreach (var n in nodes) {
						var c = n.Components.Get(t);
						if (c != null && t.IsAssignableFrom(c.GetType())) {
							components.Add(c);
							nodesWithComponent.Add(n);
						}
					}
					PopulateContentForType(
						type: t,
						objects: components,
						rootObjects: nodesWithComponent,
						animableByPath: !Document.Current.InspectRootNode,
						widget: widget,
						propertyPath: SerializeMutuallyExclusiveComponentGroupBaseType(t)
					).ToList();
				}
				AddComponentsMenu(nodes, widget);
			}

			if (Footer != null) {
				widget.AddNode(Footer);
			}
			LoadExpandedStates();
		}

		private void AddEasingEditor()
		{
			var marker = Inspector.FindMarkerBehind();
			if (marker != null) {
				new BezierEasingPropertyEditor(new PropertyEditorParams(widget, marker, nameof(Marker.BezierEasing)) {
					ShowLabel = false,
					History = History,
					PropertySetter = SetProperty
				});
			}
		}

		private string SerializeMutuallyExclusiveComponentGroupBaseType(Type t)
		{
			var current = t;
			while (current != typeof(NodeComponent)) {
				if (current.IsDefined(typeof(MutuallyExclusiveDerivedComponentsAttribute), false)) {
					t = current;
					break;
				}
				current = current.BaseType;
			}
			return $"[{Yuzu.Util.TypeSerializer.Serialize(t)}]";
		}

		private IEnumerable<IPropertyEditor> BuildForObjectsHelper(
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects = null,
			bool animableByPath = true,
			Widget widget = null,
			string propertyPath = ""
		) {
			if (widget == null) {
				widget = this.widget;
			}
			if (objects.Any(o => o == null)) {
				yield break;
			}
			foreach (var t in GetTypes(objects)) {
				var o = objects.Where(i => t.IsInstanceOfType(i)).ToList();
				foreach (var e in PopulateContentForType(
					type: t,
					objects: o,
					rootObjects: rootObjects ?? o,
					animableByPath: !Document.Current.InspectRootNode && animableByPath && objects.Any(_ => _ is IAnimable),
					widget: widget,
					propertyPath: propertyPath
				)) {
					yield return e;
				}
			}
		}

		private void Clear()
		{
			row = 1;
			widget.Nodes.Clear();
			editors.Clear();
		}

		public void DropFiles(List<string> files)
		{
			var nodeUnderMouse = WidgetContext.Current.NodeUnderMouse;
			foreach (var e in editors) {
				if (nodeUnderMouse != null && nodeUnderMouse.SameOrDescendantOf(e.ContainerWidget)) {
					e.DropFiles(files);
					files.Clear();
					break;
				}
			}
		}

		public static IEnumerable<Type> GetTypes(IEnumerable<object> objects)
		{
			var types = new List<Type>();
			foreach (var o in objects) {
				var inheritanceList = new List<Type>();
				for (var t = o.GetType(); t != typeof(object); t = t.BaseType) {
					inheritanceList.Add(t);
					if (t.IsSubclassOf(typeof(NodeComponent))) {
						break;
					}
				}
				inheritanceList.Reverse();
				foreach (var t in inheritanceList) {
					if (!types.Contains(t)) {
						types.Add(t);
					}
				}
			}
			return types;
		}

		private bool ShouldInspectProperty(Type type, IEnumerable<object> objects, PropertyInfo property)
		{
			if (property.GetIndexParameters().Length > 0) {
				// we dont inspect indexers (they have "Item" name by default
				return false;
			}
			var yuzuItem = Yuzu.Metadata.Meta.Get(type, InternalPersistence.Instance.YuzuCommonOptions).Items.Find(i => i.PropInfo == property);
			var tang = PropertyAttributes<TangerineKeyframeColorAttribute>.Get(type, property.Name);
			var tangIgnore = PropertyAttributes<TangerineIgnoreAttribute>.Get(type, property.Name);
			var tangInspect = PropertyAttributes<TangerineInspectAttribute>.Get(type, property.Name);
			if (tangInspect == null && (yuzuItem == null && tang == null || tangIgnore != null)) {
				return false;
			}
			if (type.IsSubclassOf(typeof(Node))) {
				// Root must be always visible
				if (Document.Current.InspectRootNode && property.Name == nameof(Widget.Visible)) {
					return false;
				}
			}
			if (objects.Any(obj =>
				obj is IPropertyLocker propertyLocker &&
				propertyLocker.IsPropertyLocked(property.Name,
					(obj is Node node && !string.IsNullOrEmpty(node.ContentsPath)) ||
					(obj is NodeComponent component && !string.IsNullOrEmpty(component.Owner?.ContentsPath))
				)
			)) {
				return false;
			}
			return true;
		}

		private IEnumerable<IPropertyEditor> PopulateContentForType(
			Type type,
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects,
			bool animableByPath,
			Widget widget,
			string propertyPath
		) {
			var categoryLabelAdded = false;
			var editorParams = new Dictionary<string, List<PropertyEditorParams>>();
			bool isSubclassOfNode = type.IsSubclassOf(typeof(Node));
			bool isSubclassOfNodeComponent = type.IsSubclassOf(typeof(NodeComponent));
			var objectsCount = objects.Count();
			if (isSubclassOfNodeComponent) {
				var label = CreateComponentLabel(type, objects.Cast<NodeComponent>());
				if (label != null) {
					widget.AddNode(label);
				}
			}
			var bindingFlags = BindingFlags.Instance | BindingFlags.Public;
			if (!isSubclassOfNodeComponent) {
				bindingFlags |= BindingFlags.DeclaredOnly;
			}
			foreach (var property in type.GetProperties(bindingFlags)) {
				if (!ShouldInspectProperty(type, objects, property)) {
					continue;
				}
				if (isSubclassOfNode && !categoryLabelAdded) {
					categoryLabelAdded = true;
					string tooltipText = type.GetCustomAttribute<TangerineTooltipAttribute>()?.Text;
					var text = type.Name;
					if (text == "Node" && !objects.Skip(1).Any()) {
						text += $" of type '{objects.First().GetType().Name}'";
					}
					if (totalObjectCount > 1) {
						text += $" ({objectsCount}/{totalObjectCount})";
					}
					var label = CreateCategoryLabel(
						text: text,
						iconTexture: NodeIconPool.GetTexture(type),
						color: ColorTheme.Current.Inspector.CategoryLabelBackground,
						tooltipText: tooltipText
					);
					if (label != null) {
						widget.AddNode(label);
					}
				}
				var @params = new PropertyEditorParams(widget, objects, rootObjects, type, property.Name,
					string.IsNullOrEmpty(propertyPath)
						? property.Name
						: propertyPath + "." + property.Name
				) {
					NumericEditBoxFactory = () => new TransactionalNumericEditBox(History),
					History = History,
					Editable = Enabled,
					DefaultValueGetter = () => {
						var defaultValueAttribute = type.GetProperty(property.Name).GetCustomAttribute<TangerinePropertyDefaultValueAttribute>();
						if (defaultValueAttribute != null) {
							return defaultValueAttribute.GetValue();
						}

						var ctr = type.GetConstructor(new Type[] {});
						if (ctr == null) {
							return null;
						}
						var obj = ctr.Invoke(null);
						var prop = type.GetProperty(property.Name);
						return prop.GetValue(obj);
					},
					IsAnimableByPath = animableByPath,
					DisplayName = PropertyAttributes<TangerineDisplayNameAttribute>.Get(property)?.DisplayName,
				};
				@params.PropertySetter = @params.IsAnimable ? (PropertySetterDelegate)SetAnimableProperty : SetProperty;
				if (!editorParams.Keys.Contains(@params.Group)) {
					editorParams.Add(@params.Group, new List<PropertyEditorParams>());
				}
				editorParams[@params.Group].Add(@params);
			}

			foreach (var propertyEditor in PopulatePropertyEditors(type, objects, rootObjects, widget, editorParams)) {
				propertyEditor.Enabled = Enabled;
				yield return propertyEditor;
			}

			var methodBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
			foreach (var method in type.GetMethods(methodBindingFlags)) {
				if (!Attribute.IsDefined(method, typeof(TangerineCreateButtonAttribute))) {
					continue;
				}
				if (0 != method.GetParameters().Length || method.IsStatic) {
					throw new NotSupportedException(
						$"TangerineCreateButtonAttribute Method {method.Name} " +
						"only non-static methods with no arguments are supported!");
				}
				var attributeType = typeof(TangerineCreateButtonAttribute);
				var attribute = Attribute.GetCustomAttribute(method, attributeType);
				var typedAttribute = (TangerineCreateButtonAttribute)attribute;
				widget.AddNode(new Widget {
					Layout = new HBoxLayout(),
					Anchors = Anchors.LeftRight,
					Padding = new Thickness(4, 14, 2, 2),
					Nodes = {
						new ThemedButton(typedAttribute.Name ?? method.Name) {
							Anchors = Anchors.LeftRight,
							MaxWidth = 10000,
							Clicked = () => {
								foreach (var component in objects) {
									method.Invoke(component, null);
								}
							}
						}
					}
				});
			}
		}

		private IEnumerable<IPropertyEditor> PopulatePropertyEditors(
			Type type,
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects,
			Widget widget,
			Dictionary<string, List<PropertyEditorParams>> editorParams
		) {
			foreach (var header in editorParams.Keys.OrderBy((s) => s)) {
				AddGroupHeader(header, widget);
				foreach (var param in editorParams[header]) {
					bool isPropertyRegistered = false;
					IPropertyEditor editor = null;
					foreach (var i in InspectorPropertyRegistry.Instance.Items) {
						if (i.Condition(param)) {
							isPropertyRegistered = true;
							editor = i.Builder(param);
							break;
						}
					}

					if (!isPropertyRegistered) {
						var propertyType = param.PropertyInfo.PropertyType;
						var interfaces = propertyType.GetInterfaces();
						var iListInterface = interfaces.FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
						var iDictionaryInterface = interfaces.FirstOrDefault(i =>
							i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>) &&
							i.GetGenericArguments()[0] == typeof(string)
						);
						if (propertyType.IsEnum) {
							editor = CreateEditorForEnum(param);
						} else if (iListInterface != null) {
							editor = PopulateEditorsForListType(objects, rootObjects, param, iListInterface);
						} else if (iDictionaryInterface != null) {
							editor = PopulateEditorsForDictionaryType(objects, rootObjects, param, iDictionaryInterface);
						} else if ((propertyType.IsClass || propertyType.IsInterface) && !propertyType.GetInterfaces().Contains(typeof(IEnumerable))) {
							editor = PopulateEditorsForInstanceType(objects, rootObjects, param);
						}
					}

					if (editor != null) {
						editor.Enabled = Enabled;
						DecoratePropertyEditor(editor, row++);
						editors.Add(editor);
						var showCondition = PropertyAttributes<TangerineIgnoreIfAttribute>.Get(type, param.PropertyInfo.Name);
						if (showCondition != null) {
							editor.ContainerWidget.Updated += (delta) => {
								editor.ContainerWidget.Visible = !showCondition.Check(param.Objects.First());
							};
						}
						yield return editor;
					}
				}
			}
		}

		private IPropertyEditor PopulateEditorsForDictionaryType(
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects,
			PropertyEditorParams param,
			Type iDictionaryInterface
		) {
			var dictionaryGenericArgument = iDictionaryInterface.GetGenericArguments().ToArray();
			var genericArguments = new[] { param.PropertyInfo.PropertyType, dictionaryGenericArgument[1], };

			IEnumerable<IPropertyEditor> OnAdd(Type type, PropertyEditorParams p, Widget w, object list)
			{
				return PopulatePropertyEditors(
					type: type,
					objects: new[] { list },
					rootObjects: rootObjects,
					widget: w,
					editorParams: new Dictionary<string, List<PropertyEditorParams>> { { "", new List<PropertyEditorParams> { p } } }
				);
			}

			var specializedEditorType = typeof(DictionaryPropertyEditor<,>).MakeGenericType(genericArguments);
			var editor = Activator.CreateInstance(
				specializedEditorType,
				param,
				(Func<Type, PropertyEditorParams, Widget, object, IEnumerable<IPropertyEditor>>) OnAdd
			);
			return (IPropertyEditor)editor;
		}

		private IPropertyEditor PopulateEditorsForListType(
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects,
			PropertyEditorParams param,
			Type iListInterface
		) {
			var listGenericArgument = iListInterface.GetGenericArguments().First();
			Func<PropertyEditorParams, Widget, IList, IEnumerable<IPropertyEditor>> onAdd = (p, w, list) => {
				return PopulatePropertyEditors(param.PropertyInfo.PropertyType, new[] {list}, rootObjects, w,
					new Dictionary<string, List<PropertyEditorParams>> {{"", new List<PropertyEditorParams> {p}}});
			};
			var specializedICollectionPropertyEditorType =
				typeof(ListPropertyEditor<,>)
				.MakeGenericType(param.PropertyInfo.PropertyType, listGenericArgument);
			var editor = Activator.CreateInstance(specializedICollectionPropertyEditorType, param, onAdd) as IPropertyEditor;
			return editor;
		}

		private IPropertyEditor PopulateEditorsForInstanceType(
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects,
			PropertyEditorParams param
		) {
			var instanceEditors = new List<IPropertyEditor>();
			var onValueChanged = new Action<Widget>((w) => {
				w.Nodes.Clear();
				foreach (var e in instanceEditors) {
					editors.Remove(e);
				}

				instanceEditors.Clear();
				bool allObjectsAnimable = objects.All(o => o is IAnimable);
				instanceEditors.AddRange(BuildForObjectsHelper(objects.Select(
						o => param.IndexInList == -1 ? param.PropertyInfo.GetValue(o) : param.PropertyInfo.GetValue(o, new object[] {param.IndexInList})),
					rootObjects,
					param.IsAnimableByPath && allObjectsAnimable,
					w,
					param.PropertyPath));
			});
			Type et = typeof(InstancePropertyEditor<>).MakeGenericType(param.PropertyInfo.PropertyType);
			var editor = Activator.CreateInstance(et, param, onValueChanged) as IPropertyEditor;
			return editor;
		}

		private static IPropertyEditor CreateEditorForEnum(PropertyEditorParams param)
		{
			IPropertyEditor editor;
			var specializedEnumPropertyEditorType = typeof(EnumPropertyEditor<>).MakeGenericType(param.PropertyInfo.PropertyType);
			editor = Activator.CreateInstance(specializedEnumPropertyEditorType, param) as IPropertyEditor;
			return editor;
		}

		private static IEnumerable<Type> GetComponentsTypes(IReadOnlyList<Node> nodes)
		{
			var types = new List<Type>();
			foreach (var node in nodes) {
				foreach (var component in node.Components) {
					var type = component.GetType();
					if (type.IsDefined(typeof(TangerineRegisterComponentAttribute), true)) {
						if (!types.Contains(type)) {
							types.Add(type);
						}
					}
				}
			}
			return types;
		}

		private void AddComponentsMenu(IReadOnlyList<Node> nodes, Widget widget)
		{
			if (nodes.Any(n => !string.IsNullOrEmpty(n.ContentsPath))) {
				CreatedAddComponentsMenu?.Invoke(null);
				return;
			}
			var nodesTypes = nodes.Select(n => n.GetType()).ToList();
			var componentTypes = new List<Type>();
			foreach (var type in Project.Current.RegisteredComponentTypes) {
				if (
					!nodes.All(n => n.Components.Contains(type)) &&
					nodesTypes.All(t => NodeCompositionValidator.ValidateComponentType(t, type))
					) {
					componentTypes.Add(type);
				}
			}
			IMenu CreateAddComponentsMenu(bool validateClipboard)
			{
				IMenu menu = new Menu();
				foreach (var type in componentTypes) {
					var tooltipText = type.GetCustomAttribute<TangerineTooltipAttribute>()?.Text;
					var menuPath = type.GetCustomAttribute<TangerineMenuPathAttribute>()?.Path;
					ICommand command = new Command(CamelCaseToLabel(type.Name), () => CreateComponent(type, nodes)) {
						TooltipText = tooltipText
					};
					if (menuPath != null) {
						menu.InsertCommandAlongPath(command, menuPath);
					} else {
						menu.Add(command);
					}
				}
				menu.Insert(0, Command.MenuSeparator);
				NodeComponent CreateComponentFromClipboard() {
					var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(Clipboard.Text));
					try {
						return InternalPersistence.Instance.ReadObject<NodeComponent>(Document.Current.Path, stream);
					} catch {
						return null;
					}
				}
				var pasteFromClipboardCommand = new Command("Paste", () => {
					var component = CreateComponentFromClipboard();
					if (component == null) {
						new AlertDialog("Clipboard does not contain a component.", "Ok").Show();
						return;
					}
					var type = component.GetType();
					if (!componentTypes.Contains(type)) {
						new AlertDialog($"Component of type {type} can't be added", "Ok").Show();
						return;
					}
					using (Document.Current.History.BeginTransaction()) {
						foreach (var node in nodes) {
							if (!node.Components.Contains(type)) {
								SetComponent.Perform(node, Cloner.Clone(component));
							}
						}
						Document.Current.History.CommitTransaction();
					}
				});
				if (validateClipboard) {
					var component = CreateComponentFromClipboard();
					if (component == null) {
						pasteFromClipboardCommand.Enabled = false;
					} else if (!componentTypes.Contains(component.GetType())) {
						pasteFromClipboardCommand.Enabled = false;
					} else if (nodes.All(n => n.Components.Contains(component.GetType()))) {
						pasteFromClipboardCommand.Enabled = false;
					}
				}
				menu.Insert(0, pasteFromClipboardCommand);
				return menu;
			}
			var menu = CreateAddComponentsMenu(validateClipboard: false);
			CreatedAddComponentsMenu?.Invoke(menu);
			var label = new Widget {
				LayoutCell = new LayoutCell { StretchY = 0 },
				Layout = new HBoxLayout(),
				MinHeight = Theme.Metrics.DefaultButtonSize.Y,
				Nodes = {
					new ThemedAddButton {
						Clicked = () => CreateAddComponentsMenu(validateClipboard: true).Popup(),
						Enabled = componentTypes.Count > 0
					},
					new IconRedChannelToColorButton("Universal.ZoomIn", 20 * Vector2.One, Thickness.Zero) {
						DefaultColor = ColorTheme.Current.IsDark ?
							Color4.White.Darken(0.3f) : Color4.Black.Lighten(0.3f),
						HoverColor = ColorTheme.Current.IsDark ?
							Color4.White : Color4.Black,
						Clicked = () => {
							if (CreateLookupForAddComponent == null) {
								var typeHandle = AppDomain.CurrentDomain.GetAssemblies()
									.First(a => string.Equals(a.GetName().Name, "Tangerine"))
									.GetType("Tangerine.LookupAddComponentSection").TypeHandle;
								System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeHandle);
							}
							CreateLookupForAddComponent.Invoke();
						},
						Enabled = componentTypes.Count > 0
					},
					new ThemedSimpleText {
						Text = "Add Component",
						Padding = new Thickness(4, 0),
						VAlignment = VAlignment.Center,
						ForceUncutText = false,
					}
				}
			};
			label.CompoundPresenter.Add(new WidgetFlatFillPresenter(ColorTheme.Current.Inspector.CategoryLabelBackground));
			widget.AddNode(label);
		}

		private void SetProperty(object obj, string propertyName, object value)
		{
			Core.Operations.SetProperty.Perform(obj, propertyName, value);
		}

		private void SetAnimableProperty(object obj, string propertyName, object value)
		{
			Core.Operations.SetAnimableProperty.Perform(obj, propertyName, value, CoreUserPreferences.Instance.AutoKeyframes);
		}

		private Widget CreateCategoryLabel(string text, ITexture iconTexture, Color4 color, string tooltipText)
		{
			if (string.IsNullOrEmpty(text)) {
				return null;
			}
			var label = new Widget {
				LayoutCell = new LayoutCell { StretchY = 0 },
				Layout = new HBoxLayout(),
				MinHeight = Theme.Metrics.DefaultButtonSize.Y,
				HitTestTarget = tooltipText != null,
				Nodes = {
					new Image(iconTexture) {
						MinMaxSize = new Vector2(21, 19),
						Padding = new Thickness { LeftTop = new Vector2(5, 3) },
					},
					new ThemedSimpleText {
						Text = text,
						Padding = new Thickness(4, 0),
						VAlignment = VAlignment.Center,
						ForceUncutText = false,
					}
				}
			};
			label.CompoundPresenter.Add(new WidgetFlatFillPresenter(color));
			if (tooltipText != null) {
				label.Components.Add(new TooltipComponent(() => tooltipText));
			}
			return label;
		}

		private Widget CreateComponentLabel(Type type, IEnumerable<NodeComponent> components)
		{
			var text = CamelCaseToLabel(type.Name);
			var componentsCount = components.Count();
			if (totalObjectCount > 1) {
				text += $"({componentsCount}/{totalObjectCount})";
			}
			string tooltipText = type.GetCustomAttribute<TangerineTooltipAttribute>()?.Text;
			var label = CreateCategoryLabel(text, ComponentIconPool.GetTexture(type), ColorTheme.Current.Inspector.ComponentHeaderLabelBackground, tooltipText);
			label.Padding += new Thickness { Right = 10 };
			label.Nodes.Add(new ToolbarButton(IconPool.GetTexture("Inspector.Options")) {
				LayoutCell = new LayoutCell(Alignment.Center),
				Clicked = () => {
					var menu = new Menu();
					if (componentsCount == 1) {
						menu.Add(new Command("Cut", () => {
							var stream = new System.IO.MemoryStream();
							InternalPersistence.Instance.WriteObject(
								Document.Current.Path,
								stream,
								Cloner.Clone(components.First()),
								Persistence.Format.Json
							);
							Clipboard.Text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
							RemoveComponents(components);
						}));
						menu.Add(new Command("Copy", () => {
							var stream = new System.IO.MemoryStream();
							InternalPersistence.Instance.WriteObject(
								Document.Current.Path,
								stream,
								Cloner.Clone(components.First()),
								Persistence.Format.Json
							);
							Clipboard.Text = System.Text.Encoding.UTF8.GetString(stream.ToArray());
						}));
					}
					menu.Add(new Command("Remove", () => RemoveComponents(components)));
					menu.Popup();
				}
			});
			return label;
		}

		private void AddGroupHeader(string text, Widget widget)
		{
			if (string.IsNullOrEmpty(text)) {
				return;
			}

			var label = new Widget {
				LayoutCell = new LayoutCell { StretchY = 0 },
				Layout = new StackLayout(),
				MinHeight = Theme.Metrics.DefaultButtonSize.Y,
				Nodes = {
					new ThemedSimpleText {
						Text = text,
						Padding = new Thickness(12, 0),
						VAlignment = VAlignment.Center,
						ForceUncutText = false,
					}
				}
			};
			label.CompoundPresenter.Add(new WidgetFlatFillPresenter(ColorTheme.Current.Inspector.GroupHeaderLabelBackground));
			widget.AddNode(label);
		}

		private static void DecoratePropertyEditor(IPropertyEditor editor, int row)
		{
			var index = 0;
			if (editor.EditorParams.IsAnimable) {
				var keyColor = KeyframePalette.Colors[editor.EditorParams.TangerineAttribute.ColorIndex];
				var allowedKeyFunctions = PropertyAttributes<TangerineKeyframeInterpolationAttribute>
					.Get(editor.EditorParams.PropertyInfo)?.KeyframeInterpolations;
				var keyframeButton = new KeyframeButton {
					LayoutCell = new LayoutCell(Alignment.LeftCenter, stretchX: 0),
					KeyColor = keyColor,
					AllowedKeyFunctions = allowedKeyFunctions,
				};
				keyframeButton.Clicked += editor.PropertyLabel.SetFocus;
				editor.LabelContainer.Nodes.Insert(index++, keyframeButton);
				editor.ContainerWidget.Tasks.Add(new KeyframeButtonBinding(editor.EditorParams, keyframeButton, editor));
				if (editor.EditorParams.IsAnimableWithEasing) {
					editor.LabelContainer.Nodes.Insert(index++, Spacer.HSpacer(2));
					var easingButton = new EasingButton {
						LayoutCell = new LayoutCell(Alignment.LeftCenter, stretchX: 0),
					};
					editor.LabelContainer.Nodes.Insert(index++, easingButton);
					editor.ContainerWidget.Tasks.Add(new EasingButtonBinding(editor.EditorParams, easingButton));
				}
			}
			editor.ContainerWidget.Padding = new Thickness { Left = 4, Top = 3, Right = 12, Bottom = 4 };
			editor.ContainerWidget.CompoundPresenter.Add(new WidgetFlatFillPresenter(
				row % 2 == 0 ?
				ColorTheme.Current.Inspector.StripeBackground1 :
				ColorTheme.Current.Inspector.StripeBackground2
			) { IgnorePadding = true });
			editor.ContainerWidget.Components.Add(new DocumentationComponent(editor.EditorParams.PropertyInfo.DeclaringType.Name + "." + editor.EditorParams.PropertyName));
		}

		private static string CamelCaseToLabel(string text)
		{
			return Regex.Replace(Regex.Replace(text, @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
		}

		private static void CreateComponent(Type type, IEnumerable<Node> nodes)
		{
			var constructor = type.GetConstructor(Type.EmptyTypes);
			if (constructor == null) {
				throw new InvalidOperationException(
					$"Error: can't create component of type `{type.FullName}` because " +
					$"it's has no default constructor or it is not public."
				);
			}
			using (Document.Current.History.BeginTransaction()) {
				foreach (var node in nodes) {
					if (node.Components.Contains(type)) {
						continue;
					}

					var component = (NodeComponent)constructor.Invoke(new object[] { });
					SetComponent.Perform(node, component);
				}
				Document.Current.History.CommitTransaction();
			}
		}

		private void RemoveComponents(IEnumerable<NodeComponent> components)
		{
			using (Document.Current.History.BeginTransaction()) {
				foreach (var c in components) {
					if (c.Owner != null) {
						DeleteComponent.Perform(c.Owner, c);
					}
					OnComponentRemove?.Invoke(c);
				}
				Document.Current.History.CommitTransaction();
			}
		}

		public void SaveExpandedStates()
		{
			foreach (var editor in editors) {
				if (editor is IExpandablePropertyEditor expandable) {
					CoreUserPreferences.Instance.InspectorExpandableEditorsState[
						((IPropertyEditor)expandable).EditorParams.PropertyPath] = expandable.Expanded;
				}
			}
		}

		public void LoadExpandedStates()
		{
			foreach (var editor in editors) {
				if (editor is IExpandablePropertyEditor expandable) {
					expandable.Expanded =
						CoreUserPreferences.Instance.InspectorExpandableEditorsState.TryGetValue(
							((IPropertyEditor) expandable).EditorParams.PropertyPath, out var expanded) && expanded;
				}
			}
		}

		public class TransactionalNumericEditBox : ThemedNumericEditBox
		{
			public TransactionalNumericEditBox(DocumentHistory history)
			{
				BeginSpin += () => history.BeginTransaction();
				EndSpin += () => {
					history.CommitTransaction();
					history.EndTransaction();
				};
				Submitted += s => {
					if (history.IsTransactionActive) {
						history.RollbackTransaction();
					}
				};
			}
		}
	}
}
