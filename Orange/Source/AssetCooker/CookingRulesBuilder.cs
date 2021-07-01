using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Lime;
using Yuzu;
using Yuzu.Json;
using Yuzu.Metadata;

namespace Orange
{
	// NB: When packing textures into atlases, Orange chooses texture format with highest value
	// among all atlas items.
	public enum PVRFormat
	{
		PVRTC4,
		PVRTC4_Forced,
		PVRTC2,
		ETC2,
		RGB565,
		RGBA4,
		ARGB8,
	}

	public enum DDSFormat
	{
		DXTi,
		Uncompressed
	}

	public enum AtlasOptimization
	{
		Memory,
		DrawCalls
	}

	public enum ModelCompression
	{
		None,
		Deflate,
		LZMA,
	}

	// Specifying empty target platforms for single cooking rule means no platform must be specified in cooking rules.
	public class TargetPlatformsAttribute : Attribute
	{
		public readonly TargetPlatform[] TargetPlatforms;

		public TargetPlatformsAttribute(params TargetPlatform []TargetPlatforms)
		{
			this.TargetPlatforms = TargetPlatforms;
		}
	}

	public interface ICookingRules
	{
		string TextureAtlas { get; }
		bool MipMaps { get; }
		bool HighQualityCompression { get; }
		bool GenerateOpacityMask { get; }
		float TextureScaleFactor { get; }
		PVRFormat PVRFormat { get; }
		DDSFormat DDSFormat { get; }
		string[] Bundles { get; }
		bool Ignore { get; }
		/// <summary>
		/// Asset goes into the bundle Only if specified target is chosen.
		/// </summary>
		bool Only { get; }
		int ADPCMLimit { get; }
		AtlasOptimization AtlasOptimization { get; }
		ModelCompression ModelCompression { get; }
		string AtlasPacker { get; }
		string CustomRule { get; }
		[TargetPlatforms]
		TextureWrapMode WrapMode { get; }
		[TargetPlatforms]
		TextureFilter MinFilter { get; }
		[TargetPlatforms]
		TextureFilter MagFilter { get; }
		int AtlasItemPadding { get; }
		int MaxAtlasSize { get; }
	}

	public class ParticularCookingRules : ICookingRules
	{
		// NOTE: function `Override` uses the fact that rule name being parsed matches the field name
		// for all fields marked with `YuzuMember`. So don't rename them or do so with cautiousness.
		// e.g. don't rename `Bundle` to `Bundles`

		[YuzuMember]
		public string TextureAtlas { get; set; }

		[YuzuMember]
		public bool MipMaps { get; set; }

		[YuzuMember]
		public bool HighQualityCompression { get; set; }

		[YuzuMember]
		public bool GenerateOpacityMask { get; set; }

		[YuzuMember]
		public float TextureScaleFactor { get; set; }

		[YuzuMember]
		public PVRFormat PVRFormat { get; set; }

		[YuzuMember]
		public DDSFormat DDSFormat { get; set; }

		[YuzuMember]
		public string[] Bundles { get; set; }

		[YuzuMember]
		public bool Ignore { get; set; }
		[YuzuMember]
		public bool Only { get; set; }

		[YuzuMember]
		public int ADPCMLimit { get; set; } // Kb

		[YuzuMember]
		public AtlasOptimization AtlasOptimization { get; set; }

		[YuzuMember]
		public ModelCompression ModelCompression { get; set; }

		[YuzuMember]
		public string AtlasPacker { get; set; }

		[YuzuMember]
		public TextureWrapMode WrapMode { get; set; }

		[YuzuMember]
		public TextureFilter MinFilter { get; set; }

		[YuzuMember]
		public TextureFilter MagFilter { get; set; }

		[YuzuMember]
		public string CustomRule { get; set; }

		[YuzuMember]
		public int AtlasItemPadding { get; set; } = 1;

		[YuzuMember]
		public int MaxAtlasSize { get; set; } = 2048;

		// using json format for SHA1 since binary one includes all fields definitions header anyway.
		// so adding a field with binary triggers rebuild of all bundles
		private static JsonSerializer yjs = new JsonSerializer();

