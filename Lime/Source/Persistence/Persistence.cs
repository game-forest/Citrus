using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Yuzu;

namespace Lime
{
	public class Persistence
	{
		public enum Format
		{
			Json,
			Binary,
		}

		private Yuzu.Clone.AbstractCloner cloner;

		public readonly CommonOptions YuzuOptions = NewDefaultYuzuOptions();
		public readonly Yuzu.Json.JsonSerializeOptions YuzuJsonOptions = NewDefaultYuzuJsonOptions();

		// used internally to get same defaults as persistence and slightly modify
		// (e.g. for migrations or schema generation)
		internal static CommonOptions NewDefaultYuzuOptions() =>
			new CommonOptions {
				AllowEmptyTypes = true,
				CheckForEmptyCollections = true,
#if TANGERINE || DEBUG
				ReportErrorPosition = true,
#endif // TANGERINE || DEBUG
			};

		// used internally to get same defaults as persistence and
		// slightly modify (e.g. for migrations or schema generation)
		internal static Yuzu.Json.JsonSerializeOptions NewDefaultYuzuJsonOptions() =>
			new Yuzu.Json.JsonSerializeOptions {
				SaveClass = Yuzu.Json.JsonSaveClass.UnknownOrRoot | Yuzu.Json.JsonSaveClass.UnknownPrimitive,
				Unordered = true,
				MaxOnelineFields = 8,
				BOM = true,
			};

		private const uint BinarySignature = 0xdeadbabe;

		private static readonly Func<Persistence, Yuzu.Clone.AbstractCloner> ClonerFactory =
			persistence => new YuzuGenerated.LimeCloner {
				Options = persistence.YuzuOptions,
			};

		public Persistence()
		{ }

		public Persistence(
			CommonOptions yuzuOptions,
			Yuzu.Json.JsonSerializeOptions yuzuJsonOptions
		) {
			YuzuOptions = yuzuOptions;
			YuzuJsonOptions = yuzuJsonOptions ?? YuzuJsonOptions;
		}

		public virtual string WriteToString(object @object, Format format)
		{
			using var stream = new MemoryStream();
			WriteToStream(stream, @object, format);
			return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
		}

		public virtual void WriteToFile(string path, object @object, Format format)
		{
			using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
			WriteToStream(stream, @object, format);
		}

		public virtual void WriteToBundle(
			AssetBundle bundle,
			string path,
			object @object,
			Format format,
			SHA256 cookingUnitHash,
			AssetAttributes attributes
		) {
			using MemoryStream stream = new MemoryStream();
			WriteToStream(stream, @object, format);
			stream.Seek(0, SeekOrigin.Begin);
			bundle.ImportFile(path, stream, cookingUnitHash, attributes);
		}

		public virtual void WriteToStream(Stream stream, object @object, Format format)
		{
			AbstractWriterSerializer ys = null;
			if (format == Format.Binary) {
				WriteYuzuBinarySignature(stream);
				ys = new global::Yuzu.Binary.BinarySerializer { Options = YuzuOptions };
			} else if (format == Format.Json) {
				ys = new global::Yuzu.Json.JsonSerializer {
					Options = YuzuOptions,
					JsonOptions = YuzuJsonOptions,
				};
			}
			ys.ToStream(@object, stream);
		}

		private static void WriteYuzuBinarySignature(Stream s)
		{
			new BinaryWriter(s).Write(BinarySignature);
		}

		public virtual T ReadFromString<T>(string source, object @object = null)
		{
			using var stream = new MemoryStream(Encoding.UTF8.GetBytes(source));
			return ReadFromStream<T>(stream, @object);
		}

		public T ReadFromCurrentBundle<T>(string path, object @object = null)
		{
			return ReadFromBundle<T>(AssetBundle.Current, path, @object);
		}

		public virtual T ReadFromBundle<T>(AssetBundle bundle, string path, object @object = null)
		{
			using Stream stream = bundle.OpenFileLocalized(path);
			return ReadFromStream<T>(stream, @object);
		}

		public virtual T ReadFromFile<T>(string path, object @object = null)
		{
			using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ReadFromStream<T>(stream, @object);
		}

		public virtual T ReadFromStream<T>(Stream stream, object @object = null)
		{
			stream = CopyToMemoryStreamIfRequired(stream);
			var format = DeduceFormat(stream);
			AbstractDeserializer d = CreateDeserializer(format);
			return @object == null
				? d.FromStream<T>(stream)
				: (T)d.FromStream(@object, stream);
		}

		// 1. prefetching into memory speeds up deserialization
		// 2. stream may be compressed thus not supporting Seek() which is required by Yuzu Json parser
		private static Stream CopyToMemoryStreamIfRequired(Stream stream)
		{
			if (!(stream is MemoryStream)) {
				var ms = new MemoryStream();
				stream.CopyTo(ms);
				ms.Seek(0, SeekOrigin.Begin);
				stream = ms;
			}
			return stream;
		}

		private AbstractDeserializer CreateDeserializer(Format format)
		{
			return format switch {
				Format.Binary => new YuzuGenerated.LimeDeserializer {
					Options = YuzuOptions,
				},
				Format.Json => new Yuzu.Json.JsonDeserializer {
					JsonOptions = YuzuJsonOptions,
					Options = YuzuOptions,
				},
				_ => throw new NotSupportedException("Format not supported."),
			};
		}

		private static Format DeduceFormat(Stream stream)
		{
			return CheckBinarySignature(stream) ? Format.Binary : Format.Json;
		}

		public virtual int CalcObjectCheckSum(object @object)
		{
			using var stream = new MemoryStream();
			WriteToStream(stream, @object, Format.Binary);
			stream.Flush();
			return Toolbox.ComputeHash(stream.GetBuffer(), (int)stream.Length);
		}

		protected static bool CheckBinarySignature(Stream stream)
		{
			uint signature;
			try {
				stream.Seek(0, SeekOrigin.Begin);
				using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
				signature = reader.ReadUInt32();
			} catch {
				stream.Seek(0, SeekOrigin.Begin);
				return false;
			}
			bool result = signature == BinarySignature;
			if (!result) {
				stream.Seek(0, SeekOrigin.Begin);
			}
			return result;
		}

		/// <summary>
		/// Clone object using serialization scheme.
		/// </summary>
		/// <param name="obj">A source object to clone.</param>
		/// <returns></returns>
		public object Clone(object obj)
		{
			if (cloner == null) {
				cloner = ClonerFactory(this);
			}
			return cloner.DeepObject(obj);
		}

		/// <summary>
		/// Clone object using serialization scheme.
		/// </summary>
		/// <typeparam name="T">A type of object that need to be returned.</typeparam>
		/// <param name="obj">A source object to clone.</param>
		/// <returns></returns>
		public T Clone<T>(T obj) => (T)Clone((object)obj);
	}
}
