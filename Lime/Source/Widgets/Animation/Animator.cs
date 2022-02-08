using System;
using System.Collections;
using System.Collections.Generic;
using Yuzu;
using SkinnedVertex = Lime.Animesh.SkinnedVertex;

namespace Lime
{
	public interface IAbstractAnimator
	{
		IAnimable Animable { get; }
		int TargetPropertyPathComparisonCode { get; }
		int Duration { get; }
		bool IsTriggerable { get; }
		Type ValueType { get; }
		void Apply(double time);
		void ExecuteTriggersInRange(double minTime, double maxTime, bool executeTriggerAtMaxTime);
	}

	internal interface IAbstractAnimator<T> : IAbstractAnimator
	{
		T CalcValue(double time);
		IAbstractAnimatorSetter<T> Setter { get; }
	}

	internal interface IAbstractAnimatorSetter<T>
	{
		void SetValue(T value);
	}

	public interface IAnimator : IDisposable, IAbstractAnimator
	{
		IAnimationHost Owner { get; set; }
		string TargetPropertyPath { get; set; }
		string AnimationId { get; set; }
		bool Enabled { get; set; }
		IKeyframeList ReadonlyKeys { get; }
		IKeyframeList Keys { get; }
		object UserData { get; set; }
		void Unbind();
		bool IsZombie { get; }
		object CalcValue(double time);
		void ExecuteTrigger(int frame, double animationTimeCorrection);
		void ResetCache();
#if TANGERINE
		int Version { get; }
		void IncreaseVersion();
#endif // TANGERINE
	}

	public interface IKeyframeList : IList<IKeyframe>
	{
		IKeyframe CreateKeyframe();
		IKeyframe GetByFrame(int frame);

		void Add(int frame, object value, KeyFunction function = KeyFunction.Linear);
		void AddOrdered(int frame, object value, KeyFunction function = KeyFunction.Linear);
		void AddOrdered(IKeyframe keyframe);
#if TANGERINE
		int Version { get; }
#endif // TANGERINE
	}

	public class Animator<T> : IAnimator, IAbstractAnimator<T>, IAbstractAnimatorSetter<T>
	{
		public IAnimationHost Owner { get; set; }
		public IAnimable Animable
		{
			get
			{
				if (animable == null && !IsZombie) {
					Bind();
				}
				return animable;
			}
		}
		private IAnimable animable;
		private double minTime;
		private double maxTime;
		private KeyframeParams @params;
		private int keyIndex;
		protected T value1;
		protected T value2;
		protected T value3;
		protected T value4;
		private bool isTriggerable;
		public bool IsTriggerable
		{
			get
			{
				if (animable == null && !IsZombie) {
					Bind();
				}
				return isTriggerable;
			}
		}
		public bool Enabled { get; set; } = true;
		private delegate void SetterDelegate(T value);
		private delegate void IndexedSetterDelegate(int index, T value);
		private SetterDelegate setter;
		private bool isZombie;
		public bool IsZombie
		{
			get => isZombie;
			private set
			{
				isZombie = value;
#if TANGERINE
				version++;
#endif // TANGERINE
			}
		}

#if TANGERINE
		private int version;
		public int Version => version + ReadonlyKeys.Version;
		public void IncreaseVersion() => version++;
#endif // TANGERINE

		private string targetPropertyPath;
		[YuzuMember("TargetProperty")]
		public string TargetPropertyPath
		{
			get => targetPropertyPath;
			set
			{
				targetPropertyPath = value;
				TargetPropertyPathComparisonCode = Toolbox.StringUniqueCodeGenerator.Generate(value);
			}
		}

		public int TargetPropertyPathComparisonCode { get; private set; }

		public Type ValueType => typeof(T);

		private TypedKeyframeList<T> readonlyKeys;

		[YuzuMember]
		[YuzuCopyable]
		public TypedKeyframeList<T> ReadonlyKeys
		{
			get => readonlyKeys;
			set
			{
				if (readonlyKeys != value) {
					readonlyKeys?.Release();
					readonlyKeys = value;
					readonlyKeys?.AddRef();
					boxedKeys = null;
				}
			}
		}

		[YuzuMember]
		public string AnimationId { get; set; }

