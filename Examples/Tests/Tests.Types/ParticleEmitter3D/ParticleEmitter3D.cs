using Lime;
using System;
using System.Collections.Generic;
using Yuzu;

namespace Tests.Types
{
	public enum EmitterShape3D
	{
		Point,
	}

	public enum BillboardMode
	{
		Disabled,
		ViewPointOriented,
		ViewPlaneAligned,
		WorldOriented,
		AxialY,

	}

	[TangerineRegisterNode(Order = 6)]
	[TangerineNodeBuilder("BuildForTangerine")]
	[TangerineAllowedChildrenTypes(typeof(ParticleModifier3D))]
	[TangerineVisualHintGroup("/All/Nodes/Particles")]
	public partial class ParticleEmitter3D : Node3D, ITangerinePreviewAnimationListener, IUpdatableNode
	{
		internal static System.Random Rng = new System.Random();

		public class Particle
		{
			public ParticleModifier3D Modifier;
			// Position of particle with random motion.
			public Vector3 FullPosition;
			// Position if particle without random motion.
			public Vector3 RegularPosition;
			// Motion direction with random motion.
			public Vector3 FullDirection;
			// Motion direction without random motion(in degrees).
			public Vector3 RegularDirection;
			// Velocity of motion.
			public float Velocity;
			public Vector3 WindDirection;
			// Velocity of particle windage.
			public float WindAmount;
			public Vector3 GravityDirection;
			// Strength of gravity.
			public float GravityAmount;
			// Acceleration of gravity(calculated through gravityAmount).
			public float GravityAcceleration;
			// Velocity of the particle caused by gravity(calculated through gravityAcceleration).
			public float GravityVelocity;
			// Strength of magnet's gravity at the moment of particle birth.
			public Vector3 ScaleInitial;
			// Scale of particle in the current moment.
			public Vector3 ScaleCurrent;
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
			public Vector3 RandomSplineVertex0;
			public Vector3 RandomSplineVertex1;
			public Vector3 RandomSplineVertex2;
			public Vector3 RandomSplineVertex3;
			// Current angle of spline control point, relative to center of random motion.
			public Vector3 RandomRayDirection;
			// Current offset of spline beginning(0..1).
			public float RandomSplineOffset;
			// Current texture of the particle.
			public float TextureIndex;
			// modifier.Animators.OverallDuration / LifeTime
			public float AgeToAnimationTime;
			public Matrix44 Rotation;
		}

		[YuzuMember]
		public BillboardMode BillboardMode { get; set; }
		/// <summary>
		/// Particles are generated once and live forever.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(8)]
		public bool ImmortalParticles { get; set; }
		[YuzuMember]
		public bool NumberPerBurst { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(9)]
		public EmitterShape3D Shape { get; set; }
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
		[YuzuMember]
		[TangerineKeyframeColor(17)]
		public Vector3 WindDirection { get; set; }
		[YuzuMember]
		public NumericRange WindDirectionSpread { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(19)]
		public NumericRange WindAmount { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(20)]
		public Vector3 GravityDirection { get; set; }
		[YuzuMember]
		public NumericRange GravityDirectionSpread { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(21)]
		public NumericRange GravityAmount { get; set; }
		/// <summary>
		/// Rotation angle of generated particles
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(23)]
		public NumericRange Orientation { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(24)]
		public Vector3 Direction { get; set; }
		[YuzuMember]
		public NumericRange DirectionSpread { get; set; }
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
		[YuzuMember]
		[TangerineKeyframeColor(31)]
		public NumericRange RandomMotionRadius { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(1)]
		public NumericRange RandomMotionSpeed { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(3)]
		public Vector3 RandomMotionDirection { get; set; }
		[YuzuMember]
		[TangerineKeyframeColor(3)]
		public NumericRange RandomMotionDirectionSpread { get; set; }
		/// <summary>
		/// Initial color of the particle at the moment of birth.
		/// </summary>
		[YuzuMember]
		public Color4 InitialColor { get; set; }
		[Trigger]
		[TangerineKeyframeColor(1)]
		public EmitterAction Action { get; set; }

		internal static Dictionary<ITexture, CommonMaterial> materialCache
			= new Dictionary<ITexture, CommonMaterial>();

