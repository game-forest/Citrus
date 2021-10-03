using System.Collections.Generic;
using System.Diagnostics;

namespace Tests.Scripts.Common
{
	public abstract class TestTask : Test
	{
		protected TestTask(Command.TimeFlow timeFlow = null, bool displayShortLog = true) : base(timeFlow, displayShortLog) { }

		protected abstract IEnumerator<object> RunTest();

		protected override IEnumerator<object> CreateEntryTask() => RunTest();
	}

	public abstract class TestCoroutine : Test
	{
		protected TestCoroutine(Command.TimeFlow timeFlow = null, bool displayShortLog = true) : base(timeFlow, displayShortLog) { }

		protected abstract Coroutine RunTest();

		protected override IEnumerator<object> CreateEntryTask() => RunTest().ToLimeTask();
	}

	public abstract class Test
	{
		private readonly Command.TimeFlow timeFlow;

		public bool WasCompleted { get; private set; }

		protected Test(Command.TimeFlow timeFlow = null, bool displayShortLog = true)
		{
			this.timeFlow = timeFlow ?? new Command.AcceleratedTimeFlow();
		}

		public void Run(object taskTag = null)
		{
			ScriptingToolbox.PostLateTasks.Add(ExecuteTest, taskTag);
		}

		public IEnumerator<object> ExecuteTest()
		{
			Logger.Instance.Script($@"Starting ""{GetType().Name}""");
			var stopwatch = new Stopwatch();
			stopwatch.Start();
			var success = false;
			try {
				yield return 2.5f;

				timeFlow.Apply();
				var taskList = new Lime.TaskList { CreateEntryTask() };
				try {
					while (taskList.Count > 0) {
						try {
							taskList.Update(Lime.Task.Current.Delta);
						} catch (System.Exception exception) {
							Logger.Instance.Warn($@"Unhandled exception while execution ""{GetType().Name}"":");
							Logger.Instance.Warn(exception.ToString());
							yield break;
						}
						yield return null;
					}
					success = true;
				} finally {
					taskList.Stop();

					// Dirty Hack: Trying to stop input simulation after test end
					// Required to fix coroutine's finally blocks calling
					while (Lime.Application.Input.Simulator.ActiveRunners > 0) {
						InputSimulator.End();
					}
					foreach (var key in Lime.Key.Enumerate()) {
						InputSimulator.ReleaseKey(key);
					}
				}
				if (success) {
					WasCompleted = true;
				}
			} finally {
				stopwatch.Stop();
				Logger.Instance.Script($@"{(success ? "Successfully finished" : "Terminated")} ""{GetType().Name}""! ({stopwatch.Elapsed:hh\:mm\:ss})");
				timeFlow.Dispose();
			}
		}

		protected abstract IEnumerator<object> CreateEntryTask();
	}
}
