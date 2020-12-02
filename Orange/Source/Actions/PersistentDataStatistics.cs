using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Lime;
using static System.Math;
using System.Reflection;

namespace Orange
{
	public static partial class Actions
	{
		[Export(nameof(Orange.OrangePlugin.MenuItems))]
		[ExportMetadata(nameof(IMenuItemMetadata.Label), "Collect statistics for persistent data")]
		public static void CollectStatisticsForPersistentData() => new PersistentDataStatistics().Run();

		private class PersistentDataStatistics
		{
			[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
			private class CsvColumnNameAttribute : Attribute
			{
				public string Name;
				public CsvColumnNameAttribute(string name) { Name = name; }
			}
			private class StatisticsRecord
			{
				[CsvColumnName("name")]
				public string Filename;

				// own, bytes
				[CsvColumnName("size")]
				public long Size;

				// own compressed, bytes
				[CsvColumnName("compressed_size")]
				public long CompressedSize;

				// bytes read for this asset + bytes read for external scenes and animations
				[CsvColumnName("external_nodes_size")]
				public long ExternalNodesSize;

				[CsvColumnName("external_animations_size")]
				public long ExternalAnimationsSize;

				// summ of three above
				[CsvColumnName("total_size")]
				public long TotalSize;

				[CsvColumnName("size_without_animators")]
				public long SizeWithoutAnimators;

				[CsvColumnName("size_without_meshes")]
				public long SizeWithoutMeshes;

				[CsvColumnName("node_count")]
				public int NodeCount;

				// excluding empty transforms
				[CsvColumnName("non_frame_node_count")]
				public int NonFrameNodeCount;

				[CsvColumnName("legacy_animation_count")]
				public int LegacyAnimationCount;

				[CsvColumnName("non_legacy_animation_count")]
				public int NonLegacyAnimationCount;

				[CsvColumnName("animator_count")]
				public int AnimatorCount;

				[CsvColumnName("max_nesting")]
				public int MaxNesting;

				[CsvColumnName("expected_nesting")]
				public double ExpectedNesting;

				[CsvColumnName("max_children")]
				public int MaxChildren;

				// without number of children of leaf nodes i.e. zero
				[CsvColumnName("expected_children")]
				public double ExpectedChildren;

				[CsvColumnName("external_count")]
				public int ExternalSceneCount;

				public Dictionary<string, int> NodeCountByType = new Dictionary<string, int>();
				// ms
				public List<List<double>> LoadingTimes;

				public StatisticsRecord(string filename)
				{
					Filename = filename;
					LoadingTimes = new List<List<double>>();
					for (int i = 0; i < (int)BenchmarkState.Last; i++) {
						LoadingTimes.Add(Enumerable.Repeat(0.0, MeasurementCount).ToList());
					}
				}
			}

			private enum BenchmarkState
			{
				[CsvColumnName("time_wo_externals")]
				WithoutExternals,
				[CsvColumnName("time_w_externals")]
				WithExternals,
				[CsvColumnName("time_wo_animators_wo_externals")]
				WithoutAnimatorsWithoutExternals,
				[CsvColumnName("time_wo_animators_w_externals")]
				WithoutAnimatorsWithExternals,
				[CsvColumnName("time_wo_meshes_wo_externals")]
				WithoutMeshesWithoutExternals,
				[CsvColumnName("time_wo_meshes_w_externals")]
				WithoutMeshesWithExternals,
				// surrogate states:
				[CsvColumnName("time_w_externals_ant")]
				Ant,
				[CsvColumnName("time_w_externals_externals")]
				Externals,
				Last,
			}

			private static bool IsStateWithExternals(BenchmarkState state) {
				switch (state) {
					case BenchmarkState.WithoutExternals:
					case BenchmarkState.WithoutAnimatorsWithoutExternals:
					case BenchmarkState.WithoutMeshesWithoutExternals:
						return false;
					case BenchmarkState.WithExternals:
					case BenchmarkState.WithoutAnimatorsWithExternals:
					case BenchmarkState.WithoutMeshesWithExternals:
						return true;
					case BenchmarkState.Ant:
					case BenchmarkState.Externals:
					case BenchmarkState.Last:
					default:
						throw new InvalidOperationException();
				}
			}

			private readonly StatisticsRecord totalStatistics = new StatisticsRecord("total");
			private readonly StatisticsRecord tanTotalStatistics = new StatisticsRecord("tan total");
			private readonly StatisticsRecord t3dTotalStatistics = new StatisticsRecord("t3d total");
			private readonly StatisticsRecord antTotalStatistics = new StatisticsRecord("ant total");
			private const int MeasurementCount = 13;
			private readonly Dictionary<string, StatisticsRecord> statisticsForPath = new Dictionary<string, StatisticsRecord>(StringComparer.OrdinalIgnoreCase);
			private readonly Stopwatch antStopwatch = new Stopwatch();
			private readonly string[] extensions = new[] { ".t3d", ".tan", ".ant" };
			private readonly (StatisticsRecord Totals, string Extension)[] totalsPerExtension;
			private readonly string TempNodesDirectory = null;
			private int currentMeasurementIteration;
			private StatisticsRecord currentStatistics;
			private long currentTotalBytes;
			private BenchmarkState benchmarkState;
			private string currentExternal = null;
			private Stopwatch externalStopwatch = new Stopwatch();

			public PersistentDataStatistics()
			{
				totalsPerExtension = new[] {
					(t3dTotalStatistics, ".t3d"),
					(tanTotalStatistics, ".tan"),
					(antTotalStatistics, ".ant")
				};
				TempNodesDirectory = Path.Combine(Workspace.Instance.AssetsDirectory, "..", "nodes_in_bundle_without_animators");
			}

			public void Run()
			{
				Console.WriteLine($"Stopwatch is high precision: {Stopwatch.IsHighResolution}");
				Console.WriteLine($"Stopwatch frequency: {Stopwatch.Frequency}");

				var savedAssetBundle = AssetBundle.Current;
				var savedNodeSceneLoading = Node.SceneLoading;
				var savedAnimationDataLoading = Animation.AnimationData.Loading;
				var savedAnimationDataLoaded = Animation.AnimationData.Loaded;

				var target = The.UI.GetActiveTarget();
				var ac = new AssetCooker(target);
				var bundles = ac.GetListOfAllBundles();

				statisticsForPath.Clear();
				foreach (var ext in extensions) {
					CollectStatistics(ext, bundles, target);
				}

				// tracking when external scene loading is triggered to add up to total size
				Node.SceneLoading = new ThreadLocal<Node.SceneLoadingDelegate>(() => OnSceneLoading);
				Node.SceneLoaded = new ThreadLocal<Node.SceneLoadedDelegate>(() => OnSceneLoaded);
				// tracking when animation data is loading to add up to total size and time
				Animation.AnimationData.Loading = new ThreadLocal<Animation.AnimationData.LoadingDelegate>(() => AnimationDataLoading);
				Animation.AnimationData.Loaded = new ThreadLocal<Animation.AnimationData.LoadedDelegate>(() => OnAnimationDataLoaded);

				// non cached: AssetBundle.Current = new AggregateAssetBundle(
				// bundles.Select(bundleName => new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName))).ToArray());

				AssetBundle.Current = new CachingBundle(EnumeratePersistentDataInBundles(target, bundles));
				benchmarkState = BenchmarkState.WithoutExternals;
				CollectDeserializationTimes(".t3d");
				CollectDeserializationTimes(".tan");
				benchmarkState = BenchmarkState.WithExternals;
				CollectDeserializationTimes(".t3d");
				CollectDeserializationTimes(".tan");

				ResaveProcessedDocuments(new [] { ".tan", ".t3d" }, bundles, target, (node) => {
					foreach (var animation in node.Animations) {
						animation.ContentsPath = null;
					}
					node.Animators.Clear();
				}, (inputPath, outputPath) => {
					this[inputPath].SizeWithoutAnimators = new System.IO.FileInfo(outputPath).Length;
				});
				AssetBundle.Current = new CachingBundle(TempNodesDirectory);

				benchmarkState = BenchmarkState.WithoutAnimatorsWithoutExternals;
				CollectDeserializationTimes(".t3d");
				CollectDeserializationTimes(".tan");
				benchmarkState = BenchmarkState.WithoutAnimatorsWithExternals;
				CollectDeserializationTimes(".t3d");
				CollectDeserializationTimes(".tan");

				ResaveProcessedDocuments(new[] { ".tan", ".t3d", ".ant" }, bundles, target, (node) => {
					if (node is Mesh3D mesh3d) {
						foreach (var submesh in mesh3d.Submeshes) {
							submesh.Mesh = null;
						}
					}
				}, (inputPath, outputPath) => {
					this[inputPath].SizeWithoutMeshes = new System.IO.FileInfo(outputPath).Length;
				});
				AssetBundle.Current = new CachingBundle(TempNodesDirectory);

				benchmarkState = BenchmarkState.WithoutMeshesWithoutExternals;
				CollectDeserializationTimes(".t3d");
				CollectDeserializationTimes(".tan");
				benchmarkState = BenchmarkState.WithoutMeshesWithExternals;
				CollectDeserializationTimes(".t3d");
				CollectDeserializationTimes(".tan");

				CalcTotals(totalStatistics, "");
				foreach (var (totals, extension) in totalsPerExtension) {
					CalcTotals(totals, extension);
				}

				var filename = "persistent_data_statistics.csv";
				SaveStatisticsToCsv(filename);

				Console.WriteLine($"Statistics saved to {Path.Combine(Directory.GetCurrentDirectory(), filename)}");

				if (Directory.Exists(TempNodesDirectory)) {
					Directory.Delete(TempNodesDirectory, recursive: true);
				}
				AssetBundle.Current = savedAssetBundle;
				Node.SceneLoading = savedNodeSceneLoading;
				Animation.AnimationData.Loading = savedAnimationDataLoading;
				Animation.AnimationData.Loaded = savedAnimationDataLoaded;
			}

			private IEnumerable<(string, Stream)> EnumeratePersistentDataInBundles(Target target, IEnumerable<string> bundles)
			{
				foreach (var bundleName in bundles) {
					using (var bundle = new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName))) {
						foreach (var file in bundle.EnumerateFiles()) {
							bool include = false;
							foreach (var extension in extensions) {
								if (file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
									include = true;
								}
							}
							if (include) {
								yield return (file, bundle.OpenFile(file));
							}
						}
					}
				}
			}