		public object UserData { get; set; }

		IAbstractAnimatorSetter<T> IAbstractAnimator<T>.Setter => this;

		public Animator()
		{
			ReadonlyKeys = new TypedKeyframeList<T>();
			ReadonlyKeys.AddRef();
		}

		public void Dispose()
		{
			ReadonlyKeys.Release();
		}

		public TypedKeyframeList<T> Keys
		{
			get
			{
				EnsureKeysAreNotShared();
				return ReadonlyKeys;
			}
		}

		IKeyframeList IAnimator.Keys
		{
			get
			{
				EnsureKeysAreNotShared();
				return ((IAnimator)this).ReadonlyKeys;
			}
		}

		private IKeyframeList boxedKeys;

		IKeyframeList IAnimator.ReadonlyKeys
		{
			get
			{
				if (boxedKeys == null) {
					boxedKeys = new BoxedKeyframeList<T>(ReadonlyKeys);
				}
				return boxedKeys;
			}
		}

		private void EnsureKeysAreNotShared()
		{
			if (ReadonlyKeys.RefCount > 1) {
				ReadonlyKeys = Cloner.Clone(ReadonlyKeys);
			}
		}

		public void Unbind()
		{
			IsZombie = false;
			setter = null;
			animable = null;
		}

		public int Duration => (ReadonlyKeys.Count == 0) ? 0 : ReadonlyKeys[ReadonlyKeys.Count - 1].Frame;

		protected virtual T InterpolateLinear(float t) => value2;
		protected virtual T InterpolateSplined(float t) => InterpolateLinear(t);

		public void Clear()
		{
			keyIndex = 0;
			Keys.Clear();
		}

		public void ExecuteTrigger(int frame, double animationTimeCorrection)
		{
			if (!Enabled || IsZombie || !IsTriggerable) {
				return;
			}
			foreach (var key in ReadonlyKeys) {
				if (key.Frame == frame) {
					Owner?.OnTrigger(TargetPropertyPath, key.Value, animationTimeCorrection: animationTimeCorrection);
					break;
				}
			}
		}

		public void ExecuteTriggersInRange(double minTime, double maxTime, bool executeTriggerAtMaxTime)
		{
			if (!Enabled || IsZombie || !IsTriggerable || Owner == null) {
				return;
			}
			int minFrame = AnimationUtils.SecondsToFramesCeiling(minTime);
			int maxFrame = AnimationUtils.SecondsToFramesCeiling(maxTime) + (executeTriggerAtMaxTime ? 1 : 0);
			if (minFrame >= maxFrame) {
				return;
			}
			foreach (var key in ReadonlyKeys) {
				if (key.Frame >= maxFrame) {
					break;
				} else if (key.Frame >= minFrame) {
					var t = minTime - AnimationUtils.FramesToSeconds(key.Frame);
					Owner.OnTrigger(TargetPropertyPath, key.Value, animationTimeCorrection: t);
				}
			}
		}

		public void SetValue(T value)
		{
			if (Enabled && !IsZombie && ReadonlyKeys.Count > 0) {
				if (setter == null) {
					Bind();
					if (IsZombie) {
						return;
					}
				}
				setter(value);
			}
		}

		public void Apply(double time)
		{
			SetValue(CalcValue(time));
		}

		private void Bind()
		{
			var (p, a, index) = AnimationUtils.GetPropertyByPath(Owner, TargetPropertyPath);
			var mi = p.Info?.GetSetMethod();
			IsZombie =
				a == null
				|| mi == null
				|| p.Info.PropertyType != typeof(T)
				|| a is IList list
					&& index >= list.Count;
			if (IsZombie) {
				return;
			}
			animable = a;
			isTriggerable = p.Triggerable;
			if (index == -1) {
				setter = (SetterDelegate)Delegate.CreateDelegate(typeof(SetterDelegate), a, mi);
			} else {
				var indexedSetter = (IndexedSetterDelegate)Delegate.CreateDelegate(
					typeof(IndexedSetterDelegate), a, mi
				);
				setter = (v) => {
					indexedSetter(index, v);
				};
			}
		}

