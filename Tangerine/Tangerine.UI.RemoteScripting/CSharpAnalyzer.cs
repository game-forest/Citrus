#if WIN
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buildalyzer;
using Lime;

namespace Tangerine.UI.RemoteScripting
{
	public class CSharpAnalyzer
	{
		private readonly string projectFilePath;
		private readonly IAnalyzerResult analyzerResult;

		public CSharpAnalyzer(string projectFilePath)
		{
			this.projectFilePath = projectFilePath;
			var manager = new AnalyzerManager();
			var analyzer = manager.GetProject(projectFilePath);
			analyzerResult = analyzer.Build().First();
		}

		public IEnumerable<string> GetCompileItems()
		{
			if (!analyzerResult.Items.TryGetValue("Compile", out var items) || items.Length == 0) {
				throw new System.Exception($"There is no items to compile in {projectFilePath}");
			}
			var directory = Path.GetDirectoryName(projectFilePath);
			foreach (var item in items) {
				yield return AssetPath.CorrectSlashes(Path.Combine(directory, item.ItemSpec));
			}
		}
	}
}
#endif // WIN