		private static CommonMaterial noTextureMaterial = new CommonMaterial() { DiffuseTexture = null, };

		public Vector2 UV0 => Vector2.Zero;

		public Vector2 UV1 => Vector2.One;

		private bool firstUpdate = true;
		/// <summary>
		/// Number of particles to generate on Update. Used to make particle count FPS independent
		/// by accumulating fractional part of number of particles to spawn on given frame.
		/// </summary>
		private float particlesToSpawn;
		public List<Particle> particles = new List<Particle>();
		private static readonly Stack<Particle> particlePool = new Stack<Particle>();
		private static readonly object particlePoolSync = new object();
		public static bool GloballyEnabled = true;

		void ITangerinePreviewAnimationListener.OnStart()
		{
			firstUpdate = true;
		}

		public ParticleEmitter3D()
		{
			BillboardMode = BillboardMode.Disabled;
			Presenter = DefaultPresenter.Instance;
			Shape = EmitterShape3D.Point;
			ParticlesLinkage = ParticlesLinkage.Parent;
			Number = 100;
			Speed = 1;
			Orientation = new NumericRange(0, 360);
			Direction = new Vector3(1, 0, 0);
			DirectionSpread = new NumericRange(0, 360);
			WindDirection = new Vector3(-1, 0, 0);
			WindDirectionSpread = new NumericRange(0, 0);
			WindAmount = new NumericRange(0, 0);
			GravityDirection = new Vector3(0, -1, 0);
			GravityDirectionSpread = new NumericRange(0, 0);
			GravityAmount = new NumericRange(0, 0);
			Lifetime = new NumericRange(1, 0);
			Zoom = new NumericRange(1, 0);
			AspectRatio = new NumericRange(1, 0);
			Velocity = new NumericRange(100, 0);
			Spin = new NumericRange(0, 0);
			RandomMotionRadius = new NumericRange(20, 0);
			RandomMotionSpeed = new NumericRange(0, 0);
			RandomMotionDirection = Vector3.UnitX;
			RandomMotionDirectionSpread = new NumericRange(0, 360);
			AlongPathOrientation = false;
			TimeShift = 0;
			ImmortalParticles = false;
			NumberPerBurst = false;
			InitialColor = Color4.White;
			Components.Add(new UpdatableNodeBehavior());
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

		private bool TryGetRandomModifier(out ParticleModifier3D modifier)
		{
			var count = 0;
			modifier = null;
			for (var n = FirstChild; n != null; n = n.NextSibling) {
				count += n is ParticleModifier3D ? 1 : 0;
			}
			if (count == 0) {
				return false;
			}
			var targetIndex = Rng.RandomInt(count);
			var index = 0;
			for (var n = FirstChild; n != null; n = n.NextSibling) {
				if (n is ParticleModifier3D particleModifier) {
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

		public Node GetLinkageNode()
		{
			switch (ParticlesLinkage) {
				case ParticlesLinkage.Parent:
					return Parent.AsNode3D == null ? this : Parent;
				case ParticlesLinkage.Other: {
					var node = Parent;
					while (node != null) {
						if (node.Id == LinkageWidgetName)
							return node;
						node = node.Parent;
					}
					return null;
				}
				case ParticlesLinkage.Root:
					Node parent = this;
					while (!(parent is Viewport3D)) {
						parent = parent.Parent;
					}
					return parent;
				default:
					return (Parent != null) ? WidgetContext.Current.Root : null;
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
			if (property == "Action") {
				var action = (EmitterAction)value;
				if (!GetTangerineFlag(TangerineFlags.Hidden)) {
					switch (action) {
						case EmitterAction.Burst:
							burstOnUpdateOnce = true;
							break;
					}
				}
			}
		}

		private void UpdateHelper(float delta)
		{
			delta *= Speed;
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
			} else {
				// this.Number per second
				particlesToSpawn += Number * delta;
			}
			var currentBoundingRect = new Rectangle();
			while (particlesToSpawn >= 1f) {
				Particle particle = AllocParticle();
				if (GloballyEnabled && Nodes.Count > 0 && InitializeParticle(particle)) {
					AdvanceParticle(particle, 0, ref currentBoundingRect);
				} else {
					FreeLastParticles(1);
				}
				particlesToSpawn -= 1;
			}
			int particlesToFreeCount = 0;
			int i = particles.Count - 1;
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
			FreeLastParticles(particlesToFreeCount);
			if (NumberPerBurst) {
				particlesToSpawn = 0.0f;
			}
			if (particles.Count == 0) {
				return;
			}
		}

		private bool burstOnUpdateOnce = false;

		static void ShiftArray(int[] arr, int cnt, int startIndex = 0)
		{
			for (int i = 0; i < cnt; i++) {
				arr[i + startIndex] = arr[i + startIndex + 1];
			}
		}

		public virtual void OnUpdate(float delta)
		{
			if (firstUpdate) {
				firstUpdate = false;
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

		private Vector3 GenerateRandomMotionControlPoint(ref Vector3 rayDirection)
		{
			rayDirection += GetRandomDirection(RandomMotionDirection, RandomMotionDirectionSpread);
			var result = rayDirection;
			NumericRange radius = RandomMotionRadius;
			if (radius.Dispersion == 0) {
				radius.Dispersion = radius.Median;
			}
			result *= Math.Abs(radius.NormalRandomNumber(Rng));
			return result;
		}

		private bool InitializeParticle(Particle p)
		{
			CalcInitialTransform(out var transform);
			transform.Decompose(out var scale, out Quaternion rotation, out var translation);
			var angles = rotation.ToEulerAngles();
			float emitterScaleAmount = scale.Z;
			float emitterAngle = angles.Z;
			NumericRange aspectRatioVariationPair = new NumericRange(0, Math.Max(0.0f, AspectRatio.Dispersion));
			float zoom = Zoom.NormalRandomNumber(Rng);
			float aspectRatio = AspectRatio.Median *
				(1 + Math.Abs(aspectRatioVariationPair.NormalRandomNumber(Rng))) /
				(1 + Math.Abs(aspectRatioVariationPair.NormalRandomNumber(Rng)));
			p.TextureIndex = 0.0f;
			p.Velocity = Velocity.NormalRandomNumber(Rng) * emitterScaleAmount;
			p.ScaleInitial = scale * ApplyAspectRatio(zoom, aspectRatio);
			p.ScaleCurrent = p.ScaleInitial;
			p.WindDirection = GetRandomDirection(WindDirection, WindDirectionSpread);
			p.WindAmount = WindAmount.NormalRandomNumber(Rng) * emitterScaleAmount;
			p.GravityVelocity = 0.0f;
			p.GravityAcceleration = 0.0f;
			p.GravityAmount = GravityAmount.NormalRandomNumber(Rng) * emitterScaleAmount;
			p.GravityDirection = GetRandomDirection(GravityDirection, GravityDirectionSpread);
			p.Lifetime = Math.Max(Lifetime.NormalRandomNumber(Rng), 0.1f);
			p.Age = 0.0f;
			p.Angle = Orientation.UniformRandomNumber(Rng) + emitterAngle;
			p.Spin = Spin.NormalRandomNumber(Rng);
			p.InitialColor = InitialColor;
			p.CurrentColor = InitialColor;
			p.RandomRayDirection = GetRandomDirection(Vector3.UnitX, new NumericRange(0, 360));
			p.RandomSplineVertex0 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
			p.RandomSplineVertex1 = Vector3.Zero;
			p.RandomSplineVertex2 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
			p.RandomSplineVertex3 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
			p.RandomMotionSpeed = RandomMotionSpeed.NormalRandomNumber(Rng);
			p.RandomSplineOffset = 0;
			Vector3 position;
			switch (Shape) {
				case EmitterShape3D.Point:
					position = Vector3.Zero;
					p.RegularDirection = GetRandomDirection(Direction, DirectionSpread);
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
			p.FullDirection = p.RegularDirection;
			p.FullPosition = p.RegularPosition;
			return true;
		}

		private static Vector3 GetRandomDirection(Vector3 direction, NumericRange spread)
		{
			var xz = spread.UniformRandomNumber() * Mathf.DegToRad;
			var yz = spread.UniformRandomNumber() * Mathf.DegToRad;
			var directionXZ = new Vector3(Mathf.Sin(xz), 0, Mathf.Cos(xz));
			var directionYZ = new Vector3(0, Mathf.Sin(yz), Mathf.Cos(yz));
			var spreadDirection = new Vector3(
				directionXZ.X * directionYZ.Z,
				directionYZ.Y,
				directionXZ.Z * directionYZ.Z
			);
			var normalizedDirection = direction.Normalized;
			var binormal = Vector3.CrossProduct(Vector3.UnitY, normalizedDirection);
			if (binormal.SqrLength < 1e-6) {
				binormal = Vector3.UnitZ;
			}
			binormal = binormal.Normalized;
			var normal = Vector3.CrossProduct(binormal, normalizedDirection);
			return
				binormal * spreadDirection.X
				+ normal * spreadDirection.Y
				+ normalizedDirection * spreadDirection.Z;
		}

		private void CalcInitialTransform(out Matrix44 transform)
		{
			transform = LocalTransform;
			var linkageWidget = GetLinkageNode();
			if (linkageWidget != null) {
				for (Node node = Parent; node != null && node != linkageWidget; node = node.Parent) {
					if (node.AsNode3D != null) {
						transform *= node.AsNode3D.LocalTransform;
					}
				}
				if (ParticlesLinkage == ParticlesLinkage.Root && linkageWidget.AsNode3D != null) {
					transform *= linkageWidget.AsNode3D.LocalTransform;
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
				if (p.Lifetime > 0.0f)
					p.Age %= p.Lifetime;
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
					while (p.TextureIndex < downLimit)
						p.TextureIndex += p.Modifier.FirstFrame - downLimit;
				} else {
					p.TextureIndex = Math.Max(p.TextureIndex, p.Modifier.LastFrame);
				}
				p.TextureIndex = Math.Min(p.TextureIndex, p.Modifier.FirstFrame);
			}
			// Updating other properties of a particle.
			float windVelocity = p.WindAmount * p.Modifier.WindAmount;
			if (windVelocity != 0) {
				p.RegularPosition += windVelocity * delta * p.WindDirection;
			}
			if (p.GravityVelocity != 0) {
				p.RegularPosition += p.GravityVelocity * delta * p.GravityDirection;
			}
			var direction = p.RegularDirection.Normalized;
			float velocity = p.Velocity * p.Modifier.Velocity;
			p.GravityAcceleration += p.GravityAmount * p.Modifier.GravityAmount * delta;
			p.GravityVelocity += p.GravityAcceleration * delta;
			p.RegularPosition += velocity * delta * direction;
			p.Angle += p.Spin * p.Modifier.Spin * delta;
			p.ScaleCurrent = p.ScaleInitial * new Vector3(p.Modifier.Scale, 1);
			p.CurrentColor = p.InitialColor * p.Modifier.Color;
			Vector3 positionOnSpline = Vector3.Zero;
			if (p.RandomMotionSpeed > 0.0f) {
				p.RandomSplineOffset += delta * p.RandomMotionSpeed;
				while (p.RandomSplineOffset >= 1.0f) {
					p.RandomSplineOffset -= 1.0f;
					p.RandomSplineVertex0 = p.RandomSplineVertex1;
					p.RandomSplineVertex1 = p.RandomSplineVertex2;
					p.RandomSplineVertex2 = p.RandomSplineVertex3;
					p.RandomSplineVertex3 = GenerateRandomMotionControlPoint(ref p.RandomRayDirection);
				}
				positionOnSpline = Mathf.CatmullRomSpline(p.RandomSplineOffset,
					p.RandomSplineVertex0, p.RandomSplineVertex1,
					p.RandomSplineVertex2, p.RandomSplineVertex3);
			}
			p.FullPosition = p.RegularPosition + positionOnSpline;
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
				GloballyVisible
#if !TANGERINE
				 && particles.Count > 0
#endif // !TANGERINE
			) {
				AddSelfToRenderChain(chain, Layer);
			}
		}

		protected override Lime.RenderObject GetRenderObject()
		{
			var ro = RenderObjectPool<RenderObject>.Acquire();
			var linkageNode = GetLinkageNode();
			var (world, worldInverse) = linkageNode?.AsNode3D != null
				? (linkageNode.AsNode3D.GlobalTransform, linkageNode.AsNode3D.GlobalTransformInverse)
				: (Matrix44.Identity, Matrix44.Identity);
			var cameraPosition = Viewport.Camera.Position * worldInverse;
			world.Decompose(out Vector3 worldScale, out Quaternion _, out var worldTranslation);
			world = Matrix44.CreateScale(worldScale) * Matrix44.CreateTranslation(worldTranslation);
			ro.World = world;
			var view = Viewport.Camera.GlobalTransform;
			var u = new Vector3(view.M11, view.M12, view.M13).Normalized;
			var r = new Vector3(view.M21, view.M22, view.M23).Normalized;
			var n = new Vector3(view.M31, view.M32, view.M33).Normalized;
			Matrix44 billboardingMatrix = Matrix44.Identity;
			switch (BillboardMode) {
				case BillboardMode.ViewPlaneAligned:
					billboardingMatrix = new Matrix44(
						u.X, u.Y, u.Z, 0,
						r.X, r.Y, r.Z, 0,
						-n.X, -n.Y, -n.Z, 0,
						0, 0, 0, 1
					);
					break;
				case BillboardMode.WorldOriented:
					u = Vector3.UnitY;
					r = Vector3.CrossProduct(-n, u).Normalized;
					u = Vector3.CrossProduct(r, -n).Normalized;
					billboardingMatrix = new Matrix44(
						r.X, r.Y, r.Z, 0,
						u.X, u.Y, u.Z, 0,
						-n.X, -n.Y, -n.Z, 0,
						0, 0, 0, 1
					);
					break;
				case BillboardMode.AxialY:
					u = Vector3.UnitY;
					r = Vector3.CrossProduct(-n, u).Normalized;
					n = Vector3.CrossProduct(r, u).Normalized;
					billboardingMatrix = new Matrix44(
						r.X, r.Y, r.Z, 0,
						u.X, u.Y, u.Z, 0,
						n.X, n.Y, n.Z, 0,
						0, 0, 0, 1
					);
					break;
			}
			ro.Color = GlobalColor;
			foreach (var mesh in meshes) {
				ReleaseMesh(mesh);
			}
			meshes.Clear();
			Mesh<Mesh3D.Vertex> currentMesh = null;
			IMaterial currentMaterial = null;
			var vertices = new List<Mesh3D.Vertex>();
			var indicies = new List<ushort>();
			foreach (var p in particles) {
				if (p.CurrentColor.A <= 0) {
					continue;
				}
				var texture = p.Modifier.GetTexture((int)p.TextureIndex - 1);
				var color = p.CurrentColor;
				if (BillboardMode == BillboardMode.ViewPointOriented) {
					billboardingMatrix = Matrix44.CreateLookAtRotation(cameraPosition, p.FullPosition, Vector3.UnitY);
				}
				var scale = p.ScaleCurrent * new Vector3(p.Modifier.Size, 1);
				var transform =
					Matrix44.CreateScale(scale)
					* Matrix44.CreateRotationZ(p.Angle * Mathf.DegToRad)
					* billboardingMatrix
					* Matrix44.CreateTranslation(p.FullPosition);
				CommonMaterial material = null;
				if (texture?.AtlasTexture == null) {
					material = noTextureMaterial;
				} else if (!materialCache.TryGetValue(texture?.AtlasTexture, out material)) {
					material = new CommonMaterial { DiffuseTexture = texture.AtlasTexture, };
					materialCache[texture.AtlasTexture] = material;
				}
				material.DiffuseColor = GlobalColor;
				if (currentMaterial == null) {
					currentMaterial = material;
					currentMesh = AcquireMesh();
				} else if (currentMaterial != material) {
					currentMesh.Vertices = vertices.ToArray();
					currentMesh.Indices = indicies.ToArray();
					currentMesh.DirtyFlags |= MeshDirtyFlags.VerticesIndices;
					ro.RenderData.Add((currentMesh, currentMaterial));
					currentMaterial = material;
					currentMesh = AcquireMesh();
					meshes.Add(currentMesh);
					vertices.Clear();
					indicies.Clear();
				}
				var position = new Vector3(-Vector2.Half, 0);
				var size = Vector2.One;
				var index = (ushort)vertices.Count;
				var v0 = position * transform;
				var v1 = (position + new Vector3(size.X, 0, 0)) * transform;
				var v2 = (position + new Vector3(size.X, size.Y, 0)) * transform;
				var v3 = (position + new Vector3(0, size.Y, 0)) * transform;
				vertices.Add(new Mesh3D.Vertex {
					Pos = v0,
					Color = color,
					UV1 = Vector2.Zero,
				});
				vertices.Add(new Mesh3D.Vertex {
					Pos = v1,
					Color = color,
					UV1 = new Vector2(1, 0),
				});
				vertices.Add(new Mesh3D.Vertex {
					Pos = v2,
					Color = color,
					UV1 = Vector2.One,
				});
				vertices.Add(new Mesh3D.Vertex {
					Pos = v3,
					Color = color,
					UV1 = new Vector2(0, 1),
				});
				indicies.Add(index);
				indicies.Add((ushort)(index + 1));
				indicies.Add((ushort)(index + 3));
				indicies.Add((ushort)(index + 1));
				indicies.Add((ushort)(index + 2));
				indicies.Add((ushort)(index + 3));
			}
			if (vertices.Count > 0) {
				currentMesh.Vertices = vertices.ToArray();
				currentMesh.Indices = indicies.ToArray();
				currentMesh.DirtyFlags |= MeshDirtyFlags.VerticesIndices;
				ro.RenderData.Add((currentMesh, currentMaterial));
			}
			ro.Color = GlobalColor;
			return ro;
		}

		private static Queue<Mesh<Mesh3D.Vertex>> meshPool = new Queue<Mesh<Mesh3D.Vertex>>();
		private List<Mesh<Mesh3D.Vertex>> meshes = new List<Mesh<Mesh3D.Vertex>>();

		private static Mesh<Mesh3D.Vertex> AcquireMesh()
		{
			Mesh<Mesh3D.Vertex> mesh;
			lock (meshPool) {
				if (meshPool.Count > 0) {
					mesh = meshPool.Dequeue();
				} else {
					mesh = new Mesh<Mesh3D.Vertex> {
						AttributeLocations = new[] {
							ShaderPrograms.Attributes.Pos1, ShaderPrograms.Attributes.Color1, ShaderPrograms.Attributes.UV1,
							ShaderPrograms.Attributes.BlendIndices, ShaderPrograms.Attributes.BlendWeights,
							ShaderPrograms.Attributes.Normal, ShaderPrograms.Attributes.Tangent,
						},
						Topology = PrimitiveTopology.TriangleList,
						DirtyFlags = MeshDirtyFlags.All,
					};
				}
			}
			mesh.VertexCount = -1;
			mesh.IndexCount = -1;
			return mesh;
		}

		private static void ReleaseMesh(Mesh<Mesh3D.Vertex> mesh)
		{
			mesh.Vertices = null;
			mesh.Indices = null;
			mesh.IndexCount = -1;
			mesh.VertexCount = -1;
			Window.Current.InvokeOnRendering(() => {
				lock (meshPool) {
					meshPool.Enqueue(mesh);
				}
			});
		}

		public void DeleteAllParticles()
		{
			FreeLastParticles(particles.Count);
		}

		public static Vector2 ApplyAspectRatio(Vector2 scale, float aspectRatio)
		{
			return new Vector2(scale.X * aspectRatio, scale.Y / Math.Max(0.0001f, aspectRatio));
		}

		public static Vector3 ApplyAspectRatio(float zoom, float aspectRatio)
		{
			return new Vector3(zoom * aspectRatio, zoom / Math.Max(0.0001f, aspectRatio), 1);
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

		private void BuildForTangerine()
		{
			var defaultModifier = new ParticleModifier3D() {
				Id = "ParticleModifier"
			};
			Nodes.Add(defaultModifier);
		}

		private class RenderObject : Lime.RenderObject3D
		{
			public Matrix44 World;
			public Color4 Color;
			public List<(Mesh<Mesh3D.Vertex>, IMaterial)> RenderData =
				new List<(Mesh<Mesh3D.Vertex>, IMaterial)>();


			public override void Render()
			{
				Renderer.PushState(RenderState.World);
				Opaque = true;
				Renderer.World = World;
				foreach (var (mesh, material) in RenderData) {
					material.Apply(0);
					mesh.DrawIndexed(0, mesh.Indices.Length);
				}
				Renderer.PopState();
			}

			protected override void OnRelease()
			{
				RenderData.Clear();
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
