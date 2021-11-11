using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Yuzu;

namespace Lime
{
	internal class InternalPersistence : Persistence
	{
		internal static InternalPersistence Current => stackOfCurrent.Value.Count > 0 ? stackOfCurrent.Value.Peek() : null;
		private static void PushCurrent(InternalPersistence persistence) => stackOfCurrent.Value.Push(persistence);
		private static InternalPersistence PopCurrent() => stackOfCurrent.Value.Pop();

		private static readonly ThreadLocal<Stack<InternalPersistence>> stackOfCurrent = new ThreadLocal<Stack<InternalPersistence>>(() => new Stack<InternalPersistence>());

		private readonly Stack<string> pathStack = new Stack<string>();

		private static readonly Regex conflictRegex = new Regex("^<<<<<<<.*?^=======.*?^>>>>>>>",
			RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.Singleline);

		public static InternalPersistence Instance => threadLocalInstance.Value;
		private static readonly ThreadLocal<InternalPersistence>  threadLocalInstance = new ThreadLocal<InternalPersistence>(() => new InternalPersistence());

		public InternalPersistence()
		{

		}

		public InternalPersistence(CommonOptions yuzuOptions, Yuzu.Json.JsonSerializeOptions yuzuJsonOptions)
			: base(yuzuOptions, yuzuJsonOptions)
		{ }

		internal string ShrinkPath(string path)
		{
			if (pathStack.Count == 0 || string.IsNullOrEmpty(path)) {
				return path;
			}
			// when current directory is an empty string it means the root of assets directory,
			// thus every path is being treated as relative to it, so we don't add leading '/'.
			// And when current directory is null, it means calling code wants to treat all paths as absolute (e.g. copy/paste)
			var d = GetCurrentSerializationDirectory();
			if (d == null) {
				return '/' + path;
			} else if (d == string.Empty) {
				return path;
			}
			d += '/';
			return path.StartsWith(d) ? path.Substring(d.Length) : '/' + path;
		}

		internal string ExpandPath(string path)
		{
			if (pathStack.Count == 0 || string.IsNullOrEmpty(path)) {
				return path;
			}
			if (path[0] == '/') {
				return path.Substring(1);
			} else {
				var d = GetCurrentSerializationDirectory();
				if (string.IsNullOrEmpty(d)) {
					return path;
				}
				return d + '/' + path;
			}
		}

		internal string GetCurrentSerializationPath()
		{
			return pathStack.Peek();
		}

		private string GetCurrentSerializationDirectory()
		{
			var path = Path.GetDirectoryName(pathStack.Peek());
			if (!string.IsNullOrEmpty(path)) {
				path = AssetPath.CorrectSlashes(path);
			}
			return path;
		}

		public override int CalcObjectCheckSum(object @object)
		{
			throw new NotSupportedException();
		}

		public int CalcObjectCheckSum(string path, object obj)
		{
			using var stream = new MemoryStream();
			WriteToStream(path, stream, obj, Format.Binary);
			stream.Flush();
			return Toolbox.ComputeHash(stream.GetBuffer(), (int)stream.Length);
		}

		public override string WriteToString(object @object, Format format)
		{
			throw new NotSupportedException();
		}

		public string WriteToString(string path, object @object, Format format)
		{
			using var stream = new MemoryStream();
			WriteToStream(path, stream, @object, format);
			return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
		}

		public override void WriteToFile(string path, object @object, Format format)
		{
			using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
			WriteToStream(path, stream, @object, format);
		}

		public override void WriteToBundle(
			AssetBundle bundle,
			string path,
			object @object,
			Format format,
			SHA256 cookingUnitHash,
			AssetAttributes attributes
		) {
			using MemoryStream stream = new MemoryStream();
			WriteToStream(path, stream, @object, format);
			stream.Seek(0, SeekOrigin.Begin);
			bundle.ImportFile(path, stream, cookingUnitHash, attributes);
		}

		public override void WriteToStream(Stream stream, object @object, Format format)
		{
			throw new NotSupportedException();
		}

		public void WriteToStream(string path, Stream stream, object @object, Format format)
		{
			pathStack.Push(path);
			PushCurrent(this);
			try {
				base.WriteToStream(stream, @object, format);
			} finally {
				pathStack.Pop();
				PopCurrent();
			}
		}

		public override T ReadFromString<T>(string source, object @object = null)
		{
			throw new NotSupportedException();
		}

		public T ReadFromString<T>(string path, string source, object @object = null)
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
			return ReadFromStream<T>(path, stream, @object);
		}

		public override T ReadFromBundle<T>(AssetBundle bundle, string path, object obj = null)
		{
			using Stream stream = bundle.OpenFileLocalized(path);
			return ReadFromStream<T>(path, stream, obj);
		}

		public override T ReadFromFile<T>(string path, object @object = null)
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ReadFromStream<T>(path, stream, @object);
		}

		public override T ReadFromStream<T>(Stream stream, object @object = null)
		{
			throw new NotSupportedException();
		}

		public T ReadFromStream<T>(string path, Stream stream, object obj = null)
		{
			pathStack.Push(path);
			PushCurrent(this);
			try {
				return base.ReadFromStream<T>(stream, obj);
			} catch {
				if (!stream.CanSeek) {
					var ms = new MemoryStream();
					stream.CopyTo(ms);
					ms.Seek(0, SeekOrigin.Begin);
					stream = ms;
				}
				if (!CheckBinarySignature(stream) && HasConflicts(stream)) {
					throw new InvalidOperationException($"{path} has git conflicts");
				} else {
					throw;
				}
			} finally {
				pathStack.Pop();
				PopCurrent();
			}
		}

		private static bool HasConflicts(Stream stream)
		{
			stream.Seek(0, SeekOrigin.Begin);
			using var reader = new StreamReader(stream);
			return conflictRegex.IsMatch(reader.ReadToEnd());
		}
	}
}
