using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Yuzu;

namespace Lime
{
	public class Animation
	{
		public const string ZeroPoseId = "ZeroPose";
		public readonly static int ZeroPoseIdComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate(ZeroPoseId);
#if TANGERINE
		public static Func<Animation, bool> EasingEnabledChecker;
		public readonly ComponentCollection<Component> Components = new ComponentCollection<Component>();
#endif
		private string id;
		private bool isRunning;
		private AnimationData externalAnimationData;
		private bool? hasEasings;
		private bool applyZeroPose = true;
		internal BucketQueueNode<Animation> QueueNode;
		internal double TimeInternal;
		public Marker MarkerAhead;
		public Action Stopped;
		public Action AssuredStopped;
		public int EffectiveAnimatorsVersion;
		public string RunningMarkerId { get; set; }

		/// <summary>
		/// Cached list of all animators used by this animation.
		/// This cache may be in invalid state, in the user code use <see cref="ValidatedEffectiveAnimators"/>.
		/// </summary>
		public List<IAbstractAnimator> EffectiveAnimators;

#if TANGERINE
		public HashSet<IAbstractAnimator> EffectiveAnimatorsSet;

		public HashSet<IAbstractAnimator> ValidatedEffectiveAnimatorsSet
		{
			get
			{
				if (!AnimationEngine.AreEffectiveAnimatorsValid(this)) {
					AnimationEngine.BuildEffectiveAnimators(this);
				}
				return EffectiveAnimatorsSet;
			}
		}
#endif

		/// <summary>
		/// Cached list of only triggerable animators used by this animation.
		/// This cache may be in invalid state, in the user code use <see cref="ValidatedEffectiveTriggerableAnimators"/>.
		/// </summary>
		public List<IAbstractAnimator> EffectiveTriggerableAnimators;
		public AnimationBezierEasingCalculator BezierEasingCalculator { get; private set; }
		public AnimationEngine AnimationEngine = DefaultAnimationEngine.Instance;
		private AnimationComponent owner;

		/// <summary>
		/// Cached list of all animators used by this animation.
		/// </summary>
		public List<IAbstractAnimator> ValidatedEffectiveAnimators
		{
			get
			{
				if (!AnimationEngine.AreEffectiveAnimatorsValid(this)) {
					AnimationEngine.BuildEffectiveAnimators(this);
				}
				return EffectiveAnimators;
			}
		}

		/// <summary>
		/// Cached list of only triggerable animators used by this animation.
		/// </summary>
		public List<IAbstractAnimator> ValidatedEffectiveTriggerableAnimators
		{
			get
			{
				if (!AnimationEngine.AreEffectiveAnimatorsValid(this)) {
					AnimationEngine.BuildEffectiveAnimators(this);
				}
				return EffectiveTriggerableAnimators;
			}
		}

		[YuzuMember]
		public bool IsCompound { get; set; }

		[YuzuMember]
		[TangerineIgnore]
		public AnimationTrackList Tracks { get; private set; }

		[YuzuMember]
		[TangerineIgnore]
		public MarkerList Markers { get; private set; }

		[YuzuMember]
		public string Id
		{
			get => id;
			set
			{
				if (id != value) {
					IdComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate(value);
					id = value;
				}
			}
		}

		public int IdComparisonCode { get; private set; }

		[YuzuMember]
		[TangerineIgnore]
		public bool IsLegacy { get; set; }

		[YuzuMember]
		public bool ApplyZeroPose
		{
			get => applyZeroPose;
			set
			{
				if (applyZeroPose != value) {
					applyZeroPose = value;
					EffectiveAnimators = null;
				}
			}
		}

		[YuzuMember]
		public string ContentsPath { get; set; }

		public double Time
		{
			get { return TimeInternal; }
			set
			{
				TimeInternal = value;
				MarkerAhead = null;
				RunningMarkerId = null;
				ApplyAnimators();
			}
		}

		public int Frame
		{
			get { return AnimationUtils.SecondsToFrames(Time); }
			set { Time = AnimationUtils.FramesToSeconds(value); }
		}

