using Yuzu;

namespace Lime
{
	[TangerineRegisterNode(Order = 21)]
	[TangerineMenuPath("3D/Camera 3D")]
	[TangerineVisualHintGroup("/All/Nodes/3D")]
	public class Camera3D : Node3D
	{
		private CameraProjectionMode projectionMode = CameraProjectionMode.Perspective;
		private float fieldOfView;
		private float orthographicSize;
		private float aspectRatio;
		private float nearClipPlane;
		private float farClipPlane;
		private Matrix44 projection;
		private bool projectionDirty;

		[YuzuMember]
		[TangerineKeyframeColor(1)]
		public float FieldOfView
		{
			get { return fieldOfView; }
			set
			{
				if (fieldOfView != value) {
					fieldOfView = value;
					projectionDirty = true;
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(2)]
		public float AspectRatio
		{
			get { return aspectRatio; }
			set
			{
				if (aspectRatio != value) {
					aspectRatio = value;
					projectionDirty = true;
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(3)]
		public float NearClipPlane
		{
			get { return nearClipPlane; }
			set
			{
				if (nearClipPlane != value) {
					nearClipPlane = value;
					projectionDirty = true;
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(4)]
		public float FarClipPlane
		{
			get { return farClipPlane; }
			set
			{
				if (farClipPlane != value) {
					farClipPlane = value;
					projectionDirty = true;
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(5)]
		public float OrthographicSize
		{
			get { return orthographicSize; }
			set
			{
				if (orthographicSize != value) {
					orthographicSize = value;
					projectionDirty = true;
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(6)]
		public CameraProjectionMode ProjectionMode
		{
			get { return projectionMode; }
			set
			{
				if (projectionMode != value) {
					projectionMode = value;
					projectionDirty = true;
				}
			}
		}

		public Matrix44 ViewProjection
		{
			get { return View * Projection; }
		}

		public Matrix44 View
		{
			get { return GlobalTransformInverse; }
		}

		public Matrix44 Projection
		{
			get
			{
				if (projectionDirty) {
					projectionDirty = false;
					if (projectionMode == CameraProjectionMode.Perspective) {
						projection = Matrix44.CreatePerspectiveFieldOfView(
							fieldOfView / aspectRatio,
							aspectRatio,
							nearClipPlane,
							farClipPlane
						);
					} else {
						projection = Matrix44.CreateOrthographic(
							orthographicSize * aspectRatio,
							orthographicSize,
							nearClipPlane,
							farClipPlane
						);
					}
				}
				return projection;
			}
		}
	}

	public enum CameraProjectionMode
	{
		Orthographic,
		Perspective,
	}
}
