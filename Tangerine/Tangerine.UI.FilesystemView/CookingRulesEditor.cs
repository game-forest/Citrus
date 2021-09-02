using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using Lime;
using Orange;
using Tangerine.Core;
using Yuzu;
using Yuzu.Metadata;
using CookingRulesMap = System.Collections.Generic.Dictionary<string, Orange.CookingRules>;

namespace Tangerine.UI.FilesystemView
{
	public class CookingRulesEditor
	{
		// attached to widget responsible for displaying override
		// of a certain field in a cooking rules
		private class PropertyOverrideComponent : NodeComponent
		{
			public CookingRules Rules;
			public Yuzu.Metadata.Meta.Item YuzuItem;
		}

		private Target ActiveTarget { get; set; }
		private readonly Toolbar toolbar;
		public Widget RootWidget;
		private readonly ThemedScrollView scrollView;
		private FilesystemSelection savedFilesystemSelection;
		private const float RowHeight = 16.0f;
		private Action<string> navigateAndSelect;
		private Dictionary<Yuzu.Metadata.Meta.Item, bool> cookingRulesFoldState = new Dictionary<Meta.Item, bool>();
		Texture2D cachedZebraTexture = null;

		public CookingRulesEditor(Action<string> navigateAndSelect)
		{
			this.navigateAndSelect = navigateAndSelect;
			scrollView = new ThemedScrollView();
			scrollView.Content.Layout = new VBoxLayout();
			ThemedDropDownList targetSelector;
			toolbar = new Toolbar();
			toolbar.Nodes.AddRange(
				targetSelector = new ThemedDropDownList {
					LayoutCell = new LayoutCell(Alignment.Center)
				}
			);
			targetSelector.Items.Add(new CommonDropDownList.Item("None", null));
			foreach (var t in Orange.The.Workspace.Targets) {
				targetSelector.Items.Add(new CommonDropDownList.Item(t.Name, t));
			}
			targetSelector.Changed += (value) => {
				if (value.ChangedByUser) {
					ActiveTarget = (Target)value.Value;
					Invalidate(savedFilesystemSelection);
				}
			};
			targetSelector.Index = 0;
			ActiveTarget = null;
			RootWidget = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					toolbar,
					scrollView
				}
			};
		}

		public void Invalidate(FilesystemSelection filesystemSelection)
		{
			savedFilesystemSelection = filesystemSelection;
			scrollView.Content.Nodes.Clear();
			if (RootWidget.Parent == null) {
				return;
			}
			if (filesystemSelection == null || filesystemSelection.Empty) {
				return;
			}
			var targetDir = Path.GetDirectoryName(filesystemSelection.First());
			if (Orange.The.Workspace.AssetsDirectory == null || !targetDir.StartsWith(Orange.The.Workspace.AssetsDirectory)) {
				// We're somewhere outside the project directory
				return;
			}
			var t = Orange.CookingRulesBuilder.Build(
				new BundleForCookingRulesBuilder(AssetBundle.Current, Orange.The.Workspace.AssetsDirectory, targetDir),
				ActiveTarget,
				AssetBundle.Current.FromSystemPath(targetDir));
			foreach (var path in filesystemSelection) {
				CreateEditingInterfaceForPath(t, path);
			}
			scrollView.Content.Presenter = new SyncDelegatePresenter<Widget>((w) => {
				if (cachedZebraTexture == null) {
					cachedZebraTexture = new Texture2D();
					cachedZebraTexture.LoadImage(new[] { Theme.Colors.ZebraColor1, Theme.Colors.ZebraColor2 }, 1, 2);
					cachedZebraTexture.TextureParams = new TextureParams {
						WrapMode = TextureWrapMode.Repeat,
						MinMagFilter = TextureFilter.Nearest
					};
				}
				w.PrepareRendererState();
				Renderer.DrawSprite(cachedZebraTexture, Color4.White, Vector2.Zero, w.Size, Vector2.Zero, w.Size / (Vector2)cachedZebraTexture.ImageSize / RowHeight);
			});
		}

		private void CreateEditingInterfaceForPath(CookingRulesMap rulesMap, string path)
		{
			var key = NormalizePath(path);
			if (!rulesMap.ContainsKey(key)) {
				throw new Lime.Exception("CookingRulesCollection should already contain a record for the item");
			}
			scrollView.Content.AddNode(CreateHeader(path));
			var meta = Meta.Get(typeof(ParticularCookingRules), new CommonOptions());
			foreach (var yi in meta.Items) {
				CreateWidgetsForSingleField(rulesMap, path, yi);
			}
		}

		private Widget CreateHeader(string path)
		{
			return new Frame {
				Nodes = {
					new ThemedSimpleText {
						Text = path,
						OverflowMode = TextOverflowMode.Ignore,
					}
				},
				MinHeight = RowHeight,
				MinWidth = 100,
				Presenter = new WidgetFlatFillPresenter(ColorTheme.Current.Inspector.CategoryLabelBackground)
			};
		}

		private void CreateWidgetsForSingleField(CookingRulesMap rulesMap, string path, Meta.Item yi)
		{
			Widget headerWidget;
			Widget overridesWidget;
			var fieldRootWidget = new Widget {
				Layout = new VBoxLayout(),
				Nodes = {
					(headerWidget = new Widget {
						Layout = new HBoxLayout {
							IgnoreHidden = false,
						},
						// TODO: maybe some Metrics.ScrollView.SliderWidth ? (though ScrollView is decorated in DesktopTheme which is inside Lime)
						Padding = new Thickness { Right = 10.0f },
					}),
					(overridesWidget = new Widget {
						Visible = cookingRulesFoldState.TryGetValue(yi, out bool v) ? v : false,
						Layout = new VBoxLayout(),
						Padding = new Thickness {
							Left = 30.0f
						}
					}),
				}
			};
			fieldRootWidget.AddChangeWatcher(() => WidgetContext.Current.NodeUnderMouse, (value) => {
				if (value != null && value.Parent == fieldRootWidget) {
					Window.Current?.Invalidate();
				}
			});
			scrollView.Content.AddNode(fieldRootWidget);
			bool rootAdded = false;
			var key = NormalizePath(path);
			var parent = rulesMap[key];
			var affectingRules = FindAffectingCookingRules(rulesMap[key], yi);
			while (parent != null) {
				var isRoot = parent == rulesMap[key];
				foreach (var (target, rules) in parent.Enumerate()) {
					if (isRoot && !rootAdded) {
						rootAdded = true;
						CreateHeaderWidgets(rulesMap, path, yi, headerWidget, overridesWidget, parent);
					}
					if (rules.FieldOverrides.Contains(yi)) {
						overridesWidget.AddNode(CreateOverridesWidgets(target, yi, parent, rules == affectingRules));
					}
				}
				parent = parent.Parent;
			}
		}

		private ParticularCookingRules FindAffectingCookingRules(CookingRules topmostRules, Yuzu.Metadata.Meta.Item yi)
		{
			List<Target> targets = new List<Target>();
			if (ActiveTarget != null) {
				var t = ActiveTarget;
				while (t != null) {
					targets.Add(t);
					t = t.BaseTarget;
				}
			}
			var rules = topmostRules;
			while (rules != null) {
				foreach (var t in targets) {
					if (rules.TargetRules.TryGetValue(t, out ParticularCookingRules targetRules) && targetRules.FieldOverrides.Contains(yi)) {
						return targetRules;
					}
				}
				if (rules.CommonRules.FieldOverrides.Contains(yi)) {
					return rules.CommonRules;
				}
				rules = rules.Parent;
			}
			return null;
		}

		private void CreateHeaderWidgets(CookingRulesMap rulesMap, string path, Meta.Item yi,
			Widget headerWidget, Widget overridesWidget, CookingRules rules)
		{
			SimpleText computedValueText;
			Button createOrDestroyOverride = null;
			headerWidget.HitTestTarget = true;
			headerWidget.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((widget) => {
				if (widget.IsMouseOver()) {
					widget.PrepareRendererState();
					Renderer.DrawRect(
						Vector2.Zero,
						widget.Size,
						Theme.Colors.SelectedBackground.Transparentify(0.8f));
				}
			}));
			Func<ITexture> btnTexture = () => IsOverridedByAssociatedCookingRules(rulesMap, path, yi)
				? IconPool.GetTexture("Filesystem.Cross")
				: IconPool.GetTexture("Filesystem.Plus");
			Widget foldButton;
			headerWidget.Nodes.AddRange(
				foldButton = CreateFoldButton(overridesWidget, yi),
				new ThemedSimpleText {
					ForceUncutText = false,
					VAlignment = VAlignment.Center,
					HAlignment = HAlignment.Left,
					OverflowMode = TextOverflowMode.Ellipsis,
					LayoutCell = new LayoutCell { StretchX = 1 },
					Size = new Vector2(150, RowHeight),
					MinSize = new Vector2(100, RowHeight),
					MaxSize = new Vector2(200, RowHeight),
					Text = yi.Name,
				},
				computedValueText = new ThemedSimpleText {
					LayoutCell = new LayoutCell { StretchX = 3 },
					ForceUncutText = false,
					HAlignment = HAlignment.Left,
					Size = new Vector2(150, RowHeight),
					MinSize = new Vector2(50, RowHeight),
					MaxSize = new Vector2(300, RowHeight),
				},
				createOrDestroyOverride = new ToolbarButton {
					Texture = btnTexture(),
					Clicked = () => CreateOrDestroyFieldOverride(rulesMap, path, yi, overridesWidget, createOrDestroyOverride),
					Enabled = CookingRulesBuilder.CanSetRulePerTarget(yi.Name, ActiveTarget),
			}
			);
			headerWidget.Clicked = foldButton.Clicked;
			createOrDestroyOverride.Padding = Thickness.Zero;
			createOrDestroyOverride.Size = createOrDestroyOverride.MinMaxSize = RowHeight * Vector2.One;
			if (IsCookingRulesFileItself(path)) {
				rules = GetAssociatedCookingRules(rulesMap, path);
			}
			computedValueText.AddChangeWatcher(() => yi.GetValue(rules.EffectiveRules),
				(o) => computedValueText.Text = rules.FieldValueToString(yi, yi.GetValue(rules.EffectiveRules)));
		}

		private Widget CreateFoldButton(Widget container, Yuzu.Metadata.Meta.Item yi)
		{
			var b = new ThemedExpandButton {
				Size = Vector2.One * RowHeight,
				MinMaxSize = Vector2.One * RowHeight,
				Padding = Thickness.Zero,
				Highlightable = false,
			};
			b.Clicked = () => {
				container.Visible = !container.Visible;
				cookingRulesFoldState[yi] = container.Visible;
			};
			b.Updated += (dt) => {
				b.Visible = container.Nodes.Count != 0;
			};
			return b;
		}

		private bool IsCookingRulesFileItself(string path)
		{
			path = AssetPath.CorrectSlashes(path);
			if (File.GetAttributes(path) == FileAttributes.Directory) {
				return false;
			}
			bool isPerDirectory = Path.GetFileName(path) == CookingRulesBuilder.CookingRulesFilename;
			bool isPerFile = path.EndsWith(".txt") && File.Exists(path.Remove(path.Length - 4));
			return isPerDirectory || isPerFile;
		}


		private void CreateOrDestroyFieldOverride(CookingRulesMap rulesMap, string path, Meta.Item yi, Widget overridesWidget, Button addRemoveField)
		{
			var overrided = IsOverridedByAssociatedCookingRules(rulesMap, path, yi);
			var key = NormalizePath(path);
			if (overrided) {
				var rules = GetAssociatedCookingRules(rulesMap, path);
				var targetRules = RulesForActiveTarget(rules);
				targetRules.FieldOverrides.Remove(yi);
				rules.Save();
				if (!rules.HasOverrides()) {
					var acr = GetAssociatedCookingRules(rulesMap, rules.SystemSourcePath);
					if (!acr.SystemSourcePath.EndsWith(key)) {
						rulesMap[key] = rules.Parent;
					}
					rulesMap.Remove(NormalizePath(acr.SystemSourcePath));
					System.IO.File.Delete(rules.SystemSourcePath);
				}
				List<Node> toUnlink = new List<Node>();
				foreach (var node in overridesWidget.Nodes) {
					var c = node.Components.Get<PropertyOverrideComponent>();
					if (c.Rules == rules && c.YuzuItem == yi) {
						toUnlink.Add(node);
					}
				}
				foreach (var node in toUnlink) {
					node.Unlink();
				}
				addRemoveField.Texture = IconPool.GetTexture("Filesystem.Plus");
			} else {
				var associatedRules = GetAssociatedCookingRules(rulesMap, path, true);
				var targetRules = RulesForActiveTarget(associatedRules);
				targetRules.Override(yi.Name);
				associatedRules.Save();
				addRemoveField.Texture = IconPool.GetTexture("Filesystem.Cross");
				overridesWidget.AddNode(CreateOverridesWidgets(ActiveTarget, yi, associatedRules, true));
			}
		}

		private Widget CreateOverridesWidgets(Target target, Meta.Item yi, CookingRules rules, bool affectsActiveTarget)
		{
			Widget innerContainer;
			var SystemSourcePathText = string.IsNullOrEmpty(rules.SystemSourcePath)
				? "Default"
				: rules.SystemSourcePath.Substring(The.Workspace.AssetsDirectory.Length);
			var targetName = target == null ? "" : $" ({target.Name})";
			var container = new Widget {
				Padding = new Thickness { Right = 30 },
				Nodes = {
					(innerContainer = new Widget {
						Layout = new HBoxLayout(),
					}),
					new ThemedSimpleText(SystemSourcePathText + targetName) {
						FontHeight = 16,
						ForceUncutText = false,
						OverflowMode = TextOverflowMode.Ellipsis,
						HAlignment = HAlignment.Right,
						VAlignment = VAlignment.Center,
						MinSize = new Vector2(100, RowHeight),
						MaxSize = new Vector2(500, RowHeight)
					},
					new ToolbarButton {
						Texture = IconPool.GetTexture("Filesystem.ArrowRight"),
						Padding = Thickness.Zero,
						Size = RowHeight * Vector2.One,
						MinMaxSize = RowHeight * Vector2.One,
						Clicked = () => navigateAndSelect(rules.SystemSourcePath),
					}
				},
				Layout = new HBoxLayout(),
			};
			container.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
				w.PrepareRendererState();
				if (affectsActiveTarget) {
					Renderer.DrawRect(Vector2.Right * -20.0f, w.Size, Color4.Green.Lighten(0.5f).Transparentify(0.5f));
				} else {
					Renderer.DrawLine(10.0f - 30.0f, w.Height * 0.6f, w.Width - 10.0f, w.Height * 0.6f, Color4.Black.Transparentify(0.5f), 1.0f);
				}
			}));
			container.Components.Add(new PropertyOverrideComponent {
				Rules = rules,
				YuzuItem = yi,
			});
			var targetRules = RulesForTarget(rules, target);
			var editorParams = new PropertyEditorParams(targetRules, yi.Name) {
				ShowLabel = false,
				PropertySetter = (owner, name, value) => {
					yi.SetValue(owner, value);
					targetRules.Override(name);
					rules.DeduceEffectiveRules(target);
					rules.Save();
				},
				NumericEditBoxFactory = () => {
					var r = new ThemedNumericEditBox();
					r.MinMaxHeight = r.Height = RowHeight;
					r.TextWidget.VAlignment = VAlignment.Center;
					r.TextWidget.Padding = new Thickness(r.TextWidget.Padding.Left, r.TextWidget.Right(), 0.0f, 0.0f);
					return r;
				},
				DropDownListFactory = () => {
					var r = new ThemedDropDownList();
					r.MinMaxHeight = r.Height = RowHeight;
					return r;
				},
				EditBoxFactory = () => {
					var r = new ThemedEditBox();
					r.MinMaxHeight = r.Height = RowHeight;
					r.TextWidget.Padding = new Thickness(r.TextWidget.Padding.Left, r.TextWidget.Right(), 0.0f, 0.0f);
					return r;
				},
			};
			var editor = CreatePropertyEditorForType(yi, editorParams);
			innerContainer.AddNode(editor.ContainerWidget);
			return container;
		}

		private static IPropertyEditor CreatePropertyEditorForType(Meta.Item yi, IPropertyEditorParams editorParams)
		{
			if (yi.Type.IsEnum) {
				return (IPropertyEditor)Activator.CreateInstance(typeof(EnumPropertyEditor<>).MakeGenericType(yi.Type), editorParams);
			} else if (yi.Type == typeof(string)) {
				return new StringPropertyEditor(editorParams);
			} else if (yi.Type == typeof(int)) {
				return new IntPropertyEditor(editorParams);
			} else if (yi.Type == typeof(bool)) {
				return new BooleanPropertyEditor(editorParams);
			} else if (yi.Type == typeof(float)) {
				return new FloatPropertyEditor(editorParams);
			} else {
				throw new InvalidOperationException();
			}
		}

		private bool IsOverridedByAssociatedCookingRules(CookingRulesMap rulesMap, string path, Meta.Item yi)
		{
			var rules = GetAssociatedCookingRules(rulesMap, path);
			return rules != null && RulesForActiveTarget(rules).FieldOverrides.Contains(yi);
		}

		private ParticularCookingRules RulesForActiveTarget(CookingRules CookingRules)
		{
			return RulesForTarget(CookingRules, ActiveTarget);
		}

		private static ParticularCookingRules RulesForTarget(CookingRules CookingRules, Target target)
		{
			return target == null ? CookingRules.CommonRules : CookingRules.TargetRules[target];
		}

		private static CookingRules GetAssociatedCookingRules(CookingRulesMap rulesMap, string path, bool createIfNotExists = false)
		{
			Action<string, CookingRules> ignoreRules = (p, r) => {
				r = r.InheritClone();
				r.Ignore = true;
				rulesMap[NormalizePath(p)] = r;
			};
			path = AssetPath.CorrectSlashes(path);
			string key = NormalizePath(path);
			CookingRules cookingRules;
			if (File.GetAttributes(path) == FileAttributes.Directory) {
				// Directory
				var rulesPath = AssetPath.Combine(path, Orange.CookingRulesBuilder.CookingRulesFilename);
				if (rulesMap.ContainsKey(key)) {
					cookingRules = rulesMap[key];
					if (cookingRules.SystemSourcePath != rulesPath) {
						if (createIfNotExists) {
							cookingRules = cookingRules.InheritClone();
							rulesMap[key] = cookingRules;
							ignoreRules(rulesPath, cookingRules);
						} else {
							return null;
						}
					}
				} else {
					throw new Lime.Exception("CookingRules record for directory should already be present in collection.");
				}
				cookingRules.SystemSourcePath = rulesPath;
			} else {
				bool isPerDirectory = Path.GetFileName(path) == CookingRulesBuilder.CookingRulesFilename;
				bool isPerFile = path.EndsWith(".txt") && File.Exists(path.Remove(path.Length - 4));
				// ???
				string filename = isPerFile ? path.Remove(path.Length - 4) : path;
				if (isPerDirectory || isPerFile) {
					// Cooking Rules File itself
					if (rulesMap.ContainsKey(key)) {
						cookingRules = rulesMap[key].Parent;
					} else {
						throw new Lime.Exception("CookingRules record for cooking rules file itself should already be present in collection.");
					}
				} else {
					// Regular File
					var rulesPath = path + ".txt";
					var rulesKey = NormalizePath(rulesPath);
					if (rulesMap.ContainsKey(rulesKey)) {
						cookingRules = rulesMap[rulesKey].Parent;
					} else if (!createIfNotExists) {
						return null;
					} else if (rulesMap.ContainsKey(NormalizePath(path))) {
						cookingRules = rulesMap[NormalizePath(path)].InheritClone();
						cookingRules.SystemSourcePath = rulesPath;
						ignoreRules(rulesPath, cookingRules);
						rulesMap[key] = cookingRules;
					} else {
						throw new Lime.Exception("CookingRules record for any regular file should already be present in collection.");
					}
				}
			}
			return cookingRules;
		}

		private static string NormalizePath(string path)
		{
			if (!IsInAssetDir(path)) {
				throw new ConstraintException("Normalized path must be in asset directory");
			}
			path = AssetPath.CorrectSlashes(path);
			path = path.Substring(The.Workspace.AssetsDirectory.Length);
			if (path.StartsWith("/")) {
				path = path.Substring(1);
			}
			return path;
		}

		private static bool IsInAssetDir(string path)
		{
			return AssetPath.CorrectSlashes(path).StartsWith(AssetPath.CorrectSlashes(The.Workspace.AssetsDirectory));
		}
	}
}
