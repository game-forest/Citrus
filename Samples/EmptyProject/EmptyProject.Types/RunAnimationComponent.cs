using System.Collections.Generic;
using Lime;
using Yuzu;

namespace EmptyProject
{
	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(Node))]
	public class RunAnimationBehavior : NodeBehavior
	{
		[YuzuMember]
		public List<string> AnimationNames { get; private set; } = new List<string>();

		public RunAnimationBehavior()
		{ }

		public override int Order => -1000100;

		public bool IsAwoken { get; private set; }

		protected override void OnRegister()
		{
			base.OnRegister();
			if (IsAwoken) {
				return;
			}
			// OnAwake
			foreach (var name in AnimationNames) {
				Owner.RunAnimation(null, name == string.Empty ? null : name);
			}
			// OnAwake
			IsAwoken = true;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			IsAwoken = false;
		}
	}
}
