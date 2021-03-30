using Lime;

namespace Tangerine.UI.RemoteScripting
{
	public abstract class ExplorableItem
	{
		private string name;

		public string Name
		{
			get => name;
			protected set
			{
				name = value;
				NameUpdated?.Invoke(value);
			}
		}

		public Widget Content { get; protected set; }

		public delegate void NameUpdatedDelegate(string value);
		public event NameUpdatedDelegate NameUpdated;
	}
}
