using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Lime
{
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class TangerineRegisterComponentAttribute : Attribute { }

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class TangerineRegisterNodeAttribute : Attribute
	{
		public bool CanBeRoot;
		public int Order = int.MaxValue;
	}

	/// <summary>
	/// Denotes a property which can not be animated with Tangerine.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public sealed class TangerineStaticPropertyAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public sealed class TangerineReadOnlyAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class TangerineReadOnlyPropertiesAttribute : Attribute
	{
		private readonly string[] propertyNames;

		public bool Contains(string propertyName)
		{
			foreach (var name in propertyNames) {
				if (propertyName == name) {
					return true;
				}
			}
			return false;
		}

		public TangerineReadOnlyPropertiesAttribute(params string[] propertyNames) =>
			this.propertyNames = propertyNames.ToArray();
	}

	public sealed class TangerineKeyframeColorAttribute : Attribute
	{
		public int ColorIndex;

		public TangerineKeyframeColorAttribute(int colorIndex)
		{
			ColorIndex = colorIndex;
		}
	}

	public sealed class TangerineNodeBuilderAttribute : Attribute
	{
		public string MethodName { get; private set; }

		public TangerineNodeBuilderAttribute(string methodName)
		{
			MethodName = methodName;
		}
	}

	public sealed class TangerineAllowedParentTypes : Attribute
	{
		public Type[] Types;

		public TangerineAllowedParentTypes(params Type[] types)
		{
			Types = types;
		}
	}

	public sealed class TangerineAllowedChildrenTypes : Attribute
	{
		public Type[] Types;

		public TangerineAllowedChildrenTypes(params Type[] types)
		{
			Types = types;
		}
	}

	public sealed class TangerineLockChildrenNodeList : Attribute
	{ }

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public sealed class TangerineIgnoreIfAttribute : Attribute
	{
		public readonly string Method;

		private Func<object, bool> checker;

		public TangerineIgnoreIfAttribute(string method)
		{
			Method = method;
		}

		public bool Check(object obj)
		{
			if (checker == null) {
				var fn = obj.GetType().GetMethod(
					Method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance
				);
				if (fn == null) {
					throw new System.Exception("Couldn't find method " + Method);
				}

				var p = Expression.Parameter(typeof(object));
				var e = Expression.Call(Expression.Convert(p, fn.DeclaringType), fn);
				checker = Expression.Lambda<Func<object, bool>>(e, p).Compile();
			}

			return checker(obj);
		}
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Class, AllowMultiple = false)]
	public sealed class TangerineIgnoreAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public sealed class TangerineInspectAttribute : Attribute
	{ }

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public sealed class TangerineGroupAttribute : Attribute
	{
		public readonly string Name;

		public TangerineGroupAttribute(string name)
		{
			Name = name ?? string.Empty;
		}
	}

	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
	public sealed class TangerineOnPropertySetAttribute : Attribute
	{
		private readonly string methodName;
		private MethodInfo method;

		public TangerineOnPropertySetAttribute(string methodName)
		{
			this.methodName = methodName;
		}

		public void Invoke(object o)
		{
			if (method == null) {
				var type = o.GetType();
				method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
			}
			method.Invoke(o, null);
		}
	}

	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
	public sealed class TangerineVisualHintGroupAttribute : Attribute
	{
		public readonly string Group;
		public readonly string AliasTypeName;

		public TangerineVisualHintGroupAttribute(string group, string aliasTypeName = null)
		{
			Group = group ?? "/";
			AliasTypeName = aliasTypeName;
		}
	}

	public sealed class TangerineFilePropertyAttribute : Attribute
	{
		public readonly string[] AllowedFileTypes;
		public readonly bool TrimExtension;
		private readonly string valueToStringMethodName;
		private readonly string stringToValueMethodName;
		private MethodInfo valueToStringMethod;
		private MethodInfo stringToValueMethod;
		private readonly object[] parameters = new object[1];

		public TangerineFilePropertyAttribute(
			string[] allowedFileTypes,
			string valueToStringMethodName = null,
			string stringToValueMethodName = null,
			bool trimExtension = true
		) {
			AllowedFileTypes = allowedFileTypes;
			this.stringToValueMethodName = stringToValueMethodName;
			this.valueToStringMethodName = valueToStringMethodName;
			this.TrimExtension = trimExtension;
		}

		public T StringToValueConverter<T>(Type type, string s)
		{
			if (string.IsNullOrEmpty(stringToValueMethodName)) {
				return (T)(object)(s ?? string.Empty);
			} else {
				parameters[0] = s;
				stringToValueMethod ??= type.GetMethod(stringToValueMethodName);
				return (T)stringToValueMethod.Invoke(null, parameters);
			}
		}

		public string ValueToStringConverter<T>(Type type, T v)
		{
			if (string.IsNullOrEmpty(valueToStringMethodName)) {
				return (string)(object)(v == null ? (T)(object)string.Empty : v);
			} else {
				parameters[0] = v;
				valueToStringMethod ??= type.GetMethod(valueToStringMethodName);
				return (string)valueToStringMethod.Invoke(null, parameters);
			}
		}
	}

	public sealed class TangerineDropDownListPropertyEditorAttribute : Attribute
	{
		private readonly string methodName;

		public TangerineDropDownListPropertyEditorAttribute(string methodName)
		{
			this.methodName = methodName;
		}

		public IEnumerable<(string, object)> EnumerateItems(object o)
		{
			var type = o.GetType();
			var fn = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public);
			return (IEnumerable<(string, object)>)fn.Invoke(o, new object[] { });
		}
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public class TangerineNumericEditBoxStepAttribute : Attribute
	{
		public readonly float Step;

		public TangerineNumericEditBoxStepAttribute(float step)
		{
			Step = step;
		}

		public void SetProperty(object editBox) => ((NumericEditBox)editBox).Step = Step;
	}

	public enum ValidationResult
	{
		Ok,
		Info,
		Warning,
		Error,
	}

	/// <summary>
	/// Everything that leads to exception is Error, except if exception throw is
	/// influenced by something from outside (e.g. another property).
	/// Otherwise it's Warning.
	/// </summary>
	public abstract class TangerineValidationAttribute : Attribute
	{
		public abstract ValidationResult IsValid(object owner, object value, out string message);
	}

	public abstract class TangerineTextureInfoAttribute : TangerineValidationAttribute
	{
		private readonly Type[] validatableTypes;
		private MethodInfo[] getTextureMethods;

		public TangerineTextureInfoAttribute(params Type[] validatableTypes)
		{
			this.validatableTypes = validatableTypes;
			getTextureMethods = validatableTypes.Select(type => type.GetProperty("Texture").GetGetMethod()).ToArray();
		}

		protected abstract ValidationResult Validate(ITexture texture, object value, out string message);

		public override ValidationResult IsValid(object owner, object value, out string message)
		{
			var ownerType = owner.GetType();
			var index = -1;
			for (int i = 0; i < validatableTypes.Length; i++) {
				if (ownerType == validatableTypes[i]) {
					index = i;
					break;
				}
			}
			if (index != -1) {
				var texture = getTextureMethods[index].Invoke(owner, new object[0]) as ITexture;
				return Validate(texture, value, out message);
			}
			message = string.Empty;
			return ValidationResult.Ok;
		}
	}

	public class TangerineSizeInfoAttribute : TangerineTextureInfoAttribute
	{
		public TangerineSizeInfoAttribute(params Type[] validatableTypes) : base(validatableTypes)
		{
		}

		protected override ValidationResult Validate(ITexture texture, object value, out string message)
		{
			if (!(texture is null) && value is Vector2 size) {
				var imageSize = texture.ImageSize;
				var accuracy = Mathf.ZeroTolerance;
				if (Math.Abs(imageSize.Height - size.Y) > accuracy || Math.Abs(imageSize.Width - size.X) > accuracy) {
					message = $"The size is different from the size of the " +
						$"original image ({imageSize.Width}x{imageSize.Height})";
					return ValidationResult.Info;
				}
			}
			message = string.Empty;
			return ValidationResult.Ok;
		}
	}

	public class TangerineRatioInfoAttribute : TangerineTextureInfoAttribute
	{
		public TangerineRatioInfoAttribute(params Type[] validatableTypes) : base(validatableTypes)
		{
		}

		protected override ValidationResult Validate(ITexture texture, object value, out string message)
		{
			if (!(texture is null) && value is Vector2 size) {
				var imageSize = texture.ImageSize;
				var accuracy = Mathf.ZeroTolerance;
				var originalAspectRatio = (float)imageSize.Width / (float)imageSize.Height;
				var currentAspectRatio = size.X / size.Y;
				if (Math.Abs(currentAspectRatio - originalAspectRatio) > accuracy) {
					message = $"Aspect ratio ({currentAspectRatio}) is different from " +
						$"the aspect ratio of the original image ({originalAspectRatio})";
					return ValidationResult.Info;
				}
			}
			message = string.Empty;
			return ValidationResult.Ok;
		}
	}

	public class TangerineValidRangeAttribute : TangerineValidationAttribute
	{
		public ValidationResult WarningLevel = ValidationResult.Warning;

		public object Minimum { get; private set; }
		public object Maximum { get; private set; }

		public TangerineValidRangeAttribute(int minimum, int maximum)
		{
			Maximum = maximum;
			Minimum = minimum;
		}

		public TangerineValidRangeAttribute(float minimum, float maximum)
		{
			Maximum = maximum;
			Minimum = minimum;
		}

		public override ValidationResult IsValid(object owner, object value, out string message)
		{
			var min = (IComparable)Minimum;
			var max = (IComparable)Maximum;
			message = (value is Vector2 v)
				? min.CompareTo(v.X) > 0 || max.CompareTo(v.X) < 0
					? $"Value X should be in range [{Minimum}, {Maximum}]."
					: min.CompareTo(v.Y) > 0 || max.CompareTo(v.Y) < 0
						? $"Value Y should be in range [{Minimum}, {Maximum}]."
						: null
				: min.CompareTo(value) > 0 || max.CompareTo(value) < 0
					? $"Value should be in range [{Minimum}, {Maximum}]."
					: null;
			return message == null ? ValidationResult.Ok : WarningLevel;
		}
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public class TangerineDefaultCharsetAttribute : TangerineValidationAttribute
	{
		private static readonly Regex regex = new Regex(@"\p{IsCyrillic}", RegexOptions.Compiled);

		public override ValidationResult IsValid(object owner, object value, out string message)
		{
			return IsValid(value as string, out message);
		}

		public static ValidationResult IsValid(string value, out string message)
		{
			message = value == null || value is string s && !regex.IsMatch(s) ? null : "Wrong charset";
			return message == null ? ValidationResult.Ok : ValidationResult.Warning;
		}
	}

	public class TangerineTileImageTextureAttribute : TangerineValidationAttribute
	{
		public override ValidationResult IsValid(object owner, object value, out string message)
		{
			var res = value is ITexture texture
				&& (
					texture.IsStubTexture
					|| !(
						texture.TextureParams.WrapModeU == TextureWrapMode.Clamp
						|| texture.TextureParams.WrapModeV == TextureWrapMode.Clamp
					)
				);
			message = res
				? null
				: $"Texture of TiledImage should have WrapMode set to either Repeat or MirroredRepeat.";
			return res ? ValidationResult.Ok : ValidationResult.Warning;
		}
	}

	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
	public class TangerinePropertyDefaultValueAttribute : Attribute
	{
		private readonly Type type;
		private readonly string method;

		private Func<object> getDefaultValue;

		public TangerinePropertyDefaultValueAttribute(Type type, string method)
		{
			this.type = type;
			this.method = method;
		}

		public object GetValue()
		{
			if (getDefaultValue == null) {
				var fn = type.GetMethod(method);
				if (fn == null) {
					throw new System.Exception();
				}
				getDefaultValue = () => fn.Invoke(type, null);
			}
			return getDefaultValue();
		}
	}

	public sealed class TangerineKeyframeInterpolationAttribute : Attribute
	{
		public readonly KeyFunction[] KeyframeInterpolations;

		public TangerineKeyframeInterpolationAttribute(params KeyFunction[] keyFunctions)
		{
			KeyframeInterpolations = keyFunctions;
		}
	}

	public sealed class TangerineDisplayNameAttribute : Attribute
	{
		public readonly string DisplayName;

		public TangerineDisplayNameAttribute(string displayName)
		{
			DisplayName = displayName;
		}
	}

	[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
	public sealed class TangerineTooltipAttribute : Attribute
	{
		public readonly string Text;

		public TangerineTooltipAttribute(string text)
		{
			Text = text;
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public abstract class TangerineCustomIconAttribute : Attribute
	{
		public int Priority { get; }

		protected TangerineCustomIconAttribute(int priority)
		{
			Priority = priority;
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public sealed class TangerineBase64IconAttribute : TangerineCustomIconAttribute
	{
		public string Base64 { get; }

		///<param name="base64">Base64 encoded PNG image.</param>
		public TangerineBase64IconAttribute(string base64, int priority = 0) : base(priority)
		{
			Base64 = base64;
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public sealed class TangerineIconGenerationAttribute : TangerineCustomIconAttribute
	{
		public string Abbreviation { get; }
		public Color4? CommonColor { get; }
		public Color4? SecondaryColor { get; }

		public TangerineIconGenerationAttribute(int priority = 0) : base(priority) { }

		public TangerineIconGenerationAttribute(string abbreviation, int priority = 0) : base(priority)
		{
			Abbreviation = abbreviation;
		}

		public TangerineIconGenerationAttribute(
			string commonColor, string secondaryColor, int priority = 0
		) : base(priority)
		{
			CommonColor = Color4.Parse(commonColor);
			SecondaryColor = Color4.Parse(secondaryColor);
		}

		public TangerineIconGenerationAttribute(
			string abbreviation, string commonColor, string secondaryColor, int priority = 0
		) : base(priority)
		{
			Abbreviation = abbreviation;
			CommonColor = Color4.Parse(commonColor);
			SecondaryColor = Color4.Parse(secondaryColor);
		}
	}

	/// <summary>
	/// '/' Separated path to either component or node create command in menu.
	/// If path ends with '/' the command Text will be taken as is and last part of
	/// path will be treated as last nested menu for the command. Otherwise last part of the path
	/// will be assigned to command.Text. <see cref="Tangerine.Core.MenuExtensions.InsertCommandAlongPath" />
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class TangerineMenuPathAttribute : Attribute
	{
		public string Path;
		public TangerineMenuPathAttribute(string path)
		{
			Path = path;
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
	public class TangerineCreateButtonAttribute : Attribute
	{
		public string Name;
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false)]
	public abstract class TangerineToStringAttribute : Attribute
	{
		protected const string NullText = "<null>";
		public virtual string GetText(object obj) => obj?.ToString() ?? NullText;
	}

	public class TangerineToStringUsingMethodAttribute : TangerineToStringAttribute
	{
		private readonly string methodName;
		private Func<object, string> idGetter;
		private readonly object[] parameters = new object[1];

		public TangerineToStringUsingMethodAttribute(string methodName)
		{
			this.methodName = methodName;
		}

		public override string GetText(object obj)
		{
			if (obj == null) {
				return NullText;
			}
			if (idGetter == null) {
				var classMethod = obj.GetType().GetMethod(methodName);
				idGetter = (o) => (string)classMethod.Invoke(null, parameters);
			}
			parameters[0] = obj;
			return idGetter(obj);
		}
	}
}
