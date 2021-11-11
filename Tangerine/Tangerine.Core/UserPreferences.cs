using System;
using System.Linq;
using Lime;
using Yuzu;
using System.Collections.Generic;

namespace Tangerine.Core
{
	public class UserPreferences : ComponentCollection<Component>
	{
		public static UserPreferences Instance { get; private set; }

		public static void Initialize()
		{
			if (Instance != null) {
				throw new InvalidOperationException();
			}
			Instance = new UserPreferences();
			Instance.Load();
		}

		public void Load()
		{
			var path = GetPath();
			if (!System.IO.File.Exists(path)) {
				return;
			}
			try {
				Clear();
				TangerinePersistence.Instance.ReadFromFile<UserPreferences>(path, this);
			} catch (System.Exception e) {
				Clear();
				Debug.Write($"Failed to load the user preferences ({path}): {e}");
			}
		}

		public void Save()
		{
			// .ToList() crashes, so using iteration
			var sortedComponents = new List<Component>();
			foreach (var c in this) {
				sortedComponents.Add(c);
			}
			sortedComponents.Sort((a, b) => String.Compare(a.GetType().FullName, b.GetType().FullName, StringComparison.Ordinal));
			TangerinePersistence.Instance.WriteToFile(GetPath(), sortedComponents, Persistence.Format.Json);
		}

		public static string GetPath()
		{
			return System.IO.Path.Combine(Lime.Environment.GetDataDirectory("Tangerine"), "UserPreferences");
		}
	}
}
