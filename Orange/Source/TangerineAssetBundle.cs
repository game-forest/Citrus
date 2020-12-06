using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Lime;
using Orange;
using Orange.FbxImporter;
using Yuzu;

namespace Tangerine.Core
{
	public class TangerineAssetBundle : UnpackedAssetBundle
	{
		private const string VersionFile = "__CACHE_VERSION__";

		public class CacheMeta
		{
			private const string CurrentVersion = "1.20";

			[YuzuRequired]
			public string Version { get; set; } = CurrentVersion;

			public bool IsActual => Version == CurrentVersion;
		}

		public TangerineAssetBundle(string baseDirectory) : base(baseDirectory) { }

		public bool IsActual()
		{
			try {
				using (var cacheBundle = OpenCacheBundle(AssetBundleFlags.Writable)) {
					if (!cacheBundle.FileExists(VersionFile)) {
						return false;
					}
					try {
						using (var stream = cacheBundle.OpenFile(VersionFile)) {
							var cacheMeta = TangerinePersistence.Instance.ReadObject<CacheMeta>(VersionFile, stream);
							if (!cacheMeta.IsActual) {
								return false;
							}
						}
					} catch {
						return false;
					}
				}
				return true;
			} catch (InvalidBundleVersionException) {
				return false;
			}
		}

		public void CleanupBundle()
		{
			try {
				using (var cacheBundle = OpenCacheBundle(AssetBundleFlags.Writable)) {
					foreach (var path in cacheBundle.EnumerateFiles().ToList()) {
						cacheBundle.DeleteFile(path);
					}
					InternalPersistence.Instance.WriteObjectToBundle(
						bundle: cacheBundle,
						path: VersionFile,
						instance: new CacheMeta(),
						format: Persistence.Format.Binary,
						default,
						attributes: AssetAttributes.None
					);
				}
			} catch (InvalidBundleVersionException) {
				File.Delete(The.Workspace.GetTangerineCacheBundlePath());
				CleanupBundle();
			}
		}

		public override Stream OpenFile(string path, FileMode mode = FileMode.Open)
		{
			var ext = Path.GetExtension(path);
			if (ext == ".t3d") {
				var exists3DScene = base.FileExists(path);
				var fbxPath = Path.ChangeExtension(path, "fbx");
				var existsFbx = base.FileExists(fbxPath);
				if (existsFbx && exists3DScene) {
					throw new Lime.Exception($"Ambiguity between: {path} and {fbxPath}");
				}
				return exists3DScene ? base.OpenFile(path, mode) : OpenFbx(path);
			}
			if (ext == ".ant") {
				var fbxPath = GetFbxPathFromAnimationPath(path);
				if (fbxPath != null) {
					CheckFbx(fbxPath);
					using (var cacheBundle = OpenCacheBundle()) {
						return cacheBundle.OpenFile(path, mode);
					}
				}
			}
			return base.OpenFile(path, mode);
		}

		private Stream OpenFbx(string path)
		{
			CheckFbx(path);
			using (var cacheBundle = OpenCacheBundle()) {
				return cacheBundle.OpenFile(path);
			}
		}

		private AssetBundle OpenCacheBundle(AssetBundleFlags flags = AssetBundleFlags.None)
		{
			return new PackedAssetBundle(The.Workspace.GetTangerineCacheBundlePath(), flags);
		}

