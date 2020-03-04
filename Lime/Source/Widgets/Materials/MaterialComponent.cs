namespace Lime
{
	public interface ITextureRenderWidget
	{
		IMaterial Material { get; set; }
		ITexture Texture { get; set; }
		Vector2 UV0 { get; set; }
		Vector2 UV1 { get; set; }
	}

	[MutuallyExclusiveDerivedComponents]
	[AllowedComponentOwnerTypes(typeof(ITextureRenderWidget))]
	public class MaterialComponent : NodeComponent
	{

	}

	public class MaterialComponent<T> : MaterialComponent where T : IMaterial, new()
	{
		protected T CustomMaterial { get; private set; }
		private IMaterial savedCustomMaterial;

		public MaterialComponent()
		{
			CustomMaterial = new T();
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner != null) {
				((ITextureRenderWidget)oldOwner).Material = savedCustomMaterial;
			}
			if (Owner != null) {
				savedCustomMaterial = ((ITextureRenderWidget)Owner).Material;
				((ITextureRenderWidget)Owner).Material = CustomMaterial;
			}
		}
	}
}