			private void CalcTotals(StatisticsRecord t, string suffixFilter)
			{
				t.Filename = suffixFilter + " total:";
				var filteredRecords = statisticsForPath.Where(i => i.Key.EndsWith(suffixFilter, StringComparison.OrdinalIgnoreCase)).ToList();
				foreach (var (k, r) in filteredRecords) {
					t.AnimatorCount += r.AnimatorCount;
					t.ExternalAnimationsSize += r.ExternalAnimationsSize;
					t.ExternalNodesSize += r.ExternalNodesSize;
					t.LegacyAnimationCount += r.LegacyAnimationCount;
					for (var s = BenchmarkState.WithoutExternals; s < BenchmarkState.Last; s++) {
						int i = (int)s;
						for (int j = 0; j < MeasurementCount; j++) {
							t.LoadingTimes[i][j] += r.LoadingTimes[i][j];
						}
					}
					t.NodeCount += r.NodeCount;
					foreach (var (typeName, nodeCount) in r.NodeCountByType) {
						if (!t.NodeCountByType.ContainsKey(typeName)) {
							t.NodeCountByType.Add(typeName, nodeCount);
						} else {
							t.NodeCountByType[typeName] += nodeCount;
						}
					}
					t.NonLegacyAnimationCount += r.NonLegacyAnimationCount;
					t.Size += r.Size;
					t.CompressedSize += r.CompressedSize;
					t.TotalSize += r.TotalSize;
					t.NonFrameNodeCount += r.NonFrameNodeCount;
					t.MaxNesting = Max(t.MaxNesting, r.MaxNesting);
					t.MaxChildren = Max(t.MaxChildren, r.MaxChildren);
					t.ExternalSceneCount += r.ExternalSceneCount;
					t.SizeWithoutMeshes += r.SizeWithoutMeshes;
					t.SizeWithoutAnimators += r.SizeWithoutAnimators;
				}
				t.ExpectedNesting = filteredRecords.Select(i => i.Value.ExpectedNesting).Average();
				t.ExpectedChildren = filteredRecords.Select(i => i.Value.ExpectedChildren).Average();
			}

