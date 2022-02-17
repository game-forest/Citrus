using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class InstancePropertyEditor<T> : ExpandablePropertyEditor<T>
	{
		public InstancePropertyEditor(
			IPropertyEditorParams editorParams,
			Action<Widget> onValueChanged
		) : base(editorParams)
		{
			var selectImplementationButton = new ThemedButton {
				LayoutCell = new LayoutCell(Alignment.Center),
				MaxSize = Vector2.PositiveInfinity,
				MinSize = Vector2.Zero,
			};
			var propertyType = typeof(T);
			var meta = Yuzu.Metadata.Meta.Get(editorParams.Type, InternalPersistence.Instance.YuzuOptions);
			selectImplementationButton.Clicked = () => {
				IMenu menu = new Menu();
				foreach (var type in GetPossibleTypes(propertyType)) {
					var tooltipText = ClassAttributes<TangerineTooltipAttribute>.Get(type, true)?.Text;
					var menuPath = ClassAttributes<TangerineMenuPathAttribute>.Get(type, true)?.Path;
					ICommand command = new Command(
						text: type.Name,
						execute: () => {
							SetProperty<object>((_) => type != null ? Activator.CreateInstance(type) : null);
						}
					) {
						TooltipText = tooltipText,
					};
					if (menuPath != null) {
						menu.InsertCommandAlongPath(command, menuPath);
					} else {
						menu.Add(command);
					}
				}
				menu.Popup();
			};
			EditorContainer.AddLateChangeWatcher(
				provider: CoalescedPropertyValue(
					comparator: (t1, t2) =>
						   t1 == null
						&& t2 == null
						|| t1 != null
						&& t2 != null
						&& t1.GetType() == t2.GetType()
				),
				action: v => {
					onValueChanged?.Invoke(ExpandableContent);
					string tooltipText = null;
					if (v.IsDefined) {
						var type = v.Value?.GetType();
						selectImplementationButton.Text = type?.Name ?? "<not set>";
						if (type != null) {
							tooltipText = ClassAttributes<TangerineTooltipAttribute>.Get(type, true)?.Text;
						}
					} else {
						selectImplementationButton.Text = ManyValuesText;
						tooltipText = "Multiple distinct values are selected.";
					}
					var ttc = selectImplementationButton.Components.GetOrAdd<TooltipComponent>();
					ttc.GetText = () => tooltipText;
				}
			);
			object defaultValue = null;
			if (!propertyType.IsInterface && !propertyType.IsAbstract && IsContainerType(editorParams.Type)) {
				defaultValue = Activator.CreateInstance(propertyType);
			} else {
				var propertyMetaItem = meta.Items.FirstOrDefault(i => i.Name == editorParams.PropertyName);
				defaultValue = propertyMetaItem?.GetValue(meta.Default);
			}
			var resetToDefaultButton = new ToolbarButton(IconPool.GetTexture("Tools.Revert")) {
				Clicked = () => SetProperty(Cloner.Clone(defaultValue)),
			};
			if (!GetPossibleTypes(propertyType).Skip(1).Any()) {
				var t = GetPossibleTypes(propertyType).First();
				var createButton = new ToolbarButton("Create") {
					TabTravesable = new TabTraversable(),
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Padding = new Thickness(left: 5.0f),
					HitTestTarget = true,
					MinWidth = 0,
					MaxWidth = float.PositiveInfinity,
				};
				createButton.Clicked = () => {
					createButton.Visible = false;
					SetProperty<object>(_ => t != null ? Activator.CreateInstance(t) : null);
					onValueChanged?.Invoke(ExpandableContent);
					Expanded = true;
				};
				var value = CoalescedPropertyValue().GetValue();
				createButton.Visible = Equals(value.Value, defaultValue);
				resetToDefaultButton.Clicked = () => {
					SetProperty(Cloner.Clone(defaultValue));
					createButton.Visible = Equals(CoalescedPropertyValue().GetValue().Value, defaultValue);
					onValueChanged?.Invoke(ExpandableContent);
				};
				EditorContainer.AddNode(createButton);
				EditorContainer.AddNode(Spacer.HStretch());
				onValueChanged?.Invoke(ExpandableContent);
			} else {
				EditorContainer.Nodes.Insert(0, selectImplementationButton);
			}
			EditorContainer.AddNode(resetToDefaultButton);
			EditorContainer.AddLateChangeWatcher(CoalescedPropertyValue(), v => {
				resetToDefaultButton.Visible = !Equals(v.Value, defaultValue);
			});
		}

		private static readonly Dictionary<Type, List<Type>> cache = new Dictionary<Type, List<Type>>();

		static InstancePropertyEditor()
		{
			Project.Opening += _ => cache.Clear();
		}

		public static bool IsContainerType(Type type)
		{
			// This function was constructed as a result of combining multiple Yuzu.Util methods.
			try {
				return
					type.IsArray ||
					yuzuContainerNames.Contains(type.Name) ||
					yuzuContainerNames.Any(name => type.GetInterface(name) != null);
			} catch (AmbiguousMatchException) {
				return false;
			}
		}

		private static readonly HashSet<string> yuzuContainerNames = new HashSet<string> {
			"ICollection",
			"ICollection`1",
			"IEnumerable`1",
			"IDictionary`2",
		};

		public static IEnumerable<Type> GetPossibleTypes(Type propertyType)
		{
			if (!cache.TryGetValue(propertyType, out List<Type> derivedTypes)) {
				HashSet<Type> typesHash = new HashSet<Type>();
				cache.Add(propertyType, derivedTypes = new List<Type>());
				if (!propertyType.IsInterface && !propertyType.IsAbstract) {
					typesHash.Add(propertyType);
				}
				foreach (var (name, assembly) in Orange.AssemblyTracker.Instance) {
					try {
						// TODO: reduce reflection usage
						var types = assembly
							.GetTypes()
							.Where(t => !t.IsInterface
								&& !t.IsAbstract
								&& ClassAttributes<TangerineIgnoreAttribute>.Get(t) == null
								&& t != propertyType
								&& propertyType.IsAssignableFrom(t)
							).ToList();
						foreach (var type in types) {
							typesHash.Add(type);
						}
					} catch (ReflectionTypeLoadException e) {
						Debug.Write($"Failed to enumerate types in '{assembly.FullName}'");
					}
				}
				derivedTypes.AddRange(typesHash);
				MenuExtensions.SortTypesByMenuPath(derivedTypes);
			}
			return derivedTypes;
		}
	}
}
