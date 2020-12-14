using System.Linq;
using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(CanBeRoot = true, Order = 22)]
	[TangerineVisualHintGroup("/All/Nodes/3D")]
	public class Model3D : Node3D
	{
		[YuzuAfterDeserialization]
		public void OnAfterDeserialization()
		{
			RebuildSkeleton();
			LoadEntryTrigger();
		}

		public void RebuildSkeleton()
		{
			var submeshes = Descendants
				.OfType<Mesh3D>()
				.SelectMany(m => m.Submeshes);
			foreach (var sm in submeshes) {
				sm.RebuildSkeleton(this);
			}
		}

		private void LoadEntryTrigger()
		{
			if (ContentsPath == null) {
				return;
			}
			var attachmentPath = System.IO.Path.ChangeExtension(ContentsPath, ".Attachment.txt");
			// This code used to be executed in LoadExternalScenes when serialization path stack
			// contains only empty string. Now it's executed during serialization. ContentsPath property
			// actually returns ShrinkPath(contentsPath) which is rooted when current serialization directory
			// (top of path stack) is not sub-directory of contents path. Rooted paths aren't acceptable by
			// asset bundle that's why we temporarily call ExpandPath manually in order to 'disroot' contents
			// path. In the near future we'll migrate to absolute paths only model.
			attachmentPath = InternalPersistence.Current?.ExpandPath(attachmentPath) ?? attachmentPath;
			if (AssetBundle.Current.FileExists(attachmentPath)) {
				var attachment = InternalPersistence.Instance.ReadObjectFromBundle<Model3DAttachmentParser.ModelAttachmentFormat>(AssetBundle.Current, attachmentPath);
				if (string.IsNullOrEmpty(attachment.EntryTrigger)) {
					return;
				}
				var blender = Components.Get<AnimationBlender>();
				var enabledBlending = false;
				if (blender != null) {
					enabledBlending = blender.Enabled;
					blender.Enabled = false;
				}

				// TODO: Move this to Orange.FbxModelImporter
				var oldTrigger = Trigger;
				Trigger = attachment.EntryTrigger;
				TriggerMultipleAnimations(Trigger);
				var animationBehavior = Components.Get<AnimationComponent>();
				if (animationBehavior != null) {
					foreach (var a in animationBehavior.Animations) {
						if (a.IsRunning) {
							a.Advance(0);
						}
					}
				}
				Trigger = oldTrigger;

				if (blender != null) {
					blender.Enabled = enabledBlending;
				}
			}
		}
	}
}
