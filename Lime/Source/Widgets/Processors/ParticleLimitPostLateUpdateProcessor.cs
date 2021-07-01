namespace Lime
{
	public class ParticleLimitPostLateUpdateProcessor : NodeProcessor
	{
		private ParticleLimiter particleLimiter;

		public override void Start()
		{
			particleLimiter = Manager.ServiceProvider.RequireService<ParticleLimiter>();
		}

		public override void Stop(NodeManager manager)
		{
			particleLimiter = null;
		}

		public override void Update(float delta)
		{
			particleLimiter.Update(delta);
		}
	}
}
