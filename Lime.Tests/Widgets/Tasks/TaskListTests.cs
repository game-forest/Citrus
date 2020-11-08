using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Lime.Tests.Source.Widgets.Tasks
{
	[TestClass]
	public class TaskListTests
	{
		[TestMethod]
		public void StopTest()
		{
			var list = new TaskList();
			var task1 = list.Add(TwoFramesTask);
			var task2 = list.Add(TwoFramesTask);
			var task3 = list.Add(TwoFramesTask);
			list.Stop();
			Assert.AreEqual(0, list.Count);
			CollectionAssert.AreEqual(new TaskList(), list);
			Assert.IsTrue(task1.Completed);
			Assert.IsTrue(task2.Completed);
			Assert.IsTrue(task3.Completed);
		}

		[TestMethod]
		public void StopByPredicateTest()
		{
			var list = new TaskList();
			var task1 = list.Add(TwoFramesTask);
			var task2 = list.Add(TwoFramesTask);
			object tag = new object();
			task2.Tag = tag;
			var task3 = list.Add(TwoFramesTask);
			list.Stop(t => t.Tag == tag);
			CollectionAssert.Contains(list, task1);
			CollectionAssert.DoesNotContain(list, task2);
			CollectionAssert.Contains(list, task3);
			Assert.IsTrue(task2.Completed);
		}

		[TestMethod]
		public void StopByTagTest()
		{
			var list = new TaskList();
			var task1 = list.Add(TwoFramesTask);
			var task2 = list.Add(TwoFramesTask);
			var tag = new object();
			task2.Tag = tag;
			var task3 = list.Add(TwoFramesTask);
			list.StopByTag(tag);
			CollectionAssert.Contains(list, task1);
			CollectionAssert.DoesNotContain(list, task2);
			CollectionAssert.Contains(list, task3);
			Assert.IsTrue(task2.Completed);
		}

		[TestMethod]
		public void AddWithoutTagTest()
		{
			var list = new TaskList();
			var task1 = list.Add(TwoFramesTask());
			var task2 = list.Add(TwoFramesTask);
			CollectionAssert.Contains(list, task1);
			Assert.IsNull(task1.Tag);
			CollectionAssert.Contains(list, task2);
			Assert.IsNull(task2.Tag);
			CollectionAssert.AreEqual(new List<Task> { task1, task2 }, list);
		}

		[TestMethod]
		public void AddWithTagTest()
		{
			var list = new TaskList();
			var tag1 = new object();
			var tag2 = new object();
			var task1 = list.Add(TwoFramesTask(), tag1);
			var task2 = list.Add(TwoFramesTask, tag2);
			CollectionAssert.Contains(list, task1);
			Assert.AreEqual(tag1, task1.Tag);
			CollectionAssert.Contains(list, task2);
			Assert.AreEqual(tag2, task2.Tag);
			CollectionAssert.AreEqual(new List<Task> { task1, task2 }, list);
		}

		[TestMethod]
		[ExpectedException(typeof(PassTestException))]
		public void UpdateNestedTest()
		{
			var list = new TaskList();
			 list.Add(UpdateNestedTestTask(list));
			list.Update(0);
			list.Update(0);
			Assert.Fail("Task didn't throw PassTestException on second Update(). Possible nested Update() call.");
		}

		[TestMethod]
		public void UpdateTest()
		{
			const float UpdateDelta = 0.1f;
			var list = new TaskList();
			var task1 = list.Add(TwoFramesTask);
			var task2 = list.Add(ThreeFramesTask);
			list.Update(UpdateDelta);
			Assert.IsTrue(!task1.Completed);
			Assert.IsTrue(list.Contains(task1));
			Assert.IsTrue(!task2.Completed);
			CollectionAssert.Contains(list, task2);
			list.Update(UpdateDelta);
			Assert.IsTrue(task1.Completed);
			CollectionAssert.Contains(list, task1);
			Assert.IsTrue(!task2.Completed);
			CollectionAssert.Contains(list, task2);
			list.Update(UpdateDelta);
			CollectionAssert.DoesNotContain(list, task1);
			Assert.IsTrue(task2.Completed);
			CollectionAssert.DoesNotContain(list, task2);
			list.Update(UpdateDelta);
			CollectionAssert.DoesNotContain(list, task2);
		}

		private IEnumerator<object> UpdateNestedTestTask(TaskList list)
		{
			list.Update(0);
			yield return null;
			throw new PassTestException();
		}

		private IEnumerator<object> TwoFramesTask()
		{
			yield return null;
		}
		private IEnumerator<object> ThreeFramesTask()
		{
			yield return null;
			yield return null;
		}
	}
}