		public SHA256 Hash => SHA256.Compute(Encoding.UTF8.GetBytes(yjs.ToString(this).ToLower()));

		public HashSet<Meta.Item> FieldOverrides;

		public ParticularCookingRules Parent;

		private static readonly Meta meta = Meta.Get(typeof (ParticularCookingRules), new CommonOptions());

		private static readonly Dictionary<string, Meta.Item> fieldNameToYuzuMetaItemCache =
			new Dictionary<string, Meta.Item>();

		private static readonly Dictionary<TargetPlatform, ParticularCookingRules> defaultRules =
			new Dictionary<TargetPlatform, ParticularCookingRules>();

		static ParticularCookingRules()
		{
			foreach (var item in meta.Items) {
				fieldNameToYuzuMetaItemCache.Add(item.Name, item);
			}
			// initializing all fields here, so any changes to yuzu default values won't affect us here
			yjs.JsonOptions = new JsonSerializeOptions {
				ArrayLengthPrefix = false,
				ClassTag = "class",
				DateFormat = "O",
				TimeSpanFormat = "c",
				DecimalAsString = false,
				EnumAsString = false,
				FieldSeparator = "",
				IgnoreCompact = false,
				Indent = "",
				Int64AsString = false,
				MaxOnelineFields = 0,
				SaveRootClass = false,
				Unordered = false,
			};
		}

		public void Override(string fieldName)
		{
			FieldOverrides.Add(fieldNameToYuzuMetaItemCache[fieldName]);
		}

		public static ParticularCookingRules GetDefault(TargetPlatform platform)
		{
			if (defaultRules.ContainsKey(platform)) {
				return defaultRules[platform];
			}
			defaultRules.Add(platform, new ParticularCookingRules
			{
				TextureAtlas = null,
				MipMaps = false,
				HighQualityCompression = false,
				GenerateOpacityMask = false,
				TextureScaleFactor = 1.0f,
				PVRFormat = platform == TargetPlatform.Android ? PVRFormat.ETC2 : PVRFormat.PVRTC4,
				DDSFormat = DDSFormat.DXTi,
				Bundles = new[] { CookingRulesBuilder.MainBundleName },
				Ignore = false,
				Only = false,
				ADPCMLimit = 100,
				AtlasOptimization = AtlasOptimization.Memory,
				ModelCompression = ModelCompression.Deflate,
				FieldOverrides = new HashSet<Meta.Item>(),
				AtlasItemPadding = 1,
				MaxAtlasSize = 2048,
			});
			return defaultRules[platform];
		}

		public ParticularCookingRules InheritClone()
		{
			var r = (ParticularCookingRules)MemberwiseClone();
			r.FieldOverrides = new HashSet<Meta.Item>();
			r.Parent = this;
			return r;
		}
	}

	public class CookingRules : ICookingRules
	{
		/// <summary>
		/// Path to the cooking rules source file within input AssetBundle.
		/// </summary>
		public string SourcePath;

		/// <summary>
		/// Path to the cooking rules source file within OS file system.
		/// </summary>
		public string SystemSourcePath
		{
			get => AssetBundle.Current.ToSystemPath(SourcePath);
			set => SourcePath = AssetBundle.Current.FromSystemPath(value);
		}

		public readonly Dictionary<Target, ParticularCookingRules> TargetRules = new Dictionary<Target, ParticularCookingRules>();
		public ParticularCookingRules CommonRules;
		public CookingRules Parent;
		public string TextureAtlas => EffectiveRules.TextureAtlas;
		public bool MipMaps => EffectiveRules.MipMaps;
		public bool HighQualityCompression => EffectiveRules.HighQualityCompression;
		public bool GenerateOpacityMask => EffectiveRules.GenerateOpacityMask;
		public float TextureScaleFactor => EffectiveRules.TextureScaleFactor;
		public PVRFormat PVRFormat => EffectiveRules.PVRFormat;
		public DDSFormat DDSFormat => EffectiveRules.DDSFormat;
		public string[] Bundles => EffectiveRules.Bundles;
		public int ADPCMLimit => EffectiveRules.ADPCMLimit;
		public AtlasOptimization AtlasOptimization => EffectiveRules.AtlasOptimization;
		public ModelCompression ModelCompression => EffectiveRules.ModelCompression;
		public string AtlasPacker => EffectiveRules.AtlasPacker;
		public TextureWrapMode WrapMode => EffectiveRules.WrapMode;
		public TextureFilter MinFilter => EffectiveRules.MinFilter;
		public TextureFilter MagFilter => EffectiveRules.MagFilter;
		public string CustomRule => EffectiveRules.CustomRule;
		public int AtlasItemPadding => EffectiveRules.AtlasItemPadding;
		public int MaxAtlasSize => EffectiveRules.MaxAtlasSize;

