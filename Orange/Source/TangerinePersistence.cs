using System.Threading;
using Yuzu;
using Yuzu.Json;

namespace Tangerine.Core
{
	public class TangerinePersistence
	{
		public static Lime.Persistence Instance => threadLocalInstance.Value;
		private static readonly ThreadLocal<Lime.Persistence> threadLocalInstance = new ThreadLocal<Lime.Persistence>(() => new Lime.Persistence(
			new CommonOptions {
				TagMode = TagMode.Aliases,
				AllowEmptyTypes = true,
				CheckForEmptyCollections = true,
				AllowUnknownFields = true,
			},
			new JsonSerializeOptions {
				ArrayLengthPrefix = false,
				Indent = "\t",
				FieldSeparator = "\n",
				SaveRootClass = true,
				Unordered = true,
				MaxOnelineFields = 8,
				BOM = true,
			}
		));
	}
}
