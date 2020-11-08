using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Widgets.Tasks
{
	[TestClass]
	public class TaskTests
	{
		[TestMethod]
		public void ToStringTest()
		{
			var nestingEnumerator = ToStringTask();
			var nestingToString = nestingEnumerator.GetType().ToString();
			nestingEnumerator.MoveNext();
			var nestedEnumerator = (IEnumerator<object>) nestingEnumerator.Current;
			var nestedToString = nestedEnumerator.GetType().ToString();
			var task = new Task(ToStringTask());
			Assert.AreEqual(nestingToString, task.ToString());
			task.Advance(0);
			Assert.AreEqual(nestedToString, task.ToString());
			task.Advance(0);
			Assert.AreEqual("Completed", task.ToString());
		}

		private IEnumerator<object> ToStringTask()
		{
			yield return ToStringNestedTask();
		}

		private IEnumerator<object> ToStringNestedTask()
		{
			yield return null;
		}

		[TestMethod]
		public void AdvanceDeltaTest()
		{
			var task = new Task(TwoFramesTask());
			task.Advance(0);
			Assert.AreEqual(0, task.Delta);
			task.Advance(0.5f);
			Assert.AreEqual(0.5f, task.Delta);
		}

		[TestMethod]
		public void AdvanceCompletedTaskTest()
		{
			var task = new Task(TwoFramesTask());
			task.Advance(0);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		[TestMethod]
		public void AdvanceZeroYieldReturnNullTest()
		{
			var task = new Task(AdvanceYieldReturnNullTestTask());
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		[TestMethod]
		public void AdvanceFloatYieldReturnNullTest()
		{
			var task = new Task(AdvanceYieldReturnNullTestTask());
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnNullTestTask()
		{
			yield return null;
		}

		[TestMethod]
		public void AdvanceZeroYieldReturnIntTest()
		{
			var task = new Task(AdvanceYieldReturnIntTestTask());
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			task.Advance(0);
			Assert.IsFalse(task.Completed);
		}

		[TestMethod]
		public void AdvanceFloatYieldReturnIntTest()
		{
			var task = new Task(AdvanceYieldReturnIntTestTask());
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnIntTestTask()
		{
			yield return 1;
		}

		[TestMethod]
		public void AdvanceZeroYieldReturnFloatTest()
		{
			var task = new Task(AdvanceYieldReturnFloatTestTask());
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			task.Advance(0);
			Assert.IsFalse(task.Completed);
		}

		[TestMethod]
		public void AdvanceFloatYieldReturnFloatTest()
		{
			var task = new Task(AdvanceYieldReturnFloatTestTask());
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnFloatTestTask()
		{
			yield return 0.5f;
		}

		[TestMethod]
		public void AdvanceYieldReturnIEnumeratorTest()
		{
			var task = new Task(AdvanceYieldReturnIEnumeratorTestTask());
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnIEnumeratorTestTask()
		{
			yield return AdvanceYieldReturnIEnumeratorTestNestedTask();
		}

		private IEnumerator<object> AdvanceYieldReturnIEnumeratorTestNestedTask()
		{
			yield return null;
		}

		[TestMethod]
		public void AdvanceYieldReturnWaitPredicateTest()
		{
			var task = new Task(AdvanceYieldReturnWaitPredicateTestTask());
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsFalse(task.Completed);
			task.Advance(0.5f);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnWaitPredicateTestTask()
		{
			yield return Task.WaitWhile(totalTime => totalTime < 1);
		}

		[TestMethod]
		public void AdvanceYieldReturnNodeTest()
		{
			var node = new TestNode();
			node.DefaultAnimation.Markers.Add(new Marker("Start", 0, MarkerAction.Play));
			node.DefaultAnimation.Markers.Add(new Marker("Stop", 1, MarkerAction.Play));
			node.RunAnimation("Start");
			var task = new Task(AdvanceYieldReturnNodeTestTask(node));
			node.Update(1);
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			node.Update(1);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnNodeTestTask(Node node)
		{
			yield return node;
		}

		[TestMethod]
		public void AdvanceYieldReturnIEnumerableTest()
		{
			Assert.Fail("Fix Task");
			var task = new Task(AdvanceYieldReturnIEnumerableTestTask());
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> AdvanceYieldReturnIEnumerableTestTask()
		{
			yield return AdvanceYieldReturnIEnumerableTestNestedTask();
		}

		private IEnumerable<object> AdvanceYieldReturnIEnumerableTestNestedTask()
		{
			yield return null;
		}

		[TestMethod]
		public void AdvanceYieldReturnOtherTest()
		{
			var task = new Task(AdvanceYieldReturnOtherTestTask());
			task.Advance(0);
			//Assert.Throws(() => task.Advance(0));
		}

		private IEnumerator<object> AdvanceYieldReturnOtherTestTask()
		{
			yield return new object();
		}

		[TestMethod]
		public void WaitWhileTest()
		{
			var shouldContinueTask = true;
			Func<bool> conditionProvider = () => shouldContinueTask;
			var task = new Task(WaitWhileTestTask(conditionProvider));
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			shouldContinueTask = false;
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> WaitWhileTestTask(Func<bool> shouldContinue)
		{
			yield return Task.WaitWhile(shouldContinue);
		}

		[TestMethod]
		public void WaitWhileTest1()
		{
			var task = new Task(WaitWhileTest1Task());
			task.Advance(1);
			Assert.IsFalse(task.Completed);
			task.Advance(1);
			Assert.IsFalse(task.Completed);
			task.Advance(1);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> WaitWhileTest1Task()
		{
			yield return Task.WaitWhile(totalTime => totalTime < 2);
		}

		[TestMethod]
		public void WaitForAnimationTest()
		{
			var node = new TestNode();
			node.DefaultAnimation.Markers.Add(new Marker("Start", 0, MarkerAction.Play));
			node.DefaultAnimation.Markers.Add(new Marker("Stop", 1, MarkerAction.Play));
			node.RunAnimation("Start");
			var task = new Task(WaitForAnimationTestTask(node));
			node.Update(1);
			task.Advance(0);
			Assert.IsFalse(task.Completed);
			node.Update(1);
			task.Advance(0);
			Assert.IsTrue(task.Completed);
		}

		private IEnumerator<object> WaitForAnimationTestTask(Node node)
		{
			yield return Task.WaitForAnimation(node.DefaultAnimation);
		}

		[TestMethod]
		[Ignore("Test this method in other way")]
		public void ExecuteAsyncTest()
		{
			var sleepTime = TimeSpan.FromMilliseconds(100);
			var task = new Task(Task.ExecuteAsync(() => Thread.Sleep(sleepTime)));
			var stopWatch = new Stopwatch();
			stopWatch.Start();
			while (!task.Completed) {
				task.Advance(0);
			}
			stopWatch.Stop();
			Assert.AreEqual(stopWatch.Elapsed.TotalMilliseconds, sleepTime.TotalMilliseconds, 10);
		}

		[TestMethod]
		[ExpectedException(typeof(PassTestException))]
		public void SinMotionTest()
		{
			var task = new Task(SinMotionTestTask());
			task.Advance(1);
			task.Advance(1f / 3f);
			task.Advance(1f / 6f);
			task.Advance(1f / 6f);
			task.Advance(1f / 3f);
			Assert.Fail("Task didn't finish with PassTestException.");
		}

		private IEnumerator<object> SinMotionTestTask()
		{
			var sequence = Task.SinMotion(1, 0, 1).GetEnumerator();
			sequence.MoveNext();
			Assert.AreEqual(0, sequence.Current);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(0.5f, sequence.Current);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(Mathf.Sqrt(0.5f), sequence.Current, Mathf.ZeroTolerance);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(Mathf.Sqrt(3) / 2, sequence.Current, Mathf.ZeroTolerance);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(1, sequence.Current);
			throw new PassTestException();
		}

		[TestMethod]
		[ExpectedException(typeof(PassTestException))]
		public void SqrtMotionTest()
		{
			var task = new Task(SqrtMotionTestTask());
			task.Advance(1);
			task.Advance(1);
			task.Advance(1);
			Assert.Fail("Task didn't finish with PassTestException.");
		}

		private IEnumerator<object> SqrtMotionTestTask()
		{
			var sequence = Task.SqrtMotion(2, 0, 1).GetEnumerator();
			sequence.MoveNext();
			Assert.AreEqual(0, sequence.Current);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(Mathf.Sqrt(0.5f), sequence.Current, float.Epsilon);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(1, sequence.Current);
			throw new PassTestException();
		}

		[TestMethod]
		[ExpectedException(typeof(PassTestException))]
		public void LinearMotionTest()
		{
			var task = new Task(LinearMotionTestTask());
			task.Advance(1);
			task.Advance(1);
			task.Advance(1);
			Assert.Fail("Task didn't finish with PassTestException.");
		}

		private IEnumerator<object> LinearMotionTestTask()
		{
			var sequence = Task.LinearMotion(2, 0, 1).GetEnumerator();
			sequence.MoveNext();
			Assert.AreEqual(0, sequence.Current);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(0.5f, sequence.Current);
			yield return null;
			sequence.MoveNext();
			Assert.AreEqual(1, sequence.Current);
			throw new PassTestException();
		}

		private IEnumerator<object> TwoFramesTask()
		{
			yield return null;
		}
	}
}