		public SHA256 Hash => EffectiveRules.Hash;

		public bool Ignore
		{
			get => EffectiveRules.Ignore;
			set
			{
				foreach (var target in The.Workspace.Targets) {
					TargetRules[target].Ignore = value;
				}
				CommonRules.Ignore = value;
				if (EffectiveRules != null) {
					EffectiveRules.Ignore = value;
				}
			}
		}

		public bool Only => EffectiveRules.Only;

		public ParticularCookingRules EffectiveRules { get; private set; }

		public IEnumerable<(Target Target, ParticularCookingRules Rules)> Enumerate()
		{
			yield return (null, CommonRules);
			foreach (var (k, v) in TargetRules) {
				yield return (k, v);
			}
		}

		public CookingRules(bool initialize = true)
		{
			if (!initialize) {
				return;
			}
			foreach (var t in The.Workspace.Targets) {
				TargetRules.Add(t, ParticularCookingRules.GetDefault(t.Platform));
			}
			CommonRules = ParticularCookingRules.GetDefault(The.UI.GetActiveTarget().Platform);
		}

		public CookingRules InheritClone()
		{
			var r = new CookingRules(false);
			r.Parent = this;
			foreach (var kv in TargetRules) {
				r.TargetRules.Add(kv.Key, kv.Value.InheritClone());
			}
			if (EffectiveRules != null) {
				r.CommonRules = EffectiveRules.InheritClone();
				r.EffectiveRules = EffectiveRules.InheritClone();
			} else {
				r.CommonRules = CommonRules.InheritClone();
			}
			return r;
		}

		public void Save()
		{
			using (
				var fs = AssetBundle.Current.OpenFile(SourcePath, FileMode.Create)) {
				using (var sw = new StreamWriter(fs)) {
					SaveCookingRules(sw, CommonRules, null);
					foreach (var kv in TargetRules) {
						SaveCookingRules(sw, kv.Value, kv.Key);
					}
				}
			}
		}

		public string FieldValueToString(Meta.Item yi, object value)
		{
			if (value == null) {
				return "";
			} if (yi.Name == "Bundles") {
				var vlist = (string[])value;
				return string.Join(",", vlist);
			} else if (value is bool) {
				return (bool)value ? "Yes" : "No";
			} else if (value is DDSFormat) {
				return (DDSFormat)value == DDSFormat.DXTi ? "DXTi" : "RGBA8";
			} else if (yi.Name == "TextureAtlas") {
				var atlasName = Path.GetDirectoryName(SourcePath).
					Replace('\\', '#').
					Replace('/', '#');
				if (!atlasName.StartsWith("#")) {
					atlasName = '#' + atlasName;
				}
				if (atlasName == value.ToString()) {
					return CookingRulesBuilder.DirectoryNameToken;
				} else {
					return value.ToString();
				}
			} else {
				return value.ToString();
			}
		}

		private void SaveCookingRules(StreamWriter sw, ParticularCookingRules rules, Target target)
		{
			var targetString = target == null ? "" : $"({target.Name})";
			foreach (var yi in rules.FieldOverrides) {
				var value = yi.GetValue(rules);
				var valueString = FieldValueToString(yi, value);
				if (!string.IsNullOrEmpty(valueString)) {
					sw.Write($"{yi.Name}{targetString} {valueString}\n");
				}
			}
		}

