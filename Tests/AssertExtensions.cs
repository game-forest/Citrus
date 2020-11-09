using Microsoft.VisualStudio.TestTools.UnitTesting;
using Lime;

namespace Citrus.Tests
{
	internal class TestNode : Node
	{
		public override void AddToRenderChain(RenderChain chain)
		{ }
	}

	internal class PassTestException : System.Exception
	{
	}

	internal static class AssertExtensions
	{
		public static void AreEqual(this Assert assert, IntVector2 expected, IntVector2 actual)
		{
			Assert.AreEqual(expected.X, actual.X);
			Assert.AreEqual(expected.Y, actual.Y);
		}

		public static void AreEqual(this Assert assert, Size expected, Size actual)
		{
			Assert.AreEqual(expected.Width, actual.Width);
			Assert.AreEqual(expected.Height, actual.Height);
		}

		public static void AreEqual(this Assert assert, Vector2 expected, Vector2 actual)
		{
			Assert.AreEqual(expected.X, actual.X);
			Assert.AreEqual(expected.Y, actual.Y);
		}

		public static void AreEqual(this Assert assert, Vector2 expected, Vector2 actual, float delta)
		{
			Assert.AreEqual(expected.X, actual.X, delta);
			Assert.AreEqual(expected.Y, actual.Y, delta);
		}

		public static void AreEqual(this Assert assert, Vector3 expected, Vector3 actual)
		{
			Assert.AreEqual(expected.X, actual.X);
			Assert.AreEqual(expected.Y, actual.Y);
			Assert.AreEqual(expected.Z, actual.Z);
		}

		public static void AreEqual(this Assert assert, Vector3 expected, Vector3 actual, float delta)
		{
			Assert.AreEqual(expected.X, actual.X, delta);
			Assert.AreEqual(expected.Y, actual.Y, delta);
			Assert.AreEqual(expected.Z, actual.Z, delta);
		}

		public static void AreEqual(this Assert assert, BoundingSphere expected, BoundingSphere actual)
		{
			Assert.AreEqual(expected.Radius, actual.Radius);
			Assert.That.AreEqual(expected.Center, actual.Center);
		}

		public static void AreEqual(this Assert assert, Color4 expected, Color4 actual)
		{
			Assert.AreEqual(expected.A, actual.A);
			Assert.AreEqual(expected.R, actual.R);
			Assert.AreEqual(expected.G, actual.G);
			Assert.AreEqual(expected.B, actual.B);
		}

		public static void AreEqual(this Assert assert, IntRectangle expected, IntRectangle actual)
		{
			Assert.That.AreEqual(expected.A, actual.A);
			Assert.That.AreEqual(expected.B, actual.B);
		}

		public static void AreEqual(this Assert assert, Rectangle expected, Rectangle actual)
		{
			Assert.AreEqual(expected.AX, actual.AX);
			Assert.AreEqual(expected.AY, actual.AY);
			Assert.AreEqual(expected.BX, actual.BX);
			Assert.AreEqual(expected.BY, actual.BY);
		}

		public static void AreEqual(this Assert assert, WindowRect expected, WindowRect actual)
		{
			Assert.AreEqual(expected.X, actual.X);
			Assert.AreEqual(expected.Y, actual.Y);
			Assert.AreEqual(expected.Width, actual.Width);
			Assert.AreEqual(expected.Height, actual.Height);
		}
	}
}
