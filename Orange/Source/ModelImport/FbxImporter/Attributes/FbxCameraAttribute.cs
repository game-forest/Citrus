using System;
using System.Runtime.InteropServices;
using Lime;

namespace Orange.FbxImporter
{
	public class FbxCameraAttribute : FbxNodeAttribute
	{
		public override FbxNodeType Type { get; } = FbxNodeType.Camera;

		public string Name { get; }

		public float FieldOfView { get; }

		public float AspectRatio { get; }

		public float NearClipPlane { get; }

		public float FarClipPlane { get; }

		public float OrthoZoom { get; }

		public CameraProjectionMode ProjectionMode { get; }

		public FbxCameraAttribute(IntPtr ptr) : base(ptr)
		{
			var native = FbxNodeGetCameraAttribute(ptr);
			if (native == IntPtr.Zero) {
				throw new FbxAtributeImportException(Type);
			}
			var cam = native.ToStruct<Camera>();
			Name = cam.name;
			FieldOfView = cam.FieldOfView;
			AspectRatio = cam.AspectRatio;
			NearClipPlane = cam.ClipPlaneNear;
			FarClipPlane = cam.ClipPlaneFar;
			OrthoZoom = cam.OrthoZoom;
			ProjectionMode = cam.ProjectionMode;
		}

		[DllImport(ImportConfig.LibName, CallingConvention = CallingConvention.Cdecl)]
		private static extern IntPtr FbxNodeGetCameraAttribute(IntPtr node);

		[StructLayout(LayoutKind.Sequential, CharSet = ImportConfig.Charset)]
		private class Camera
		{
			public string name;

			[MarshalAs(UnmanagedType.R4)]
			public float FieldOfView;

			[MarshalAs(UnmanagedType.R4)]
			public float ClipPlaneFar;

			[MarshalAs(UnmanagedType.R4)]
			public float ClipPlaneNear;

			[MarshalAs(UnmanagedType.R4)]
			public float AspectRatio;

			[MarshalAs(UnmanagedType.R4)]
			public float OrthoZoom;

			public CameraProjectionMode ProjectionMode;
		}
	}
}