			private void SaveStatisticsToCsv(string filepath)
			{
				var nodeTypes = new List<string>();
				foreach (var (_, r) in statisticsForPath) {
					foreach (var (k, _) in r.NodeCountByType) {
						if (!nodeTypes.Contains(k)) {
							nodeTypes.Add(k);
						}
					}
				}
				nodeTypes.Sort();
				List<(MemberInfo MemberInfo, CsvColumnNameAttribute CsvColumn)> columns = typeof(StatisticsRecord).GetMembers()
					.Where(memberInfo => memberInfo.MemberType == System.Reflection.MemberTypes.Property ||
						memberInfo.MemberType == System.Reflection.MemberTypes.Field)
					.Select(memberInfo => (memberInfo, memberInfo.GetCustomAttribute<CsvColumnNameAttribute>()))
					.Where(p => p.Item2 != null)
					.ToList();
				using (var stream = new FileStream(filepath, FileMode.Create)) {
					using (var writer = new StreamWriter(stream)) {
						var headerRow = string.Empty;
						foreach (var column in columns) {
							headerRow += (column == columns.First() ? "" : ",") + column.CsvColumn.Name;
						}
						foreach (var nodeType in nodeTypes) {
							headerRow += $",{nodeType}";
						}
						for (var s = BenchmarkState.WithoutExternals; s < BenchmarkState.Last; s++) {
							var memberInfo = typeof(BenchmarkState).GetMember(s.ToString()).First();
							var header = memberInfo.GetCustomAttribute<CsvColumnNameAttribute>().Name;
							for (int i = 1; i <= MeasurementCount; i++) {
								headerRow += $",{header}_{i}";
							}
						}
						headerRow += "\n";
						writer.Write(headerRow);
						WriteRecord(totalStatistics, writer);
						foreach (var (r, _) in totalsPerExtension) {
							WriteRecord(r, writer);
						}
						foreach (var (_, r) in statisticsForPath) {
							WriteRecord(r, writer);
						}
					}
				}

				void WriteRecord(StatisticsRecord record, StreamWriter writer)
				{
					var r = record;
					var row = string.Empty;
					foreach (var column in columns) {
						row += (column == columns.First() ? "" : ",") +
							(column.MemberInfo as FieldInfo)?.GetValue(record) ??
							(column.MemberInfo as PropertyInfo).GetValue(record);
					}
					foreach (var nodeType in nodeTypes) {
						row += $",{(r.NodeCountByType.ContainsKey(nodeType) ? r.NodeCountByType[nodeType] : 0)}";
					}
					for (var s = BenchmarkState.WithoutExternals; s < BenchmarkState.Last; s++) {
						foreach (var time in r.LoadingTimes[(int)s]) {
							row += $",{time}";
						}
					}
					row += "\n";
					writer.Write(row);
				}
			}