		public AnimationComponent Owner
		{
			get => owner;
			internal set
			{
				owner = value;
				InvalidateCache();
			}
		}

		public Node OwnerNode => Owner?.Owner;

		public bool IsRunning
		{
			get { return isRunning; }
			set
			{
				if (isRunning != value) {
					bool wasRunning = isRunning;
					isRunning = value;
					if (wasRunning) {
						RaiseStopped();
					}
					if (isRunning) {
						Load();
					}
					if (isRunning) {
						Owner?.OnAnimationRun(this);
					} else {
						Owner?.OnAnimationStopped(this);
					}
				}
			}
		}

		public Animation()
		{
			Markers = new MarkerList(this);
			Tracks = new AnimationTrackList(this);
			BezierEasingCalculator = new AnimationBezierEasingCalculator(Markers, this);
		}

		public void Advance(float delta)
		{
			if (IsRunning) {
				AnimationEngine.AdvanceAnimation(this, delta);
			}
		}

		public void Run(string markerId = null)
		{
			if (!TryRun(markerId)) {
				throw new Exception($"Unknown marker '{markerId}'");
			}
		}

		public bool TryRun(string markerId = null, double animationTimeCorrection = 0)
		{
			bool wasRunning = IsRunning;
			if (AnimationEngine.TryRunAnimation(this, markerId, animationTimeCorrection)) {
				Stopped = null;
				if (wasRunning) {
					RaiseStopped();
				}
				return true;
			}
			return false;
		}

		internal void InvalidateCache()
		{
			MarkerAhead = null;
			EffectiveAnimators = null;
			hasEasings = null;
			BezierEasingCalculator.Invalidate();
		}

		public void ApplyAnimators()
		{
			Load();
			AnimationEngine.ApplyAnimatorsAndExecuteTriggers(this, Time, Time, false);
		}

		public void ScheduleAssuredStopped(Action onStopped, bool immediatelyInvokeIfStopped)
		{
			if (!IsRunning) {
				if (immediatelyInvokeIfStopped) {
					onStopped();
				}
				return;
			}
			AssuredStopped += onStopped;
		}

		internal void RaiseStopped()
		{
			AnimationEngine.RaiseStopped(this);
		}

		public int CalcDurationInFrames()
		{
			var durationInFrames = 0;
			foreach (var a in ValidatedEffectiveAnimators) {
				durationInFrames = Math.Max(durationInFrames, a.Duration);
			}
			if (Markers.Count > 0) {
				durationInFrames = Math.Max(durationInFrames, Markers[Markers.Count - 1].Frame);
			}
			return durationInFrames;
		}

		public double CalcDurationInSeconds() => CalcDurationInFrames() * AnimationUtils.SecondsPerFrame;

		public void Load()
		{
			if (externalAnimationData != null || string.IsNullOrEmpty(ContentsPath) || OwnerNode == null) {
				return;
			}
			externalAnimationData = AnimationData.Load(ContentsPath);
			foreach (var animator in externalAnimationData.Animators) {
				var clone = Cloner.Clone(animator);
				var (host, index) = AnimationUtils.GetPropertyHost(OwnerNode, clone.TargetPropertyPath);
				if (host == null) {
					continue;
				}
				clone.TargetPropertyPath = clone.TargetPropertyPath.Substring(index);
				host.Animators.Add(clone);
			}
		}

		public AnimationData GetData()
		{
			var d = new AnimationData();
			foreach (var animator in ValidatedEffectiveAnimators.OfType<IAnimator>()) {
				var node = (Node)animator.Owner;
				var propertyPath = $"{node.Id}/{animator.TargetPropertyPath}";
				while (node.Parent != OwnerNode) {
					node = node.Parent;
					propertyPath = $"{node.Id}/{propertyPath}";
				}
				var clone = Cloner.Clone(animator);
				clone.TargetPropertyPath = propertyPath;
				d.Animators.Add(clone);
			}
			return d;
		}

		public static string FixAntPath(string path) => path.Replace('|', '_');

