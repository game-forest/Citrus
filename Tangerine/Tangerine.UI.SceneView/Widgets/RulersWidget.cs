using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI.SceneView
{
	public class RulersWidget : Widget
	{
		public const float SegmentDeltaMax = 100;
		public const float SegmentDeltaMin = 50;
		public const int Tesselation = 5;
		public const int RulerHeight = 20;

		private static SceneView SceneView => SceneView.Instance;
		private static Widget Container => Document.Current.RootNode.AsWidget;
		private static Ruler Ruler => ProjectUserPreferences.Instance.ActiveRuler;
		private readonly Widget topRulerBar;
		private readonly Widget leftRulerBar;

		public RulersWidget()
		{
			LayoutCell = new LayoutCell();
			Anchors = Anchors.LeftRightTopBottom;
			topRulerBar = new Widget {
				Anchors = Anchors.LeftRight,
				Id = "TopRuler",
				HitTestTarget = true,
				MinMaxHeight = RulerHeight,
				Height = RulerHeight,
			};

			leftRulerBar = new Widget {
				Anchors = Anchors.Left | Anchors.Bottom | Anchors.Top,
				HitTestTarget = true,
				Id = "LeftRuler",
				MinMaxWidth = RulerHeight,
				Width = RulerHeight,
			};

			topRulerBar.Tasks.Add(CreateLineTask(topRulerBar, RulerOrientation.Horizontal));
			leftRulerBar.Tasks.Add(CreateLineTask(leftRulerBar, RulerOrientation.Vertical));

			Nodes.Add(leftRulerBar);
			Nodes.Add(topRulerBar);
			topRulerBar.CompoundPostPresenter.Push(new RulerBarPresenter(RulerOrientation.Horizontal));
			leftRulerBar.CompoundPostPresenter.Push(new RulerBarPresenter(RulerOrientation.Vertical));
			CompoundPostPresenter.Push(new RulerLinesPresenter(leftRulerBar, topRulerBar));
			this.AddChangeWatcher(LocalMousePosition, _ => {
				if (SceneView.Panel.IsMouseOverThisOrDescendant()) {
					Window.Current.Invalidate();
				}
			});
			this.AddChangeWatcher(
				() => ProjectUserPreferences.Instance.RulerVisible,
				v => {
					topRulerBar.Visible = v;
					leftRulerBar.Visible = v;
				});
		}

		private static IEnumerator<object> CreateLineTask(
			Widget widget, RulerOrientation lineRulerOrientation = RulerOrientation.Horizontal
		) {
			while (true) {
				if (widget.Input.WasMousePressed() && !Document.Current.PreviewScene) {
					var rect = new Rectangle(Vector2.Zero, widget.Size);
					SceneView.Components.Add(new CreateLineRequestComponent {
						Orientation = lineRulerOrientation,
						IsHovered = () => rect.Contains(widget.LocalMousePosition()),
					});
				}
				yield return null;
			}
		}

		private class RulerLinesPresenter : SyncCustomPresenter<Widget>
		{
			private readonly Widget leftRulerBar;
			private readonly Widget topRulerBar;
			private Vector2 guidesPosition = Vector2.Zero;

			public RulerLinesPresenter(Widget leftRulerBar, Widget topRulerBar)
			{
				this.leftRulerBar = leftRulerBar;
				this.topRulerBar = topRulerBar;
			}

			protected override void InternalRender(Widget widget)
			{
				if (Document.Current.PreviewScene || !ProjectUserPreferences.Instance.RulerVisible) {
					return;
				}

				widget.PrepareRendererState();
				var t = Container.LocalToWorldTransform
					* SceneView.Scene.LocalToWorldTransform
					* widget.LocalToWorldTransform.CalcInversed();
				var lineComponent = SceneView.Components.Get<LineSelectionComponent>();
				foreach (var line in Ruler.Lines) {
					if (line == lineComponent?.Line) {
						continue;
					}

					DrawLine(
						widget.Size,
						line.GetClosestPointToOrigin() * t,
						line.RulerOrientation,
						ColorTheme.Current.SceneView.RulerEditable
					);
				}

				var mousePos = widget.LocalMousePosition();
				var guidesColor = ColorTheme.Current.SceneView.RulerEditable;
				if (lineComponent != null) {
					guidesColor = ColorTheme.Current.SceneView.RulerEditableActiveDraging;
				}

				if (SceneView.Panel.IsMouseOverThisOrDescendant()) {
					guidesPosition = mousePos;
					if (lineComponent != null) {
						var mask = lineComponent.Line.RulerOrientation.GetDirection();
						guidesPosition =
							lineComponent.Line.GetClosestPointToOrigin() * t * mask + mousePos * (Vector2.One - mask);
					}
				}

				if (guidesPosition.X > RulerHeight) {
					Renderer.DrawLine(
						new Vector2(guidesPosition.X, 0), new Vector2(guidesPosition.X, topRulerBar.Size.Y), guidesColor
					);
				}
				if (guidesPosition.Y > RulerHeight) {
					Renderer.DrawLine(
						new Vector2(0, guidesPosition.Y),
						new Vector2(leftRulerBar.Size.X, guidesPosition.Y),
						guidesColor
					);
				}

				if (lineComponent != null) {
					const float fontSize = 15f;
					const float letterspacing = 1;
					var padding = Vector2.One * 5f;
					var isVertical = lineComponent.Line.RulerOrientation == RulerOrientation.Vertical;
					var value = lineComponent.Line.GetClosestPointToOrigin();
					var text = $"{(isVertical ? "X:" : "Y:")} {(isVertical ? value.X : value.Y).ToString("G")}";
					var size = FontPool.Instance.DefaultFont.MeasureTextLine(
						text, fontSize, letterspacing
					) + 2 * padding;
					var textPos = mousePos + Vector2.One * 10f;
					DrawLine(
						widget.Size,
						value * t,
						lineComponent.Line.RulerOrientation,
						ColorTheme.Current.SceneView.RulerEditableActiveDraging
					);
					Renderer.DrawRect(textPos, textPos + size, ColorTheme.Current.SceneView.RulerBackground);
					Renderer.DrawRectOutline(textPos, textPos + size, ColorTheme.Current.SceneView.RulerTextColor);
					Renderer.DrawTextLine(
						position: textPos + new Vector2(padding.X, (size.Y - fontSize) / 2),
						text: text,
						fontHeight: fontSize,
						color: ColorTheme.Current.SceneView.RulerTextColor,
						letterSpacing: letterspacing
					);
				}
			}

			private static void DrawLine(
				Vector2 containerSize, Vector2 value, RulerOrientation orientation, Color4 color
			) {
				if (value.Y <= RulerHeight && orientation == RulerOrientation.Horizontal ||
					value.X <= RulerHeight && orientation == RulerOrientation.Vertical) {
					return;
				}
				var mask = orientation.GetDirection();
				var a = (Vector2.One - mask) * RulerHeight + mask * value;
				var b = (Vector2.One - mask) * containerSize + mask * value;
				Renderer.DrawLine(a, b, color);
			}
		}

		public static float CalculateEffectiveStep()
		{
			var step = SegmentDeltaMax;
			while (true) {
				var value = step * SceneView.Scene.Scale.X;
				if (value <= SegmentDeltaMax && value >= SegmentDeltaMin) {
					break;
				}

				if (Math.Abs(step % 2) > Mathf.ZeroTolerance && Mathf.Abs(step % 5) < Mathf.ZeroTolerance) {
					step -= 5;
				} else {
					if (value < SegmentDeltaMin) {
						step *= 2;
					}

					if (value > SegmentDeltaMax) {
						step /= 2;
					}
				}
			}

			return step;
		}

		private class RulerBarPresenter : SyncCustomPresenter<Widget>
		{
			private RulerOrientation RulerOrientation { get; set; }

			public RulerBarPresenter(RulerOrientation rulerOrientation)
			{
				RulerOrientation = rulerOrientation;
			}

			protected override void InternalRender(Widget canvas)
			{
				if (Document.Current.ExpositionMode || !ProjectUserPreferences.Instance.RulerVisible) {
					return;
				}

				var containerHull =
					Document.Current.RootNode.AsWidget.CalcHull()
						.Transform(SceneView.CalcTransitionFromSceneSpace(canvas));
				canvas.PrepareRendererState();
				Renderer.DrawRect(Vector2.Zero, canvas.Size, ColorTheme.Current.SceneView.RulerBackground);
				var start = (
					RulerOrientation == RulerOrientation.Horizontal ? canvas.Size.Y : canvas.Size.X
				) * Vector2.One;
				Renderer.DrawLine(start, canvas.Size, ColorTheme.Current.SceneView.RulerTextColor);
				DrawSegments(
					new RulerData(
						containerHull.V1,
						0,
						RulerOrientation == RulerOrientation.Horizontal
							? canvas.Size.X
							: canvas.Size.Y,
						RulerHeight,
						RulerOrientation
					),
					RulerHeight
				);
			}

			private static float GetVectorComponentForOrientation(Vector2 value, RulerOrientation orientation)
			{
				return orientation == RulerOrientation.Horizontal ? value.X : value.Y;
			}

			private void DrawSegments(
				RulerData rulerData, float strokeDelta, float strokeLength, float? strokeValue = null
			) {
				var maskInverted = rulerData.RulerOrientation.GetDirection();
				var mask = Vector2.One - maskInverted;
				var delta = mask * strokeDelta;
				var j = -(int)Math.Round(((rulerData.Origin * mask).Length - rulerData.LeftStoper) / strokeDelta);
				var b = maskInverted * rulerData.Offset + rulerData.Origin * mask + j * strokeDelta * mask;
				var a = b - maskInverted * strokeLength;
				var fontHeight = 14f;
				var letterspacing = 1f;
				var textOffset = Vector2.Zero;
				do {
					if (GetVectorComponentForOrientation(a, rulerData.RulerOrientation) - rulerData.Offset >= 0) {
						Renderer.DrawLine(a, b, ColorTheme.Current.SceneView.RulerTextColor);
						if (strokeValue != null) {
							var lengthMarkerText = ((int)(j * strokeValue.Value)).ToString();
							var textLength = FontPool.Instance.DefaultFont.MeasureTextLine(
								lengthMarkerText, fontHeight, letterspacing
							);
							var lengthMarkerPosition = a + textOffset;
							Renderer.PushState(RenderState.Transform1);
							Renderer.MultiplyTransform1(
								Matrix32.Translation(lengthMarkerPosition.X.Round(), lengthMarkerPosition.Y.Round())
							);
							if (rulerData.RulerOrientation == RulerOrientation.Vertical) {
								Renderer.MultiplyTransform1(Matrix32.Rotation(-Mathf.HalfPi));
								textOffset = Vector2.Down * (5 + textLength.X);
							} else {
								textOffset = Vector2.Right * 5;
							}
							Renderer.DrawTextLine(
								Vector2.Zero,
								lengthMarkerText,
								fontHeight,
								ColorTheme.Current.SceneView.RulerTextColor,
								letterspacing
							);
							Renderer.PopState();
						}
					}
					j++;
					a += delta;
					b += delta;
				} while (rulerData.RightStoper > GetVectorComponentForOrientation(a, rulerData.RulerOrientation));
			}

			private void DrawSegments(RulerData rulerData, float strokeLength)
			{
				var step = (float)Math.Truncate(CalculateEffectiveStep());
				var delta = step * SceneView.Scene.Scale.X;
				DrawSegments(rulerData, delta, strokeLength, step);
				var reduceFactor = 1f / Tesselation;
				DrawSegments(rulerData, delta * reduceFactor, strokeLength * reduceFactor);
			}

			private struct RulerData
			{
				public Vector2 Origin;
				public float LeftStoper;
				public float RightStoper;
				public float Offset;
				public RulerOrientation RulerOrientation;

				public RulerData(
					Vector2 origin, float leftStoper, float rightStoper, float offset, RulerOrientation rulerOrientation
				) {
					Origin = origin;
					LeftStoper = leftStoper;
					RightStoper = rightStoper;
					Offset = offset;
					RulerOrientation = rulerOrientation;
				}
			}
		}
	}

	public class LineSelectionComponent : Component
	{
		public RulerLine Line { get; set; }
	}

	public class CreateLineRequestComponent : Component
	{
		public RulerOrientation Orientation { get; set; }
		public Func<bool> IsHovered { get; set; }
	}
}
