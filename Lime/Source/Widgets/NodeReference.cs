using System;
using Yuzu;

namespace Lime
{
	/// <summary>
	/// This interface is used to determine if some object is a <see cref="NodeReference{T}"/>.
	/// </summary>
	internal interface INodeReference { }

	/// <summary>
	/// A node reference is used for referencing to a node within a serialized scene by the node Id.
	/// </summary>
	[YuzuSpecializeWith(typeof(Lime.Camera3D))]
	[YuzuSpecializeWith(typeof(Lime.Spline))]
	[YuzuSpecializeWith(typeof(Lime.Widget))]
	public class NodeReference<T> : INodeReference
		where T : Node
	{
		private string id;
		private Node cachedRoot;
		private T cachedNode;
		private long cacheValidationCode;

		[YuzuMember]
		public string Id
		{
			get { return id; }
			set
			{
				if (id != value) {
					id = value;
					cachedRoot = null;
					cachedNode = null;
					cacheValidationCode = 0;
				}
			}
		}

		public NodeReference() { }

		public NodeReference(string id)
		{
			Id = id;
		}

		public NodeReference<T> Clone() => new NodeReference<T>(Id);

		public T GetNode(Node root)
		{
			if (cachedRoot != root || cacheValidationCode != Node.NodeReferenceCacheValidationCode) {
				cachedRoot = root;
				cachedNode = root.TryFind<T>(Id);
				cacheValidationCode = Node.NodeReferenceCacheValidationCode;
			}
			return cachedNode;
		}
	}
}
