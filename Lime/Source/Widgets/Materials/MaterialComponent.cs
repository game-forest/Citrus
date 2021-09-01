namespace Lime
{
	/// <summary>
	/// This interface must implements every node which can be used as a owner of MaterialComponent.
	/// </summary>
	public interface IMaterialComponentOwner
	{
		IMaterial Material { get; set; }
		ITexture Texture { get; }
		Vector2 UV0 { get; }
		Vector2 UV1 { get; }
	}

	[ComponentSettings(StartEquivalenceClass = true)]
	[AllowedComponentOwnerTypes(typeof(IMaterialComponentOwner))]
	public class MaterialComponent : NodeComponent
	{

	}

	/// <summary>
	/// Replace owner material with specified material
	/// </summary>
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
				w.Material = null;
			}
			if (Owner is IMaterialComponentOwner w1) {
				w1.Material = CustomMaterial;
			}
		}
	}
}
