using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Yuzu;

namespace Lime
{
	public interface ITangerinePreviewAnimationListener
	{
		void OnStart();
	}

	public enum EmitterShape
	{
		Point,
		Line,
		Ellipse,
		Area,
		/// <summary>
		/// Particles are generated inside polygon defined by EmitterShapePoint points.
		/// Polygon points are oriented clockwise.
		/// </summary>
		Custom,
	}

	[Flags]
	public enum EmissionType
	{
		None,
		Inner = 1,
		Outer = 2,
	}

	// TODO: Introduce
	// [YuzuMember]
	// public EmissionMethod EmissionMethod { get; set; }
	// when migrations are ready and migrate ImmortalParticles
	// and NumberPerBurst flags into that field.
	// public enum EmissionMethod
	// {
	//     NumberPerSecond,
	//     ConstantNumber,
	//     NumberPerBurst,
	// }
	public enum EmitterAction
	{
		Burst,
		Reset,
	}

	/// <summary>
	/// Particles move with widget they're linked to.
	/// </summary>
	public enum ParticlesLinkage
	{
		/// <summary>
		/// No linkage (default)
		/// </summary>
		Root,
		/// <summary>
		/// Emitter parent.
		/// </summary>
		Parent,
		/// <summary>
		/// User defined widget via LinkageWidgetName
		/// </summary>
		Other,
	}

	[TangerineRegisterNode(Order = 8)]
	[TangerineAllowedParentTypes(typeof(ParticleEmitter))]
	public class EmitterShapePoint : PointObject { }

