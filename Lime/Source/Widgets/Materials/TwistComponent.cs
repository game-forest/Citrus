using System.Collections.Generic;
using Yuzu;

namespace Lime
{
	[TangerineRegisterComponent]
	public class TwistComponent : MaterialComponent<TwistMaterial>
	{
		[YuzuMember]
		public Blending Blending
		{
			get => CustomMaterial.Blending;
			set => CustomMaterial.Blending = value;
		}

		[YuzuMember]
		public float Angle
		{
			get => CustomMaterial.Angle;
			set => CustomMaterial.Angle = value;
		}

		[YuzuMember]
		public Vector2 Pivot
		{
			get => CustomMaterial.Pivot;
			set => CustomMaterial.Pivot = value;
		}

		[YuzuMember]
		public float RadiusFactor
		{
			get => CustomMaterial.RadiusFactor;
			set => CustomMaterial.RadiusFactor = value;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner != null && updateUVOnTextureChangeTask != null) {
				oldOwner.Tasks.Remove(updateUVOnTextureChangeTask);
			}
			if (Owner != null) {
				var image = (IMaterialComponentOwner)Owner;
				// Usually Texture or Owner is changed before LateUpdate stage
				// so we'd better check on texture change at least as late as LateUpdate stage.
				updateUVOnTextureChangeTask = Owner.AsWidget.LateTasks.Add(UpdateUVOnTextureChange(image));
				UpdateUV(image);
			}
		}

		private void UpdateUV(IMaterialComponentOwner owner)
		{
			var uv0 = owner.UV0;
			var uv1 = owner.UV1;
			owner.Texture.TransformUVCoordinatesToAtlasSpace(ref uv0);
			owner.Texture.TransformUVCoordinatesToAtlasSpace(ref uv1);
			CustomMaterial.UV0 = uv0;
			CustomMaterial.UV1 = uv1;
		}

		private Task updateUVOnTextureChangeTask;

		private IEnumerator<object> UpdateUVOnTextureChange(IMaterialComponentOwner owner)
		{
			ITexture texture = null;
			while (true) {
				if (owner.Texture != texture) {
					texture = owner.Texture;
					UpdateUV(owner);
				}
				yield return null;
			}
		}
	}
}
