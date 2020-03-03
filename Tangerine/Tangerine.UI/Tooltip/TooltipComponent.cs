using System;
using Lime;

namespace Tangerine.UI
{
	public class TooltipComponent : NodeComponent
	{
		public readonly Func<string> GetText;

		/// <summary>
		/// Delay to show tooltip.
		/// </summary>
		public float ShowDelay { get; set; } = 0.5f;

		/// <summary>
		/// Delay to hide tooltip after specified value if it's more than zero.
		/// </summary>
		public float HideDelay { get; set; } = 10.0f;

		public TooltipComponent(Func<string> textGetter)
		{
			GetText = textGetter;
		}
	}
}