		public void DeduceEffectiveRules(Target target)
		{
			EffectiveRules = CommonRules.InheritClone();
			if (target != null) {
				var targetStack = new Stack<Target>();
				var t = target;
				while (t != null) {
					targetStack.Push(t);
					t = t.BaseTarget;
				}
				while (targetStack.Count != 0) {
					t = targetStack.Pop();
					var targetRules = TargetRules[t];
					foreach (var i in targetRules.FieldOverrides) {
						i.SetValue(EffectiveRules, i.GetValue(targetRules));
					}
					// TODO: implement this workaround in a general way
					if (t.Platform == TargetPlatform.Android) {
						switch (EffectiveRules.PVRFormat) {
							case PVRFormat.PVRTC2:
							case PVRFormat.PVRTC4:
							case PVRFormat.PVRTC4_Forced:
								EffectiveRules.PVRFormat = PVRFormat.ETC2;
								break;
						}
					}
				}
				if (!EffectiveRules.Only && TargetRules.Any(f => f.Key != target && f.Value.Only)) {
					EffectiveRules.Ignore = true;
				}
			}
			if (EffectiveRules.WrapMode != TextureWrapMode.Clamp) {
				EffectiveRules.TextureAtlas = null;
			}
		}

		public bool HasOverrides()
		{
			var r = false;
			r = r || CommonRules.FieldOverrides.Count > 0;
			foreach (var cr in TargetRules) {
				r = r || cr.Value.FieldOverrides.Count > 0;
			}
			return r;
		}
	}

	public class CookingRulesBuilder
	{
		public const string MainBundleName = "Main";
		public const string CookingRulesFilename = "#CookingRules.txt";
		public const string DirectoryNameToken = "${DirectoryName}";

		// pass target as null to build cooking rules disregarding targets
		public static Dictionary<string, CookingRules> Build(AssetBundle bundle, Target target, string path = null)
		{
			var pathStack = new Stack<string>();
			var rulesStack = new Stack<CookingRules>();
			var map = new Dictionary<string, CookingRules>(StringComparer.OrdinalIgnoreCase);
			pathStack.Push("");
			var rootRules = new CookingRules();
			rootRules.DeduceEffectiveRules(target);
			rulesStack.Push(rootRules);
			foreach (var filePath in bundle.EnumerateFiles(path)) {
				while (!filePath.StartsWith(pathStack.Peek())) {
					rulesStack.Pop();
					pathStack.Pop();
				}
				if (Path.GetFileName(filePath) == CookingRulesFilename) {
					var dirName = AssetPath.GetDirectoryName(filePath);
					pathStack.Push(dirName == string.Empty ? "" : dirName + "/");
					var rules = ParseCookingRules(bundle, rulesStack.Peek(), filePath, target);
					rules.SourcePath = filePath;
					rulesStack.Push(rules);
					// Add 'ignore' cooking rules for this #CookingRules.txt itself
					var ignoreRules = rules.InheritClone();
					ignoreRules.Ignore = true;
					map[filePath] = ignoreRules;
					var directoryName = pathStack.Peek();
					if (!string.IsNullOrEmpty(directoryName)) {
						directoryName = directoryName.Remove(directoryName.Length - 1);
						// it is possible for map to not contain this directoryName since not every IFileEnumerator enumerates directories
						if (map.ContainsKey(directoryName)) {
							map[directoryName] = rules;
						}
					}
				} else  {
					if (filePath.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) {
						var filename = filePath.Remove(filePath.Length - 4);
						if (bundle.FileExists(filename)) {
							continue;
						}
					}
					var rulesFile = filePath + ".txt";
					var rules = rulesStack.Peek();
					if (bundle.FileExists(rulesFile)) {
						rules = ParseCookingRules(bundle, rulesStack.Peek(), rulesFile, target);
						rules.SourcePath = rulesFile;
						// Add 'ignore' cooking rules for this cooking rules text file
						var ignoreRules = rules.InheritClone();
						ignoreRules.Ignore = true;
						map[rulesFile] = ignoreRules;
					}
					map[filePath] = rules;
				}
			}
			return map;
		}

		private static bool ParseBool(string value)
		{
			if (value != "Yes" && value != "No") {
				throw new Lime.Exception("Invalid value. Must be either 'Yes' or 'No'");
			}
			return value == "Yes";
		}

