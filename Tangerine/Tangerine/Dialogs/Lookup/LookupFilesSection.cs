using System;
using System.IO;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine
{
	public class LookupFilesSection : LookupSection
	{
		private const string PrefixConst = "f";

		public override string Breadcrumb { get; } = "Search File";
		public override string Prefix { get; } = $"{PrefixConst} ";
		public override string HelpText { get; } = $"Type '{PrefixConst}' to search for file in the current project";

		public override void FillLookup(LookupWidget lookupWidget)
		{
			if (Project.Current == null) {
				lookupWidget.AddItem(
					"Open any project to use Go To File function",
					() => {
						new FileOpenProject();
						LookupDialog.Sections.Drop();
					}
				);
				return;
			}

			void GetFilesRecursively(string directory)
			{
				foreach (var filePath in Directory.GetFiles(directory)) {
					if (
						filePath.EndsWith(".tan", StringComparison.OrdinalIgnoreCase) ||
						filePath.EndsWith(".t3d", StringComparison.OrdinalIgnoreCase) ||
						filePath.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase)
					) {
						lookupWidget.AddItem(filePath, () => LookupDialog.Sections.Push(new LookupSceneMenuSection(filePath)));
					} else if (filePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) {
						lookupWidget.AddItem(filePath, () => LookupDialog.Sections.Push(new LookupImageMenuSection(filePath)));
					} else if (filePath.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase)) {
						lookupWidget.AddItem(filePath, () => {
							Document.Current.History.DoTransaction(() => {
								if (!Utils.ExtractAssetPathOrShowAlert(filePath, out var assetPath, out _)) {
									LookupDialog.Sections.Drop();
									return;
								}
								var node = Core.Operations.CreateNode.Perform(typeof(Audio));
								var sample = new SerializableSample(assetPath);
								SetProperty.Perform(node, nameof(Audio.Sample), sample);
								SetProperty.Perform(node, nameof(Node.Id), Path.GetFileNameWithoutExtension(assetPath));
								SetProperty.Perform(node, nameof(Audio.Volume), 1);
								var key = new Keyframe<AudioAction> {
									Frame = Document.Current.AnimationFrame,
									Value = AudioAction.Play
								};
								SetKeyframe.Perform(node, nameof(Audio.Action), Document.Current.AnimationId, key);
							});
							LookupDialog.Sections.Drop();
						});
					}
				}
				foreach (var d in Directory.GetDirectories(directory)) {
					GetFilesRecursively(d);
				}
			}
			GetFilesRecursively(Project.Current.AssetsDirectory);
		}
	}

	public class LookupSceneMenuSection : LookupSection
	{
		private readonly string filePath;

		public override string Breadcrumb { get; }
		public override string Prefix { get; } = null;

		public LookupSceneMenuSection(string filePath)
		{
			this.filePath = filePath;
			Breadcrumb = Path.GetFileName(filePath);
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			lookupWidget.AddItem("Open in New Tab", () => {
				if (Utils.ExtractAssetPathOrShowAlert(filePath, out var assetPath, out _)) {
					Project.Current.OpenDocument(assetPath);
				}
				LookupDialog.Sections.Drop();
			});
			lookupWidget.AddItem("Add As External Scene", () => {
				if (
					Utils.ExtractAssetPathOrShowAlert(filePath, out var assetPath, out var assetType) &&
					Utils.AssertCurrentDocument(assetPath, assetType)
				) {
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
				LookupDialog.Sections.Drop();
			});
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

		private readonly string filePath;

		public override string Breadcrumb { get; }
		public override string Prefix { get; } = null;

		public LookupImageMenuSection(string filePath)
		{
			this.filePath = filePath;
			Breadcrumb = Path.GetFileName(filePath);
		}

		public override void FillLookup(LookupWidget lookupWidget)
		{
			foreach (var imageType in imageTypes) {
				if (!NodeCompositionValidator.Validate(Document.Current.Container.GetType(), imageType)) {
					continue;
				}
				var imageTypeClosed = imageType;
				lookupWidget.AddItem(
					$"Create {imageType.Name}",
					() => {
						Document.Current.History.DoTransaction(() => {
							if (!Utils.ExtractAssetPathOrShowAlert(filePath, out var assetPath, out _)) {
								LookupDialog.Sections.Drop();
								return;
							}
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
						LookupDialog.Sections.Drop();
					}
				);
			}
		}
	}
}
