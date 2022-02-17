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
		private static readonly Dictionary<string, ITexture> fileTypeTextures;

		static LookupFilesSection()
		{
			var sceneIcon = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;
			fileTypeTextures = new Dictionary<string, ITexture> {
				{ ".tan", sceneIcon },
				{ ".t3d", sceneIcon },
				{ ".fbx", NodeIconPool.GetTexture(typeof(Model3D)) },
				{ ".png", NodeIconPool.GetTexture(typeof(Image)) },
				{ ".ogg", NodeIconPool.GetTexture(typeof(Audio)) },
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
				var cancellationToken = fillingLookupCancellationSource.Token;
				mutableItemList = await System.Threading.Tasks.Task.Run(
					() => GetLookupItems(lookupWidget, cancellationToken),
					cancellationToken
				);
				lookupWidget.MarkFilterAsDirty();
				fillingLookupCancellationSource = null;
			} catch (OperationCanceledException) {
				// Suppress
			} catch (System.Exception exception) {
				Console.WriteLine(exception);
				fillingLookupCancellationSource = null;
			}
		}

		private List<LookupItem> GetLookupItems(LookupWidget lookupWidget, CancellationToken cancellationToken)
		{
			var items = new List<LookupItem>();
			foreach (var (_, asset) in Project.Current.AssetDatabase) {
				cancellationToken.ThrowIfCancellationRequested();
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
							if (Document.Current == null) {
								AlertDialog.Show($"Open any document to add an audio");
								return;
							}
							CreateAudioFromAsset.Perform(asset.Path);
							Sections.Drop();
						};
						break;
					default:
						continue;
				}
				items.Add(new LookupDialogItem(
					fileName + asset.Type,
					asset.Path + asset.Type,
					fileTypeTextures[asset.Type],
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
			var filteredItemsLimit =
				CoreUserPreferences.Instance.LookupItemsLimit >= 1
					? CoreUserPreferences.Instance.LookupItemsLimit
					: 30;
			var filteredItems = new List<LookupItem>();
			var success = false;
			applyingFilterCancellationSource = new CancellationTokenSource();
			try {
				var cancellationToken = applyingFilterCancellationSource.Token;
				var filterEnumerable = ApplyLookupFilter(text, mutableItemList, cancellationToken);
				await System.Threading.Tasks.Task.Run(
					() => {
						foreach (var item in filterEnumerable) {
							cancellationToken.ThrowIfCancellationRequested();
							filteredItems.Add(item);
							if (filteredItems.Count >= filteredItemsLimit) {
								break;
							}
						}
					},
					cancellationToken
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
				lookupWidget.AddRange(filteredItems);
				lookupWidget.SelectItem(index: 0);
				lookupWidget.ScrollView.ScrollPosition = 0;
			}
		}

		private void ApplyingLookupCancel()
		{
			applyingFilterCancellationSource?.Cancel();
			applyingFilterCancellationSource = null;
		}

		protected override IEnumerable<LookupItem> ApplyLookupFilter(string text, IReadOnlyList<LookupItem> items)
		{
			return items;
		}
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
			if (Document.Current != null) {
				lookupWidget.AddItem(new LookupDialogItem(
					"Add As External Scene",
					null,
					() => {
						try {
							if (Utils.AssertCurrentDocument(assetPath, assetType)) {
								CreateNodeFromAsset.Perform(assetPath);
							}
						} catch (System.Exception exception) {
							AlertDialog.Show(exception.Message);
						}
						Sections.Drop();
					}
				));
			}
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
			if (!RequireDocumentOrAddAlertItem(lookupWidget, "Open any document to add a image")) {
				return;
			}
			foreach (var imageType in imageTypes) {
				if (!NodeCompositionValidator.Validate(Document.Current.Container.GetType(), imageType)) {
					continue;
				}
				var imageTypeClosed = imageType;
				lookupWidget.AddItem(new LookupDialogItem(
					$"Create {imageType.Name}",
					null,
					() => {
						CreateTexturedWidgetFromAsset.Perform(assetPath, imageTypeClosed);
						Sections.Drop();
					}
				));
			}
		}
	}
}
