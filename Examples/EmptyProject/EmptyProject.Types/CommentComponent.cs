using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Yuzu;
#if TANGERINE
using Tangerine.UI.Timeline;
#endif // TANGERINE

namespace EmptyProject.Types
{
	[RemoveOnAssetCook]
	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(Node))]
	public class CommentComponent : NodeComponent
#if TANGERINE
		, ITimelineIconProvider
#endif // TANGERINE
	{
		[YuzuMember]
		public List<string> Comments { get; set; }
		
#if TANGERINE
		private const int IconCount = 10;

		private static readonly ITexture[] icons;
			
		static CommentComponent()
		{
			icons = new ITexture[IconCount];
			for (int i = 1; i <= IconCount; ++i) {
				string name = i <= 9 ? $"CommentCount{i}" : "CommentCount9+";
				icons[i - 1] = Tangerine.UI.IconPool.GetTexture(name);
			}
		}

		private readonly List<string> cachedComments = new List<string>();
		
		private int textureVersion;
		private ITexture cachedTexture;
		private string cachedTooltip;

		int ITimelineIconProvider.TextureVersion
		{
			get
			{
				if (IsCommentsChanged()) {
					cachedComments.Clear();
					if (Comments != null) {
						cachedComments.AddRange(Comments);
						cachedTexture = icons[Math.Clamp(value: Comments.Count - 1, min: 0, max: IconCount - 1)];
						const int MaxVisibleCommentCount = 9;
						cachedTooltip = string.Join(
							separator: "\n\n",
							values: cachedComments.Take(MaxVisibleCommentCount).Select(
								c => string.IsNullOrEmpty(c) ? "empty comment" : c)
						);
						if (cachedComments.Count > MaxVisibleCommentCount) {
							cachedTooltip += $"\n\nand {cachedComments.Count - MaxVisibleCommentCount} more comments";
						}
					} else {
						cachedTexture = null;
					}
					textureVersion = unchecked(1 + textureVersion);
				}
				return textureVersion;
			}
		}

		ITexture ITimelineIconProvider.Texture => cachedTexture;
		
		string ITimelineIconProvider.Tooltip => cachedTooltip;

		private bool IsCommentsChanged()
		{
			if (Comments == null) {
				return cachedComments.Count > 0;
			}
			return !cachedComments.SequenceEqual(Comments);
		}
#endif // TANGERINE
	}
}
