using System.Collections.Generic;
using System.IO;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;

namespace Tangerine.Common.FilesDropHandlers
{
	/// <summary>
	/// Handles audios drop.
	/// </summary>
	public class AudiosDropHandler
	{
		/// <summary>
		/// Handles files drop.
		/// </summary>
		/// <param name="files">Dropped files.</param>
		public void Handle(List<string> files)
		{
			using (Document.Current.History.BeginTransaction()) {
				foreach (var file in files.Where(f => Path.GetExtension(f) == ".ogg").ToList()) {
					files.Remove(file);
					if (Utils.ExtractAssetPathOrShowAlert(file, out var assetPath, out _)) {
						CreateAudioFromAsset.Perform(assetPath);
					}
				}
				Document.Current.History.CommitTransaction();
			}

		}
	}
}