			private void ResaveProcessedDocuments(IEnumerable<string> extensions, IEnumerable<string> bundles, Target target, Action<Node> process, Action<string, string> afterSave)
			{
				if (Directory.Exists(TempNodesDirectory)) {
					Directory.Delete(TempNodesDirectory, recursive: true);
				}
				foreach (var bundleName in bundles) {
					var bundle = new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName));
					AssetBundle.Current = bundle;
					foreach (var f in bundle.EnumerateFiles()) {
						if (!extensions.Any(e => f.EndsWith(e, StringComparison.OrdinalIgnoreCase))) {
							continue;
						}
						var path = Path.Combine(TempNodesDirectory, f);
						Directory.CreateDirectory(Path.GetDirectoryName(path));
						if (f.EndsWith(".ant", StringComparison.OrdinalIgnoreCase)) {
							using (var inputStream = AssetBundle.Current.OpenFile(f))
							using (var outputStream = new FileStream(path, FileMode.Create)) {
								inputStream.CopyTo(outputStream);
							}
							continue;
						}
						var node = Node.Load(Path.ChangeExtension(f, null), ignoreExternals: true);
						foreach (var n in node.SelfAndDescendants) {
							process(n);
						}
						InternalPersistence.Instance.WriteObjectToFile(path, node, Persistence.Format.Binary);
						afterSave(f, path);
					}
				}
			}

			private void CollectStatistics(string extension, IEnumerable<string> bundles, Target target)
			{
				foreach (var bundleName in bundles) {
					var bundle = new PackedAssetBundle(The.Workspace.GetBundlePath(target.Platform, bundleName));
					AssetBundle.Current = bundle;
					foreach (var f in bundle.EnumerateFiles(null, extension)) {
						this[f].CompressedSize = bundle.GetFileSize(f);
						using (var memoryStream = new MemoryStream())
						using (var compressedStream = bundle.OpenFile(f)) {
							compressedStream.CopyTo(memoryStream);
							this[f].TotalSize = this[f].Size = memoryStream.Length;
						}
					}
					if (string.Equals(extension, ".ant", StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					foreach (var f in bundle.EnumerateFiles()) {
						if (!f.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
							continue;
						}
						var r = this[f];
						var node = Node.Load(Path.ChangeExtension(f, null), ignoreExternals: true);
						var nestings = new List<int>();
						var childrenCounts = new List<int> { 1 };
						foreach (var n in node.SelfAndDescendants) {
							foreach (var animation in n.Animations) {
								animation.Load();
								if (animation.IsLegacy) {
									r.LegacyAnimationCount++;
								} else {
									r.NonLegacyAnimationCount++;
								}
							}
						}
						foreach (var n in node.SelfAndDescendants) {
							var parent = n;
							var nesting = -1;
							while (parent != null) {
								parent = parent.Parent;
								nesting++;
							}
							nestings.Add(nesting);
							r.MaxNesting = Max(r.MaxNesting, nesting);
							if (n.Nodes.Count != 0) {
								childrenCounts.Add(n.Nodes.Count);
							}
							r.MaxChildren = Max(r.MaxChildren, n.Nodes.Count);
							r.NodeCount++;
							if (n.GetType() != typeof(Frame)) {
								r.NonFrameNodeCount++;
							}
							r.AnimatorCount += n.Animators.Count;
							var nodeTypeName = Yuzu.Util.TypeSerializer.Serialize(n.GetType());
							nodeTypeName = nodeTypeName.Substring(0, nodeTypeName.IndexOf(","));
							if (!r.NodeCountByType.ContainsKey(nodeTypeName)) {
								r.NodeCountByType.Add(nodeTypeName, 1);
							} else {
								r.NodeCountByType[nodeTypeName]++;
							}
							if (!string.IsNullOrEmpty(n.ContentsPath)) {
								var path = n.ContentsPath;
								if (path.StartsWith("/", StringComparison.OrdinalIgnoreCase)) {
									path =  Path.GetDirectoryName(f) + path;
								}
								if (statisticsForPath.ContainsKey(path + ".t3d") || statisticsForPath.ContainsKey(path + ".tan")) {
									r.ExternalSceneCount++;
								}
							}
						}
						r.ExpectedNesting = nestings.Average();
						r.ExpectedChildren = childrenCounts.Average();
					}
				}
			}

			private void CollectDeserializationTimes(string extension)
			{
				bool ignoreExternals = !IsStateWithExternals(benchmarkState);
				var savedProcessorAffinity = System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity;
				var savedProcessPriority = System.Diagnostics.Process.GetCurrentProcess().PriorityClass;
				var savedThreadPriority = Thread.CurrentThread.Priority;
				System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity = new IntPtr(1);
				System.Diagnostics.Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
				Thread.CurrentThread.Priority = ThreadPriority.Highest;
				GC.Collect();
				GC.WaitForPendingFinalizers();
				GC.Collect();

				for (int i = 0; i < MeasurementCount; i++) {
					currentMeasurementIteration = i;
					currentTotalBytes = 0;
					double time = 0;
					var sw = new System.Diagnostics.Stopwatch();
					foreach (var f in AssetBundle.Current.EnumerateFiles(null, extension)) {
						currentStatistics = this[f];
						sw.Restart();
						var node = Node.Load(Path.ChangeExtension(f, null), ignoreExternals: ignoreExternals);
						sw.Stop();
						foreach (var n in node.SelfAndDescendants) {
							foreach (var a in n.Animations) {
								var hasContentPath = !string.IsNullOrEmpty(a.ContentsPath);
								if (hasContentPath) {
									sw.Start();
								}
								a.Load();
								if (hasContentPath) {
									sw.Stop();
								}
							}
						}
						currentStatistics.LoadingTimes[(int)benchmarkState][currentMeasurementIteration] = sw.Elapsed.TotalMilliseconds;
						time += sw.Elapsed.TotalMilliseconds;
					}
					Console.WriteLine($"[{i + 1}/{MeasurementCount}] read {extension} from bundles read time: {time}ms");
					Console.WriteLine($"[{i + 1}/{MeasurementCount}]: bytes per ms {currentTotalBytes / time}");
				}
				System.Diagnostics.Process.GetCurrentProcess().ProcessorAffinity = savedProcessorAffinity;
				System.Diagnostics.Process.GetCurrentProcess().PriorityClass = savedProcessPriority;
				Thread.CurrentThread.Priority = savedThreadPriority;
			}

			private bool OnSceneLoading(string path, ref Node instance, bool external, bool ignoreExternals)
			{
				if (!statisticsForPath.TryGetValue(path + ".tan", out var value) &&
					!statisticsForPath.TryGetValue(path + ".t3d", out value)
				) {
					throw new System.InvalidOperationException($"path not found: {path}");
				}
				currentTotalBytes += value.Size;
				if (!currentStatistics.Filename.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
					currentStatistics.ExternalNodesSize += value.Size;
					currentStatistics.TotalSize += value.Size;
					if (string.IsNullOrEmpty(currentExternal)) {
						currentExternal = path;
						externalStopwatch.Restart();
					}
				}
				return false;
			}

			private void OnSceneLoaded(string path, Node instance, bool external)
			{
				if (currentExternal == path) {
					externalStopwatch.Stop();
					currentExternal = null;
					switch (benchmarkState) {
						case BenchmarkState.WithExternals:
							currentStatistics.LoadingTimes[(int)BenchmarkState.Externals][currentMeasurementIteration] += externalStopwatch.Elapsed.TotalMilliseconds;
							break;
					}
				}
			}

			private bool AnimationDataLoading(string path, ref Animation.AnimationData instance)
			{
				if (!statisticsForPath.TryGetValue(path, out var value)) {
					throw new System.InvalidOperationException($"path not found: {path}");
				}
				switch (benchmarkState) {
					case BenchmarkState.WithExternals:
						currentStatistics.ExternalAnimationsSize += value.Size;
						currentStatistics.TotalSize += value.Size;
						break;
				}
				currentTotalBytes += value.Size;
				antStopwatch.Restart();
				return false;
			}

			private void OnAnimationDataLoaded(string path, Animation.AnimationData instance)
			{
				antStopwatch.Stop();
				var value = this[path];
				var elapsedMilliseconds = antStopwatch.Elapsed.TotalMilliseconds;
				if (currentExternal != null) {
					// if something (like attachment.EntryTrigger) triggered animation loading when loading externals
					// substitute it from the time consumed by loading externals since external + ant time should be <= total time
					currentStatistics.LoadingTimes[(int)BenchmarkState.Externals][currentMeasurementIteration] -= elapsedMilliseconds;
				}
				switch (benchmarkState) {
					case BenchmarkState.WithoutExternals:
						value.LoadingTimes[(int)BenchmarkState.WithoutExternals][currentMeasurementIteration] = elapsedMilliseconds;
						break;
					case BenchmarkState.WithExternals:
						value.LoadingTimes[(int)BenchmarkState.WithExternals][currentMeasurementIteration] = elapsedMilliseconds;
						currentStatistics.LoadingTimes[(int)BenchmarkState.Ant][currentMeasurementIteration] += elapsedMilliseconds;
						break;
				}
			}

			private StatisticsRecord this[string filename]
			{
				get
				{
					if (!statisticsForPath.TryGetValue(filename, out var value)) {
						statisticsForPath.Add(filename, value = new StatisticsRecord(filename));
					}
					return value;
				}
			}
		}

		private class UncloseableMemoryStream : MemoryStream
		{
			protected override void Dispose(bool disposing)
			{ }
		}

		private class CachingBundle : AssetBundle
		{
			private readonly Dictionary<string, UncloseableMemoryStream> files = new Dictionary<string, UncloseableMemoryStream>(StringComparer.OrdinalIgnoreCase);

			public CachingBundle(string directory)
			{
				using (var bundle = new UnpackedAssetBundle(directory)) {
					foreach (var f in bundle.EnumerateFiles()) {
						var memoryStream = new UncloseableMemoryStream();
						using (var stream = bundle.OpenFile(f)) {
							stream.CopyTo(memoryStream);
						}
						files.Add(f, memoryStream);
					}
				}
			}

			public CachingBundle(IEnumerable<(string Filepath, Stream Stream)> streamEnumerator)
			{
				foreach (var (file, stream) in streamEnumerator) {
					if (files.ContainsKey(file)) {
						continue;
					}
					var memoryStream = new UncloseableMemoryStream();
					stream.CopyTo(memoryStream);
					files.Add(file, memoryStream);
				}
			}

			public override void DeleteFile(string path) => throw new NotSupportedException();

			public override IEnumerable<string> EnumerateFiles(string path = null, string extension = null)
			{
				foreach (var file in files.Keys) {
					if (path != null && !file.StartsWith(path, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					if (extension != null && !file.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) {
						continue;
					}
					yield return file;
				}
			}

			public override bool FileExists(string path) => files.ContainsKey(path);

			public override string FromSystemPath(string systemPath) => throw new NotSupportedException();

			public override string ToSystemPath(string bundlePath) => throw new NotSupportedException();

			public override int GetFileSize(string path) => (int)files[path].Length;

			public override void ImportFile(string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes) => throw new NotSupportedException();

			public override void ImportFileRaw(string path, Stream stream, SHA256 cookingUnitHash, AssetAttributes attributes) => throw new NotImplementedException();

			public override Stream OpenFile(string path, FileMode fileMode = FileMode.Open)
			{
				var stream = files[path];
				stream.Seek(0, SeekOrigin.Begin);
				return stream;
			}

			public override Stream OpenFileRaw(string path, FileMode fileMode = FileMode.Open) => OpenFile(path, fileMode);

			public override SHA256 GetHash(string path) => throw new NotImplementedException();

			public override SHA256 GetCookingUnitHash(string path) => throw new NotImplementedException();
		}
	}
}
