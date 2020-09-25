namespace Lime
{
	public class CommandProcessor : NodeProcessor
	{
		private CommandHandlerList commandHandlerList;

		public override void Start()
		{
			commandHandlerList = Manager.ServiceProvider.RequireService<CommandHandlerList>();
		}

		public override void Stop(NodeManager manager)
		{
			commandHandlerList = null;
		}

		public override void Update(float delta)
		{
			commandHandlerList.ProcessCommands();
		}
	}
}
