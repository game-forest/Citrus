using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests
{
	[TestClass]
	public class ExceptionTests
	{
		[TestMethod]
		public void ExceptionTest()
		{
			var exceptionMessage = "Sample text";
			var e = Assert.ThrowsException<Lime.Exception>(() => { throw new Lime.Exception(exceptionMessage); });
			Assert.AreEqual(exceptionMessage, e.Message);
			exceptionMessage = "Some text with variables: {0}, {1}, {2}";
			var variables = new object[] { 1, true, "test" };
			e = Assert.ThrowsException<Lime.Exception>(() => { throw new Lime.Exception(exceptionMessage, variables); });
			Assert.AreEqual(string.Format(exceptionMessage, variables), e.Message);
		}
	}
}
