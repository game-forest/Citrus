using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Console = System.Console;

namespace Tangerine
{
	public class LookupFilesSection : LookupSection
	{
		private const string PrefixConst = "f";
		private static readonly Dictionary<string, Icon> fileTypesIcons;

		static LookupFilesSection()
		{
			var sceneIcon = IconPool.GetIcon("Lookup.SceneFileIcon");
			fileTypesIcons = new Dictionary<string, Icon> {
				{ ".tan", sceneIcon },
				{ ".t3d", sceneIcon },
				{ ".fbx", NodeIconPool.GetIcon(typeof(Model3D)) },
				{ ".png", NodeIconPool.GetIcon(typeof(Image)) },
				{ ".ogg", NodeIconPool.GetIcon(typeof(Audio)) },
			};
		}

		private IReadOnlyList<LookupItem> mutableItemList = new List<LookupItem>(0);
		private CancellationTokenSource fillingLookupCancellationSource;
		private CancellationTokenSource applyingFilterCancellationSource;

		public override string Breadcrumb { get; } = "Search File";
		public override string Prefix { get; } = $"{PrefixConst}:";
		public override string HelpText { get; } = $"Type '{PrefixConst}:' to search for file in the current project";

		public LookupFilesSection(LookupSections sections) : base(sections) { }

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (!RequireProjectOrAddAlertItem(lookupWidget, "Open any project to use Go To File function")) {
				return;
			}
			FillingLookupCancel();
			FillLookupAsync(lookupWidget);
		}

