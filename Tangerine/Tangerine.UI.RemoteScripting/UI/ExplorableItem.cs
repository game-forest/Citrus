using System;
using Lime;

namespace Tangerine.UI.RemoteScripting
{
	public abstract class ExplorableItem
	{
		private string name;
		private ITexture iconTexture;
		private IPresenter iconPresenter;

		public string Name
		{
			get => name;
			protected set
			{
				name = value;
				NameUpdated?.Invoke();
			}
		}

		public ITexture IconTexture
		{
			get => iconTexture;
			protected set
			{
				iconTexture = value;
				IconUpdated?.Invoke();
			}
		}

		public IPresenter IconPresenter
		{
			get => iconPresenter;
			protected set
			{
				iconPresenter = value;
				IconUpdated?.Invoke();
			}
		}

		public Widget Content { get; protected set; }

		public event UpdatingDelegate Updating;
		public event Action NameUpdated;
		public event Action IconUpdated;

		public void OnUpdate(float delta) => Updating?.Invoke(delta);
	}
}
