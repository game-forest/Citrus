using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Yuzu;

namespace Lime
{
	/// <summary>
	/// Parent-relative layout.
	/// </summary>
	[Flags]
	public enum Anchors
	{
		None,
		Left = 1,
		Right = 2,
		Top = 4,
		Bottom = 8,
		CenterH = 16,
		CenterV = 32,
		LeftRight = Left | Right,
		TopBottom = Top | Bottom,
		LeftRightTopBottom = Left | Right | Top | Bottom,
		Center = CenterH | CenterV,
	}

	public enum HitTestMethod
	{
		BoundingRect,
		Contents,
		Skip
	}

	/// <summary>
	/// Base class for any rendered object.
	/// </summary>
	[DebuggerTypeProxy(typeof(WidgetDebugView))]
	[TangerineVisualHintGroup("/All/Nodes/Containers")]
	public class Widget : Node
	{
		private Vector2 position;
		private Vector2 size;
		private float rotation;
		private Vector2 direction;
		private Color4 color;
		private bool enabled = true;
		private bool visible;
		private bool freezeInvisible = true;
		private Blending blending;
		private ShaderId shader;
		private Vector2 pivot;
		private Vector2 scale;
		private WidgetInput input;
		/// <summary>
		/// <code>
		/// Transition matrix from the local basis to the parent basis:
		/// MT(-pivot * size) * MS(scale) * MR(rotation) * MT(position) * M(bones)
		/// , where:
		/// MT(-pivot * size) - translation matrix of the pivot point,
		/// MS(scale) - scaling matrix of local transformation,
		/// MR(rotation) - rotation matrix of local transformation,
		/// MT(position) - translation matrix of local transformation,
		/// M(bones) - weighted transition matrix from the local basis of connected bones to the parent basis.
		/// </code>
		/// </summary>
		private Matrix32 localToParentTransform;
		private Matrix32 localToWorldTransform;
		private Rectangle boundingRect;
		protected Color4 globalColor;
		protected Blending globalBlending;
		protected ShaderId globalShader;
		protected bool globallyEnabled;
		protected bool globallyVisible;

		public static Widget Focused { get; private set; }
		public static readonly Vector2 DefaultWidgetSize = new Vector2(100);
		public static bool RenderTransparentWidgets;
		public static bool EnableViewCulling = true;

		#region Layout properties
		private LayoutManager layoutManager;
		public LayoutManager LayoutManager
		{
			get
			{
				if (CleanDirtyFlags(DirtyFlags.LayoutManager)) {
					if (ParentWidget != null) {
						layoutManager = ParentWidget.LayoutManager;
					}
				}
				return layoutManager;
			}
			set
			{
				if (layoutManager != value) {
					layoutManager = value;
					PropagateDirtyFlags(DirtyFlags.LayoutManager);
					InvalidateConstraintsAndArrangementForSelfAndDescendants();
				}
			}
		}

		public ILayout Layout
		{
			get => Components.Get<Layout>() ?? AnchorLayout.Instance;
			set
			{
				var layoutComponent = Components.Get<Layout>();
				bool isAnchorLayout = value is AnchorLayout || value == null;
				if (layoutComponent == null) {
					if (isAnchorLayout) {
						return;
					}
					Components.Add((Layout)value);
				} else if (isAnchorLayout) {
					Components.Remove(layoutComponent);
					return;
				} else {
					Components.Remove(layoutComponent);
					Components.Add((Layout)value);
				}
			}
		}

		/// <summary>
		/// Gets the layout-specific data.
		/// </summary>
		public LayoutCell LayoutCell
		{
			get => Components.Get<LayoutCell>();
			set => Components.Replace(value);
		}

		internal MeasuredSize MeasuredSize
		{
			get => Components.GetOrAdd<MeasuredSize>();
			set => Components.Replace(value);
		}

		public LayoutConstraints LayoutConstraints
		{
			get => Components.GetOrAdd<LayoutConstraints>();
			set => Components.Replace(value);
		}

		public Vector2 MeasuredMinSize
		{
			get => MeasuredSize.MeasuredMinSize;
			set => MeasuredSize.MeasuredMinSize = value;
		}

		public Vector2 MeasuredMaxSize
		{
			get => MeasuredSize.MeasuredMaxSize;
			set => MeasuredSize.MeasuredMaxSize = value;
		}

		public virtual Vector2 EffectiveMinSize => Vector2.Max(LayoutConstraints.MinSize, MeasuredSize.MeasuredMinSize);

		public virtual Vector2 EffectiveMaxSize => Vector2.Max(Vector2.Min(LayoutConstraints.MaxSize, MeasuredSize.MeasuredMaxSize), EffectiveMinSize);

		public Vector2 MinSize
		{
			get => LayoutConstraints.MinSize;
			set => LayoutConstraints.MinSize = value;
		}

		public Vector2 MaxSize
		{
			get => LayoutConstraints.MaxSize;
			set => LayoutConstraints.MaxSize = value;
		}

		public float MinWidth
		{
			get { return LayoutConstraints.MinSize.X; }
			set { LayoutConstraints.MinSize = new Vector2(value, LayoutConstraints.MinSize.Y); }
		}

		public float MinHeight
		{
			get { return LayoutConstraints.MinSize.Y; }
			set { LayoutConstraints.MinSize = new Vector2(LayoutConstraints.MinSize.X, value); }
		}

		public float MaxWidth
		{
			get { return LayoutConstraints.MaxSize.X; }
			set { LayoutConstraints.MaxSize = new Vector2(value, LayoutConstraints.MaxSize.Y); }
		}

		public float MaxHeight
		{
			get { return LayoutConstraints.MaxSize.Y; }
			set { LayoutConstraints.MaxSize = new Vector2(LayoutConstraints.MaxSize.X, value); }
		}