		private static DDSFormat ParseDDSFormat(string value)
		{
			switch (value) {
				case "DXTi":
					return DDSFormat.DXTi;
				case "ARGB8":
				case "RGBA8":
					return DDSFormat.Uncompressed;
				default:
					throw new Lime.Exception("Error parsing DDS format. Must be either DXTi or ARGB8");
			}
		}

		private static PVRFormat ParsePVRFormat(string value)
		{
			switch (value) {
				case "":
				case "PVRTC4":
					return PVRFormat.PVRTC4;
				case "PVRTC4_Forced":
					return PVRFormat.PVRTC4_Forced;
				case "PVRTC2":
					return PVRFormat.PVRTC2;
				case "RGBA4":
					return PVRFormat.RGBA4;
				case "RGB565":
					return PVRFormat.RGB565;
				case "ARGB8":
					return PVRFormat.ARGB8;
				case "RGBA8":
					return PVRFormat.ARGB8;
				default:
					throw new Lime.Exception(
						"Error parsing PVR format. Must be one of: PVRTC4, PVRTC4_Forced, PVRTC2, RGBA4, RGB565, ARGB8");
			}
		}

		private static AtlasOptimization ParseAtlasOptimization(string value)
		{
			switch (value) {
				case "":
				case "Memory":
					return AtlasOptimization.Memory;
				case "DrawCalls":
					return AtlasOptimization.DrawCalls;
				default:
					throw new Lime.Exception("Error parsing AtlasOptimization. Must be one of: Memory, DrawCalls");
			}
		}

		private static ModelCompression ParseModelCompression(string value)
		{
			switch (value) {
				case "None":
					return ModelCompression.None;
				case "":
				case "Deflate":
					return ModelCompression.Deflate;
				case "LZMA":
					return ModelCompression.LZMA;
				default:
					throw new Lime.Exception("Error parsing ModelCompression. Must be one of: None, Deflate, LZMA");
			}
		}

		private static CookingRules ParseCookingRules(AssetBundle bundle, CookingRules basicRules, string path, Target target)
		{
			var rules = basicRules.InheritClone();
			var currentRules = rules.CommonRules;
			try {
				using (var s = bundle.OpenFile(path)) {
					TextReader r = new StreamReader(s);
					string line;
					while ((line = r.ReadLine()) != null) {
						line = line.Trim();
						if (line == "") {
							continue;
						}
						var words = line.Split(' ');
						if (words.Length < 2) {
							throw new Lime.Exception("Invalid rule format");
						}
						// target-specific cooking rules
						if (words[0].EndsWith(")")) {
							int cut = words[0].IndexOf('(');
							if (cut >= 0) {
								string targetName = words[0].Substring(cut + 1, words[0].Length - cut - 2);
								words[0] = words[0].Substring(0, cut);
								currentRules = null;
								Target currentTarget = null;
								foreach (var t in The.Workspace.Targets) {
									if (targetName == t.Name) {
										currentTarget = t;
									}
								}
								if (currentTarget == null) {
									throw new Lime.Exception($"Invalid target: {targetName}");
								}
								currentRules = rules.TargetRules[currentTarget];
								{
									if (!CanSetRulePerTarget(words[0], currentTarget)) {
										throw new Lime.Exception($"Invalid platform {target.Platform} for cooking rule {words[0]}");
									}
								}
							}
						} else {
							currentRules = rules.CommonRules;
						}
						ParseRule(currentRules, words, path);
					}
				}
			} catch (Lime.Exception e) {
				if (!Path.IsPathRooted(path)) {
					path = Path.Combine(Directory.GetCurrentDirectory(), path);
				}
				throw new Lime.Exception("Syntax error in {0}: {1}", path, e.Message);
			}
			rules.DeduceEffectiveRules(target);
			return rules;
		}

		public static bool CanSetRulePerTarget(string cookingRulePropertyName, Target target)
		{
			if (target == null) {
				return true;
			}
			var targetPlatformAttribute = (TargetPlatformsAttribute)typeof(ICookingRules)
				.GetProperty(cookingRulePropertyName)
				.GetCustomAttribute(typeof(TargetPlatformsAttribute));
			if (targetPlatformAttribute != null && !targetPlatformAttribute.TargetPlatforms.Contains(target.Platform)) {
				return false;
			}
			return true;
		}

