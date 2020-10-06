using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine.Common.FilesDropHandlers
{
	/// <summary>
	/// Handles images drop.
	/// </summary>
	public class ImagesDropHandler
	{
		private static readonly Type[] imageTypes = {
			typeof(Image), typeof(DistortionMesh), typeof(NineGrid),
			typeof(TiledImage), typeof(ParticleModifier), typeof(Animesh),
		};

		private readonly Action onBeforeDrop;
		private readonly Action<Node> postProcessNode;

		/// <summary>
		/// Constructs ImagesDropHandler.
		/// </summary>
		/// <param name="onBeforeDrop">Called before dropped files processing.</param>
		/// <param name="postProcessNode">Called after node creation.</param>
		public ImagesDropHandler(Action onBeforeDrop = null, Action<Node> postProcessNode = null)
		{
			this.onBeforeDrop = onBeforeDrop;
			this.postProcessNode = postProcessNode;
		}

		/// <summary>
		/// Handles files drop.
		/// </summary>
		/// <param name="files">Dropped files.</param>
		public void Handle(List<string> files)
		{
			var supportedFiles = files.Where(f => Path.GetExtension(f) == ".png" ).ToList();
			if (supportedFiles.Any()) {
				supportedFiles.ForEach(f => files.Remove(f));
				CreateContextMenu(supportedFiles);
			}
		}

		private void CreateContextMenu(List<string> files)
		{
			var menu = new Menu();
			foreach (var imageType in imageTypes) {
				if (NodeCompositionValidator.Validate(Document.Current.Container.GetType(), imageType)) {
					menu.Add(new Command($"Create {imageType.Name}",
						() => CreateImageTypeInstance(imageType, files)));
				}
			}
			menu.Add(new Command("Create sprite animated Image", () => CreateSpriteAnimatedImage(files)));
			menu.Popup();
		}

		private void CreateSpriteAnimatedImage(List<string> files)
		{
			onBeforeDrop?.Invoke();
			var assetPaths = new List<string>();
			foreach (var file in files) {
				if (Utils.ExtractAssetPathOrShowAlert(file, out var assetPath, out _)) {
					assetPaths.Add(assetPath);
				}
			}
			var node = CreateAnimationSequenceImageFromAssets.Perform(assetPaths);
			postProcessNode?.Invoke(node);
		}

		private void CreateImageTypeInstance(Type type, List<string> files)
		{
			onBeforeDrop?.Invoke();
			using (Document.Current.History.BeginTransaction()) {
				foreach (var file in files) {
					if (Utils.ExtractAssetPathOrShowAlert(file, out var assetPath, out _)) {
						var node = CreateTexturedWidgetFromAsset.Perform(assetPath, type);
						postProcessNode?.Invoke(node);
					}
				}
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
