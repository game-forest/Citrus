using Lime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine.Common.FilesDropHandlers
{
	/// <summary>
	/// Handles 3D models drop.
	/// </summary>
	public class Models3DDropHandler
	{
		private readonly Action onBeforeDrop;
		private readonly Action<Node> postProcessNode;

		/// <summary>
		/// Constructs Models3DDropHandler.
		/// </summary>
		/// <param name="onBeforeDrop">Called before dropped files processing.</param>
		/// <param name="postProcessNode">Called after node creation.</param>
		public Models3DDropHandler(Action onBeforeDrop = null, Action<Node> postProcessNode = null)
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
			onBeforeDrop?.Invoke();
			using (Document.Current.History.BeginTransaction()) {
				foreach (var file in files.Where(f => Path.GetExtension(f) == ".fbx").ToList()) {
					files.Remove(file);
					if (Utils.ExtractAssetPathOrShowAlert(file, out var assetPath, out _)) {
						if (!NodeCompositionValidator.Validate(Document.Current.Container.GetType(), typeof(Model3D))) {
							var viewport = CreateNode.Perform(typeof(Viewport3D));
							SetProperty.Perform(viewport, nameof(Node.Id), nameof(Viewport3D));
							postProcessNode?.Invoke(viewport);
							EnterNode.Perform(viewport);
						}
						var node = CreateModel3DFromAsset.Perform(assetPath);
						postProcessNode?.Invoke(node);
					}
				}
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