		private async void FillLookupAsync(LookupWidget lookupWidget)
		{
			mutableItemList = new List<LookupItem>(0);
			fillingLookupCancellationSource = new CancellationTokenSource();
			try {
				mutableItemList = await System.Threading.Tasks.Task.Run(() => GetLookupItems(lookupWidget), fillingLookupCancellationSource.Token);
				lookupWidget.MarkFilterAsDirty();
				fillingLookupCancellationSource = null;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (System.Exception exception) {
				Console.WriteLine(exception);
				fillingLookupCancellationSource = null;
			}
		}

		private List<LookupItem> GetLookupItems(LookupWidget lookupWidget)
		{
			var items = new List<LookupItem>();
			foreach (var (_, asset) in Project.Current.AssetsDatabase) {
				var fileName = Path.GetFileName(asset.Path);
				Action action;
				switch (asset.Type) {
					case ".tan":
					case ".t3d":
					case ".fbx":
						action = () => Sections.Push(new LookupSceneMenuSection(Sections, asset.Path, asset.Type));
						break;
					case ".png":
						action = () => Sections.Push(new LookupImageMenuSection(Sections, asset.Path, asset.Type));
						break;
					case ".ogg":
						action = () => {
							Document.Current.History.DoTransaction(() => {
								var node = Core.Operations.CreateNode.Perform(typeof(Audio));
								var sample = new SerializableSample(asset.Path);
								SetProperty.Perform(node, nameof(Audio.Sample), sample);
								SetProperty.Perform(node, nameof(Node.Id), fileName);
								SetProperty.Perform(node, nameof(Audio.Volume), 1);
								var key = new Keyframe<AudioAction> {
									Frame = Document.Current.AnimationFrame,
									Value = AudioAction.Play
								};
								SetKeyframe.Perform(node, nameof(Audio.Action), Document.Current.AnimationId, key);
							});
							Sections.Drop();
						};
						break;
					default:
						continue;
				}
				items.Add(new LookupDialogItem(
					fileName + asset.Type,
					asset.Path + asset.Type,
					fileTypesIcons[asset.Type].AsTexture,
					action
				));
			}
			return items;
		}

		public override void Dropped()
		{
			FillingLookupCancel();
			ApplyingLookupCancel();
			mutableItemList = new List<LookupItem>(0);
		}

		private void FillingLookupCancel()
		{
			fillingLookupCancellationSource?.Cancel();
			fillingLookupCancellationSource = null;
		}

		protected override void ApplyingLookupFilter(LookupWidget lookupWidget, string text)
		{
			ApplyingLookupCancel();
			ApplyingLookupFilterAsync(lookupWidget, text);
		}

		private async void ApplyingLookupFilterAsync(LookupWidget lookupWidget, string text)
		{
			var filteredItemsLimit = CoreUserPreferences.Instance.LookupItemsLimit >= 1 ? CoreUserPreferences.Instance.LookupItemsLimit : 30;
			var filteredItems = new List<LookupItem>();
			var filterEnumarable = base.ApplyLookupFilter(text, mutableItemList);
			var success = false;
			applyingFilterCancellationSource = new CancellationTokenSource();
			try {
				await System.Threading.Tasks.Task.Run(
					() => {
						foreach (var item in filterEnumarable) {
							filteredItems.Add(item);
							if (filteredItems.Count >= filteredItemsLimit) {
								break;
							}
						}
					},
					applyingFilterCancellationSource.Token
				);
				applyingFilterCancellationSource = null;
				success = true;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (System.Exception exception) {
				Console.WriteLine(exception);
				applyingFilterCancellationSource = null;
			}
			if (success) {
				lookupWidget.ClearItems(disposeItems: false);
				foreach (var item in filteredItems) {
					lookupWidget.AddItem(item);
				}
				lookupWidget.SelectItem(index: 0);
				lookupWidget.ScrollView.ScrollPosition = 0;
			}
		}

		private void ApplyingLookupCancel()
		{
			applyingFilterCancellationSource?.Cancel();
			applyingFilterCancellationSource = null;
		}

		protected override IEnumerable<LookupItem> ApplyLookupFilter(string text, IReadOnlyList<LookupItem> items) => items;
	}

	public class LookupSceneMenuSection : LookupSection
	{
		private readonly string assetPath;
		private readonly string assetType;

		public override string Breadcrumb { get; }
		public override string Prefix { get; } = null;

		public LookupSceneMenuSection(LookupSections sections, string assetPath, string assetType) : base(sections)
		{
			this.assetPath = assetPath;
			this.assetType = assetType;
			Breadcrumb = Path.GetFileName(assetPath) + assetType;
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			lookupWidget.AddItem(new LookupDialogItem(
				"Open in New Tab",
				null,
				() => {
					Project.Current.OpenDocument(assetPath);
					Sections.Drop();
				}
			));
			lookupWidget.AddItem(new LookupDialogItem(
				"Add As External Scene",
				null,
				() => {
					if (Utils.AssertCurrentDocument(assetPath, assetType)) {
						var scene = Node.CreateFromAssetBundle(assetPath, persistence: TangerinePersistence.Instance);
						if (NodeCompositionValidator.Validate(Document.Current.Container.GetType(), scene.GetType())) {
							Document.Current.History.DoTransaction(() => {
								var node = Core.Operations.CreateNode.Perform(scene.GetType());
								SetProperty.Perform(node, nameof(Widget.ContentsPath), assetPath);
								SetProperty.Perform(node, nameof(Widget.Id), Path.GetFileNameWithoutExtension(assetPath));
								if (node is IPropertyLocker propertyLocker) {
									var id = propertyLocker.IsPropertyLocked("Id", true) ? scene.Id : Path.GetFileName(assetPath);
									SetProperty.Perform(node, nameof(Node.Id), id);
								}
								if (scene is Widget widget) {
									SetProperty.Perform(node, nameof(Widget.Pivot), Vector2.Half);
									SetProperty.Perform(node, nameof(Widget.Size), widget.Size);
								}
								node.LoadExternalScenes();
								SelectNode.Perform(node);
							});
						} else {
							AlertDialog.Show($"Can't put {scene.GetType()} into {Document.Current.Container.GetType()}");
						}
					}
					Sections.Drop();
				}
			));
		}
	}

	public class LookupImageMenuSection : LookupSection
	{
		private static readonly Type[] imageTypes = {
			typeof(Image),
			typeof(DistortionMesh),
			typeof(NineGrid),
			typeof(TiledImage),
			typeof(ParticleModifier),
		};

		private readonly string assetPath;

		public override string Breadcrumb { get; }
		public override string Prefix { get; } = null;

		public LookupImageMenuSection(LookupSections sections, string assetPath, string assetType) : base(sections)
		{
			this.assetPath = assetPath;
			Breadcrumb = Path.GetFileName(assetPath) + assetType;
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			foreach (var imageType in imageTypes) {
				if (!NodeCompositionValidator.Validate(Document.Current.Container.GetType(), imageType)) {
					continue;
				}
				var imageTypeClosed = imageType;
				lookupWidget.AddItem(new LookupDialogItem(
					$"Create {imageType.Name}",
					null,
					() => {
						Document.Current.History.DoTransaction(() => {
							var node = Core.Operations.CreateNode.Perform(imageTypeClosed);
							var texture = new SerializableTexture(assetPath);
							var nodeSize = (Vector2)texture.ImageSize;
							var nodeId = Path.GetFileNameWithoutExtension(assetPath);
							if (node is Widget) {
								SetProperty.Perform(node, nameof(Widget.Texture), texture);
								SetProperty.Perform(node, nameof(Widget.Pivot), Vector2.Half);
								SetProperty.Perform(node, nameof(Widget.Size), nodeSize);
								SetProperty.Perform(node, nameof(Widget.Id), nodeId);
							} else if (node is ParticleModifier) {
								SetProperty.Perform(node, nameof(ParticleModifier.Texture), texture);
								SetProperty.Perform(node, nameof(ParticleModifier.Size), nodeSize);
								SetProperty.Perform(node, nameof(ParticleModifier.Id), nodeId);
							}
						});
						Sections.Drop();
					}
				));
			}
		}
	}
}