		private void CheckFbx(string path)
		{
			var target = Orange.The.UI.GetActiveTarget();

			using (var cacheBundle = OpenCacheBundle(AssetBundleFlags.Writable)) {
				Model3DAttachment attachment = null;
				Model3D model = null;
				var fbxPath = Path.ChangeExtension(path, "fbx");
				var fbxExists = base.FileExists(fbxPath);
				var fbxCached = cacheBundle.FileExists(path);
				var fbxUpToDate = fbxCached == fbxExists &&
					(!fbxExists || cacheBundle.GetFileCookingUnitHash(path) != base.GetFileCookingUnitHash(fbxPath));

				var attachmentPath = Path.ChangeExtension(path, Model3DAttachment.FileExtension);
				var attachmentExists = base.FileExists(attachmentPath);
				var attachmentCached = cacheBundle.FileExists(attachmentPath);
				var attachmentUpToDate = attachmentCached == attachmentExists &&
					(!attachmentExists || cacheBundle.GetFileCookingUnitHash(attachmentPath) != base.GetFileCookingUnitHash(attachmentPath));
				var fbxImportOptions = new FbxImportOptions {
					Path = fbxPath,
					Target = target,
					ApplyAttachment = false
				};

				var attachmentMetaPath = Path.ChangeExtension(path, Model3DAttachmentMeta.FileExtension);
				var attachmentMetaCached = cacheBundle.FileExists(attachmentMetaPath);
				var attachmentMetaUpToDate = attachmentMetaCached &&
					cacheBundle.GetFileCookingUnitHash(attachmentMetaPath) != base.GetFileCookingUnitHash(fbxPath);
				if (!attachmentMetaUpToDate && fbxExists) {
					using (var fbxImporter = new FbxModelImporter(fbxImportOptions)) {
						model = fbxImporter.LoadModel();
						var meta = new Model3DAttachmentMeta();
						foreach (var animation in model.Animations) {
							meta.SourceAnimationIds.Add(animation.Id);
						}

						foreach (var mesh in model.Descendants.OfType<Mesh3D>()) {
							meta.MeshIds.Add(mesh.Id);
							foreach (var submesh3D in mesh.Submeshes) {
								if (meta.SourceMaterials.All(m => m.Id != submesh3D.Material.Id)) {
									meta.SourceMaterials.Add(submesh3D.Material);
								}
							}
						}
						InternalPersistence.Instance.WriteObjectToBundle(
							cacheBundle,
							attachmentMetaPath,
							meta, Persistence.Format.Binary,
							base.GetFileCookingUnitHash(fbxPath),
							AssetAttributes.None);
					}
				}

				if (fbxUpToDate && attachmentUpToDate) {
					return;
				}

				var animationPathPrefix = Orange.Toolbox.ToUnixSlashes(Path.GetDirectoryName(path) + "/" + Path.GetFileNameWithoutExtension(path) + "@");
				foreach (var assetPath in cacheBundle.EnumerateFiles().ToList()) {
					if (assetPath.EndsWith(".ant") && assetPath.StartsWith(animationPathPrefix)) {
						cacheBundle.DeleteFile(assetPath);
					}
				}

				if (fbxExists) {
					if (model == null) {
						using (var fbxImporter = new FbxModelImporter(fbxImportOptions)) {
							model = fbxImporter.LoadModel();
						}
					}
					if (attachmentExists) {
						attachment = Model3DAttachmentParser.GetModel3DAttachment(fbxPath);
						if (attachment.Animations != null) {
							foreach (var animation in attachment.Animations) {
								if (animation.SourceAnimationId == null) {
									var ac = model.Components.Get<AnimationComponent>();
									animation.SourceAnimationId = ac != null && ac.Animations.Count > 0 ? ac.Animations[0].Id : null;
								}
							}
						}
						attachment.Apply(model);
					}

					foreach (var animation in model.Animations) {
						if (animation.IsLegacy) {
							continue;
						}
						var animationPathWithoutExt = animationPathPrefix + animation.Id;
						animationPathWithoutExt = Animation.FixAntPath(animationPathWithoutExt);
						var animationPath = animationPathWithoutExt + ".ant";
						animation.ContentsPath = animationPathWithoutExt;
						InternalPersistence.Instance.WriteObjectToBundle(cacheBundle, animationPath, animation.GetData(), Persistence.Format.Binary,
							base.GetFileCookingUnitHash(fbxPath), AssetAttributes.None);
						foreach (var animator in animation.ValidatedEffectiveAnimators.OfType<IAnimator>().ToList()) {
							animator.Owner.Animators.Remove(animator);
						}
					}
					InternalPersistence.Instance.WriteObjectToBundle(cacheBundle, path, model, Persistence.Format.Binary,
						base.GetFileCookingUnitHash(fbxPath), AssetAttributes.None);

				} else if (fbxCached) {
					cacheBundle.DeleteFile(path);
					cacheBundle.DeleteFile(attachmentMetaPath);
				}

				if (attachmentExists) {
					InternalPersistence.Instance.WriteObjectToBundle(
						cacheBundle,
						attachmentPath,
						Model3DAttachmentParser.ConvertToModelAttachmentFormat(attachment), Persistence.Format.Binary,
						base.GetFileCookingUnitHash(attachmentPath),
						AssetAttributes.None);
				} else if (attachmentCached) {
					cacheBundle.DeleteFile(attachmentPath);
				}
			}
		}

		public override bool FileExists(string path)
		{
			var ext = Path.GetExtension(path);
			if (ext == ".t3d") {
				var exists3DScene = base.FileExists(path);
				var fbxPath = Path.ChangeExtension(path, "fbx");
				var existsFbx = base.FileExists(fbxPath);
				if (existsFbx && exists3DScene) {
					throw new Lime.Exception($"Ambiguity between: {path} and {fbxPath}");
				}
				return exists3DScene || existsFbx;
			}
			if (ext == ".ant") {
				var fbxPath = GetFbxPathFromAnimationPath(path);
				if (fbxPath != null) {
					CheckFbx(fbxPath);
					using (var cacheBundle = OpenCacheBundle()) {
						return cacheBundle.FileExists(path);
					}
				}
			}
			return base.FileExists(path);
		}

		private string GetFbxPathFromAnimationPath(string animationPath)
		{
			var separatorIndex = animationPath.LastIndexOf("@");
			if (separatorIndex >= 0) {
				return animationPath.Remove(separatorIndex) + ".fbx";
			}
			return null;
		}
	}

	public class Model3DAttachmentMeta
	{
		public const string FileExtension = ".AttachmentMeta";

		[YuzuMember]
		public ObservableCollection<IMaterial> SourceMaterials = new ObservableCollection<IMaterial>();

		[YuzuMember]
		public ObservableCollection<string> SourceAnimationIds = new ObservableCollection<string>();

		[YuzuMember]
		public ObservableCollection<string> MeshIds = new ObservableCollection<string>();
	}
}
