using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
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
		Uncompressed,
	}

	public enum AtlasOptimization
	{
		Memory,
		DrawCalls,
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

		public TargetPlatformsAttribute(params TargetPlatform[] targetPlatforms)
		{
			this.TargetPlatforms = targetPlatforms;
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
		/// <summary>
		/// Asset alias.
		/// </summary>
		string Alias { get; }
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
		bool AtlasDebug { get; }
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
		public string Alias { get; set; }

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
		public bool AtlasDebug { get; set; } = false;

		[YuzuMember]
		public int MaxAtlasSize { get; set; } = 2048;

		// Json is used instead of binary because binary format includes
		// a definition of CookingRules class serializable fields. Which
		// means adding a field to cooking rules class will change binary
		// representation of already existing cooking rules. Consequently
		// cooking rules hash will also change and all bundles for all
		// projects will require rebuild.
		private static readonly ThreadLocal<JsonSerializer> yjs =
			new ThreadLocal<JsonSerializer>(() => new JsonSerializer() {
				JsonOptions = jsonSerializeOptions,
			});

		public override string ToString() => yjs.Value.ToString(this);

		public SHA256 Hash => SHA256.Compute(Encoding.UTF8.GetBytes(yjs.Value.ToString(this)));

		// Yuzu.Meta.Item for the field and flag if override should be propagated down the directory hierarchy
		public Dictionary<Meta.Item, (bool Propagate, ParticularCookingRules Source)> FieldOverrides;

		public ParticularCookingRules Parent;

		private static readonly Meta meta = Meta.Get(typeof(ParticularCookingRules), new CommonOptions());

		private static readonly Dictionary<string, Meta.Item> fieldNameToYuzuMetaItemCache =
			new Dictionary<string, Meta.Item>();

		private static readonly Dictionary<TargetPlatform, ParticularCookingRules> defaultRules =
			new Dictionary<TargetPlatform, ParticularCookingRules>();

		private static JsonSerializeOptions jsonSerializeOptions = new JsonSerializeOptions {
			ArrayLengthPrefix = false,
			ClassTag = "class",
			DateFormat = "O",
			TimeSpanFormat = "c",
			DecimalAsString = false,
			EnumAsString = false,
			FieldSeparator = string.Empty,
			IgnoreCompact = false,
			Indent = string.Empty,
			Int64AsString = false,
			MaxOnelineFields = 0,
			SaveClass = JsonSaveClass.None,
			Unordered = false,
		};

		static ParticularCookingRules()
		{
			foreach (var item in meta.Items) {
				fieldNameToYuzuMetaItemCache.Add(item.Name, item);
			}
		}

		public void Override(string fieldName, bool propagate, ParticularCookingRules source)
		{
			FieldOverrides[fieldNameToYuzuMetaItemCache[fieldName]] = (propagate, source);
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
				Bundles = new[] { "Data" },
				Ignore = false,
				Only = false,
				ADPCMLimit = 100,
				AtlasOptimization = AtlasOptimization.Memory,
				ModelCompression = ModelCompression.Deflate,
				FieldOverrides = new Dictionary<Meta.Item, (bool, ParticularCookingRules)>(),
				AtlasItemPadding = 1,
				AtlasDebug = false,
				MaxAtlasSize = 2048,
			});
			return defaultRules[platform];
		}

		public ParticularCookingRules InheritClone()
		{
			var r = (ParticularCookingRules)MemberwiseClone();
			r.FieldOverrides = new Dictionary<Meta.Item, (bool, ParticularCookingRules)>();
			foreach (var (k, v) in FieldOverrides) {
				if (v.Propagate) {
					r.FieldOverrides.Add(k, v);
				}
			}
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

		public readonly Dictionary<Target, ParticularCookingRules> TargetRules =
			new Dictionary<Target, ParticularCookingRules>();
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
		public bool AtlasDebug => EffectiveRules.AtlasDebug;
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
				if (EffectiveRules != null) {
					EffectiveRules.Ignore = value;
				}
			}
		}

		public bool Only => EffectiveRules.Only;

		public string Alias => EffectiveRules.Alias;

		public ParticularCookingRules EffectiveRules { get; private set; }

		public IEnumerable<(Target Target, ParticularCookingRules Rules)> Enumerate()
		{
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
		}

		public CookingRules InheritClone(Target target, string sourcePath)
		{
			var r = new CookingRules(false);
			r.SourcePath = sourcePath;
			r.Parent = this;
			foreach (var kv in TargetRules) {
				r.TargetRules.Add(kv.Key, kv.Value.InheritClone());
			}
			if (EffectiveRules == null) {
				DeduceEffectiveRules(target);
			}
			r.EffectiveRules = EffectiveRules.InheritClone();
			return r;
		}

		public void Save()
		{
			using var fs = AssetBundle.Current.OpenFile(SourcePath, FileMode.Create);
			using var sw = new StreamWriter(fs);
			foreach (var kv in TargetRules) {
				SaveCookingRules(sw, kv.Value, kv.Key);
			}
		}

		public string FieldValueToString(Meta.Item yi, object value)
		{
			if (value == null) {
				return string.Empty;
			}

			if (yi.Name == "Bundles") {
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
			var targetString = target == Target.RootTarget ? string.Empty : $"({target.Name})";
			foreach (var (yi, (propagate, source)) in rules.FieldOverrides) {
				var value = yi.GetValue(rules);
				var valueString = FieldValueToString(yi, value);
				if (!string.IsNullOrEmpty(valueString) && source == rules) {
					sw.Write($"{(propagate ? "!" : string.Empty)}{yi.Name}{targetString} {valueString}\n");
				}
			}
		}

		public void DeduceEffectiveRules(Target target)
		{
			EffectiveRules ??= TargetRules[Target.RootTarget].InheritClone();
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
					foreach (var (i, propagate) in targetRules.FieldOverrides) {
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

		public bool HasOwnOverrides()
		{
			var r = false;
			foreach (var cr in TargetRules) {
				r = r || cr.Value.FieldOverrides.Any(o => !o.Value.Propagate || o.Value.Source == cr.Value);
			}
			return r;
		}
	}

	public class CookingRulesBuilder
	{
		public const string CookingRulesFilename = "#CookingRules.txt";
		public const string DirectoryNameToken = "${DirectoryName}";
		private static readonly Dictionary<(string bundlePath, string targetName), CacheRecord> cache =
			new Dictionary<(string, string), CacheRecord>();

		private class CacheRecord
		{
			public bool Dirty;
			public Dictionary<string, CookingRules> Map =
				new Dictionary<string, CookingRules>(StringComparer.Ordinal);
			private readonly IFileSystemWatcher watcher;
			public CacheRecord(string path)
			{
				watcher = new Lime.FileSystemWatcher(path, includeSubdirectories: true);
				watcher.Changed += p => OnChanged(p);
				watcher.Created += p => OnChanged(p);
				watcher.Deleted += p => OnChanged(p);
				watcher.Renamed += (_, p) => OnChanged(p);
				void OnChanged(string p)
				{
					if (
						!Path.GetFileName(p)?.Equals(UnpackedAssetBundle.IndexFile, StringComparison.Ordinal)
						?? false
					) {
						Dirty = true;
					}
				}
			}
		}

		// pass target as null to build cooking rules disregarding targets
		public static Dictionary<string, CookingRules> Build(AssetBundle bundle, Target target, string path = null)
		{
			CacheRecord cacheRecord = null;
			if (bundle is UnpackedAssetBundle unpackedBundle && path == null) {
				var bundlePath = unpackedBundle.BaseDirectory;
				var targetName = target?.Name ?? string.Empty;
				if (!cache.TryGetValue((bundlePath, targetName), out cacheRecord)) {
					cache.Add((bundlePath, targetName), cacheRecord = new CacheRecord(bundlePath));
				} else {
					if (!cacheRecord.Dirty) {
						return cacheRecord.Map;
					} else {
						cacheRecord.Map.Clear();
						cacheRecord.Dirty = false;
					}
				}
			}
			Console.WriteLine("Building Cooking Rules.");
			var sw = System.Diagnostics.Stopwatch.StartNew();
			var pathStack = new Stack<string>();
			var rulesStack = new Stack<CookingRules>();
			var map = cacheRecord?.Map ?? new Dictionary<string, CookingRules>(StringComparer.Ordinal);
			pathStack.Push(string.Empty);
			var rootRules = new CookingRules();
			rootRules.DeduceEffectiveRules(target);
			rulesStack.Push(rootRules);
			var files = bundle.EnumerateFiles(path).ToList();
			files.Sort();
			foreach (var filePath in files) {
				while (!filePath.StartsWith(pathStack.Peek())) {
					rulesStack.Pop();
					pathStack.Pop();
				}
				if (Path.GetFileName(filePath) == CookingRulesFilename) {
					var dirName = AssetPath.GetDirectoryName(filePath);
					pathStack.Push(dirName == string.Empty ? string.Empty : dirName + "/");
					var rules = ParseCookingRules(bundle, rulesStack.Peek(), filePath, target);
					rulesStack.Push(rules);
					// Add 'ignore' cooking rules for this #CookingRules.txt itself
					var ignoreRules = rules.InheritClone(target, filePath);
					ignoreRules.Ignore = true;
					map[filePath] = ignoreRules;
					var directoryName = pathStack.Peek();
					if (!string.IsNullOrEmpty(directoryName)) {
						directoryName = directoryName.Remove(directoryName.Length - 1);
						// it is possible for map to not contain this directoryName
						// since not every IFileEnumerator enumerates directories
						if (map.ContainsKey(directoryName)) {
							map[directoryName] = rules;
						}
					}
				} else {
					if (filePath.EndsWith(".txt", StringComparison.Ordinal)) {
						var filename = filePath.Remove(filePath.Length - 4);
						if (bundle.FileExists(filename)) {
							continue;
						}
					}
					var rulesFile = filePath + ".txt";
					var rules = rulesStack.Peek();
					if (bundle.FileExists(rulesFile)) {
						rules = ParseCookingRules(bundle, rulesStack.Peek(), rulesFile, target);
						// Add 'ignore' cooking rules for this cooking rules text file
						var ignoreRules = rules.InheritClone(target, rulesFile);
						ignoreRules.Ignore = true;
						map[rulesFile] = ignoreRules;
					}
					map[filePath] = rules;
				}
			}
			sw.Stop();
			System.Console.WriteLine($"Done building cooking rules ({sw.ElapsedMilliseconds}ms).");
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
						"Error parsing PVR format. Must be one of: PVRTC4, PVRTC4_Forced, PVRTC2, RGBA4, RGB565, ARGB8"
					);
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

		private static CookingRules ParseCookingRules(
			AssetBundle bundle, CookingRules basicRules, string path, Target target
		) {
			var rules = basicRules.InheritClone(target, path);
			try {
				using var s = bundle.OpenFile(path);
				TextReader r = new StreamReader(s);
				string line;
				while ((line = r.ReadLine()) != null) {
					line = line.Trim();
					if (line == string.Empty) {
						continue;
					}
					var words = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
					if (words.Length < 2) {
						throw new Lime.Exception("Invalid rule format");
					}
					foreach (ref var word in words.AsSpan()) {
						word = word.Trim();
					}
					var ruleTarget = Target.RootTarget;
					bool propagateRule = false;
					if (words[0].StartsWith("!")) {
						words[0] = words[0][1..];
						propagateRule = true;
					}
					if (words[0].EndsWith(")")) {
						int cut = words[0].IndexOf('(');
						if (cut >= 0) {
							string targetName = words[0].Substring(cut + 1, words[0].Length - cut - 2);
							words[0] = words[0].Substring(0, cut);
							foreach (var t in The.Workspace.Targets) {
								if (targetName == t.Name) {
									ruleTarget = t;
									break;
								}
							}
							if (ruleTarget == Target.RootTarget) {
								throw new Lime.Exception($"Invalid target: {targetName}");
							}
						}
					}
					var targetRules = rules.TargetRules[ruleTarget];
					if (ruleTarget != Target.RootTarget && !CanSetRulePerTarget(words[0], ruleTarget)) {
						throw new Lime.Exception($"Invalid platform {target.Platform} for cooking rule {words[0]}");
					}
					ParseRule(targetRules, words, path, bundle, propagateRule);
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

		private static void ParseRule(
			ParticularCookingRules rules,
			IReadOnlyList<string> words,
			string path,
			AssetBundle bundle,
			bool propagateRule
		) {
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
					rules.Bundles = words.Skip(1).ToArray();
					break;
				case "Ignore":
					rules.Ignore = ParseBool(words[1]);
					break;
				case "Only":
					rules.Only = ParseBool(words[1]);
					break;
				case "Alias":
					// Alias is defined relative to the directory where cooking rules files is placed and
					// left unexpanded. Cooking rules should't be modified when parsing them because that way
					// cooking rules editor will save the modification.
					rules.Alias = words[1];
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
				case "AtlasDebug":
					rules.AtlasDebug = ParseBool(words[1]);
					break;
				case "MaxAtlasSize":
					rules.MaxAtlasSize = int.Parse(words[1]);
					break;
				default:
					throw new Lime.Exception("Unknown attribute {0}", words[0]);
				}
				rules.Override(words[0], propagateRule, rules);
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
	}
}
