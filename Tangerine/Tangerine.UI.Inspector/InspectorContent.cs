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
		private readonly Widget rootWidget;
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
			rootWidget = widget;
			editors = new List<IPropertyEditor>();
			ReadonlyEditors = new ReadOnlyCollection<IPropertyEditor>(editors);
		}

		public void Build(IEnumerable<object> objects)
		{
			totalObjectCount = objects.Count();
			if (Widget.Focused != null && Widget.Focused.DescendantOf(rootWidget)) {
				rootWidget.SetFocus();
			}
			Clear();
			if (CoreUserPreferences.Instance.InspectEasing) {
				AddEasingEditor();
			}
			foreach (var w in BuildHelper(objects)) {
				DecorateElement(w);
				rootWidget.AddNode(w);
			}
			if (objects.Any() && objects.All(o => o is Node)) {
				var nodes = objects.Cast<Node>().ToList();
				var queriedComponents = new List<NodeComponent>();
				foreach (var t in GetComponentTypes(nodes)) {
					var nodesComponents = new List<List<NodeComponent>>();
					var maxCount = 0;
					foreach (var n in nodes) {
						var nodeComponents = new List<NodeComponent>();
						nodesComponents.Add(nodeComponents);
						queriedComponents.Clear();
						n.Components.GetAll(t, queriedComponents);
						foreach (var c in queriedComponents) {
							if (c != null && t == c.GetType()) {
								nodeComponents.Add(c);
							}
						}
						maxCount = Math.Max(nodeComponents.Count, maxCount);

					}
					for (int i = 0; i < maxCount; i++) {
						// Column slice
						var components = new List<NodeComponent>();
						var nodesWithComponent = new List<Node>();
						for (int j = 0; j < nodesComponents.Count; j++) {
							var row = nodesComponents[j];
							if (i >= row.Count) {
								continue;
							}
							components.Add(row[i]);
							nodesWithComponent.Add(nodes[j]);
						}
						var headerWidget = CreateComponentHeader(t, components);
						headerWidget.Components.Add(new HeaderElementComponent());
						DecorateElement(headerWidget);
						rootWidget.AddNode(headerWidget);
						var elementWidgets = CreateElementsForType(
							type: t,
							objects: components,
							rootObjects: nodesWithComponent,
							animableByPath: !Document.Current.InspectRootNode,
							propertyPath: SerializeMutuallyExclusiveComponentGroupBaseType(t)
						).ToList();
						foreach (var w in elementWidgets) {
							DecorateElement(w);
							rootWidget.AddNode(w);
						}
					}
				}
				AddComponentsMenu(nodes, rootWidget);
			}

			if (Footer != null) {
				rootWidget.AddNode(Footer);
			}
		}

		private void AddEasingEditor()
		{
			var marker = Inspector.FindMarkerBehind();
			if (marker != null) {
				var editor = new BezierEasingPropertyEditor(
					new PropertyEditorParams(marker, nameof(Marker.BezierEasing)) {
						ShowLabel = false,
						History = History,
						PropertySetter = SetProperty
					}
				);
				rootWidget.Nodes.Add(editor.ContainerWidget);
			}
		}

		private static IEnumerable<Type> GetComponentTypes(IReadOnlyList<Node> nodes)
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

		private static string SerializeMutuallyExclusiveComponentGroupBaseType(Type t)
		{
			var current = t;
			while (current != typeof(NodeComponent)) {
				var allowOnlyOne = ClassAttributes<AllowOnlyOneComponentAttribute>.Get(current);
				var allowMultiple = ClassAttributes<AllowMultipleComponentsAttribute>.Get(current);
				if (allowMultiple != null || allowOnlyOne != null) {
					t = allowOnlyOne == null ? t : current;
					break;
				}
				current = current.BaseType;
			}
			return $"[{Yuzu.Util.TypeSerializer.Serialize(t)}]";
		}

		private IEnumerable<Widget> BuildHelper(
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects = null,
			bool animableByPath = true,
			string propertyPath = ""
		) {
			if (objects.Any(o => o == null)) {
				yield break;
			}
			foreach (var t in GetTypes(objects)) {
				var o = objects.Where(i => t.IsInstanceOfType(i)).ToList();
				// Create elements for this type
				var elements = CreateElementsForType(
					type: t,
					objects: o,
					rootObjects: rootObjects ?? o,
					animableByPath: !Document.Current.InspectRootNode
						&& animableByPath
						&& objects.Any(_ => _ is IAnimable),
					propertyPath: propertyPath
				).ToList();
				// Create group header for block of elements belonging to the type if there's at least once element.
				if (elements.Count > 0) {
					var objectsCount = o.Count;
					string tooltipText = ClassAttributes<TangerineTooltipAttribute>.Get(t, true)?.Text;
					var text = t.Name;
					if (text == "Node" && !objects.Skip(1).Any()) {
						text += $" of type '{objects.First().GetType().Name}'";
					}
					if (totalObjectCount > 1) {
						text += $" ({objectsCount}/{totalObjectCount})";
					}
					var widgetHeader = CreateCategoryHeader(
						text: text,
						iconTexture: NodeIconPool.GetTexture(t),
						color: ColorTheme.Current.Inspector.CategoryLabelBackground,
						tooltipText: tooltipText
					);
					widgetHeader.Components.Add(new HeaderElementComponent());
					yield return widgetHeader;
				}
				foreach (var e in elements) {
					yield return e;
				}
			}
		}

		private void Clear()
		{
			row = 1;
			rootWidget.Nodes.Clear();
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

		private IEnumerable<Widget> CreateElementsForType(
			Type type,
			IEnumerable<object> objects,
			IEnumerable<object> rootObjects,
			bool animableByPath,
			string propertyPath
		) {
			var editorParams = new List<PropertyEditorParams>();
			bool isSubclassOfNodeComponent = type.IsSubclassOf(typeof(NodeComponent));
			var propertyBindingFlags = BindingFlags.Instance | BindingFlags.Public;
			if (!isSubclassOfNodeComponent) {
				propertyBindingFlags |= BindingFlags.DeclaredOnly;
			}
			var properties = type.GetProperties(propertyBindingFlags)
				.Where(p => ShouldInspectProperty(type, objects, p, isSubclassOfNodeComponent));
			if (isSubclassOfNodeComponent) {
				properties = properties
					.GroupBy(p => p.DeclaringType)
					.Reverse()
					.SelectMany(group => group);
			}
			foreach (var property in properties) {
				var @params = new PropertyEditorParams(
					objects,
					rootObjects,
					type,
					property.Name,
					string.IsNullOrEmpty(propertyPath)
						? property.Name
						: propertyPath + "." + property.Name
				) {
					NumericEditBoxFactory = () => new TransactionalNumericEditBox(History),
					History = History,
					Editable = Enabled,
					DefaultValueGetter = () => {
						var defaultValueAttribute =
							PropertyAttributes<TangerinePropertyDefaultValueAttribute>.Get(property, true);
						if (defaultValueAttribute != null) {
							return defaultValueAttribute.GetValue();
						}
						var ctr = type.GetConstructor(Array.Empty<Type>());
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
				@params.PropertySetter = @params.IsAnimable
					? (PropertySetterDelegate)SetAnimableProperty
					: SetProperty;
				editorParams.Add(@params);
			}
			string previousGroup = null;
			foreach (var p in editorParams.OrderBy(p => p.Group)) {
				var editorWidget = CreatePropertyEditor(p);
				if (editorWidget == null) {
					continue;
				}
				if (p.Group != previousGroup && !string.IsNullOrEmpty(p.Group)) {
					var headerWidget = CreateGroupHeader(p.Group);
					headerWidget.Components.Add(new HeaderElementComponent());
					previousGroup = p.Group;
					yield return headerWidget;
				}
				yield return editorWidget;
			}

			var methodBindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static;
			var methods = type.GetMethods(methodBindingFlags)
				.Where(m => MethodAttributes<TangerineCreateButtonAttribute>.Get(m, true) != null);
			foreach (var method in methods) {
				if (0 != method.GetParameters().Length || method.IsStatic) {
					throw new NotSupportedException(
						$"TangerineCreateButtonAttribute Method {method.Name} " +
						"only non-static methods with no arguments are supported!"
					);
				}
				var attribute = MethodAttributes<TangerineCreateButtonAttribute>.Get(method, true);
				var buttonWidget = new Widget {
					Layout = new HBoxLayout(),
					Anchors = Anchors.LeftRight,
					Padding = new Thickness(4, 14, 2, 2),
					Nodes = {
						new ThemedButton(attribute.Name ?? method.Name) {
							Anchors = Anchors.LeftRight,
							MaxWidth = 10000,
							Clicked = () => {
								foreach (var component in objects) {
									method.Invoke(component, null);
								}
							}
						}
					}
				};
				buttonWidget.Components.Add(new WidgetElementComponent());
				yield return buttonWidget;
			}
		}

		private static bool ShouldInspectProperty(
			Type type,
			IEnumerable<object> objects,
			PropertyInfo property,
			bool inspectOverridenVirtualProperties
		) {
			if (property.GetIndexParameters().Length > 0) {
				// we don't inspect indexers (they have "Item" name by default
				return false;
			}
			var yuzuItem = Yuzu.Metadata.Meta.Get(type, InternalPersistence.Instance.YuzuCommonOptions)
				.Items
				.Find(i => i.PropInfo == property);
			var hasKeyframeColor = PropertyAttributes<TangerineKeyframeColorAttribute>
				.Get(type, property.Name, true) != null;
			var shouldIgnore = PropertyAttributes<TangerineIgnoreAttribute>
				.Get(type, property.Name, true) != null;
			var shouldInspect = PropertyAttributes<TangerineInspectAttribute>
				.Get(type, property.Name, true) != null;
			if (!shouldInspect && (yuzuItem == null && !hasKeyframeColor || shouldIgnore)) {
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
			// if `inspectOverridenVirtualProperties` is set to `false` then we only inspect
			// virtual properies for types that introduce `YuzuMember` or `TangerineInspect`.
			if (
				!inspectOverridenVirtualProperties
				&& property.GetMethod.IsVirtual
				&& !(
					PropertyAttributes<Yuzu.YuzuMember>.Get(type, property.Name, false) != null
					|| PropertyAttributes<TangerineInspectAttribute>.Get(type, property.Name, false) != null
					|| PropertyAttributes<TangerineKeyframeColorAttribute>.Get(type, property.Name, false) != null
				)
			) {
				return false;
			}
			return true;
		}

		private Widget CreatePropertyEditor(PropertyEditorParams editorParams) {
			var p = editorParams;
			bool isPropertyRegistered = false;
			IPropertyEditor editor = null;
			foreach (var i in InspectorPropertyRegistry.Instance.Items) {
				if (i.Condition(p)) {
					isPropertyRegistered = true;
					editor = i.Builder(p);
					break;
				}
			}
			if (!isPropertyRegistered) {
				var propertyType = p.PropertyInfo.PropertyType;
				var interfaces = propertyType.GetInterfaces();
				var iListInterface = interfaces.FirstOrDefault(
					i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)
				);
				var iDictionaryInterface = interfaces.FirstOrDefault(
					i => i.IsGenericType
					&& i.GetGenericTypeDefinition() == typeof(IDictionary<,>)
					&& i.GetGenericArguments()[0] == typeof(string)
				);
				if (propertyType.IsEnum) {
					editor = CreateEnumPropertyEditor(p);
				} else if (iListInterface != null) {
					editor = CreateListPropertyEditor(p, iListInterface);
				} else if (iDictionaryInterface != null) {
					editor = CreateDictionaryPropertyEditor(p, iDictionaryInterface);
				} else if (
					(propertyType.IsClass || propertyType.IsInterface)
					&& !propertyType.GetInterfaces().Contains(typeof(IEnumerable))
				) {
					editor = CreateInstancePropertyEditor(p);
				}
			}
			if (editor != null) {
				editor.ContainerWidget.Components.Add(new EditorElementComponent(editor));
				return editor.ContainerWidget;
			} else {
				System.Console.WriteLine(
					$"[Warning] No registered property editor for `{p.Type.FullName}`"
				);
				// TODO: Should throw here when there's no registered property editor for the type.
				// If editing property is explicitly forbidden in current state it should be separate condition
				// instead of merging it into condition checking for property editor being registered.
				// And that condition should be checked earlier than invoking this method.
				return null;
			}
		}

		private IPropertyEditor CreateDictionaryPropertyEditor(PropertyEditorParams param, Type iDictionaryInterface)
		{
			var dictionaryGenericArgument = iDictionaryInterface.GetGenericArguments().ToArray();
			var genericArguments = new[] { param.PropertyInfo.PropertyType, dictionaryGenericArgument[1], };

			Widget OnAdd(PropertyEditorParams p)
			{
				var w = CreatePropertyEditor(p);
				DecorateElement(w);
				return w;
			}

			var specializedEditorType = typeof(DictionaryPropertyEditor<,>).MakeGenericType(genericArguments);
			var editor = Activator.CreateInstance(
				specializedEditorType,
				param,
				(Func<PropertyEditorParams, Widget>) OnAdd
			);
			return (IPropertyEditor)editor;
		}

		private IPropertyEditor CreateListPropertyEditor(PropertyEditorParams editorParams, Type iListInterface)
		{
			var listGenericArgument = iListInterface.GetGenericArguments().First();
			Func<PropertyEditorParams, Widget> onAdd = (p) => {
				var w = CreatePropertyEditor(p);
				DecorateElement(w);
				return w;
			};
			var specializedICollectionPropertyEditorType = typeof(ListPropertyEditor<,>).MakeGenericType(
				editorParams.PropertyInfo.PropertyType, listGenericArgument
			);
			var editor = Activator.CreateInstance(
				specializedICollectionPropertyEditorType,
				editorParams,
				onAdd
			) as IPropertyEditor;
			return editor;
		}

		private IPropertyEditor CreateInstancePropertyEditor(PropertyEditorParams param)
		{
			var instanceEditors = new List<IPropertyEditor>();
			var onValueChanged = new Action<Widget>((containerWidget) => {
				containerWidget.Nodes.Clear();
				foreach (var e in instanceEditors) {
					editors.Remove(e);
				}
				instanceEditors.Clear();
				bool allObjectsAnimable = param.Objects.All(o => o is IAnimable);
				var elementWidgets = BuildHelper(
					param.Objects.Select(
						o => param.IndexInList == -1
							? param.PropertyInfo.GetValue(o)
							: param.PropertyInfo.GetValue(o, new object[] { param.IndexInList })
						),
					param.RootObjects,
					param.IsAnimableByPath && allObjectsAnimable,
					param.PropertyPath
				).ToList();
				foreach (var w in elementWidgets) {
					DecorateElement(w);
					containerWidget.AddNode(w);
				}
				instanceEditors.AddRange(elementWidgets
					// TODO: Should use Contains instead of null check, but it's broken now.
					.Where(e => e.Components.Get<EditorElementComponent>() != null)
					.Select(e => e.Components.Get<EditorElementComponent>().Editor)
				);
			});
			Type et = typeof(InstancePropertyEditor<>).MakeGenericType(param.PropertyInfo.PropertyType);
			var editor = Activator.CreateInstance(et, param, onValueChanged) as IPropertyEditor;
			return editor;
		}

		private static IPropertyEditor CreateEnumPropertyEditor(PropertyEditorParams param)
		{
			IPropertyEditor editor;
			var specializedEnumPropertyEditorType = typeof(EnumPropertyEditor<>)
				.MakeGenericType(param.PropertyInfo.PropertyType);
			editor = Activator.CreateInstance(specializedEnumPropertyEditorType, param) as IPropertyEditor;
			return editor;
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
					!nodes.All(n => !n.Components.CanAdd(type)) &&
					nodesTypes.All(t => NodeCompositionValidator.ValidateComponentType(t, type))
				) {
					componentTypes.Add(type);
				}
			}
			IMenu CreateAddComponentsMenu(bool validateClipboard)
			{
				IMenu menu = new Menu();
				foreach (var type in componentTypes) {
					var tooltipText = ClassAttributes<TangerineTooltipAttribute>.Get(type, true)?.Text;
					var menuPath = ClassAttributes<TangerineMenuPathAttribute>.Get(type, true)?.Path;
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
							if (node.Components.CanAdd(type)) {
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
					} else if (nodes.All(n => !n.Components.CanAdd(component.GetType()))) {
						pasteFromClipboardCommand.Enabled = false;
					}
				}
				menu.Insert(0, pasteFromClipboardCommand);
				return menu;
			}
			var menu = CreateAddComponentsMenu(validateClipboard: false);
			CreatedAddComponentsMenu?.Invoke(menu);
			var label = new Widget {
				Visible = componentTypes.Count > 0,
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
								// TODO: invert dependency on Tangerine assembly
								var assembly = Orange.AssemblyTracker.Instance.GetAssemblyByName("Tangerine");
								var typeHandle = assembly.GetType("Tangerine.LookupAddComponentSection").TypeHandle;
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
			Core.Operations.SetAnimableProperty.Perform(
				obj, propertyName, value, CoreUserPreferences.Instance.AutoKeyframes
			);
		}

		private Widget CreateComponentHeader(Type type, IEnumerable<NodeComponent> components)
		{
			var text = CamelCaseToLabel(type.Name);
			var componentsCount = components.Count();
			if (totalObjectCount > 1) {
				text += $"({componentsCount}/{totalObjectCount})";
			}
			string tooltipText = ClassAttributes<TangerineTooltipAttribute>.Get(type, true)?.Text;
			var label = CreateCategoryHeader(
				text: text,
				iconTexture: ComponentIconPool.GetTexture(type),
				color: ColorTheme.Current.Inspector.ComponentHeaderLabelBackground,
				tooltipText: tooltipText
			);
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

		private static Widget CreateCategoryHeader(string text, ITexture iconTexture, Color4 color, string tooltipText)
		{
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

		private static Widget CreateGroupHeader(string text)
		{
			var header = new Widget {
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
			header.CompoundPresenter.Add(
				new WidgetFlatFillPresenter(ColorTheme.Current.Inspector.GroupHeaderLabelBackground)
			);
			return header;
		}

		private void DecorateElement(Widget widget)
		{
			var element = widget.Components.Get<InspectorElementComponent>();
			if (element == null) {
				throw new InvalidOperationException("Must have InspectorElementComponent");
			}
			switch (element) {
				case EditorElementComponent editorElement:
					DecoratePropertyEditor(editorElement.Editor, row++);
					break;
				case HeaderElementComponent header:
					break;
				case WidgetElementComponent widgetElement:
					break;
			}
		}

		private void DecoratePropertyEditor(IPropertyEditor editor, int row)
		{
			editor.Enabled = Enabled;
			editors.Add(editor);
			var param = editor.EditorParams;
			var showCondition = PropertyAttributes<TangerineIgnoreIfAttribute>
				.Get(param.Type, param.PropertyInfo.Name);
			if (showCondition != null) {
				editor.ContainerWidget.Updated += (delta) => {
					editor.ContainerWidget.Visible = !showCondition.Check(param.Objects.First());
				};
			}
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
					if (!node.Components.CanAdd(type)) {
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

		[AllowOnlyOneComponent]
		private class InspectorElementComponent : NodeComponent
		{
		}

		private class EditorElementComponent : InspectorElementComponent
		{
			public IPropertyEditor Editor { get; private set; }

			public EditorElementComponent(IPropertyEditor editor)
			{
				Editor = editor;
			}
		}

		private class HeaderElementComponent : InspectorElementComponent
		{
			public HeaderElementComponent()
			{
			}
		}

		private class WidgetElementComponent : InspectorElementComponent
		{
			public WidgetElementComponent()
			{
			}
		}
	}
}