		public bool HasEasings()
		{
			if (!hasEasings.HasValue) {
				hasEasings = false;
				foreach (var marker in Markers) {
					if (!marker.BezierEasing.IsDefault()) {
						hasEasings = true;
					}
				}
			}
			return hasEasings.Value;
		}

		public class AnimationData
		{
			private static readonly WeakReferencePool<string, AnimationData> weakReferencePool =
				new WeakReferencePool<string, AnimationData>(path => InternalPersistence.Instance.ReadFromCurrentBundle<AnimationData>(path));

			public delegate bool LoadingDelegate(string path, ref AnimationData instance);
			public delegate void LoadedDelegate(string path, AnimationData instance);
			public static ThreadLocal<LoadingDelegate> Loading;
			public static ThreadLocal<LoadedDelegate> Loaded;

			[YuzuMember]
			public List<IAnimator> Animators { get; private set; } = new List<IAnimator>();

			public static AnimationData Load(string path)
			{
				AnimationData instance = null;
				path = FixAntPath(path);
				path += ".ant";
				if (Loading?.Value?.Invoke(path, ref instance) ?? false) {
					Loaded?.Value?.Invoke(path, instance);
					return instance;
				}
				instance = weakReferencePool.GetItem(path);
				Loaded?.Value?.Invoke(path, instance);
				return instance;
			}
		}

		public class AnimationBezierEasingCalculator
		{
			private readonly Animation owner;
			private readonly MarkerList markers;
			private double easingStartTime;
			private double easingEndTime;
			private double previousTime;
			private double easedPreviousTime;
			private double currentTime;
			private double easedCurrentTime;
			private CubicBezier easingCurve;

			public AnimationBezierEasingCalculator(MarkerList markers, Animation owner)
			{
				this.owner = owner;
				this.markers = markers;
				Invalidate();
			}

			public void Invalidate()
			{
				easingCurve = null;
				easingStartTime = easingEndTime = 0;
				currentTime = previousTime = float.NaN;
			}

			private void CacheEasing(double time)
			{
				easingCurve = null;
				easingStartTime = 0;
				easingEndTime = 0;
				if (markers.Count == 0) {
					easingStartTime = double.NegativeInfinity;
					easingEndTime = double.PositiveInfinity;
					return;
				}
				var frame = AnimationUtils.SecondsToFrames(time);
				int i = -1;
				foreach (var marker in markers) {
					if (marker.Frame > frame) {
						break;
					}
					i++;
				}
				if (i == -1) {
					easingStartTime = double.NegativeInfinity;
					easingEndTime = markers[0].Time;
					return;
				}
				if (i == markers.Count - 1) {
					easingStartTime = markers[i].Time;
					easingEndTime = double.PositiveInfinity;
					return;
				}
				var currentMarker = markers[i];
				var nextMarker = markers[i + 1];
				easingStartTime = currentMarker.Time;
				easingEndTime = nextMarker.Time;
				if (!currentMarker.BezierEasing.IsDefault()) {
					var e = currentMarker.BezierEasing;
					easingCurve = new CubicBezier(e.P1X, e.P1Y, e.P2X, e.P2Y);
				}
			}

			public double EaseTime(double time)
			{
#if TANGERINE
				if (!EasingEnabledChecker?.Invoke(owner) ?? true) {
					return time;
				}
#endif
				if (time == previousTime) {
					return easedPreviousTime;
				}
				if (time == currentTime) {
					return easedCurrentTime;
				}
				if (time < easingStartTime || time >= easingEndTime) {
					CacheEasing(time);
				}
				previousTime = currentTime;
				easedPreviousTime = easedCurrentTime;
				currentTime = time;
				if (easingCurve != null) {
					var d = easingEndTime - easingStartTime;
					var p = (time - easingStartTime) / d;
					var p2 = easingCurve.SolveWithEpsilon(p, 1e-5);
					easedCurrentTime = p2 * d + easingStartTime;
				} else {
					easedCurrentTime = time;
				}
				return easedCurrentTime;
			}
		}
	}
}