		private static void ParseRule(ParticularCookingRules rules, IReadOnlyList<string> words, string path)
		{
			try {
				switch (words[0]) {
				case "TextureAtlas":
					switch (words[1]) {
					case "None":
						rules.TextureAtlas = null;
						break;
					case DirectoryNameToken:
						string atlasName = "#" + Lime.AssetPath.GetDirectoryName(path).Replace('/', '#');
						if (string.IsNullOrEmpty(atlasName)) {
							throw new Lime.Exception(
								"Atlas directory is empty. Choose another atlas name");
						}
						rules.TextureAtlas = atlasName;
						break;
					default:
						rules.TextureAtlas = words[1];
						break;
					}
					break;
				case "MipMaps":
					rules.MipMaps = ParseBool(words[1]);
					break;
				case "HighQualityCompression":
					rules.HighQualityCompression = ParseBool(words[1]);
					break;
				case "GenerateOpacityMask":
					rules.GenerateOpacityMask = ParseBool(words[1]);
					break;
				case "PVRFormat":
					rules.PVRFormat = ParsePVRFormat(words[1]);
					break;
				case "DDSFormat":
					rules.DDSFormat = ParseDDSFormat(words[1]);
					break;
				case "Bundles":
					rules.Bundles = new string[words.Count - 1];
					for (var i = 0; i < rules.Bundles.Length; i++) {
						rules.Bundles[i] = ParseBundle(words[i + 1]);
					}
					break;
				case "Ignore":
					rules.Ignore = ParseBool(words[1]);
					break;
				case "Only":
					rules.Only = ParseBool(words[1]);
					break;
				case "ADPCMLimit":
					rules.ADPCMLimit = int.Parse(words[1]);
					break;
				case "TextureScaleFactor":
					rules.TextureScaleFactor = float.Parse(words[1]);
					break;
				case "AtlasOptimization":
					rules.AtlasOptimization = ParseAtlasOptimization(words[1]);
					break;
				case "AtlasPacker":
					rules.AtlasPacker = words[1];
					break;
				case "ModelCompression":
					rules.ModelCompression = ParseModelCompression(words[1]);
					break;
				case "CustomRule":
					rules.CustomRule = words[1];
					break;
				case "WrapMode":
					rules.WrapMode = ParseWrapMode(words[1]);
					break;
				case "MinFilter":
					rules.MinFilter = ParseTextureFilter(words[1]);
					break;
				case "MagFilter":
					rules.MagFilter = ParseTextureFilter(words[1]);
					break;
				case "AtlasItemPadding":
					rules.AtlasItemPadding = int.Parse(words[1]);
					break;
				case "MaxAtlasSize":
					rules.MaxAtlasSize = int.Parse(words[1]);
					break;
				default:
					throw new Lime.Exception("Unknown attribute {0}", words[0]);
				}
				rules.Override(words[0]);
			} catch (System.Exception e) {
				Debug.Write("Failed to parse cooking rules: {0} {1} {2}", string.Join(",", words), path, e);
				throw;
			}
		}

		private static TextureFilter ParseTextureFilter(string value)
		{
			switch (value) {
			case "":
			case "Linear":
				return TextureFilter.Linear;
			case "Nearest":
				return TextureFilter.Nearest;
			default:
				throw new Lime.Exception("Error parsing TextureFtiler. Must be one of: Linear, Nearest");
			}
		}

		private static TextureWrapMode ParseWrapMode(string value)
		{
			switch (value) {
			case "":
			case "Clamp":
				return TextureWrapMode.Clamp;
			case "Repeat":
				return TextureWrapMode.Repeat;
			case "MirroredRepeat":
				return TextureWrapMode.MirroredRepeat;
			default:
				throw new Lime.Exception("Error parsing AtlasOptimization. Must be one of: Memory, DrawCalls");
			}
		}

		private static string ParseBundle(string word)
		{
			if (word.ToLowerInvariant() == "<default>" || word.ToLowerInvariant() == "data") {
				return MainBundleName;
			} else {
				return word;
			}
		}
	}
}

