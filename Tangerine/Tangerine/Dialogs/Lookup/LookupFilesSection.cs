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
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for file in the current project";

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
			var assetsDirectory = Project.Current.AssetsDirectory;
			var items = new List<LookupItem>();

			void GetFilesRecursively(string directory)
			{
				foreach (var filePath in Directory.GetFiles(directory)) {
					var relativePath = filePath.Substring(assetsDirectory.Length + 1);
					relativePath = AssetPath.CorrectSlashes(relativePath);
					var assetType = Path.GetExtension(relativePath).ToLower();
					var assetPath =
						string.IsNullOrEmpty(assetType) ?
							relativePath :
							relativePath.Substring(0, relativePath.Length - assetType.Length);
					var fileName = Path.GetFileName(assetPath);
					Action action;
					switch (assetType) {
						case ".tan":
						case ".t3d":
						case ".fbx":
							action = () => Sections.Push(new LookupSceneMenuSection(Sections, assetPath, assetType));
							break;
						case ".png":
							action = () => Sections.Push(new LookupImageMenuSection(Sections, assetPath));
							break;
						case ".ogg":
							action = () => {
								Document.Current.History.DoTransaction(() => {
									var node = Core.Operations.CreateNode.Perform(typeof(Audio));
									var sample = new SerializableSample(assetPath);
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
					var item = new LookupDialogItem(lookupWidget, fileName + assetType, assetPath + assetType, action) {
						IconTexture = fileTypesIcons[assetType].AsTexture,
					};
					items.Add(item);
				}
				foreach (var d in Directory.GetDirectories(directory)) {
					GetFilesRecursively(d);
				}
			}
			GetFilesRecursively(assetsDirectory);
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
			const int FilteredItemsLimit = 100;
			var filteredItems = new List<LookupItem>();
			var filterEnumarable = base.ApplyLookupFilter(text, mutableItemList);
			var success = false;
			applyingFilterCancellationSource = new CancellationTokenSource();
			try {
				await System.Threading.Tasks.Task.Run(
					() => {
						foreach (var item in filterEnumarable) {
							filteredItems.Add(item);
							if (filteredItems.Count >= FilteredItemsLimit) {
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
			Breadcrumb = Path.GetFileName(assetPath);
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			lookupWidget.AddItem(new LookupDialogItem(
				lookupWidget,
				"Open in New Tab",
				null,
				() => {
					Project.Current.OpenDocument(assetPath);
					Sections.Drop();
				}
			));
			lookupWidget.AddItem(new LookupDialogItem(
				lookupWidget,
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

		public LookupImageMenuSection(LookupSections sections, string assetPath) : base(sections)
		{
			this.assetPath = assetPath;
			Breadcrumb = Path.GetFileName(assetPath);
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			foreach (var imageType in imageTypes) {
				if (!NodeCompositionValidator.Validate(Document.Current.Container.GetType(), imageType)) {
					continue;
				}
				var imageTypeClosed = imageType;
				lookupWidget.AddItem(new LookupDialogItem(
					lookupWidget,
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
