using Lime;

namespace Tangerine.UI.Timeline
{
	/// <summary>
	///
	/// </summary>
	public interface ITimelineIconProvider
	{
		/// <summary>
		/// Texture version.
		/// </summary>
		/// <remarks>
		/// <para>Polled every frame.</para>
		/// <para>It is guaranteed that this property will be read before the others.</para>
		/// </remarks>
		int TextureVersion { get; }

		/// <summary>
		/// The texture of the icon that will be drawn on the timeline.
		/// </summary>
		/// <remarks>
		/// Changes of this property will be processed only after changing the <see cref="TextureVersion"/> property.
		/// </remarks>
		ITexture Texture { get; }

		/// <summary>
		/// A tooltip that will be displayed when you hover over the icon.
		/// </summary>
		/// <remarks>
		/// The tooltip will be reassigned to an icon every frame.
		/// </remarks>
		string Tooltip { get; }
	}
}
