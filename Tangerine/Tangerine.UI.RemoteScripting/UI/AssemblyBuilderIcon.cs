using Lime;

namespace Tangerine.UI.RemoteScripting
{
	public class AssemblyBuilderIcon
	{
		public bool IsDynamic { get; protected set; }
		public Color4 TopLeftColor { get; protected set; }
		public Color4 TopRightColor { get; protected set; }
		public Color4 BottomRightColor { get; protected set; }
		public Color4 BottomLeftColor { get; protected set; }

		protected AssemblyBuilderIcon() { }

		protected AssemblyBuilderIcon(Color4 commonColor)
		{
			TopLeftColor = TopRightColor = BottomRightColor = BottomLeftColor = commonColor;
		}

		public virtual void Update(float delta) { }
	}

	public class AssemblyBuilderDefaultIcon : AssemblyBuilderIcon
	{
		public static AssemblyBuilderDefaultIcon Instance { get; } = new AssemblyBuilderDefaultIcon();

		private AssemblyBuilderDefaultIcon() : base(ColorTheme.Current.RemoteScripting.AssemblyDefaultIcon) { }
	}

	public class AssemblyBuilderBuildingIcon : AssemblyBuilderIcon
	{
		private const float Period = 1.5f;

		private float time;

		public AssemblyBuilderBuildingIcon()
		{
			IsDynamic = true;
			UpdateColors(0);
		}

		public override void Update(float delta)
		{
			time = (time + delta) % Period;
			UpdateColors(time / Period);
		}

		private void UpdateColors(float progress)
		{
			var defaultColor = ColorTheme.Current.RemoteScripting.AssemblyDefaultIcon;
			var buildingColor = ColorTheme.Current.RemoteScripting.AssemblyBuildSucceededIcon;
			var p = GetCornerProgress(progress, 0);
			TopLeftColor = Color4.Lerp(p, defaultColor, buildingColor);
			p = GetCornerProgress(progress, 0.25f);
			TopRightColor = Color4.Lerp(p, defaultColor, buildingColor);
			p = GetCornerProgress(progress, 0.5f);
			BottomRightColor = Color4.Lerp(p, defaultColor, buildingColor);
			p = GetCornerProgress(progress, 0.75f);
			BottomLeftColor = Color4.Lerp(p, defaultColor, buildingColor);

			static float GetCornerProgress(float overallProgress, float cornerProgressPoint)
			{
				var v = Mathf.Min(Mathf.Abs(overallProgress - cornerProgressPoint), Mathf.Abs(overallProgress - cornerProgressPoint - 1));
				var p = 1 - Mathf.Clamp(v * 4, 0, 1);
				return Mathf.Sin(p * Mathf.HalfPi);
			}
		}
	}

	public class AssemblyBuilderBuildFailedIcon : AssemblyBuilderIcon
	{
		public static AssemblyBuilderBuildFailedIcon Instance { get; } = new AssemblyBuilderBuildFailedIcon();

		private AssemblyBuilderBuildFailedIcon() : base(ColorTheme.Current.RemoteScripting.AssemblyBuildFailedIcon) { }
	}

	public class AssemblyBuilderBuildSucceededIcon : AssemblyBuilderIcon
	{
		public static AssemblyBuilderBuildSucceededIcon Instance { get; } = new AssemblyBuilderBuildSucceededIcon();

		private AssemblyBuilderBuildSucceededIcon() : base(ColorTheme.Current.RemoteScripting.AssemblyBuildSucceededIcon) { }
	}

	public class AssemblyBuilderTransitionIcon : AssemblyBuilderIcon
	{
		private const float Duration = 0.25f;

		private readonly AssemblyBuilderIcon sourceIcon;
		private float time;

		public AssemblyBuilderIcon DestinationIcon { get; }
		public bool IsFinished => time >= Duration;

		public AssemblyBuilderTransitionIcon(AssemblyBuilderIcon sourceIcon, AssemblyBuilderIcon destinationIcon)
		{
			IsDynamic = true;
			this.sourceIcon = sourceIcon;
			DestinationIcon = destinationIcon;
			UpdateColors(0);
		}

		public override void Update(float delta)
		{
			sourceIcon.Update(delta);
			DestinationIcon.Update(delta);
			time = Mathf.Clamp(time + delta, 0, Duration);
			UpdateColors(time / Duration);
		}

		private void UpdateColors(float progress)
		{
			TopLeftColor = Color4.Lerp(progress, sourceIcon.TopLeftColor, DestinationIcon.TopLeftColor);
			TopRightColor = Color4.Lerp(progress, sourceIcon.TopRightColor, DestinationIcon.TopRightColor);
			BottomRightColor = Color4.Lerp(progress, sourceIcon.BottomRightColor, DestinationIcon.BottomRightColor);
			BottomLeftColor = Color4.Lerp(progress, sourceIcon.BottomLeftColor, DestinationIcon.BottomLeftColor);
		}
	}
}
