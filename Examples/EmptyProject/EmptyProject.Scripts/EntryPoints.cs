using EmptyProject.Scripts.Common;
using EmptyProject.Scripts.Tests;
using RemoteScripting;

namespace EmptyProject.Scripts
{
	public static class EntryPoints
	{
		[PortableEntryPoint("Stop all tests", -3000)]
		public static void StopAllTests() => ScriptingToolbox.PostLateTasks.StopByTag(RemoteScripting.ScriptsTasksTag);

		[PortableEntryPoint("Dummy test", -2000)]
		public static void DummyTest() => RunTest(new DummyTest());

		[PortableEntryPoint("Bounding Boxes Tool", -1000)]
		public static void BoundingBoxesTool() => RunTest(new BoundingBoxesTool());

		[PortableEntryPoint("Example Test")]
		public static void ExampleTest() => RunTest(new ExampleTest());

		private static void RunTest(Test test) => test.Run(RemoteScripting.ScriptsTasksTag);
	}
}
