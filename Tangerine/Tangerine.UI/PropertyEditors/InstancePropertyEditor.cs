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
		public InstancePropertyEditor(IPropertyEditorParams editorParams, Action<Widget> onValueChanged) : base(editorParams)
		{
			var selectImplementationButton = new ThemedButton {
				LayoutCell = new LayoutCell(Alignment.Center),
				MaxSize = Vector2.PositiveInfinity,
				MinSize = Vector2.Zero,
			};
			var propertyType = typeof(T);
			var meta = Yuzu.Metadata.Meta.Get(editorParams.Type, InternalPersistence.Instance.YuzuCommonOptions);
			selectImplementationButton.Clicked = () => {
				IMenu menu = new Menu();
				foreach (var type in GetPossibleTypes(propertyType)) {
					var tooltipText = type.GetCustomAttribute<TangerineTooltipAttribute>()?.Text;
					var menuPath = type.GetCustomAttribute<TangerineMenuPathAttribute>()?.Path;
					ICommand command = new Command(
						type.Name,
						() => SetProperty<object>((_) => type != null ? Activator.CreateInstance(type) : null)
					) {
						TooltipText = tooltipText
					};
					if (menuPath != null) {
						menu.InsertCommandAlongPath(command, menuPath);
					} else {
						menu.Add(command);
					}
				}
				menu.Popup();
			};
			EditorContainer.AddChangeLateWatcher(
				CoalescedPropertyValue(
					comparator: (t1, t2) =>
						   t1 == null
						&& t2 == null
						|| t1 != null
						&& t2 != null
						&& t1.GetType() == t2.GetType()
				),
				v => {
					onValueChanged?.Invoke(ExpandableContent);
					string tooltipText;
					if (v.IsDefined) {
						var type = v.Value?.GetType();
						selectImplementationButton.Text = type?.Name ?? "<not set>";
						tooltipText = type?.GetCustomAttribute<TangerineTooltipAttribute>()?.Text ?? null;
					} else {
						selectImplementationButton.Text = ManyValuesText;
						tooltipText = "Multiple distinct values are selected.";
					}
					var ttc = selectImplementationButton.Components.GetOrAdd<TooltipComponent>();
					ttc.GetText = () => tooltipText;
				}
			);
			var propertyMetaItem = meta.Items.FirstOrDefault(i => i.Name == editorParams.PropertyName);
			var defaultValue = propertyMetaItem?.GetValue(meta.Default);
			var resetToDefaultButton = new ToolbarButton(IconPool.GetTexture("Tools.Revert")) {
				Clicked = () => SetProperty(Cloner.Clone(defaultValue))
			};
			if (!GetPossibleTypes(propertyType).Skip(1).Any()) {
				var t = GetPossibleTypes(propertyType).First();
				var b = new ToolbarButton("Create") {
					TabTravesable = new TabTraversable(),
					LayoutCell = new LayoutCell(Alignment.LeftCenter),
					Padding = new Thickness(left: 5.0f),
					HitTestTarget = true,
					MinWidth = 0,
					MaxWidth = float.PositiveInfinity
				};
				b.Clicked = () => {
					b.Visible = false;
					SetProperty<object>(_ => t != null ? Activator.CreateInstance(t) : null);
					onValueChanged?.Invoke(ExpandableContent);
					Expanded = true;
				};
				var value = CoalescedPropertyValue().GetValue();
				b.Visible = Equals(value.Value, defaultValue);
				resetToDefaultButton.Clicked = () => {
					b.Visible = true;
					SetProperty(defaultValue);
					onValueChanged?.Invoke(ExpandableContent);
				};
				EditorContainer.AddNode(b);
				EditorContainer.AddNode(Spacer.HStretch());
				onValueChanged?.Invoke(ExpandableContent);
			} else {
				EditorContainer.Nodes.Insert(0, selectImplementationButton);
			}
			EditorContainer.AddNode(resetToDefaultButton);
			EditorContainer.AddChangeLateWatcher(CoalescedPropertyValue(), v => {
				resetToDefaultButton.Visible = !Equals(v.Value, defaultValue);
			});
		}

		private static readonly Dictionary<Type, List<Type>> cache = new Dictionary<Type, List<Type>>();

		static InstancePropertyEditor()
		{
			Project.Opening += _ => cache.Clear();
		}

		public static IEnumerable<Type> GetPossibleTypes(Type propertyType)
		{
			if (!cache.TryGetValue(propertyType, out List<Type> derivedTypes)) {
				HashSet<Type> typesHash = new HashSet<Type>();
				cache.Add(propertyType, derivedTypes = new List<Type>());
				if (!propertyType.IsInterface && !propertyType.IsAbstract) {
					typesHash.Add(propertyType);
				}
				foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
					try {
						var types = assembly
							.GetTypes()
							.Where(t =>
								!t.IsInterface &&
								!t.IsAbstract &&
								t.GetCustomAttribute<TangerineIgnoreAttribute>(false) == null &&
								t != propertyType &&
								propertyType.IsAssignableFrom(t)).ToList();
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