		public void ResetCache()
		{
			minTime = maxTime = 0;
			Owner?.OnAnimatorCollectionChanged();
		}

		public T CalcValue(double time)
		{
			if (time < minTime || time >= maxTime) {
				CacheInterpolationParameters(time);
			}
			if (@params.Function == KeyFunction.Step) {
				return value2;
			}
			var t = (float)((time - minTime) / (maxTime - minTime));
			if (@params.EasingFunction != Mathf.EasingFunction.Linear) {
				t = Mathf.Easings.Interpolate(t, @params.EasingFunction, @params.EasingType);
			}
			if (@params.Function == KeyFunction.Linear) {
				return InterpolateLinear(t);
			} else {
				return InterpolateSplined(t);
			}
		}

		object IAnimator.CalcValue(double time) => CalcValue(time);

		private static KeyframeParams defaultKeyframeParams = new KeyframeParams {
			Function = KeyFunction.Step,
			EasingFunction = Mathf.EasingFunction.Linear,
		};

		private void CacheInterpolationParameters(double time)
		{
			int count = ReadonlyKeys.Count;
			if (count == 0) {
				value2 = default(T);
				minTime = -double.MaxValue;
				maxTime = double.MaxValue;
				@params = defaultKeyframeParams;
				return;
			}
			var i = keyIndex;
			if (i >= count) {
				i = count - 1;
			}
			int frame = AnimationUtils.SecondsToFrames(time);
			// find rightmost key on the left from the given frame
			while (i < count - 1 && frame > ReadonlyKeys[i].Frame) {
				i++;
			}
			while (i >= 0 && frame < ReadonlyKeys[i].Frame) {
				i--;
			}
			keyIndex = i;
			if (i < 0) {
				keyIndex = 0;
				maxTime = ReadonlyKeys[0].Frame * AnimationUtils.SecondsPerFrame;
				minTime = double.MinValue;
				value2 = ReadonlyKeys[0].Value;
				@params = defaultKeyframeParams;
			} else if (i == count - 1) {
				minTime = ReadonlyKeys[i].Frame * AnimationUtils.SecondsPerFrame;
				maxTime = double.MaxValue;
				value2 = ReadonlyKeys[i].Value;
				@params = defaultKeyframeParams;
			} else {
				var key1 = ReadonlyKeys[i];
				var key2 = ReadonlyKeys[i + 1];
				minTime = key1.Frame * AnimationUtils.SecondsPerFrame;
				maxTime = key2.Frame * AnimationUtils.SecondsPerFrame;
				value2 = key1.Value;
				value3 = key2.Value;
				@params = key1.Params;
				if (@params.Function == KeyFunction.Spline) {
					value1 = ReadonlyKeys[i < 1 ? 0 : i - 1].Value;
					value4 = ReadonlyKeys[i + 1 >= count - 1 ? count - 1 : i + 2].Value;
				} else if (@params.Function == KeyFunction.ClosedSpline) {
					value1 = ReadonlyKeys[i < 1 ? count - 2 : i - 1].Value;
					value4 = ReadonlyKeys[i + 1 >= count - 1 ? 1 : i + 2].Value;
				}
			}
		}
	}

	public class Vector2Animator : Animator<Vector2>
	{
		protected override Vector2 InterpolateLinear(float t)
		{
			Vector2 r;
			r.X = value2.X + (value3.X - value2.X) * t;
			r.Y = value2.Y + (value3.Y - value2.Y) * t;
			return r;
		}

		protected override Vector2 InterpolateSplined(float t)
		{
			return new Vector2(
				Mathf.CatmullRomSpline(t, value1.X, value2.X, value3.X, value4.X),
				Mathf.CatmullRomSpline(t, value1.Y, value2.Y, value3.Y, value4.Y)
			);
		}
	}

	public class Vector3Animator : Animator<Vector3>
	{
		protected override Vector3 InterpolateLinear(float t)
		{
			return Vector3.Lerp(t, value2, value3);
		}

		protected override Vector3 InterpolateSplined(float t)
		{
			return Mathf.CatmullRomSpline(t, value1, value2, value3, value4);
		}
	}

	public class NumericAnimator : Animator<float>
	{
		protected override float InterpolateLinear(float t)
		{
			return t * (value3 - value2) + value2;
		}

