using System.IO;
using System.Threading;
using Lime;

namespace RemoteScripting
{
	public abstract class NetworkYuzuObject<T> : NetworkMessage where T : class, new()
	{
		private static readonly ThreadLocal<Persistence> persistence = new ThreadLocal<Persistence>(() => new Persistence());

		public readonly T Data;

		protected NetworkYuzuObject(T data)
		{
			Data = data;
		}

		protected NetworkYuzuObject(byte[] body)
		{
			using (var memoryStream = new MemoryStream(body)) {
				Data = persistence.Value.ReadFromStream<T>(memoryStream);
			}
		}

		public override byte[] Serialize()
		{
			using (var memoryStream = new MemoryStream()) {
				persistence.Value.WriteToStream(memoryStream, Data, Persistence.Format.Binary);
				return memoryStream.ToArray();
			}
		}
	}
}