		public Vector2 MinMaxSize
		{
			set { LayoutConstraints.MinSize = LayoutConstraints.MaxSize = value; }
		}

		public float MinMaxWidth
		{
			set { MinWidth = MaxWidth = value; }
		}

		public float MinMaxHeight
		{
			set { MinHeight = MaxHeight = value; }
		}

#endregion

#region Transformation properties
		/// <summary>
		/// Parent-relative position.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(4)]
		[TangerineNumericEditBoxStep(1f)]
#if TANGERINE
		[TangerineIgnoreIf(nameof(ShouldInspectPosition))]
#endif // TANGERINE
		public Vector2 Position
		{
			get { return position; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value.X));
				System.Diagnostics.Debug.Assert(IsNumber(value.Y));
				if (position.X != value.X || position.Y != value.Y) {
					position = value;
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		/// <summary>
		/// Parent-relative X position.
		/// </summary>
		public float X
		{
			get { return position.X; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value));
				if (position.X != value) {
					position.X = value;
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		/// <summary>
		/// Parent-relative Y position.
		/// </summary>
		public float Y
		{
			get { return position.Y; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value));
				if (position.Y != value) {
					position.Y = value;
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(5)]
		public Vector2 Scale
		{
			get { return scale; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value.X));
				System.Diagnostics.Debug.Assert(IsNumber(value.Y));
				if (scale.X != value.X || scale.Y != value.Y) {
					scale = value;
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		/// <summary>
		/// Counter-clockwise rotation of this widget.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(3)]
		[TangerineNumericEditBoxStep(1f)]
		public float Rotation {
			get { return rotation; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value));
				if (rotation != value) {
					rotation = value;
					// For the tangerine we need a maximally accurate math,
					// to prevent flicker of widgets position, rotation and scale
					// on "click on pivot".
#if TANGERINE
					direction = Vector2.CosSin(Mathf.DegToRad * value);
#else
					direction = Vector2.CosSinRough(Mathf.DegToRad * value);
#endif // TANGERINE
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		[TangerineKeyframeColor(7)]
		[TangerineNumericEditBoxStep(1f)]
		public Vector2 Size
		{
			get { return size; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value.X));
				System.Diagnostics.Debug.Assert(IsNumber(value.Y));
				if (value.X != size.X || value.Y != size.Y) {
					var sizeDelta = value - size;
					size = value;
					var t = false;
					if (boundingRect.BX < value.X) { boundingRect.BX = value.X; t = true; }
					if (boundingRect.BY < value.Y) { boundingRect.BY = value.Y; t = true; }
					if (boundingRect.AX > value.X) { boundingRect.AX = value.X; t = true; }
					if (boundingRect.AY > value.Y) { boundingRect.AY = value.Y; t = true; }
					if (t) {
						PropagateParentBoundsChanged();
					}
					OnSizeChanged(sizeDelta);
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		/// <summary>
		/// SilentSize is needed to prevent unwanted propagation of `OnSizeChanged`
		/// while deserializing with Yuzu.
		/// </summary>
		[YuzuMember("Size")]
		[TangerineIgnore]
		public Vector2 SilentSize
		{
			get { return size; }
			set
			{
				size = value;
				DirtyMask |= DirtyFlags.LocalTransform;
				var t = false;
				if (boundingRect.BX < value.X) { boundingRect.BX = value.X; t = true; }
				if (boundingRect.BY < value.Y) { boundingRect.BY = value.Y; t = true; }
				if (boundingRect.AX > value.X) { boundingRect.AX = value.X; t = true; }
				if (boundingRect.AY > value.Y) { boundingRect.AY = value.Y; t = true; }
				if (t) {
					PropagateParentBoundsChanged();
				}
			}
		}

		public float Width
		{
			get { return size.X; }
			set
			{
				if (size.X != value) {
					Size = new Vector2(value, Height);
				}
			}
		}

		public float Height
		{
			get { return size.Y; }
			set
			{
				if (size.Y != value) {
					Size = new Vector2(Width, value);
				}
			}
		}

		/// <summary>
		/// Center point of rotation and scaling.
		/// (0, 0) is top-left corner, (1, 1) is bottom-right corner.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(6)]
		[TangerinePropertyDefaultValue(typeof(Widget), nameof(GetPivotDefaultValue))]
		public Vector2 Pivot
		{
			get { return pivot; }
			set
			{
				System.Diagnostics.Debug.Assert(IsNumber(value.X));
				System.Diagnostics.Debug.Assert(IsNumber(value.Y));
				if (pivot.X != value.X || pivot.Y != value.Y) {
					pivot = value;
					DirtyMask |= DirtyFlags.LocalTransform;
					PropagateParentBoundsChanged();
					PropagateDirtyFlags(DirtyFlags.GlobalTransform | DirtyFlags.GlobalTransformInverse);
				}
			}
		}

		public static object GetPivotDefaultValue() => Vector2.Half;

		/// <summary>
		/// Gets or sets the widget padding. Padding defines the white space between the widget content and the widget border.
		/// The widget presenter and layout should respect the padding.
		/// </summary>
		[YuzuMember]
		public Thickness Padding
		{
			get => padding;
			set
			{
				if (padding != value) {
					padding = value;
					Layout.InvalidateConstraintsAndArrangement();
				}
			}
		}

		private Thickness padding;

		public Vector2 ContentPosition => new Vector2(Padding.Left, Padding.Top);
		public Vector2 ContentSize => new Vector2(Size.X - Padding.Left - Padding.Right, Size.Y - Padding.Top - Padding.Bottom);

		public float ContentWidth => Size.X - Padding.Left - Padding.Right;
		public float ContentHeight => Size.Y - Padding.Top - Padding.Bottom;

		/// <summary>
		/// Gets position of this widget in the root widget space.
		/// </summary>
		public Vector2 GlobalPosition => LocalToWorldTransform.T;

		public Vector2 GlobalPivotPosition => LocalToWorldTransform * (Pivot * Size);
		/// <summary>
		/// Gets position of this widget's center in the root widget space.
		/// </summary>
		public Vector2 GlobalCenter => LocalToWorldTransform * (Size / 2);

		/// <summary>
		/// Parent-relative position of center of this widget.
		/// </summary>
		public Vector2 Center => Position + (Vector2.Half - Pivot) * Size;
#endregion

#region Misc properties
		public Widget ParentWidget => Parent?.AsWidget;
		public TabTraversable TabTravesable { get; set; }
		public KeyboardFocusScope FocusScope { get; set; }

		/// <summary>
		/// Hue of this widget. Contents color will be multiplied by it on render.
		/// </summary>
		[YuzuMember]
		[TangerineKeyframeColor(9)]
		[YuzuSerializeIf(nameof(IsNotDecorated))]
		public Color4 Color
		{
			get { return color; }
			set
			{
				if (color.ABGR != value.ABGR) {
					var visibilityChanged = (color.A == 0) != (value.A == 0);
					color = value;
					if (visibilityChanged) {
						PropagateDirtyFlags(DirtyFlags.Color | DirtyFlags.Visible | DirtyFlags.Frozen);
						Manager?.FilterNode(this);
					} else {
						PropagateDirtyFlags(DirtyFlags.Color);
					}
				}
			}
		}

		/// <summary>
		/// 0 - fully transparent. 1 - fully opaque.
		/// </summary>
		public float Opacity
		{
			get { return color.A * (1 / 255f); }
			set
			{
				var newColor = Color;
				newColor.A = (byte)(value * 255f);
				Color = newColor;
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(17)]
		public Anchors Anchors { get; set; }

		[YuzuMember]
		[TangerineKeyframeColor(22)]
		public Blending Blending
		{
			get => blending;
			set
			{
				if (blending != value) {
					blending = value;
					PropagateDirtyFlags(DirtyFlags.Blending | DirtyFlags.Material);
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(18)]
		public ShaderId Shader
		{
			get => shader;
			set
			{
				if (shader != value) {
					shader = value;
					PropagateDirtyFlags(DirtyFlags.Shader | DirtyFlags.Material);
				}
			}
		}

		public virtual bool Enabled
		{
			get => enabled;
			set
			{
				if (enabled != value) {
					enabled = value;
					PropagateDirtyFlags(DirtyFlags.Enabled);
				}
			}
		}

		[YuzuMember]
		[TangerineKeyframeColor(19)]
		public bool Visible
		{
			get { return visible; }
			set
			{
				if (visible != value) {
					visible = value;
					if (visible && DirtyMask.HasFlag(DirtyFlags.ParentBoundingRect)) {
						PropagateParentBoundsChanged();
					}
					PropagateDirtyFlags(DirtyFlags.Visible | DirtyFlags.Frozen);
					InvalidateParentConstraintsAndArrangement();
					Manager?.FilterNode(this);
				}
			}
		}

		[YuzuMember]
		[TangerineStaticProperty]
		public SkinningWeights SkinningWeights { get; set; }

		public BoneArray BoneArray;

		[YuzuMember]
		public HitTestMethod HitTestMethod { get; set; }

		/// <summary>
		/// Get or sets a label upon the widget. For widgets which can not have a label returns null.
		/// </summary>
		public virtual string Text
		{
			get { return null; }
			set { }
		}

		/// <summary>
		/// Get or sets a texture upon the widget. For widgets which can not have a texture returns null.
		/// </summary>
		public virtual ITexture Texture
		{
			get { return null; }
			set { }
		}

		public WidgetInput Input => input ?? (input = new WidgetInput(this));

		public bool HasInput() => input != null;

		/// <summary>
		/// Get the matrix represents transformation from this widget space into the root widget space.
		/// </summary>
		public Matrix32 LocalToWorldTransform
		{
			get
			{
				if (CleanDirtyFlags(DirtyFlags.GlobalTransform)) {
					RecalcGlobalTransform();
				}
				return localToWorldTransform;
			}
		}

		private void RecalcGlobalTransform()
		{
			var localToParentTransform = CalcLocalToParentTransform();
			var parentWidget = Parent?.AsWidget;
			if (parentWidget == null) {
				localToWorldTransform = localToParentTransform;
			} else {
				localToWorldTransform = localToParentTransform * parentWidget.LocalToWorldTransform;
			}
		}

		/// <summary>
		/// Gets the widget's effective color.
		/// </summary>
		public Color4 GlobalColor
		{
			get
			{
				if (CleanDirtyFlags(DirtyFlags.Color)) {
					RecalcGlobalColor();
				}
				return globalColor;
			}
		}

		private void RecalcGlobalColor()
		{
			globalColor = color;
			if (!IsRenderedToTexture() && Parent != null) {
				if (Parent.AsWidget != null) {
					globalColor = color * Parent.AsWidget.GlobalColor;
				} else if (Parent.AsNode3D != null) {
					globalColor = color * Parent.AsNode3D.GlobalColor;
				}
			}
		}

		/// <summary>
		/// Gets the widget's effective blending.
		/// </summary>
		public Blending GlobalBlending
		{
			get
			{
				if (CleanDirtyFlags(DirtyFlags.Blending)) {
					RecalcGlobalBlending();
				}
				return globalBlending;
			}
		}

		private void RecalcGlobalBlending()
		{
			if (IsRenderedToTexture()) {
				globalBlending = Blending.Inherited;
			} else if (Blending == Blending.Inherited && ParentWidget != null) {
				globalBlending = ParentWidget.GlobalBlending;
			} else {
				globalBlending = Blending;
			}
		}

		/// <summary>
		/// Gets the widget's effective shader.
		/// </summary>
		public ShaderId GlobalShader
		{
			get
			{
				if (CleanDirtyFlags(DirtyFlags.Shader)) {
					RecalcGlobalShader();
				}
				return globalShader;
			}
		}

		private void RecalcGlobalShader()
		{
			if (IsRenderedToTexture()) {
				globalShader = ShaderId.Inherited;
			} else if (Shader == ShaderId.Inherited && ParentWidget != null) {
				globalShader = ParentWidget.GlobalShader;
			} else {
				globalShader = Shader;
			}
		}

		/// <summary>
		/// Indicates whether the widget is actually enabled.
		/// </summary>
		public bool GloballyEnabled
		{
			get
			{
				if ((DirtyMask & DirtyFlags.Enabled) != 0) {
					DirtyMask &= ~DirtyFlags.Enabled;
					RecalcGloballyEnabled();
				}
				return globallyEnabled;
			}
		}

		private void RecalcGloballyEnabled()
		{
			globallyEnabled = Enabled;
			if (Parent?.AsWidget != null) {
				globallyEnabled &= Parent.AsWidget.GloballyEnabled;
			}
		}

		/// <summary>
		/// Indicates whether the widget is actually visible.
		/// </summary>
		public bool GloballyVisible
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get
			{
				if ((DirtyMask & DirtyFlags.Visible) != 0) {
					DirtyMask &= ~DirtyFlags.Visible;
					RecalcGloballyVisible();
				}
				return globallyVisible;
			}
		}

		private void RecalcGloballyVisible()
		{
			globallyVisible = Visible && (color.A != 0 || RenderTransparentWidgets);
			if (!IsRenderedToTexture() && Parent != null) {
				if (Parent.AsWidget != null) {
					globallyVisible &= Parent.AsWidget.GloballyVisible;
				} else if (Parent.AsNode3D != null) {
					globallyVisible &= Parent.AsNode3D.GloballyVisible;
				}
			}
#if TANGERINE
			globallyVisible &= !GetTangerineFlag(TangerineFlags.Hidden);
			globallyVisible |= GetTangerineFlag(TangerineFlags.Shown | TangerineFlags.DisplayContent);
			globallyVisible &= !GetTangerineFlag(TangerineFlags.HiddenOnExposition);
#endif // TANGERINE
		}

		[YuzuMember]
		[TangerineKeyframeColor(19)]
		public bool FreezeInvisible
		{
			get => freezeInvisible;
			set
			{
				if (freezeInvisible != value) {
					freezeInvisible = value;
					PropagateDirtyFlags(DirtyFlags.Frozen);
					Manager?.FilterNode(this);
				}
			}
		}

		// Temporary property for changing bounding rectangle in game code
		public Rectangle BoundingRect
		{
			get
			{
				if (CleanDirtyFlags(DirtyFlags.BoundingRect)) {
					RecalcBoundingRect();
				}
				return boundingRect;
			}
			set
			{
				boundingRect = value;
			}
		}

		/// <summary>
		/// Gets the axis-aligned bounding rectangle based on LocalToWorldTransform.
		/// </summary>
		public Rectangle CalcGlobalBoundingRect()
		{
			if (CleanDirtyFlags(DirtyFlags.GlobalTransform)) {
				RecalcGlobalTransform();
			}
			// The code below is optimized version of:
			// var v1 = localToWorldTransform.TransformVector(boundingRect.AX, boundingRect.AY);
			// var v2 = localToWorldTransform.TransformVector(boundingRect.BX, boundingRect.AY);
			// var v3 = localToWorldTransform.TransformVector(boundingRect.AX, boundingRect.BY);
			// var v4 = v2 + v3 - v1;
			var tux = localToWorldTransform.UX;
			var tuy = localToWorldTransform.UY;
			var tvx = localToWorldTransform.VX;
			var tvy = localToWorldTransform.VY;
			var ttx = localToWorldTransform.TX;
			var tty = localToWorldTransform.TY;

			var bounds = BoundingRect;
			var axux = bounds.AX * tux;
			var axuy = bounds.AX * tuy;
			var ayvx = bounds.AY * tvx + ttx;
			var ayvy = bounds.AY * tvy + tty;
			float v1x, v1y, v2x, v2y, v3x, v3y, v4x, v4y;
			v1x = axux + ayvx;
			v1y = axuy + ayvy;
			v2x = bounds.BX * tux + ayvx;
			v2y = bounds.BX * tuy + ayvy;
			v3x = axux + bounds.BY * tvx + ttx;
			v3y = axuy + bounds.BY * tvy + tty;
			v4x = v2x + v3x - v1x;
			v4y = v2y + v3y - v1y;
			// Now build an aabb.
			var r = new Rectangle(v1x, v1y, v1x, v1y);
			if (v2x < r.AX) r.AX = v2x;
			if (v2x > r.BX) r.BX = v2x;
			if (v2y < r.AY) r.AY = v2y;
			if (v2y > r.BY) r.BY = v2y;
			if (v3x < r.AX) r.AX = v3x;
			if (v3x > r.BX) r.BX = v3x;
			if (v3y < r.AY) r.AY = v3y;
			if (v3y > r.BY) r.BY = v3y;
			if (v4x < r.AX) r.AX = v4x;
			if (v4x > r.BX) r.BX = v4x;
			if (v4y < r.AY) r.AY = v4y;
			if (v4y > r.BY) r.BY = v4y;
			return r;
		}

#endregion

		public Widget()
		{
			AsWidget = this;
			Size = DefaultWidgetSize;
			Color = Color4.White;
			Scale = Vector2.One;
			Visible = true;
			Blending = Blending.Inherited;
			Shader = ShaderId.Inherited;
			direction = new Vector2(1, 0);
		}

		/// <summary>
		/// Stops all tasks and calls Dispose of all descendants.
		/// </summary>
		public override void Dispose()
		{
			input?.Dispose();
			base.Dispose();
		}

		internal protected virtual bool IsRenderedToTexture() => false;

		public virtual Action Clicked
		{
			get { return DefaultClickGesture(false)?.InternalRecognized; }
			set { DefaultClickGesture(true).InternalRecognized = value; }
		}

		private ClickGesture DefaultClickGesture(bool createIfNotExists)
		{
			foreach (var g in Gestures) {
				var cg = g as ClickGesture;
				if (cg != null && cg.ButtonIndex == 0) {
					return cg;
				}
			}
			if (createIfNotExists) {
				var g = new ClickGesture();
				Gestures.Add(g);
				return g;
			}
			return null;
		}

		public virtual bool WasClicked()
		{
			return DefaultClickGesture(true)?.WasRecognized() ?? false;
		}

		private static bool IsNumber(float x)
		{
			return !float.IsNaN(x) && !float.IsInfinity(x);
		}

		public void RefreshLayout()
		{
			OnSizeChanged(Vector2.Zero);
		}

		protected virtual void OnSizeChanged(Vector2 sizeDelta)
		{
			Layout.OnSizeChanged(this, sizeDelta);
		}

		public bool IsFocused() { return Focused == this; }
		public void SetFocus() { SetFocus(this); }
		public void RevokeFocus()
		{
			if (IsFocused()) {
				var scope = KeyboardFocusScope.GetEnclosingScope(this);
				SetFocus(scope?.Widget);
			}
		}

		internal static void SetFocus(Widget value)
		{
			// Grisha: invoke on main thread to make it possible to focus widgets not from main thread
			Application.InvokeOnMainThread(() => {
				var p = value?.Components.Get<EditorParams>();
				if (p != null) {
					Application.SoftKeyboard.Show(true, p.SoftKeyboardType);
				} else {
					Application.SoftKeyboard.Show(false, SoftKeyboardType.Default);
				}
			});

			// Perform this check after SoftKeyboard.Show() because we need to be able to show SoftKeyboard if
			// focused widget hasn't been changed but the keyboard has been hidden by OS.
			if (Focused == value) {
				return;
			}
			Focused = value;
			Application.InvalidateWindows();
		}

		internal void InvalidateParentConstraintsAndArrangement()
		{
			if (ParentWidget != null) {
				ParentWidget.Layout.InvalidateConstraintsAndArrangement();
			}
		}

		public virtual Vector2 CalcContentSize()
		{
			return Size;
		}

		/// <summary>
		/// Searches for widget with provided path or id in this widget's descendants.
		/// Throws an exception if sought-for widget doesn't exist.
		/// <para>This function is thread safe.</para>
		/// </summary>
		/// <param name="path">Id or path of widget. Path can be incomplete
		/// (i.e. for path Root/Human/Head/Eye Human or Head can be ommited).</param>
		public Widget this[string path]
		{
			get { return Find<Widget>(path); }
		}

		/// <summary>
		/// Searches for widget with provided path or id in this widget's descendants.
		/// Throws an exception if sought-for widget doesn't exist.
		/// <para>This function is thread safe.</para>
		/// </summary>
		/// <param name="format">Id or path of widget. Path can be incomplete
		/// (i.e. for path Root/Human/Head/Eye Human or Head can be ommited).</param>
		public Widget this[string format, params object[] arg]
		{
			get { return Find<Widget>(string.Format(format, arg)); }
		}

		protected override void OnParentChanged(Node oldParent)
		{
			base.OnParentChanged(oldParent);
			if (oldParent != null && oldParent.AsWidget != null) {
				var w = oldParent.AsWidget;
				if (w.LayoutManager != null) {
					w.Layout.InvalidateConstraintsAndArrangement();
				}
			}
			if (Parent?.AsWidget == null) {
				LayoutManager = null;
			} else if (LayoutManager != null) {
				InvalidateParentConstraintsAndArrangement();
				InvalidateConstraintsAndArrangementForSelfAndDescendants();
			}
		}

		private void InvalidateConstraintsAndArrangementForSelfAndDescendants()
		{
			Layout.InvalidateConstraintsAndArrangement();
			foreach (var n in Descendants) {
				var w = n as Widget;
				w?.Layout.InvalidateConstraintsAndArrangement();
			}
		}

		/// <summary>
		/// Raises Updating event in respect of animation speed.
		/// </summary>
		/// <param name="delta">Time delta from last Update.</param>
		public void RaiseUpdating(float delta)
		{
			if (Updating != null) {
				Updating(delta * AnimationSpeed);
			}
		}

		/// <summary>
		/// Raises Updated event in respect of animation speed.
		/// </summary>
		/// <param name="delta">Time delta from last Update.</param>
		public void RaiseUpdated(float delta)
		{
			if (Updated != null) {
				Updated(delta * AnimationSpeed);
			}
		}

		public bool ClipRegionTest(Rectangle clipRegion)
		{
			if (!EnableViewCulling) {
				return true;
			}
			var r = CalcGlobalBoundingRect();
			return
				r.BX >= clipRegion.AX && r.BY >= clipRegion.AY &&
				r.AX <= clipRegion.BX && r.AY <= clipRegion.BY;
		}

		public Matrix32 CalcLocalToParentTransform()
		{
			if (CleanDirtyFlags(DirtyFlags.LocalTransform)) {
				RecalcLocalToParentTransform();
			}
			return localToParentTransform;
		}

		private void RecalcLocalToParentTransform()
		{
			// WARNING: Synchronize this code with ComplexTransformationsHelper.CalcLocalToParentTransformDouble
			// after changes.
			// This is an optimized version (after profiling) of the code:
			// localToParentTransform =
			// 	Matrix32.Translation(-Pivot * Size) *
			// 				Matrix32.Transformation(
			// 		Vector2.Zero,
			// 		Scale,
			// 		Rotation * Mathf.DegToRad,
			// 		Position
			// 	);
			// if (SkinningWeights != null && Parent?.AsWidget != null) {
			// 	localToParentTransform = localToParentTransform *
			// 		Parent.AsWidget.BoneArray.CalcWeightedRelativeTransform(SkinningWeights);
			// }

			var centerX = size.X * pivot.X;
			var centerY = size.Y * pivot.Y;
			if (rotation == 0 && SkinningWeights == null) {
				localToParentTransform.UX = scale.X;
				localToParentTransform.UY = 0;
				localToParentTransform.VX = 0;
				localToParentTransform.VY = scale.Y;
				localToParentTransform.TX = position.X - centerX * scale.X;
				localToParentTransform.TY = position.Y - centerY * scale.Y;
				return;
			}
			Vector2 u, v;
			var translation = position;
			u.X = direction.X * scale.X;
			u.Y = direction.Y * scale.X;
			v.X = -direction.Y * scale.Y;
			v.Y = direction.X * scale.Y;
			if (SkinningWeights != null && Parent?.AsWidget != null) {
				var a = Parent.AsWidget.BoneArray;
				translation = a.ApplySkinningToVector(position, SkinningWeights);
				u = a.ApplySkinningToVector(u + position, SkinningWeights) - translation;
				v = a.ApplySkinningToVector(v + position, SkinningWeights) - translation;
			}
			localToParentTransform.U = u;
			localToParentTransform.V = v;
			localToParentTransform.TX = -(centerX * u.X) - centerY * v.X + translation.X;
			localToParentTransform.TY = -(centerX * u.Y) - centerY * v.Y + translation.Y;
		}

		internal void ExpandBoundingRect(Rectangle newBounds, bool propagate = true)
		{
			var t = false;
			if (boundingRect.AX > newBounds.AX) { boundingRect.AX = newBounds.AX; t = true; }
			if (boundingRect.AY > newBounds.AY) { boundingRect.AY = newBounds.AY; t = true; }
			if (boundingRect.BX < newBounds.BX) { boundingRect.BX = newBounds.BX; t = true; }
			if (boundingRect.BY < newBounds.BY) { boundingRect.BY = newBounds.BY; t = true; }
			if (propagate && t) {
				PropagateParentBoundsChanged();
			}
		}

		internal void ExpandBoundingRect(Vector2 point, bool propagate = true)
		{
			var t = false;
			if (boundingRect.AX > point.X) { boundingRect.AX = point.X; t = true; }
			if (boundingRect.AY > point.Y) { boundingRect.AY = point.Y; t = true; }
			if (boundingRect.BX < point.X) { boundingRect.BX = point.X; t = true; }
			if (boundingRect.BY < point.Y) { boundingRect.BY = point.Y; t = true; }
			if (propagate && t) {
				PropagateParentBoundsChanged();
			}
		}

		private void ExpandAncestorBoundingRect(Widget ancestor)
		{
			Matrix32 transition;
			if (ancestor == Parent) {
				if (CleanDirtyFlags(DirtyFlags.LocalTransform)) {
					RecalcLocalToParentTransform();
				}
				transition = localToParentTransform;
			} else {
				transition = CalcTransitionToSpaceOf(ancestor);
			}

			var ownBounds = BoundingRect;
			var v1 = transition.TransformVector(ownBounds.AX, ownBounds.AY);
			var v2 = transition.TransformVector(ownBounds.BX, ownBounds.AY);
			var v3 = transition.TransformVector(ownBounds.AX, ownBounds.BY);
			var v4 = v2 + v3 - v1;
			ref var r = ref ancestor.boundingRect;
			if (v1.X < r.AX) { r.AX = v1.X; }
			if (v1.X > r.BX) { r.BX = v1.X; }
			if (v1.Y < r.AY) { r.AY = v1.Y; }
			if (v1.Y > r.BY) { r.BY = v1.Y; }
			if (v2.X < r.AX) { r.AX = v2.X; }
			if (v2.X > r.BX) { r.BX = v2.X; }
			if (v2.Y < r.AY) { r.AY = v2.Y; }
			if (v2.Y > r.BY) { r.BY = v2.Y; }
			if (v3.X < r.AX) { r.AX = v3.X; }
			if (v3.X > r.BX) { r.BX = v3.X; }
			if (v3.Y < r.AY) { r.AY = v3.Y; }
			if (v3.Y > r.BY) { r.BY = v3.Y; }
			if (v4.X < r.AX) { r.AX = v4.X; }
			if (v4.X > r.BX) { r.BX = v4.X; }
			if (v4.Y < r.AY) { r.AY = v4.Y; }
			if (v4.Y > r.BY) { r.BY = v4.Y; }
		}

		/// <summary>
		/// TODO: Add summary
		/// </summary>
		public override void StaticScale(float ratio, bool roundCoordinates)
		{
			if (Animators.Count > 0) {
				StaticScaleAnimationKeys(ratio, roundCoordinates);
			}
			StaticScalePositionAndSize(ratio, roundCoordinates);
			base.StaticScale(ratio, roundCoordinates);
		}

		/// <summary>
		/// TODO: Add summary
		/// </summary>
		private void StaticScaleAnimationKeys(float ratio, bool roundCoordinates)
		{
			Animator<Vector2> posAnimator, sizeAnimator;
			if (Animators.TryFind("Position", out posAnimator)) {
				var savedPivot = pivot;
				var savedRotation = rotation;
				var savedScale = scale;
				var savedPosition = position;
				foreach (var k in posAnimator.Keys) {
					var savedSize = size;
					foreach (var a in Animators) {
						a.Apply(AnimationUtils.FramesToSeconds(k.Frame));
					}
					StaticScalePositionAndSize(ratio, roundCoordinates);
					k.Value = position;
					size = savedSize;
				}
				pivot = savedPivot;
				rotation = savedRotation;
				scale = savedScale;
				position = savedPosition;
			}
			Animators.TryFind("Size", out sizeAnimator);
			if (sizeAnimator != null) {
				foreach (var k in sizeAnimator.Keys) {
					k.Value = RoundVectorIf(k.Value * ratio, roundCoordinates);
				}
			}
		}

		private static Vector2 RoundVectorIf(Vector2 v, bool round)
		{
			return round ? new Vector2(v.X.Round(), v.Y.Round()) : v;
		}

		/// <summary>
		/// TODO: Add summary
		/// </summary>
		private void StaticScalePositionAndSize(float ratio, bool round)
		{
			var p1 = CalcLocalToParentTransform() * Vector2.Zero;
			p1 = RoundVectorIf(p1 * ratio, round);
			size = RoundVectorIf(size * ratio, round);
			var p2 = CalcLocalToParentTransform() * Vector2.Zero;
			position += (p1 - p2);
		}

		/// <summary>
		/// Adds widget and all its descendants to render chain.
		/// </summary>
		public override void AddToRenderChain(RenderChain chain)
		{
			if (GloballyVisible) {
				AddSelfAndChildrenToRenderChain(chain, Layer);
			}
		}

		public Vector2 ToLocalMousePosition(Vector2 mousePosition) => mousePosition * LocalToWorldTransform.CalcInversed();
		public Vector2 LocalMousePosition() => Window.Current.Input.MousePosition * LocalToWorldTransform.CalcInversed();

		public int GetEffectiveLayer()
		{
			for (Node node = this; node != null; node = node.Parent) {
				if (node.AsWidget != null && node.AsWidget.Layer != 0) {
					return node.AsWidget.Layer;
				}
			}
			return 0;
		}

		private static RenderChain renderChain = new RenderChain();

		/// <summary>
		/// Performs hit test only for this widget and its descendants. Returns true if the widget or one of its decendants contains the given point.
		/// This method doesn't take in account if one of the widget's ancestors overlaps the widget.
		/// </summary>
		public bool LocalHitTest(ref HitTestArgs args)
		{
			lock (renderChain) {
				var savedHitTestTarget = HitTestTarget;
				try {
					HitTestTarget = true;
					RenderChainBuilder?.AddToRenderChain(renderChain);
					return renderChain.HitTest(ref args);
				} finally {
					renderChain.Clear();
					HitTestTarget = savedHitTestTarget;
				}
			}
		}

		/// <summary>
		/// Checks whether this widget or one of its descendents contains the given point.
		/// This method doesn't take in account if one of the widget's ancestors overlaps the widget.
		/// </summary>
		public bool LocalHitTest(Vector2 point)
		{
			var args = new HitTestArgs(point);
			return LocalHitTest(ref args);
		}

		/// <summary>
		/// Checks whether this widget contains the given point.
		/// </summary>
		internal protected override bool PartialHitTest(ref HitTestArgs args)
		{
			Node targetNode;
			for (targetNode = this; targetNode != null; targetNode = targetNode.Parent) {
				var method = targetNode.AsWidget != null ? targetNode.AsWidget.HitTestMethod : HitTestMethod.Contents;
				if (method == HitTestMethod.Skip || (targetNode != this && method == HitTestMethod.BoundingRect)) {
					return false;
				}
				if (targetNode.HitTestTarget) {
					break;
				}
			}
			if (targetNode == null) {
				return false;
			}
			if (
				HitTestMethod == HitTestMethod.BoundingRect && BoundingRectHitTest(args.Point) ||
				HitTestMethod == HitTestMethod.Contents && PartialHitTestByContents(ref args)
			) {
				args.Node = targetNode;
				return true;
			}
			return false;
		}

		internal protected virtual bool PartialHitTestByContents(ref HitTestArgs args)
		{
			return false;
		}

		public bool BoundingRectHitTest(Vector2 point)
		{
			var position = LocalToWorldTransform.CalcInversed().TransformVector(point);
			var size = Size;
			if (size.X < 0) {
				position.X = -position.X;
				size.X = -size.X;
			}
			if (size.Y < 0) {
				position.Y = -position.Y;
				size.Y = -size.Y;
			}
			return position.X >= 0 && position.Y >= 0 && position.X < size.X && position.Y < size.Y;
		}

		public void CenterOnParent()
		{
			if (Parent == null) {
				throw new Lime.Exception("Parent must not be null");
			}
			Position = Parent.AsWidget.Size * 0.5f;
			Pivot = Vector2.Half;
		}

		/// <summary>
		/// Calculates the widget's transition to the space of another widget.
		/// </summary>
		public Matrix32 CalcTransitionToSpaceOf(Widget widget)
		{
			return LocalToWorldTransform * widget.LocalToWorldTransform.CalcInversed();
		}

		/// <summary>
		/// Calculates the widget's convex hull in the space of another widget.
		/// </summary>
		public Quadrangle CalcHullInSpaceOf(Widget widget)
		{
			var t = CalcTransitionToSpaceOf(widget);
			return new Quadrangle {
				V1 = t * Vector2.Zero,
				V2 = t * new Vector2(Width, 0),
				V3 = t * Size,
				V4 = t * new Vector2(0, Height)
			};
		}

		/// <summary>
		/// Calculates the widget's AABB in the space of another widget.
		/// </summary>
		public Rectangle CalcAABBInSpaceOf(Widget widget)
		{
			var hull = CalcHullInSpaceOf(widget);
			var aabb = new Rectangle(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue)
				.IncludingPoint(hull.V1)
				.IncludingPoint(hull.V2)
				.IncludingPoint(hull.V3)
				.IncludingPoint(hull.V4);
			return aabb;
		}

		public Quadrangle CalcHull()
		{
			var t = LocalToWorldTransform;
			return new Quadrangle {
				V1 = t * Vector2.Zero,
				V2 = t * new Vector2(Width, 0),
				V3 = t * Size,
				V4 = t * new Vector2(0, Height)
			};
		}

		public IntRectangle CalcAABBInViewportSpace(WindowRect viewport, Matrix44 worldViewProjection)
		{
			var aabb = CalcAABBInSpaceOf(WidgetContext.Current.Root);
			// Get the projected AABB coordinates in the normalized OpenGL space
			var window = WidgetContext.Current.Root;
			aabb.A = worldViewProjection.TransformVector(aabb.A);
			aabb.B = worldViewProjection.TransformVector(aabb.B);
			// Transform to 0,0 - 1,1 coordinate space
			aabb.Left = (1 + aabb.Left) / 2;
			aabb.Right = (1 + aabb.Right) / 2;
			aabb.Top = (1 + aabb.Top) / 2;
			aabb.Bottom = (1 + aabb.Bottom) / 2;
			// Transform to viewport coordinates
			var min = new Vector2(viewport.X, viewport.Y);
			var max = new Vector2(viewport.X + viewport.Width, viewport.Y + viewport.Height);
			return new IntRectangle {
				A = new IntVector2(
					Mathf.Lerp(aabb.Left, min.X, max.X).Round(),
					Mathf.Lerp(aabb.Bottom, min.Y, max.Y).Round()
				),
				B = new IntVector2(
					Mathf.Lerp(aabb.Right, min.X, max.X).Round(),
					Mathf.Lerp(aabb.Top, min.Y, max.Y).Round()
				)
			};
		}

		public Rectangle CalcAABBInWindowSpace()
		{
			var windowWidget = (WindowWidget)WidgetContext.Current.Root;
			var viewport = windowWidget.GetViewport();
			var aabb = (Rectangle)CalcAABBInViewportSpace(viewport, windowWidget.GetProjection());
			var vpOrigin = (Vector2)viewport.Origin;
			aabb.A += vpOrigin;
			aabb.B += vpOrigin;
			var window = CommonWindow.Current;
			aabb.A /= window.PixelScale;
			aabb.B /= window.PixelScale;
			return new Rectangle {
				A = new Vector2(aabb.A.X, window.ClientSize.Y - aabb.B.Y),
				B = new Vector2(aabb.B.X, window.ClientSize.Y - aabb.A.Y),
			};
		}

		public Vector2 CalcPositionInSpaceOf(Widget widget)
		{
			var t = CalcTransitionToSpaceOf(widget);
			return t.TransformVector(Pivot * Size);
		}

		public virtual IEnumerable<string> GetVisibilityIssues()
		{
			if (!SameOrDescendantOf(WidgetContext.Current.Root)) {
				yield return "The widget is not included to the world hierarchy";
			}
			if (!Visible) {
				yield return "The flag 'Visible' is not set";
			} else if (Opacity == 0) {
				yield return "It is fully transparent! Check up 'Opacity' property!";
			} else if (Opacity < 0.1f) {
				yield return "It is almost transparent! Check up 'Opacity' property!";
			} else if (!GloballyVisible) {
				yield return "One of its parent has 'Visible' flag not set";
			} else if (GlobalColor.A < 10) {
				yield return "One of its parent has 'Opacity' close to zero";
			}
			var transform = CalcTransitionToSpaceOf(WidgetContext.Current.Root).ToTransform2();
			if ((transform.Scale.X * Size.X).Abs() < 1 || (transform.Scale.Y * Size.Y).Abs() < 1) {
				yield return string.Format("The widget is probably too small");
			}
			bool passedHitTest = false;
			var hitTestLT = WidgetContext.Current.Root.Position;
			var hitTestRB = hitTestLT + WidgetContext.Current.Root.Size;
			for (float y = hitTestLT.Y; y < hitTestRB.Y && !passedHitTest; y++) {
				for (float x = hitTestLT.X; x < hitTestRB.X && !passedHitTest; x++) {
					var a = new HitTestArgs(new Vector2(x, y));
					if (PartialHitTest(ref a)) {
						passedHitTest = true;
					}
				}
			}
			if (!passedHitTest) {
				yield return string.Format("SelfHitTest() returns false in range [{0}] x [{1}].", hitTestLT, hitTestRB);
			}
			if (!(this is Image) && (this.Nodes.Count == 0)) {
				yield return "The widget doesn't contain any drawable node";
			}
		}

		public void ExpandToContainerWithAnchors()
		{
			Anchors = Anchors.None;
			Size = ParentWidget.Size;
			Anchors = Anchors.LeftRightTopBottom;
		}

		public override void UpdateAncestorBoundingRect(Widget ancestor)
		{
			if (Visible && CleanDirtyFlags(DirtyFlags.ParentBoundingRect)) {
				ExpandAncestorBoundingRect(ancestor);
			}
		}

		public void RecalcBoundingRect()
		{
			for (var n = FirstChild; n != null; n = n.NextSibling) {
				n.UpdateAncestorBoundingRect(this);
			}
		}

		protected override void RecalcGloballyFrozen()
		{
			base.RecalcGloballyFrozen();
			globallyFrozen |= FreezeInvisible && !((Visible && (color.A != 0 || RenderTransparentWidgets)) || GetTangerineFlag(TangerineFlags.DisplayContent));
		}
	}
}