		protected override float InterpolateSplined(float t)
		{
			return Mathf.CatmullRomSpline(t, value1, value2, value3, value4);
		}
	}

	public class IntAnimator : Animator<int>
	{
		protected override int InterpolateLinear(float t)
		{
			return (t * (value3 - value2) + value2).Round();
		}

		protected override int InterpolateSplined(float t)
		{
			return Mathf.CatmullRomSpline(t, value1, value2, value3, value4).Round();
		}
	}

	public class Color4Animator : Animator<Color4>
	{
		protected override Color4 InterpolateLinear(float t)
		{
			return Color4.Lerp(t, value2, value3);
		}
	}

	public class QuaternionAnimator : Animator<Quaternion>
	{
		protected override Quaternion InterpolateLinear(float t)
		{
			var a = 1.0f - t;
			var b = Quaternion.Dot(value2, value3) > 0.0f ? t : -t;
			Quaternion q;
			q.X = a * value2.X + b * value3.X;
			q.Y = a * value2.Y + b * value3.Y;
			q.Z = a * value2.Z + b * value3.Z;
			q.W = a * value2.W + b * value3.W;
			var invl = Mathf.FastInverseSqrt(q.LengthSquared());
			q.X *= invl;
			q.Y *= invl;
			q.Z *= invl;
			q.W *= invl;
			return q;
		}
	}

	public class Matrix44Animator : Animator<Matrix44>
	{
		protected override Matrix44 InterpolateLinear(float t)
		{
			return Matrix44.Lerp(value2, value3, t);
		}
	}

	public class ThicknessAnimator : Animator<Thickness>
	{
		protected override Thickness InterpolateLinear(float t)
		{
			Thickness r;
			r.Left = value2.Left + (value3.Left - value2.Left) * t;
			r.Right = value2.Right + (value3.Right - value2.Right) * t;
			r.Top = value2.Top + (value3.Top - value2.Top) * t;
			r.Bottom = value2.Bottom + (value3.Bottom - value2.Bottom) * t;
			return r;
		}

		protected override Thickness InterpolateSplined(float t)
		{
			return new Thickness(
				Mathf.CatmullRomSpline(t, value1.Left, value2.Left, value3.Left, value4.Left),
				Mathf.CatmullRomSpline(t, value1.Right, value2.Right, value3.Right, value4.Right),
				Mathf.CatmullRomSpline(t, value1.Top, value2.Top, value3.Top, value4.Top),
				Mathf.CatmullRomSpline(t, value1.Bottom, value2.Bottom, value3.Bottom, value4.Bottom)
			);
		}
	}

	public class NumericRangeAnimator : Animator<NumericRange>
	{
		protected override NumericRange InterpolateLinear(float t)
		{
			NumericRange r;
			r.Median = value2.Median + (value3.Median - value2.Median) * t;
			r.Dispersion = value2.Dispersion + (value3.Dispersion - value2.Dispersion) * t;
			return r;
		}

		protected override NumericRange InterpolateSplined(float t)
		{
			return new NumericRange(
				Mathf.CatmullRomSpline(t, value1.Median, value2.Median, value3.Median, value4.Median),
				Mathf.CatmullRomSpline(t, value1.Dispersion, value2.Dispersion, value3.Dispersion, value4.Dispersion)
			);
		}
	}

	public class SkinnedVertexListAnimator : Animator<List<SkinnedVertex>>
	{
		protected override List<SkinnedVertex> InterpolateLinear(float t)
		{
#if TANGERINE
			var r = new List<SkinnedVertex>();
			for (var i = 0; i < Math.Min(value2.Count, value3.Count); ++i) {
				r.Add(new SkinnedVertex {
					Pos = Mathf.Lerp(t, value2[i].Pos, value3[i].Pos),
					UV1 = Mathf.Lerp(t, value2[i].UV1, value3[i].UV1),
					Color = Color4.Lerp(t, value2[i].Color, value3[i].Color),
					BlendIndices = value2[i].BlendIndices,
					BlendWeights = value2[i].BlendWeights,
				});
			}
			return r;
#else
			return value2;
#endif
		}
	}
}
