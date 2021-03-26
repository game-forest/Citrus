namespace Lime
{
	public class ParticleLimiter
	{
		/// <summary>
		/// Total number of visible particles per given NodeManager.
		/// Calculated during LateUpdateStage.
		/// Provides previous frame value during PreLate, Late, PostLate update stages.
		/// </summary>
		public int ParticleCount { get; private set; }

		/// <summary>
		/// Maximum number of visible particles. Use -1 for unlimited.
		/// Can be changed during any stage.
		/// </summary>
		public int ParticleCountLimit { get; set; } = -1;

		internal ParticleLimiter()
		{
			Reset();
		}

		private int currentParticleCount;
		private int maxParticleCount;
		private int minParticleCount;
		private float limitRatio = 1.0f;
		private float integral = 0.0f;
		private float previousError = 0.0f;
		// PID regulator coefficients are chosen manually.
		private readonly float kp = 0.01f;
		private readonly float kd = 0.0f;
		private readonly float ki = 0.01f;
		private readonly float maxOutput = 1.0f;
		private readonly float minOutput = 0.0f;

		internal void AddParticleCount(int particleCount)
		{
			currentParticleCount += particleCount;
			maxParticleCount = System.Math.Max(maxParticleCount, particleCount);
			minParticleCount = System.Math.Min(minParticleCount, particleCount);
		}

		internal void Reset()
		{
			ParticleCount = currentParticleCount;
			currentParticleCount = 0;
			minParticleCount = int.MaxValue;
			maxParticleCount = 0;
		}

		internal void ApplyLimit(ParticleEmitter emitter, ref float spawnCount)
		{
			if (ParticleCountLimit == -1) {
				return;
			}
			var l = maxParticleCount - minParticleCount;
			var d = emitter.particles.Count - minParticleCount;
			if (l != 0) {
				limitRatio = (float)d / l * (limitRatio - maxOutput) + maxOutput;
			}
			limitRatio = Mathf.Clamp(limitRatio, minOutput, maxOutput);
			limitRatio *= limitRatio;
			spawnCount *= limitRatio;
		}

		internal void Update(float dt)
		{
			ParticleCount = currentParticleCount;
			if (ParticleCountLimit == -1) {
				previousError = integral = 0.0f;
				return;
			}
			float error = (float)ParticleCountLimit - currentParticleCount;
			integral += error * dt;
			float derivative = (error - previousError) / dt;
			previousError = error;
			float output = kp * error + kd * derivative + ki * integral;

			if (output > maxOutput) {
				output = maxOutput;
				integral -= error * dt;
			}
			if (output < minOutput) {
				output = minOutput;
				integral -= error * dt;
			}
			limitRatio = output;
		}
	}
}
