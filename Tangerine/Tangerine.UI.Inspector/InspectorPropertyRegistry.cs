using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Yuzu;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.Inspector
{
	public class InspectorPropertyRegistry
	{
		public readonly List<RegistryItem> Items;

		public static readonly InspectorPropertyRegistry Instance = new InspectorPropertyRegistry();

		static bool AllowChildren(PropertyEditorParams context)
		{
			return context.Objects.All(o => NodeCompositionValidator.CanHaveChildren(o.GetType()));
		}

		InspectorPropertyRegistry()
		{
			Items = new List<RegistryItem>();
			AddEditor(c => PropertyAttributes<TangerineDropDownListPropertyEditorAttribute>.Get(c.Type, c.PropertyName) != null,
				c => {
					var a = PropertyAttributes<TangerineDropDownListPropertyEditorAttribute>.Get(c.Type, c.PropertyName);
					Type specializedDropDownListPropertyEditorType = typeof(DropDownListPropertyEditor<>).MakeGenericType(c.PropertyInfo.PropertyType);
					return Activator.CreateInstance(specializedDropDownListPropertyEditorType, new object[] { c, a.EnumerateItems(c.Objects.First()) }) as IPropertyEditor;
				}
			);
			AddEditor(c => PropertyAttributes<TangerineFilePropertyAttribute>.Get(c.Type, c.PropertyName) != null,
				c => {
					var a = PropertyAttributes<TangerineFilePropertyAttribute>.Get(c.Type, c.PropertyName);
					Type specializedCustomFilePropertyEditorType = typeof(CustomFilePropertyEditor<>).MakeGenericType(c.PropertyInfo.PropertyType);
					return Activator.CreateInstance(specializedCustomFilePropertyEditorType, new object[] { c, a }) as IPropertyEditor;
				}
			);
			AddEditor(c => c.PropertyName == "ContentsPath" && c.Objects.All(o => o is Node), c => AllowChildren(c) ? new ContentsPathPropertyEditor(c) : null);
			AddEditor(c => c.PropertyName == "Trigger", c => AllowChildren(c) ? new TriggerPropertyEditor(c) : null);
			AddEditor(typeof(Vector2), c => new Vector2PropertyEditor(c));
			AddEditor(typeof(Vector3), c => new Vector3PropertyEditor(c));
			AddEditor(typeof(IntVector2), c => new IntVector2PropertyEditor(c));
			AddEditor(typeof(Rectangle), c => new RectanglePropertyEditor(c));
			AddEditor(typeof(IntRectangle), c => new IntRectanglePropertyEditor(c));
			AddEditor(typeof(Quaternion), c => new QuaternionPropertyEditor(c));
			AddEditor(typeof(NumericRange), c => new NumericRangePropertyEditor(c));
			AddEditor(c => c.PropertyName == "Text", c => new TextPropertyEditor(c));
			AddEditor(c => c.PropertyName == "Id", c => new NodeIdPropertyEditor(c));
			AddEditor(typeof(string), c => new StringPropertyEditor(c));
			AddEditor(typeof(float), c => {
				var attribute = PropertyAttributes<TangerineValidRangeAttribute>.Get(c.PropertyInfo, true);
				if (attribute != null) {
					float min = (float)attribute.Minimum;
					float max = (float)attribute.Maximum;
					bool noInfinity = !float.IsInfinity(min) && !float.IsInfinity(max);
					bool noMinMaxValue =
						min != float.MinValue && min != float.MaxValue &&
						max != float.MinValue && max != float.MaxValue;
					if (noInfinity && noMinMaxValue) {
						return new SliderPropertyEditor(range: new Vector2(min, max), c);
					}
				}
				return new FloatPropertyEditor(c);
			});
			AddEditor(typeof(double), c => new DoublePropertyEditor(c));
			AddEditor(typeof(bool), c => new BooleanPropertyEditor(c));
			AddEditor(typeof(int), c => new IntPropertyEditor(c));
			AddEditor(typeof(Color4), c => new Color4PropertyEditor(c));
			AddEditor(typeof(ColorGradient), c => new ColorGradientPropertyEditor(c));
			AddEditor(typeof(Anchors), c => new AnchorsPropertyEditor(c));
			AddEditor(typeof(Blending), c => new BlendingPropertyEditor(c));
			AddEditor(typeof(RenderTarget), c => new RenderTargetPropertyEditor(c));
			AddEditor(c => {
				return
					!c.Objects.Skip(1).Any() &&
					c.PropertyInfo.PropertyType == typeof(ITexture) &&
					c.PropertyInfo.GetValue(c.Objects.First())?.GetType() == typeof(RenderTexture);
			}, c => new RenderTexturePropertyEditor(c));
			AddEditor(typeof(ITexture), c => new TexturePropertyEditor<ITexture>(c));
			AddEditor(typeof(SerializableTexture), c => new TexturePropertyEditor<SerializableTexture>(c));
			AddEditor(typeof(SerializableSample), c => new AudioSamplePropertyEditor(c));
			AddEditor(typeof(SerializableFont), c => new FontPropertyEditor(c));
			AddEditor(typeof(NodeReference<Camera3D>), c => new NodeReferencePropertyEditor<Camera3D>(c));
			AddEditor(typeof(NodeReference<Image>), c => new NodeReferencePropertyEditor<Image>(c));
			AddEditor(typeof(NodeReference<Spline>), c => new NodeReferencePropertyEditor<Spline>(c));
			AddEditor(typeof(NodeReference<Widget>), c => new NodeReferencePropertyEditor<Widget>(c));
			AddEditor(typeof(NodeReference<Node3D>), c => new NodeReferencePropertyEditor<Node3D>(c));
			AddEditor(typeof(NodeReference<Spline3D>), c => new NodeReferencePropertyEditor<Spline3D>(c));
			AddEditor(typeof(SkinningWeights), c => new SkinningWeightsPropertyEditor(c));
			AddEditor(typeof(Alignment), c => new AlignmentPropertyEditor(c));
			AddEditor(typeof(Thickness), c => new ThicknessPropertyEditor(c));
		}

		void AddEditor(Type type, PropertyEditorBuilder builder)
		{
			Items.Add(new RegistryItem(c => c.PropertyInfo.PropertyType == type, builder));
		}

		void AddEditor(Func<PropertyEditorParams, bool> condition, PropertyEditorBuilder builder)
		{
			Items.Add(new RegistryItem(condition, builder));
		}

		public class RegistryItem
		{
			public readonly Func<PropertyEditorParams, bool> Condition;
			public readonly PropertyEditorBuilder Builder;

			public RegistryItem(Func<PropertyEditorParams, bool> condition, PropertyEditorBuilder builder)
			{
				Condition = condition;
				Builder = builder;
			}
		}
	}
}
