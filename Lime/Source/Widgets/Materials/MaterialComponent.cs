namespace Lime
{
	public interface IMaterialComponentOwner
	{
		void AssignMaterial(IMaterial material);
		void ResetMaterial();
		ITexture Texture { get; }
		Vector2 UV0 { get; }
		Vector2 UV1 { get; }
	}

	[MutuallyExclusiveDerivedComponents]
	[AllowedComponentOwnerTypes(typeof(IMaterialComponentOwner))]
	public class MaterialComponent : NodeComponent
	{

	}

	public class MaterialComponent<T> : MaterialComponent where T : IMaterial, new()
	{
		protected T CustomMaterial { get; private set; }

		public MaterialComponent()
		{
			CustomMaterial = new T();
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner is IMaterialComponentOwner w) {
				w.ResetMaterial();
			}
			if (Owner is IMaterialComponentOwner w1) {
				w1.AssignMaterial(CustomMaterial);
			}
		}
	}
}