	[TangerineRegisterNode(Order = 6)]
	[TangerineNodeBuilder("BuildForTangerine")]
	[TangerineAllowedChildrenTypes(typeof(ParticleModifier), typeof(EmitterShapePoint))]
	[TangerineVisualHintGroup("/All/Nodes/Particles")]
	public partial class ParticleEmitter
		: Widget, ITangerinePreviewAnimationListener, IUpdatableNode, IMaterialComponentOwner
	{
		internal static System.Random Rng = new System.Random();

		public class Particle
		{
			public ParticleModifier Modifier;
			// Position of particle with random motion.
			public Vector2 FullPosition;
			// Position if particle without random motion.
			public Vector2 RegularPosition;
			// Motion direction with random motion(in degrees).
			public float FullDirection;
			// Motion direction without random motion(in degrees).
			public float RegularDirection;
			// Velocity of motion.
			public float Velocity;
			// Velocity of changing motion direction(degrees/sec).
			public float AngularVelocity;
			// Direction of particle windage(0 - right, 90 - down).
			public float WindDirection;
			// Velocity of particle windage.
			public float WindAmount;
			// Direction of gravity(0 - right, 90 - down)
			public float GravityDirection;
			// Strength of gravity.
			public float GravityAmount;
			// Acceleration of gravity(calculated through gravityAmount).
			public float GravityAcceleration;
			// Velocity of the particle caused by gravity(calculated through gravityAcceleration).
			public float GravityVelocity;
			// Strength of magnet's gravity at the moment of particle birth.
			public float MagnetAmountInitial;
			// Strength of magnet's gravity in the current moment.
			public float MagnetAmountCurrent;
			// Scale of particle at the moment of particle birth.
			public Vector2 ScaleInitial;
			// Scale of particle in the current moment.
			public Vector2 ScaleCurrent;
			// Rotation of particle relative to its center.
			public float Angle;
			// Velocity of particle rotation(degrees/sec).
			public float Spin;
			// Age of particle in seconds.
			public float Age;
			// Full life time of particle in seconds.
			public float Lifetime;
			// Color of the particle at the moment of birth.
			public Color4 InitialColor;
			// Current color of the particle.
			public Color4 CurrentColor;
			// Velocity of random motion.
			public float RandomMotionSpeed;
			// Splined path of random particle motion.
			public Vector2 RandomSplineVertex0;
			public Vector2 RandomSplineVertex1;
			public Vector2 RandomSplineVertex2;
			public Vector2 RandomSplineVertex3;
			// Current angle of spline control point, relative to center of random motion.
			public float RandomRayDirection;
			// Current offset of spline beginning(0..1).
			public float RandomSplineOffset;
			// Current texture of the particle.
			public float TextureIndex;
			// modifier.Animators.OverallDuration / LifeTime
			public float AgeToAnimationTime;
			// The final particle's transform
			public Matrix32 Transform;
		}

		[AllowOnlyOneComponent]
		[NodeComponentDontSerialize]
		[UpdateStage(typeof(AfterAnimationStage))]
		private class EmitterUpdatableBehavior : BehaviorComponent
		{
			protected internal override void Update(float delta)
			{
				((IUpdatableNode)Owner).OnUpdate(delta);
			}
		}

		private IMaterial defaultMaterial;

		/// <summary>
		/// Particles are generated once and live forever.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(8)]
		public bool ImmortalParticles { get; set; }
		[YuzuMember]
		public bool NumberPerBurst { get; set; }
		[YuzuMember]
		public bool SpawnBetweenFrames { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(9)]
		public EmitterShape Shape { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(10)]
		public EmissionType EmissionType { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(11)]
		public ParticlesLinkage ParticlesLinkage { get; set; }
		/// <summary>
		/// When ParticleLinkage is `Other` this makes sense as name of widget particle emitter is linked to.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(12)]
		public string LinkageWidgetName { get; set; }
		/// <summary>
		/// Pixel distance to trigger burst-generation of particles.
		/// When BurstDistance > 0 ParticleEmitter will generate Number particles
		/// for every BurstDistance pixels of ParticleEmitter's movement.
		/// </summary>
		[YuzuMember]
		[TangerineValidRange(0.0f, float.PositiveInfinity)]
		[TangerineKeyframeColor(7)]
		public float BurstDistance { get; set; }
		/// <summary>
		/// Number of particles generated per second.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(13)]
		public float Number { get; set; }
		/// <summary>
		/// Simulate TimeShift seconds on first update.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(14)]
		public float TimeShift { get; set; }
		/// <summary>
		/// Update: delta *= Speed
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(15)]
		public float Speed { get; set; }
		/// <summary>
		/// Whether particles are oriented along track.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(16)]
		public bool AlongPathOrientation { get; set; }
		/// <summary>
		/// degrees (0 - right, 90 - down)
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(17)]
		public NumericRange WindDirection { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(19)]
		public NumericRange WindAmount { get; set; }
		/// <summary>
		/// degrees (0 - right, 90 - down)
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(20)]
		public NumericRange GravityDirection { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(21)]
		public NumericRange GravityAmount { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(22)]
		public NumericRange MagnetAmount { get; set; }
		/// <summary>
		/// Rotation angle of generated particles
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(23)]
		public NumericRange Orientation { get; set; }
		/// <summary>
		/// Rotation angle of emitter (degrees, clockwise)
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(24)]
		public NumericRange Direction { get; set; }
		/// <summary>
		/// Particle lifetime in seconds
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(25)]
		public NumericRange Lifetime { get; set; }
		/// <summary>
		/// Scale of generated particles
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(26)]
		public NumericRange Zoom { get; set; }
		/// <summary>
		/// Designates width to height ratio.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(27)]
		public NumericRange AspectRatio { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(28)]
		public NumericRange Velocity { get; set; }
		/// <summary>
		/// Angular velocity of particles.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(29)]
		public NumericRange Spin { get; set; }
		/// <summary>
		/// Angular velocity of emitter (degrees)
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(30)]
		public NumericRange AngularVelocity { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(31)]
		public NumericRange RandomMotionRadius { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(1)]
		public NumericRange RandomMotionSpeed { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(2)]
		public float RandomMotionAspectRatio { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(3)]
		public NumericRange RandomMotionRotation { get; set; }
		/// <summary>
		/// Initial color of the particle at the moment of birth.
		/// </summary>
		[YuzuMember]
		public Color4 InitialColor { get; set; }
		[Trigger]
		[TangerineKeyframeColor(1)]
		public EmitterAction Action { get; set; }

		public IMaterial DefaultMaterial
		{
			get
			{
				if (defaultMaterial == null || CleanDirtyFlags(DirtyFlags.Material)) {
					defaultMaterial = WidgetMaterial.GetInstance(GlobalBlending, GlobalShader, 1);
				}
				return defaultMaterial;
			}
		}

		public IMaterial Material { get; set; }

		public Vector2 UV0 => Vector2.Zero;

		public Vector2 UV1 => Vector2.One;

		private bool firstUpdate = true;

		/// <summary>
		/// In NumberPerSecond it is previous ParticleEmitter's transform.
		/// In BurstPerDistance it saves the precise location to spawn next burst.
		/// For other modes it is currently useless.
		/// </summary>
		private Matrix32 previousTransform;

		/// <summary>
		/// Holds previous ParticleEmitter's position with respect to LinkageWidget.
		/// Used for calculation of delta-distance during ParticleEmitter's movement.
		/// </summary>
		private Vector2 previousPosition;

		/// <summary>
		/// Number of particles to generate on Update. Used to make particle count FPS independent
		/// by accumulating fractional part of number of particles to spawn on given frame.
		/// </summary>
		private float particlesToSpawn;
		private float cumulativeDistance;
		public List<Particle> particles = new List<Particle>();
		private static readonly Stack<Particle> particlePool = new Stack<Particle>();
		private static readonly object particlePoolSync = new object();
		private readonly List<EmitterShapePoint> emitterShapePoints = new List<EmitterShapePoint>();
		private readonly List<Vector2> cachedShapePoints = new List<Vector2>();
		// indexed triangle list (3 values per triangle)
		private readonly List<int> cachedShapeTriangles = new List<int>();
		private readonly List<float> cachedShapeTriangleSizes = new List<float>();
		public static bool GloballyEnabled = true;

		void ITangerinePreviewAnimationListener.OnStart()
		{
			firstUpdate = true;
		}

		public ParticleEmitter()
		{
			Presenter = DefaultPresenter.Instance;
			Shape = EmitterShape.Point;
			EmissionType = EmissionType.Outer;
			ParticlesLinkage = ParticlesLinkage.Parent;
			BurstDistance = 0;
			Number = 100;
			Speed = 1;
			Orientation = new NumericRange(0, 360);
			Direction = new NumericRange(0, 360);
			WindDirection = new NumericRange(0, 0);
			WindAmount = new NumericRange(0, 0);
			GravityDirection = new NumericRange(90, 0);
			GravityAmount = new NumericRange(0, 0);
			MagnetAmount = new NumericRange(0, 0);
			Lifetime = new NumericRange(1, 0);
			Zoom = new NumericRange(1, 0);
			AspectRatio = new NumericRange(1, 0);
			Velocity = new NumericRange(100, 0);
			Spin = new NumericRange(0, 0);
			AngularVelocity = new NumericRange(0, 0);
			RandomMotionRadius = new NumericRange(20, 0);
			RandomMotionSpeed = new NumericRange(0, 0);
			RandomMotionAspectRatio = 1;
			RandomMotionRotation = new NumericRange(0, 360);
			AlongPathOrientation = false;
			TimeShift = 0;
			ImmortalParticles = false;
			NumberPerBurst = false;
			InitialColor = Color4.White;
			Components.Add(new EmitterUpdatableBehavior());
		}

		public override void Dispose()
		{
			base.Dispose();
			DeleteAllParticles();
		}

		public void ClearParticles()
		{
			particles.Clear();
		}

		private bool TryGetRandomModifier(out ParticleModifier modifier)
		{
			var count = 0;
			modifier = null;
			for (var n = FirstChild; n != null; n = n.NextSibling) {
				count += n is ParticleModifier ? 1 : 0;
			}
			if (count == 0) {
				return false;
			}
			var targetIndex = Rng.RandomInt(count);
			var index = 0;
			for (var n = FirstChild; n != null; n = n.NextSibling) {
				if (n is ParticleModifier particleModifier) {
					if (index == targetIndex) {
						// TODO: Not cloning it in Tangerine makes it impossible to modify keyframes of animors of
						// default animation of the ParticleModifier when those animators were present before
						// entering animation preview mode and system is running or there are immortal particles
						// still alive. But we can't clone the modifier because it may be animated via
						// parallel animation and assigning a clone to the particle will break that behavior.
						// TODO: Implement Node.MergeInto via Yuzu.Cloner.Merge and try to improve performance with it.
						modifier = particleModifier;
						return true;
					}
					index++;
				}
			}
			return false;
		}

		public Widget GetLinkageWidget()
		{
			switch (ParticlesLinkage) {
			case ParticlesLinkage.Parent:
				return ParentWidget;
			case ParticlesLinkage.Other: {
				var widget = ParentWidget;
				while (widget != null) {
					if (widget.Id == LinkageWidgetName) {
						return widget;
					}
					widget = widget.ParentWidget;
				}
				return null;
			}
			case ParticlesLinkage.Root:
				// Root is either widget that doesn't have a parent
				// or frame contained in WidgetAdapter3D.
				var root = ParentWidget;
				while (root.ParentWidget != null) {
					root = root.ParentWidget;
				}
				return root;
			default:
				throw new NotImplementedException();
			}
		}

		private Particle AllocParticle()
		{
			lock (particlePoolSync) {
				Particle result;
				if (particlePool.Count == 0) {
					result = new Particle();
				} else {
					result = particlePool.Pop();
				}
				particles.Add(result);
				return result;
			}
		}

		/// <summary>
		/// Remove particleCount particles from the end of particles list and put them into particlePool.
		/// </summary>
		/// <param name="particleCount"></param>
		private void FreeLastParticles(int particleCount)
		{
			lock (particlePoolSync) {
				while (particleCount > 0) {
					var p = particles[^1];
					p.Modifier = null;
					particlePool.Push(p);
					particles.RemoveAt(particles.Count - 1);
					particleCount--;
				}
			}
		}

		public override void OnTrigger(string property, object value, double animationTimeCorrection = 0)
		{
			base.OnTrigger(property, value, animationTimeCorrection);
			if (property == nameof(Action)) {
				var action = (EmitterAction)value;
				if (!GetTangerineFlag(TangerineFlags.Hidden)) {
					switch (action) {
						case EmitterAction.Reset:
							firstUpdate = true;
							break;
						case EmitterAction.Burst:
							burstOnUpdateOnce = true;
							break;
					}
				}
			}
		}

		private void UpdateHelper(float delta)
		{
			if (Shape == EmitterShape.Custom) {
				RefreshCustomShape();
			}
			delta *= Speed;
			CalcInitialTransform(out var transform);
			var position = transform.TransformVector(Vector2.Zero);
			var deltaDistance = previousPosition - position;
			previousPosition = position;
			int burstCount = 0;
			bool isInterpolationRequired = false;
			if (NumberPerBurst) {
				// Spawn this.Number of particles each time Action.Burst is triggered
				if (burstOnUpdateOnce) {
					burstOnUpdateOnce = false;
					particlesToSpawn = Number;
				}
			} else if (ImmortalParticles) {
				// Constant number each frame made equal to this.Number of immortal particles
				if (TimeShift > 0) {
					particlesToSpawn += Number * delta / TimeShift;
				} else {
					particlesToSpawn = Number;
				}
				particlesToSpawn = Math.Min(particlesToSpawn, Number - particles.Count);
				FreeLastParticles(particles.Count - (int)Number);
			} else if (BurstDistance > 0f) {
				cumulativeDistance += deltaDistance.Length;
				if (cumulativeDistance >= BurstDistance) {
					burstCount = (int)(cumulativeDistance / BurstDistance);
					particlesToSpawn = Number * burstCount;
				}
			} else {
				// this.Number per second
				particlesToSpawn += Number * delta;
				isInterpolationRequired = SpawnBetweenFrames;
			}
			var currentBoundingRect = new Rectangle();
			if (TryGetParticleLimiter(out var particleLimiter)) {
				particleLimiter.ApplyLimit(this, ref particlesToSpawn);
			}
			// For NumberPerDistance and SpawnBetweenFrames modes we use linear-interpolation between transforms.
			// availableLerpAmount is a total allowed amount to interpolate from previousTransform to current transform.
			// It can be less that 1f for NumberPerDistance mode because we need accurate calculation of positions
			// Where we spawn new particles.
			float availableLerpAmount = burstCount > 0 ? burstCount * BurstDistance / cumulativeDistance : 1f;
			float lerpStep = particlesToSpawn > 0 && (isInterpolationRequired || burstCount > 0)
				? availableLerpAmount / ((int)particlesToSpawn)
				: 0f;
			float deltaStep = delta / ((int)particlesToSpawn);
			int particleIndex = 0;
			int lerpIndex = 0;
			int numberPerBurst = ((int)particlesToSpawn) / Math.Max(1, burstCount);
			int i = particles.Count - 1;
			while (particlesToSpawn >= 1f) {
				Particle particle = AllocParticle();
				if (
					GloballyEnabled &&
					Nodes.Count > 0 &&
					InitializeParticle(
						particle,
						lerpStep > 0f ? Matrix32.Lerp(lerpStep * lerpIndex, previousTransform, transform) : transform
					)
				) {
					AdvanceParticle(
						p: particle,
						delta: isInterpolationRequired ? deltaStep * ((int)particlesToSpawn - 1) : 0f,
						boundingRect: ref currentBoundingRect
					);
				} else {
					FreeLastParticles(1);
				}
				particlesToSpawn -= 1;
				particleIndex++;
				// In NumberPerSecond+SpawnBetweenFrames mode we interpolate each particle between frames' transforms.
				if (isInterpolationRequired || burstCount == 0) {
					lerpIndex++;
				// In BurstPerDistance we spawn groups of particles on equal distances.
				} else if (particleIndex % numberPerBurst == 0) {
					lerpIndex += numberPerBurst;
				}
			}
			if (BurstDistance <= 0f) {
				previousTransform = transform;
			} else if (burstCount > 0) {
				// In BurstPerDistance mode we should calculate cumulativeDistance and previousTransform very accurately
				// We use availableLerpAmount to calculate REAL previousTransform where next particle should be spawned.
				cumulativeDistance -= burstCount * BurstDistance;
				previousTransform = Matrix32.Lerp(availableLerpAmount, previousTransform, transform);
			}
			if (MagnetAmount.Median != 0 || MagnetAmount.Dispersion != 0) {
				EnumerateMagnets();
			}
			int particlesToFreeCount = 0;
			while (i >= 0) {
				Particle particle = particles[i];
				AdvanceParticle(particle, delta, ref currentBoundingRect);
				if (!ImmortalParticles && particle.Age > particle.Lifetime) {
					particles[i] = particles[particles.Count - particlesToFreeCount - 1];
					particles[particles.Count - particlesToFreeCount - 1] = particle;
					particlesToFreeCount++;
				}
				i--;
			}
			ClearMagnets();
			FreeLastParticles(particlesToFreeCount);
			if (NumberPerBurst) {
				particlesToSpawn = 0.0f;
			}
			if (particles.Count == 0) {
				return;
			}
			var linkageWidget = GetLinkageWidget();
			if (linkageWidget != null) {
				// AABB for particles is calculated in space of linked widget,
				// so we need to transform it to local coordinates.
				// Ensure bounding rect orientation because rotation and scale can alter it.
				currentBoundingRect = currentBoundingRect
				  .ToQuadrangle()
				  .Transform(linkageWidget.CalcTransitionToSpaceOf(this))
				  .ToAABB();
			}
			// We do not narrow bounding rects anywhere.
			ExpandBoundingRect(currentBoundingRect);
		}

		private bool burstOnUpdateOnce = false;

		private bool CheckIntersection(Vector2[] v, int[] workPoints, int count, float sign, int startIndex = 0)
		{
			for (int i = 0; i < count; i++) {
				Vector2 point = cachedShapePoints[workPoints[i + startIndex]];
				if (
					Mathf.Sign(Vector2.CrossProduct(v[1] - v[0], point - v[0])) == sign &&
					Mathf.Sign(Vector2.CrossProduct(v[2] - v[1], point - v[1])) == sign &&
					Mathf.Sign(Vector2.CrossProduct(v[0] - v[2], point - v[2])) == sign
				) {
					return true;
				}
			}
			return false;
		}

		private static void ShiftArray(int[] arr, int cnt, int startIndex = 0)
		{
			for (int i = 0; i < cnt; i++) {
				arr[i + startIndex] = arr[i + startIndex + 1];
			}
		}

		private bool GetTriangleHelper(
			int[] workPoints,
			ref int pointCount,
			out int i1,
			out int i2,
			out int i3,
			float sign
		) {
			i1 = 0;
			i2 = 0;
			i3 = 0;
			if (pointCount < 3) {
				return false;
			}
			// special case #1 (first point)
			Vector2[] v = new Vector2 [3];
			v[0] = cachedShapePoints[workPoints[pointCount - 1]];
			v[1] = cachedShapePoints[workPoints[0]];
			v[2] = cachedShapePoints[workPoints[1]];
			if (
				Mathf.Sign(Vector2.CrossProduct(v[2] - v[1], v[0] - v[1])) == sign &&
				!CheckIntersection(v, workPoints, pointCount - 3, sign, 2)
			) {
				i1 = workPoints[pointCount - 1];
				i2 = workPoints[0];
				i3 = workPoints[1];
				ShiftArray(workPoints, pointCount - 1);
				pointCount--;
				return true;
			}
			// special case #2 (last point)
			v[0] = cachedShapePoints[workPoints[pointCount - 2]];
			v[1] = cachedShapePoints[workPoints[pointCount - 1]];
			v[2] = cachedShapePoints[workPoints[0]];
			if (
				Mathf.Sign(Vector2.CrossProduct(v[2] - v[1], v[0] - v[1])) == sign &&
				!CheckIntersection(v, workPoints, pointCount - 3, sign, 1)
			) {
				i1 = workPoints[pointCount - 2];
				i2 = workPoints[pointCount - 1];
				i3 = workPoints[0];
				pointCount--;
				return true;
			}
			// rest of points
			for (int i = 1; i < pointCount - 1; i++) {
				v[0] = cachedShapePoints[workPoints[i - 1]];
				v[1] = cachedShapePoints[workPoints[i]];
				v[2] = cachedShapePoints[workPoints[i + 1]];
				if (
					Mathf.Sign(Vector2.CrossProduct(v[2] - v[1], v[0] - v[1])) == sign &&
					!CheckIntersection(v, workPoints, i - 1, sign) &&
					!CheckIntersection(v, workPoints, pointCount - 2 - i, sign, i + 2)
				) {
					i1 = workPoints[i - 1];
					i2 = workPoints[i];
					i3 = workPoints[i + 1];
					ShiftArray(workPoints, pointCount - 1 - i, i);
					pointCount--;
					return true;
				}
			}
			return false;
		}

		private void RefreshCustomShape()
		{
			emitterShapePoints.Clear();
			for (int i = 0; i < Nodes.Count; i++) {
				var node = Nodes[i];
				if (node is EmitterShapePoint) {
					emitterShapePoints.Add(node as EmitterShapePoint);
				}
			}
			int pointCount = emitterShapePoints.Count;
			if (pointCount < 3) {
				cachedShapePoints.Clear();
				cachedShapeTriangles.Clear();
				cachedShapeTriangleSizes.Clear();
				return;
			}
			// Re-triangulate area if number or position of points are changed.
			bool changed = false;
			if (cachedShapePoints.Count == pointCount) {
				for (int i = 0; i < pointCount; i++) {
					if (emitterShapePoints[i].TransformedPosition != cachedShapePoints[i]) {
						changed = true;
						break;
					}
				}
			} else {
				changed = true;
			}
			if (!changed) {
				return;
			} else {
				cachedShapePoints.Clear();
				cachedShapeTriangles.Clear();
				cachedShapeTriangleSizes.Clear();
			}
			for (int i = 0; i < pointCount; i++) {
				cachedShapePoints.Add(emitterShapePoints[i].TransformedPosition);
			}
			// Find if polygon points are CW or CCW oriented.
			float angle = 0;
			angle += Vector2.AngleRad(
				cachedShapePoints[0] - cachedShapePoints[pointCount - 1],
				cachedShapePoints[1] - cachedShapePoints[0]
			);
			angle += Vector2.AngleRad(
				cachedShapePoints[pointCount - 1] - cachedShapePoints[pointCount - 2],
				cachedShapePoints[0] - cachedShapePoints[pointCount - 1]
			);
			for (int i = 1; i < pointCount - 1; i++) {
				angle += Vector2.AngleRad(
					cachedShapePoints[i] - cachedShapePoints[i - 1],
					cachedShapePoints[i + 1] - cachedShapePoints[i]
				);
			}
			float sign = Mathf.Sign(angle);
			cachedShapeTriangles.Clear();
			cachedShapeTriangleSizes.Clear();
			int[] workPoints = new int[emitterShapePoints.Count];
			for (int i = 0; i < pointCount; i++) {
				workPoints[i] = i;
			}
			float totalSpace = 0;
			while (GetTriangleHelper(workPoints, ref pointCount, out int i1, out int i2, out int i3, sign)) {
				cachedShapeTriangles.Add(i1);
				cachedShapeTriangles.Add(i2);
				cachedShapeTriangles.Add(i3);
				// calc area
				float a = (cachedShapePoints[i2] - cachedShapePoints[i1]).Length;
				float b = (cachedShapePoints[i3] - cachedShapePoints[i2]).Length;
				float c = (cachedShapePoints[i1] - cachedShapePoints[i3]).Length;
				float p = (a + b + c) * 0.5f;
				float s = Mathf.Sqrt(p * (p - a) * (p - b) * (p - c));
				cachedShapeTriangleSizes.Add(s);
				totalSpace += s;
			}
			float accum = 0;
			for (int i = 0; i < cachedShapeTriangleSizes.Count; i++) {
				accum += cachedShapeTriangleSizes[i] / totalSpace;
				cachedShapeTriangleSizes[i] = accum;
			}
		}

		public virtual void OnUpdate(float delta)
		{
			if (TryGetParticleLimiter(out var particleLimiter)) {
				particleLimiter.AddParticleCount(particles.Count);
			}
			if (firstUpdate) {
				firstUpdate = false;
				cumulativeDistance = 0f;
				CalcInitialTransform(out previousTransform);
				previousPosition = previousTransform.TransformVector(Vector2.Zero);
				const float ModellingStep = 0.04f;
				delta = Math.Max(delta, TimeShift);
				while (delta >= ModellingStep) {
					UpdateHelper(ModellingStep);
					delta -= ModellingStep;
				}
				if (delta > 0) {
					UpdateHelper(delta);
				}
			} else {
				UpdateHelper(delta);
			}
		}

		private bool TryGetParticleLimiter(out ParticleLimiter particleLimiter)
		{
			particleLimiter = null;
			return
				Application.EnableParticleLimiter &&
				GloballyVisible &&
				BurstDistance == 0f &&
				!NumberPerBurst && !ImmortalParticles &&
				Manager != null &&
				Manager.ServiceProvider.TryGetService(out particleLimiter);
		}

		private Vector2 GenerateRandomMotionControlPoint(ref float rayDirection)
		{
			rayDirection += RandomMotionRotation.UniformRandomNumber(Rng);
			Vector2 result = Vector2.CosSinRough(rayDirection * Mathf.DegToRad);
			NumericRange radius = RandomMotionRadius;
			if (radius.Dispersion == 0) {
				radius.Dispersion = radius.Median;
			}
			result *= Math.Abs(radius.NormalRandomNumber(Rng));
			if (RandomMotionAspectRatio != 1f && RandomMotionAspectRatio > 0f) {
				result.X *= RandomMotionAspectRatio;
				result.Y /= RandomMotionAspectRatio;
			}
			return result;
		}

		private bool InitializeParticle(Particle p, Matrix32 transform)
		{
			Vector2 emitterScale = new Vector2 {
				X = transform.U.Length,
				Y = transform.V.Length,
			};
			float crossProduct = Vector2.CrossProduct(transform.U, transform.V);
			if (crossProduct < 0.0f) {
				emitterScale.Y = -emitterScale.Y;
			}

			float emitterScaleAmount = Mathf.Sqrt(Math.Abs(crossProduct));
			float emitterAngle = transform.U.Atan2Deg;
			NumericRange aspectRatioVariationPair = new NumericRange(0, Math.Max(0.0f, AspectRatio.Dispersion));
			float zoom = Zoom.NormalRandomNumber(Rng);
			float aspectRatio = AspectRatio.Median *
				(1 + Math.Abs(aspectRatioVariationPair.NormalRandomNumber(Rng))) /
				(1 + Math.Abs(aspectRatioVariationPair.NormalRandomNumber(Rng)));
			p.TextureIndex = 0.0f;
			p.Velocity = Velocity.NormalRandomNumber(Rng) * emitterScaleAmount;
			p.ScaleInitial = emitterScale * ApplyAspectRatio(zoom, aspectRatio);
			p.ScaleCurrent = p.ScaleInitial;
			p.WindDirection = WindDirection.UniformRandomNumber(Rng);
			p.WindAmount = WindAmount.NormalRandomNumber(Rng) * emitterScaleAmount;
			p.GravityVelocity = 0.0f;
			p.GravityAcceleration = 0.0f;
			p.GravityAmount = GravityAmount.NormalRandomNumber(Rng) * emitterScaleAmount;
			p.GravityDirection = GravityDirection.NormalRandomNumber(Rng);
			p.MagnetAmountInitial = MagnetAmount.NormalRandomNumber(Rng);
			p.Lifetime = Math.Max(Lifetime.NormalRandomNumber(Rng), 0.1f);
			p.Age = 0.0f;
			p.AngularVelocity = AngularVelocity.NormalRandomNumber(Rng);
			p.Angle = Orientation.UniformRandomNumber(Rng) + emitterAngle;
			p.Spin = Spin.NormalRandomNumber(Rng);
			p.InitialColor = InitialColor;
			p.CurrentColor = InitialColor;
			p.RandomRayDirection = new NumericRange(0, 360).UniformRandomNumber(Rng);
			p.RandomSplineVertex0 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
			p.RandomSplineVertex1 = Vector2.Zero;
			p.RandomSplineVertex2 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
			p.RandomSplineVertex3 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
			p.RandomMotionSpeed = RandomMotionSpeed.NormalRandomNumber(Rng);
			p.RandomSplineOffset = 0;
			Vector2 position;
			switch (Shape) {
			case EmitterShape.Point:
				position = 0.5f * Size;
				p.RegularDirection = Direction.UniformRandomNumber(Rng) + emitterAngle - 90.0f;
				break;
			case EmitterShape.Line:
				position = new Vector2(Rng.RandomFloat() * Size.X, Size.Y * 0.5f);
				p.RegularDirection = Direction.UniformRandomNumber(Rng) + emitterAngle - 90.0f;
				break;
			case EmitterShape.Ellipse:
				float angle = Rng.RandomFloat(0, 360);
				Vector2 sincos = Vector2.CosSinRough(angle * Mathf.DegToRad);
				position = 0.5f * ((sincos + Vector2.One) * Size);
				p.RegularDirection = Direction.UniformRandomNumber(Rng) + emitterAngle - 90 + angle;
				break;
			case EmitterShape.Area:
				position.X = Rng.RandomFloat() * Size.X;
				position.Y = Rng.RandomFloat() * Size.Y;
				p.RegularDirection = Direction.UniformRandomNumber(Rng) + emitterAngle - 90.0f;
				break;
			case EmitterShape.Custom:
				position = GetPointInCustomShape();
				p.RegularDirection = Direction.UniformRandomNumber(Rng) + emitterAngle - 90.0f;
				break;
			default:
				throw new Lime.Exception("Invalid particle emitter shape");
			}
			p.RegularPosition = transform.TransformVector(position);
			if (!TryGetRandomModifier(out p.Modifier)) {
				return false;
			}
			var animationDuration = AnimationUtils.FramesToSeconds(p.Modifier.Animators.GetOverallDuration());
			p.AgeToAnimationTime = (float)(animationDuration / p.Lifetime);
			if (EmissionType == EmissionType.Inner) {
				p.RegularDirection += 180;
			} else if ((EmissionType & EmissionType.Inner) != 0) {
				if (Rng.RandomInt(2) == 0) {
					p.RegularDirection += 180;
				}
			} else if (EmissionType == 0) {
				return false;
			}
			p.FullDirection = p.RegularDirection;
			p.FullPosition = p.RegularPosition;
			return true;
		}

		private Vector2 GetPointInCustomShape()
		{
			if (cachedShapeTriangles.Count == 0) {
				return Vector2.Zero;
			}
			float rand = Rng.RandomFloat();
			int idx = 0;
			for (int i = 0; i < cachedShapeTriangleSizes.Count; i++) {
				if (rand < cachedShapeTriangleSizes[i]) {
					idx = i;
					break;
				}
			}
			float k1 = Rng.RandomFloat();
			float k2 = Rng.RandomFloat();
			if (k1 + k2 > 1) {
				k1 = 1 - k1;
				k2 = 1 - k2;
			}
			float k3 = 1 - k1 - k2;
			int i1 = cachedShapeTriangles[idx * 3 + 0];
			int i2 = cachedShapeTriangles[idx * 3 + 1];
			int i3 = cachedShapeTriangles[idx * 3 + 2];
			return cachedShapePoints[i1] * k1 + cachedShapePoints[i2] * k2 + cachedShapePoints[i3] * k3;
		}

		private void CalcInitialTransform(out Matrix32 transform)
		{
			transform = CalcLocalToParentTransform();
			Widget linkageWidget = GetLinkageWidget();
			if (linkageWidget != null) {
				for (Node node = Parent; node != null && node != linkageWidget; node = node.Parent) {
					if (node.AsWidget != null) {
						transform *= node.AsWidget.CalcLocalToParentTransform();
					}
				}
			}
		}

		private void AdvanceParticle(Particle p, float delta, ref Rectangle boundingRect)
		{
			p.Age += delta;
			if (p.AgeToAnimationTime > 0) {
				p.Modifier.Animators.Apply(p.Age * p.AgeToAnimationTime);
			}
			if (ImmortalParticles) {
				if (p.Lifetime > 0.0f) {
					p.Age %= p.Lifetime;
				}
			}
			// Updating a particle texture index.
			if (p.TextureIndex == 0.0f) {
				p.TextureIndex = (float)p.Modifier.FirstFrame;
			}
			if (p.Modifier.FirstFrame == p.Modifier.LastFrame) {
				p.TextureIndex = (float)p.Modifier.FirstFrame;
			} else if (p.Modifier.FirstFrame < p.Modifier.LastFrame) {
				p.TextureIndex += delta * Math.Max(0, p.Modifier.AnimationFps);
				if (p.Modifier.LoopedAnimation) {
					float upLimit = p.Modifier.LastFrame + 1.0f;
					while (p.TextureIndex > upLimit) {
						p.TextureIndex -= upLimit - p.Modifier.FirstFrame;
					}
				} else {
					p.TextureIndex = Math.Min(p.TextureIndex, p.Modifier.LastFrame);
				}
				p.TextureIndex = Math.Max(p.TextureIndex, p.Modifier.FirstFrame);
			} else {
				p.TextureIndex -= delta * Math.Max(0, p.Modifier.AnimationFps);
				if (p.Modifier.LoopedAnimation) {
					float downLimit = p.Modifier.LastFrame - 1f;
					while (p.TextureIndex < downLimit) {
						p.TextureIndex += p.Modifier.FirstFrame - downLimit;
					}
				} else {
					p.TextureIndex = Math.Max(p.TextureIndex, p.Modifier.LastFrame);
				}
				p.TextureIndex = Math.Min(p.TextureIndex, p.Modifier.FirstFrame);
			}
			// Updating other properties of a particle.
			float windVelocity = p.WindAmount * p.Modifier.WindAmount;
			if (windVelocity != 0) {
				var windDirection = Vector2.CosSinRough(p.WindDirection * Mathf.DegToRad);
				p.RegularPosition += windVelocity * delta * windDirection;
			}
			if (p.GravityVelocity != 0) {
				var gravityDirection = Vector2.CosSinRough(p.GravityDirection * Mathf.DegToRad);
				p.RegularPosition += p.GravityVelocity * delta * gravityDirection;
			}
			var direction = Vector2.CosSinRough(p.RegularDirection * Mathf.DegToRad);
			float velocity = p.Velocity * p.Modifier.Velocity;
			p.RegularDirection += p.AngularVelocity * p.Modifier.AngularVelocity * delta;
			p.GravityAcceleration += p.GravityAmount * p.Modifier.GravityAmount * delta;
			p.GravityVelocity += p.GravityAcceleration * delta;
			p.RegularPosition += velocity * delta * direction;
			p.Angle += p.Spin * p.Modifier.Spin * delta;
			p.ScaleCurrent = p.ScaleInitial * p.Modifier.Scale;
			p.CurrentColor = p.InitialColor * p.Modifier.Color;
			p.MagnetAmountCurrent = p.MagnetAmountInitial * p.Modifier.MagnetAmount;
			ApplyMagnetsToParticle(p, delta);
			Vector2 positionOnSpline = Vector2.Zero;
			if (p.RandomMotionSpeed > 0.0f) {
				p.RandomSplineOffset += delta * p.RandomMotionSpeed;
				while (p.RandomSplineOffset >= 1.0f) {
					p.RandomSplineOffset -= 1.0f;
					p.RandomSplineVertex0 = p.RandomSplineVertex1;
					p.RandomSplineVertex1 = p.RandomSplineVertex2;
					p.RandomSplineVertex2 = p.RandomSplineVertex3;
					p.RandomSplineVertex3 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
				}
				positionOnSpline = Mathf.CatmullRomSpline(
					p.RandomSplineOffset,
					p.RandomSplineVertex0,
					p.RandomSplineVertex1,
					p.RandomSplineVertex2,
					p.RandomSplineVertex3
				);
			}
			Vector2 previousPosition = p.FullPosition;
			p.FullPosition = p.RegularPosition + positionOnSpline;
			if (AlongPathOrientation) {
				Vector2 deltaPos = p.FullPosition - previousPosition;
				if (deltaPos.SqrLength > 0.00001f) {
					p.FullDirection = deltaPos.Atan2Deg;
				}
			}
			// Recalc transformation matrix.
			float angle = p.Angle;
			if (AlongPathOrientation) {
				angle += p.FullDirection;
			}
			var imageSize = p.Modifier.Size;
			var particleSize = p.ScaleCurrent * imageSize;
			var orientation = Vector2.CosSinRough(angle * Mathf.DegToRad);
			var tux = particleSize.X * orientation.X;
			var tuy = particleSize.X * orientation.Y;
			var tvx = -particleSize.Y * orientation.Y;
			var tvy = particleSize.Y * orientation.X;
			var ttx = p.FullPosition.X;
			var tty = p.FullPosition.Y;
			p.Transform.UX = tux;
			p.Transform.UY = tuy;
			p.Transform.VX = tvx;
			p.Transform.VY = tvy;
			p.Transform.TX = ttx;
			p.Transform.TY = tty;
			// Calculate particle's convex hull.
			// The code below is optimized version of:
			// var v1 = transform.TransformVector(-0.5f, -0.5f);
			// var v2 = transform.TransformVector(0.5f, -0.5f);
			// var v3 = transform.TransformVector(-0.5f, 0.5f);
			// var v4 = v2 + v3 - v1;
			var axux = -0.5f * tux;
			var axuy = -0.5f * tuy;
			var ayvx = -0.5f * tvx + ttx;
			var ayvy = -0.5f * tvy + tty;
			float v1x, v1y, v2x, v2y, v3x, v3y, v4x, v4y;
			v1x = axux + ayvx;
			v1y = axuy + ayvy;
			v2x = 0.5f * tux + ayvx;
			v2y = 0.5f * tuy + ayvy;
			v3x = axux + 0.5f * tvx + ttx;
			v3y = axuy + 0.5f * tvy + tty;
			v4x = v2x + v3x - v1x;
			v4y = v2y + v3y - v1y;
			// Update given bounding rectangle.
			if (boundingRect.AX == boundingRect.BX && boundingRect.AY == boundingRect.BY) {
				boundingRect = new Rectangle(v1x, v1y, v1x, v1y);
			}
			if (v1x < boundingRect.AX) {
				boundingRect.AX = v1x;
			}

			if (v1x > boundingRect.BX) {
				boundingRect.BX = v1x;
			}

			if (v2x < boundingRect.AX) {
				boundingRect.AX = v2x;
			}

			if (v2x > boundingRect.BX) {
				boundingRect.BX = v2x;
			}

			if (v2y < boundingRect.AY) {
				boundingRect.AY = v2y;
			}

			if (v2y > boundingRect.BY) {
				boundingRect.BY = v2y;
			}

			if (v3x < boundingRect.AX) {
				boundingRect.AX = v3x;
			}

			if (v3x > boundingRect.BX) {
				boundingRect.BX = v3x;
			}

			if (v3y < boundingRect.AY) {
				boundingRect.AY = v3y;
			}

			if (v3y > boundingRect.BY) {
				boundingRect.BY = v3y;
			}

			if (v4x < boundingRect.AX) {
				boundingRect.AX = v4x;
			}

			if (v4x > boundingRect.BX) {
				boundingRect.BX = v4x;
			}

			if (v4y < boundingRect.AY) {
				boundingRect.AY = v4y;
			}

			if (v4y > boundingRect.BY) {
				boundingRect.BY = v4y;
			}
#if TANGERINE
			if (p.AgeToAnimationTime > 0) {
				// If particle modifier's values were altered by applying modifier to
				// the particle then return it's values to the state corresponding to current time
				// of emitter's default animation. This is being done in Tangerine to avoid interfering
				// with user changing the modifier and them seeing incorrect values at given keyframe
				// after running the animation.
				// TODO: Handle non legacy animation case when getting rid of legacy animations.
				p.Modifier.Animators.Apply(DefaultAnimation.Time);
			}
#endif // TANGERINE
		}

		public override void AddToRenderChain(RenderChain chain)
		{
			if (
				GloballyVisible &&
#if !TANGERINE
				particles.Count > 0 &&
#endif // !TANGERINE
				ClipRegionTest(chain.ClipRegion)
			) {
				AddSelfToRenderChain(chain, Layer);
			}
		}

		protected internal override Lime.RenderObject GetRenderObject()
		{
			var ro = RenderObjectPool<RenderObject>.Acquire();
			var linkageWidget = GetLinkageWidget();
			if (linkageWidget != null) {
				ro.Matrix = linkageWidget.LocalToWorldTransform;
			} else {
				ro.Matrix = Matrix32.Identity;
			}
			ro.Color = GlobalColor;
			ro.Material = Material ?? DefaultMaterial;
			foreach (var p in particles) {
				if (p.CurrentColor.A <= 0) {
					continue;
				}
				var angle = p.Angle;
				if (AlongPathOrientation) {
					angle += p.FullDirection;
				}
				ro.Particles.Add(new ParticleRenderData {
					Texture = p.Modifier.GetTexture((int)p.TextureIndex - 1),
					Transform = p.Transform,
					Color = p.CurrentColor,
					Angle = angle,
				});
			}
			return ro;
		}

		public void DeleteAllParticles()
		{
			FreeLastParticles(particles.Count);
		}

		public static Vector2 ApplyAspectRatio(Vector2 scale, float aspectRatio)
		{
			return new Vector2(scale.X * aspectRatio, scale.Y / Math.Max(0.0001f, aspectRatio));
		}

		public static Vector2 ApplyAspectRatio(float zoom, float aspectRatio)
		{
			return new Vector2(zoom * aspectRatio, zoom / Math.Max(0.0001f, aspectRatio));
		}

		// Decompose 2d scale into 1d scale and aspect ratio.
		public static void DecomposeScale(Vector2 scale, out float aspectRatio, out float zoom)
		{
			if (scale.Y == 0.0f) {
				aspectRatio = 1.0f;
				zoom = 0.0f;
				return;
			}
			aspectRatio = Mathf.Sqrt(scale.X / scale.Y);
			zoom = scale.Y * aspectRatio;
		}

		public void DrawCustomShape(Color4 areaColor, Color4 lineColor, Matrix32 transform)
		{
			for (int i = 0; i < cachedShapeTriangles.Count; i += 3) {
				Vertex[] v = new Vertex[3];
				for (int j = 0; j < 3; ++j) {
					v[j].Color = areaColor;
					v[j].Pos = cachedShapePoints[cachedShapeTriangles[i + j]] * transform;
				}
				Renderer.DrawTriangleStrip(v, v.Length);
			}
			var spacing = new Vector2(3, 2);
			if (cachedShapePoints.Count < 3) {
				return;
			}
			for (int i = 0; i < cachedShapePoints.Count - 1; ++i) {
				Renderer.DrawDashedLine(
					cachedShapePoints[i] * transform,
					cachedShapePoints[i + 1] * transform,
					lineColor,
					spacing
				);
			}
			Renderer.DrawDashedLine(
				cachedShapePoints.Last() * transform,
				cachedShapePoints.First() * transform,
				lineColor,
				spacing
			);
		}

		private void BuildForTangerine()
		{
			SpawnBetweenFrames = true;
			var defaultModifier = new ParticleModifier() {
				Id = "ParticleModifier",
			};
			var animator = new Color4Animator() {
				TargetPropertyPath = "Color",
			};
			animator.ReadonlyKeys.Add(0, new Color4(255, 255, 255, 0));
			animator.ReadonlyKeys.Add(30, new Color4(255, 255, 255, 255));
			animator.ReadonlyKeys.Add(60, new Color4(255, 255, 255, 0));
			defaultModifier.Animators.Add(animator);
			Nodes.Add(defaultModifier);
		}

		private class RenderObject : Lime.RenderObject
		{
			public Matrix32 Matrix;
			public Color4 Color;
			public IMaterial Material;
			public List<ParticleRenderData> Particles = new List<ParticleRenderData>();

			public override void Render()
			{
				foreach (var p in Particles) {
					Renderer.Transform1 = p.Transform * Matrix;
					Renderer.DrawSprite(
						texture1: p.Texture,
						texture2: null,
						material: Material,
						color: p.Color * Color,
						position: -Vector2.Half,
						size: Vector2.One,
						uv0t1: Vector2.Zero,
						uv1t1: Vector2.One,
						uv0t2: Vector2.Zero,
						uv1t2: Vector2.One
					);
				}
			}

			protected override void OnRelease()
			{
				Particles.Clear();
			}
		}

		private struct ParticleRenderData
		{
			public ITexture Texture;
			public Matrix32 Transform;
			public Color4 Color;
			public float Angle;
		}
	}
}
