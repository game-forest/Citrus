using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI.Timeline.Operations.CompoundAnimations;

namespace Tangerine.UI.Timeline
{
	internal class GridPaneFilesDropHandler
	{
		public void Handle(List<string> files)
		{
			var grid = Timeline.Instance.Grid;
			var handled = new List<string>();
			var cellUnderMouseOnFilesDrop = grid.CellUnderMouse();
			var animateTextureCellOffset = 0;
			using (Document.Current.History.BeginTransaction()) {
				foreach (var file in files.ToList()) {
					if (Document.Current.Animation.IsCompound) {
						try {
							// Dirty hack: using a file drag&drop mechanics for dropping animation clips on the grid.
							var decodedAnimationId = Encoding.UTF8.GetString(Convert.FromBase64String(file));
							AddAnimationClip.Perform(
								new IntVector2(
									cellUnderMouseOnFilesDrop.X + animateTextureCellOffset,
									cellUnderMouseOnFilesDrop.Y),
								decodedAnimationId);
							return;
						} catch { }
					}
					if (!Utils.ExtractAssetPathOrShowAlert(file, out var assetPath, out var assetType)) {
						continue;
					}
					switch (assetType) {
						case ".png": {
							if (Document.Current.VisibleSceneItems.Count == 0) {
								continue;
							}
							var widget = Document.Current.VisibleSceneItems[cellUnderMouseOnFilesDrop.Y].Components.Get<NodeSceneItem>()?.Node as Widget;
							if (widget == null) {
								continue;
							}
							var key = new Keyframe<ITexture> {
								Frame = cellUnderMouseOnFilesDrop.X + animateTextureCellOffset,
								Value = new SerializableTexture(assetPath),
								Function = KeyFunction.Steep,
							};
							SetKeyframe.Perform(widget, nameof(Widget.Texture), Document.Current.Animation, key);
							animateTextureCellOffset++;
							break;
						}
						case ".ogg": {
							var node = CreateNode.Perform(typeof(Audio));
							var sample = new SerializableSample(assetPath);
							SetProperty.Perform(node, nameof(Audio.Sample), sample);
							SetProperty.Perform(node, nameof(Node.Id), assetPath);
							SetProperty.Perform(node, nameof(Audio.Volume), 1);
							var key = new Keyframe<AudioAction> {
								Frame = cellUnderMouseOnFilesDrop.X,
								Value = AudioAction.Play
							};
							SetKeyframe.Perform(node, nameof(Audio.Action), Document.Current.Animation, key);
							break;
						}
					}
					files.Remove(file);
				}
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
