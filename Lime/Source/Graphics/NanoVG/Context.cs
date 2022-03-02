using System;

namespace Lime.NanoVG
{
	public unsafe class Context : IDisposable
	{
		public static readonly Context Instance = new Context();

		private readonly IRenderingBackend renderingBackend;
		private PathCache cache;
		private float* commands;
		private int commandsCount;
		private int commandsNumber;
		private float commandX;
		private float commandY;
		private float distTol;
		private readonly bool edgeAntiAlias;
		private float fringeWidth;
		private readonly ContextState[] states = new ContextState[32];
		private int stateCount;
		private float tessTol;

		public Context(bool edgeAntiAlias = true)
		{
			renderingBackend = new RenderingBackend();
			this.edgeAntiAlias = edgeAntiAlias;
			commands = (float*)RawMemory.Allocate(sizeof(float) * 256);
			commandsNumber = 0;
			commandsCount = 256;
			cache = new PathCache();
			for (int i = 0; i < states.Length; i++) {
				states[i] = new ContextState();
			}
			Save();
			SetDevicePixelRatio(1.0f);
		}

		public void Dispose()
		{
			if (commands != null) {
				RawMemory.Free(commands);
				commands = null;
			}
			if (cache != null) {
				cache.Dispose();
				cache = null;
			}
		}

		public void Save()
		{
			if (stateCount >= states.Length) {
				throw new InvalidOperationException();
			}
			if (stateCount > 0) {
				states[stateCount - 1].CloneTo(states[stateCount]);
			} else {
				states[stateCount].Reset();
			}
			stateCount++;
		}

		public void Restore()
		{
			if (stateCount <= 1) {
				throw new InvalidOperationException();
			}
			stateCount--;
		}

		public void Reset() => GetState().Reset();

		public void ShapeAntiAlias(int enabled)
		{
			var state = GetState();
			state.ShapeAntiAlias = enabled;
		}

		public void StrokeWidth(float width)
		{
			var state = GetState();
			state.StrokeWidth = width;
		}

		public void MiterLimit(float limit)
		{
			var state = GetState();
			state.MiterLimit = limit;
		}

		public void LineCap(LineCap cap)
		{
			var state = GetState();
			state.LineCap = cap;
		}

		public void LineJoin(LineCap join)
		{
			var state = GetState();
			state.LineJoin = join;
		}

		public void GlobalAlpha(float alpha)
		{
			var state = GetState();
			state.Alpha = alpha;
		}

		public void Transform(Matrix32 transform)
		{
			var state = GetState();
			state.Transform = transform * state.Transform;
		}

		public void ResetTransform()
		{
			var state = GetState();
			state.Transform = Matrix32.Identity;
		}

		public void Translate(float x, float y)
		{
			var state = GetState();
			state.Transform = Matrix32.Translation(x, y) * state.Transform;
		}

		public void Rotate(float angle)
		{
			var state = GetState();
			state.Transform = Matrix32.Rotation(angle) * state.Transform;
		}

		public void SkewX(float angle)
		{
			var state = GetState();
			state.Transform = Matrix32.SkewX(angle) * state.Transform;
		}

		public void SkewY(float angle)
		{
			var state = GetState();
			state.Transform = Matrix32.SkewY(angle) * state.Transform;
		}

		public void Scale(float x, float y)
		{
			var state = GetState();
			state.Transform = Matrix32.Scaling(x, y) * state.Transform;
		}

		public void CurrentTransform(Matrix32 transform)
		{
			var state = GetState();
			state.Transform = transform;
		}

		public void StrokeColor(Color4 color)
		{
			var state = GetState();
			state.Stroke = new Paint(color);
		}

		public void StrokePaint(Paint paint)
		{
			var state = GetState();
			state.Stroke = paint;
			state.Stroke.Transform *= state.Transform;
		}

		public void FillColor(Color4 color)
		{
			var state = GetState();
			state.Fill = new Paint(color);
		}

		public void FillPaint(Paint paint)
		{
			var state = GetState();
			state.Fill = paint;
			state.Fill.Transform *= state.Transform;
		}

		public int CreateImageRGBA(int w, int h, ImageFlags imageFlags, byte[] data)
		{
			return renderingBackend.CreateTexture(TextureType.RGBA, w, h, imageFlags, data);
		}

		public void UpdateImage(int image, byte[] data)
		{
			renderingBackend.GetTextureSize(image, out var w, out var h);
			renderingBackend.UpdateTexture(image, 0, 0, w, h, data);
		}

		public void ImageSize(int image, out int w, out int h)
		{
			renderingBackend.GetTextureSize(image, out w, out h);
		}

		public void DeleteImage(int image)
		{
			renderingBackend.DeleteTexture(image);
		}

		public void Scissor(float x, float y, float w, float h)
		{
			var state = GetState();
			w = Math.Max(0.0f, w);
			h = Math.Max(0.0f, h);
			state.Scissor.Transform = Matrix32.Identity;
			state.Scissor.Transform.TX = x + w * 0.5f;
			state.Scissor.Transform.TY = y + h * 0.5f;
			state.Scissor.Transform *= state.Transform;
			state.Scissor.Extent.X = w * 0.5f;
			state.Scissor.Extent.Y = h * 0.5f;
		}

		public void Scissor(Vector2 position, Vector2 size) => Scissor(position.X, position.Y, size.X, size.Y);

		public void IntersectScissor(float x, float y, float w, float h)
		{
			var state = GetState();
			var rect = stackalloc float[4];
			if (state.Scissor.Extent.X < 0) {
				Scissor(x, y, w, h);
				return;
			}
			var pxform = state.Scissor.Transform;
			var ex = state.Scissor.Extent.X;
			var ey = state.Scissor.Extent.Y;
			var invxorm = state.Transform.CalcInversed();
			pxform *= invxorm;
			var tex = ex * Math.Abs(pxform.UX) + ey * Math.Abs(pxform.VX);
			var tey = ex * Math.Abs(pxform.UY) + ey * Math.Abs(pxform.VY);
			IntersectRectangles(rect, pxform.TX - tex, pxform.TY - tey, tex * 2, tey * 2, x, y, w, h);
			Scissor(rect[0], rect[1], rect[2], rect[3]);
		}

		public void IntersectScissor(Vector2 position, Vector2 size)
		{
			IntersectScissor(position.X, position.Y, size.X, size.Y);
		}

		public void ResetScissor()
		{
			var state = GetState();
			state.Scissor.Transform = new Matrix32();
			state.Scissor.Extent.X = -1.0f;
			state.Scissor.Extent.Y = -1.0f;
		}

		public void BeginPath()
		{
			commandsNumber = 0;
			ClearPathCache();
		}

		public void MoveTo(Vector2 v) => MoveTo(v.X, v.Y);

		public void MoveTo(float x, float y)
		{
			var vals = stackalloc float[3];
			vals[0] = (int)CommandType.MoveTo;
			vals[1] = x;
			vals[2] = y;
			AppendCommands(vals, 3);
		}

		public void LineTo(Vector2 v) => LineTo(v.X, v.Y);

		public void LineTo(float x, float y)
		{
			var vals = stackalloc float[3];
			vals[0] = (int)CommandType.LineTo;
			vals[1] = x;
			vals[2] = y;
			AppendCommands(vals, 3);
		}

		public void Line(Vector2 a, Vector2 b)
		{
			MoveTo(a.X, a.Y);
			LineTo(b.X, b.Y);
		}

		public void Line(float ax, float ay, float bx, float by)
		{
			MoveTo(ax, ay);
			LineTo(bx, by);
		}

		public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y)
		{
			var vals = stackalloc float[7];
			vals[0] = (int)CommandType.BezierTo;
			vals[1] = c1x;
			vals[2] = c1y;
			vals[3] = c2x;
			vals[4] = c2y;
			vals[5] = x;
			vals[6] = y;
			AppendCommands(vals, 7);
		}

		public void QuadTo(float cx, float cy, float x, float y)
		{
			var x0 = commandX;
			var y0 = commandY;
			var vals = stackalloc float[7];
			vals[0] = (int)CommandType.BezierTo;
			vals[1] = x0 + 2.0f / 3.0f * (cx - x0);
			vals[2] = y0 + 2.0f / 3.0f * (cy - y0);
			vals[3] = x + 2.0f / 3.0f * (cx - x);
			vals[4] = y + 2.0f / 3.0f * (cy - y);
			vals[5] = x;
			vals[6] = y;
			AppendCommands(vals, 7);
		}

		public void ArcTo(float x1, float y1, float x2, float y2, float radius)
		{
			var x0 = commandX;
			var y0 = commandY;
			float cx;
			float cy;
			float a0;
			float a1;
			var dir = Winding.CounterClockWise;
			if (commandsNumber == 0) {
				return;
			}
			if (
				PointsAreEquals(x0, y0, x1, y1, distTol) != 0
				|| PointsAreEquals(x1, y1, x2, y2, distTol) != 0
				|| CalcDistanceFromPointToSegment(x1, y1, x0, y0, x2, y2) < distTol * distTol || radius < distTol
			) {
				LineTo(x1, y1);
				return;
			}
			var dx0 = x0 - x1;
			var dy0 = y0 - y1;
			var dx1 = x2 - x1;
			var dy1 = y2 - y1;
			Normalize(ref dx0, ref dy0);
			Normalize(ref dx1, ref dy1);
			var a = MathF.Acos(dx0 * dx1 + dy0 * dy1);
			var d = radius / MathF.Tan(a / 2.0f);
			if (d > 10000.0f) {
				LineTo(x1, y1);
				return;
			}
			if (Cross(dx0, dy0, dx1, dy1) > 0.0f) {
				cx = x1 + dx0 * d + dy0 * radius;
				cy = y1 + dy0 * d + -dx0 * radius;
				a0 = MathF.Atan2(dx0, -dy0);
				a1 = MathF.Atan2(-dx1, dy1);
				dir = Winding.ClockWise;
			} else {
				cx = x1 + dx0 * d + -dy0 * radius;
				cy = y1 + dy0 * d + dx0 * radius;
				a0 = MathF.Atan2(-dx0, dy0);
				a1 = MathF.Atan2(dx1, -dy1);
				dir = Winding.CounterClockWise;
			}
			Arc(cx, cy, radius, a0, a1, dir);
		}

		public void ClosePath()
		{
			var vals = stackalloc float[1];
			vals[0] = (int)CommandType.Close;
			AppendCommands(vals, 1);
		}

		public void PathWinding(Solidity dir)
		{
			var vals = stackalloc float[2];
			vals[0] = (int)CommandType.Winding;
			vals[1] = (int)dir;
			AppendCommands(vals, 2);
		}

		public void Arc(float cx, float cy, float r, float a0, float a1, Winding dir)
		{
			var px = (float)0;
			var py = (float)0;
			var ptanx = (float)0;
			var ptany = (float)0;
			var vals = stackalloc float[3 + 5 * 7 + 100];
			var i = 0;
			var ndivs = 0;
			var nvals = 0;
			var move = commandsNumber > 0 ? CommandType.LineTo : CommandType.MoveTo;
			var da = a1 - a0;
			if (dir == Winding.ClockWise) {
				if (Math.Abs(da) >= 3.14159274 * 2) {
					da = (float)(3.14159274 * 2);
				} else {
					while (da < 0.0f) {
						da += (float)(3.14159274 * 2);
					}
				}
			} else {
				if (Math.Abs(da) >= 3.14159274 * 2) {
					da = (float)(-3.14159274 * 2);
				} else {
					while (da > 0.0f) {
						da -= (float)(3.14159274 * 2);
					}
				}
			}
			ndivs = Math.Max(1, Math.Min((int)(Math.Abs(da) / (3.14159274 * 0.5f) + 0.5f), 5));
			var hda = da / ndivs / 2.0f;
			var kappa = Math.Abs(4.0f / 3.0f * (1.0f - MathF.Cos(hda)) / MathF.Sin(hda));
			if (dir == Winding.CounterClockWise) {
				kappa = -kappa;
			}
			nvals = 0;
			for (i = 0; i <= ndivs; i++) {
				var a = a0 + da * (i / (float)ndivs);
				var dx = MathF.Cos(a);
				var dy = MathF.Sin(a);
				var x = cx + dx * r;
				var y = cy + dy * r;
				var tanx = -dy * r * kappa;
				var tany = dx * r * kappa;
				if (i == 0) {
					vals[nvals++] = (int)move;
					vals[nvals++] = x;
					vals[nvals++] = y;
				} else {
					vals[nvals++] = (int)CommandType.BezierTo;
					vals[nvals++] = px + ptanx;
					vals[nvals++] = py + ptany;
					vals[nvals++] = x - tanx;
					vals[nvals++] = y - tany;
					vals[nvals++] = x;
					vals[nvals++] = y;
				}
				px = x;
				py = y;
				ptanx = tanx;
				ptany = tany;
			}
			AppendCommands(vals, nvals);
		}

		public void Rect(Vector2 position, Vector2 size)
		{
			Rect(position.X, position.Y, size.X, size.Y);
		}

		public void Rect(float x, float y, float w, float h)
		{
			var vals = stackalloc float[13];
			vals[0] = (int)CommandType.MoveTo;
			vals[1] = x;
			vals[2] = y;
			vals[3] = (int)CommandType.LineTo;
			vals[4] = x;
			vals[5] = y + h;
			vals[6] = (int)CommandType.LineTo;
			vals[7] = x + w;
			vals[8] = y + h;
			vals[9] = (int)CommandType.LineTo;
			vals[10] = x + w;
			vals[11] = y;
			vals[12] = (int)CommandType.Close;
			AppendCommands(vals, 13);
		}

		public void RoundedRect(Vector2 position, Vector2 size, float r)
		{
			RoundedRectVarying(position.X, position.Y, size.X, size.Y, r, r, r, r);
		}

		public void RoundedRect(float x, float y, float w, float h, float r)
		{
			RoundedRectVarying(x, y, w, h, r, r, r, r);
		}

		public void RoundedRectVarying(
			float x,
			float y,
			float width,
			float height,
			float radTopLeft,
			float radTopRight,
			float radBottomRight,
			float radBottomLeft
		) {
			if (radTopLeft < 0.1f && radTopRight < 0.1f && radBottomRight < 0.1f && radBottomLeft < 0.1f) {
				Rect(x, y, width, height);
			} else {
				var halfw = Math.Abs(width) * 0.5f;
				var halfh = Math.Abs(height) * 0.5f;
				var rxBL = Math.Min(radBottomLeft, halfw) * Math.Sign(width);
				var ryBL = Math.Min(radBottomLeft, halfh) * Math.Sign(height);
				var rxBR = Math.Min(radBottomRight, halfw) * Math.Sign(width);
				var ryBR = Math.Min(radBottomRight, halfh) * Math.Sign(height);
				var rxTR = Math.Min(radTopRight, halfw) * Math.Sign(width);
				var ryTR = Math.Min(radTopRight, halfh) * Math.Sign(height);
				var rxTL = Math.Min(radTopLeft, halfw) * Math.Sign(width);
				var ryTL = Math.Min(radTopLeft, halfh) * Math.Sign(height);
				var vals = stackalloc float[44];
				vals[0] = (int)CommandType.MoveTo;
				vals[1] = x;
				vals[2] = y + ryTL;
				vals[3] = (int)CommandType.LineTo;
				vals[4] = x;
				vals[5] = y + height - ryBL;
				vals[6] = (int)CommandType.BezierTo;
				vals[7] = x;
				vals[8] = y + height - ryBL * (1 - 0.5522847493f);
				vals[9] = x + rxBL * (1 - 0.5522847493f);
				vals[10] = y + height;
				vals[11] = x + rxBL;
				vals[12] = y + height;
				vals[13] = (int)CommandType.LineTo;
				vals[14] = x + width - rxBR;
				vals[15] = y + height;
				vals[16] = (int)CommandType.BezierTo;
				vals[17] = x + width - rxBR * (1 - 0.5522847493f);
				vals[18] = y + height;
				vals[19] = x + width;
				vals[20] = y + height - ryBR * (1 - 0.5522847493f);
				vals[21] = x + width;
				vals[22] = y + height - ryBR;
				vals[23] = (int)CommandType.LineTo;
				vals[24] = x + width;
				vals[25] = y + ryTR;
				vals[26] = (int)CommandType.BezierTo;
				vals[27] = x + width;
				vals[28] = y + ryTR * (1 - 0.5522847493f);
				vals[29] = x + width - rxTR * (1 - 0.5522847493f);
				vals[30] = y;
				vals[31] = x + width - rxTR;
				vals[32] = y;
				vals[33] = (int)CommandType.LineTo;
				vals[34] = x + rxTL;
				vals[35] = y;
				vals[36] = (int)CommandType.BezierTo;
				vals[37] = x + rxTL * (1 - 0.5522847493f);
				vals[38] = y;
				vals[39] = x;
				vals[40] = y + ryTL * (1 - 0.5522847493f);
				vals[41] = x;
				vals[42] = y + ryTL;
				vals[43] = (int)CommandType.Close;
				AppendCommands(vals, 44);
			}
		}

		public void Ellipse(Vector2 v, float rx, float ry) => Ellipse(v.X, v.Y, rx, ry);

		public void Ellipse(float cx, float cy, float rx, float ry)
		{
			var vals = stackalloc float[32];
			vals[0] = (int)CommandType.MoveTo;
			vals[1] = cx - rx;
			vals[2] = cy;
			vals[3] = (int)CommandType.BezierTo;
			vals[4] = cx - rx;
			vals[5] = cy + ry * 0.5522847493f;
			vals[6] = cx - rx * 0.5522847493f;
			vals[7] = cy + ry;
			vals[8] = cx;
			vals[9] = cy + ry;
			vals[10] = (int)CommandType.BezierTo;
			vals[11] = cx + rx * 0.5522847493f;
			vals[12] = cy + ry;
			vals[13] = cx + rx;
			vals[14] = cy + ry * 0.5522847493f;
			vals[15] = cx + rx;
			vals[16] = cy;
			vals[17] = (int)CommandType.BezierTo;
			vals[18] = cx + rx;
			vals[19] = cy - ry * 0.5522847493f;
			vals[20] = cx + rx * 0.5522847493f;
			vals[21] = cy - ry;
			vals[22] = cx;
			vals[23] = cy - ry;
			vals[24] = (int)CommandType.BezierTo;
			vals[25] = cx - rx * 0.5522847493f;
			vals[26] = cy - ry;
			vals[27] = cx - rx;
			vals[28] = cy - ry * 0.5522847493f;
			vals[29] = cx - rx;
			vals[30] = cy;
			vals[31] = (int)CommandType.Close;
			AppendCommands(vals, 32);
		}

		public void Circle(Vector2 v, float r) => Circle(v.X, v.Y, r);

		public void Circle(float cx, float cy, float r)
		{
			Ellipse(cx, cy, r, r);
		}

		private static void MultiplyAlpha(ref Color4 c, float alpha)
		{
			c.A = (byte)(c.A * alpha);
		}

		public void Fill()
		{
			var state = GetState();
			Path path;
			var fillPaint = state.Fill;
			FlattenPaths();
			if (edgeAntiAlias && state.ShapeAntiAlias != 0) {
				ExpandFill(fringeWidth, Lime.LineCap.Miter, 2.4f);
			} else {
				ExpandFill(0.0f, Lime.LineCap.Miter, 2.4f);
			}
			MultiplyAlpha(ref fillPaint.InnerColor, state.Alpha);
			MultiplyAlpha(ref fillPaint.OuterColor, state.Alpha);
			renderingBackend.RenderFill(
				ref fillPaint, ref state.Scissor, fringeWidth, cache.Bounds, cache.Paths.ToArraySegment());
		}

		public void Stroke()
		{
			var state = GetState();
			var scale = GetAverageScale(ref state.Transform);
			var strokeWidth = Mathf.Clamp(state.StrokeWidth * scale, 0.0f, 200.0f);
			var strokePaint = state.Stroke;
			Path path;
			var i = 0;
			if (strokeWidth < fringeWidth) {
				var alpha = Mathf.Clamp(strokeWidth / fringeWidth, 0.0f, 1.0f);
				MultiplyAlpha(ref strokePaint.InnerColor, alpha * alpha);
				MultiplyAlpha(ref strokePaint.OuterColor, alpha * alpha);
				strokeWidth = fringeWidth;
			}
			MultiplyAlpha(ref strokePaint.InnerColor, state.Alpha);
			MultiplyAlpha(ref strokePaint.OuterColor, state.Alpha);
			FlattenPaths();
			if (edgeAntiAlias && state.ShapeAntiAlias != 0) {
				ExpandStroke(strokeWidth * 0.5f, fringeWidth, state.LineCap, state.LineJoin, state.MiterLimit);
			} else {
				ExpandStroke(strokeWidth * 0.5f, 0.0f, state.LineCap, state.LineJoin, state.MiterLimit);
			}
			renderingBackend.RenderStroke(
				ref strokePaint, ref state.Scissor, fringeWidth, strokeWidth, cache.Paths.ToArraySegment());
		}

		private void SetDevicePixelRatio(float ratio)
		{
			tessTol = 0.25f / ratio;
			distTol = 0.01f / ratio;
			fringeWidth = 1.0f / ratio;
		}

		private ContextState GetState() => states[stateCount - 1];

		private void AppendCommands(float* vals, int nvals)
		{
			var state = GetState();
			if (commandsNumber + nvals > commandsCount) {
				float* commands;
				var ccommands = commandsNumber + nvals + commandsCount / 2;
				commands = (float*)RawMemory.Realloc(this.commands, sizeof(float) * ccommands);
				if (commands == null) {
					return;
				}
				this.commands = commands;
				commandsCount = ccommands;
			}
			if (vals[0] != (int)CommandType.Close && vals[0] != (int)CommandType.Winding) {
				commandX = vals[nvals - 2];
				commandY = vals[nvals - 1];
			}
			var i = 0;
			while (i < nvals) {
				var cmd = vals[i];
				switch ((CommandType)cmd) {
					case CommandType.MoveTo:
						TransformPoint(ref state.Transform, ref vals[i + 1], ref vals[i + 2]);
						i += 3;
						break;
					case CommandType.LineTo:
						TransformPoint(ref state.Transform, ref vals[i + 1], ref vals[i + 2]);
						i += 3;
						break;
					case CommandType.BezierTo:
						TransformPoint(ref state.Transform, ref vals[i + 1], ref vals[i + 2]);
						TransformPoint(ref state.Transform, ref vals[i + 3], ref vals[i + 4]);
						TransformPoint(ref state.Transform, ref vals[i + 5], ref vals[i + 6]);
						i += 7;
						break;
					case CommandType.Close:
						i++;
						break;
					case CommandType.Winding:
						i += 2;
						break;
					default:
						i++;
						break;
				}
			}
			RawMemory.CopyMemory(&commands[commandsNumber], vals, nvals * sizeof(float));
			commandsNumber += nvals;
		}

		private static void TransformPoint(ref Matrix32 t, ref float x, ref float y)
		{
			var tx = x * t.UX + y * t.VX + t.TX;
			y = x * t.UY + y * t.VY + t.TY;
			x = tx;
		}

		private void ClearPathCache()
		{
			cache.Paths.Clear();
			cache.PointsNumber = 0;
		}

		private Path LastPath()
		{
			if (cache.Paths.Count > 0) {
				return cache.Paths[cache.Paths.Count - 1];
			}
			return null;
		}

		private void AddPath()
		{
			var newPath = new Path {
				First = cache.PointsNumber,
				Winding = Winding.CounterClockWise,
			};
			cache.Paths.Add(newPath);
		}

		private Point* LastPoint()
		{
			if (cache.PointsNumber > 0) {
				return &cache.Points[cache.PointsNumber - 1];
			}
			return null;
		}

		private void AddPoint(float x, float y, PointFlags flags)
		{
			var path = LastPath();
			Point* pt;
			if (path == null) {
				return;
			}
			if (path.Count > 0 && cache.PointsNumber > 0) {
				pt = LastPoint();
				if (PointsAreEquals(pt->X, pt->Y, x, y, distTol) != 0) {
					pt->Flags |= (byte)flags;
					return;
				}
			}
			if (cache.PointsNumber + 1 > cache.PointsCount) {
				Point* points;
				var cpoints = cache.PointsNumber + 1 + cache.PointsCount / 2;
				points = (Point*)RawMemory.Realloc(cache.Points, sizeof(Point) * cpoints);
				if (points == null) {
					return;
				}
				cache.Points = points;
				cache.PointsCount = cpoints;
			}
			pt = &cache.Points[cache.PointsNumber];
			pt->Reset();
			pt->X = x;
			pt->Y = y;
			pt->Flags = (byte)flags;
			cache.PointsNumber++;
			path.Count++;
		}

		private void InternalClosePath()
		{
			var path = LastPath();
			if (path == null) {
				return;
			}
			path.Closed = 1;
		}

		private void PathWinding(Winding winding)
		{
			var path = LastPath();
			if (path == null) {
				return;
			}
			path.Winding = winding;
		}

		private ArraySegment<Vertex> AllocTempVerts(int nverts)
		{
			cache.Vertices.EnsureSize(nverts);
			return new ArraySegment<Vertex>(cache.Vertices.Array);
		}

		private void TesselateBezier(
			float x1,
			float y1,
			float x2,
			float y2,
			float x3,
			float y3,
			float x4,
			float y4,
			int level,
			PointFlags type
		) {
			if (level > 10) {
				return;
			}
			var x12 = (x1 + x2) * 0.5f;
			var y12 = (y1 + y2) * 0.5f;
			var x23 = (x2 + x3) * 0.5f;
			var y23 = (y2 + y3) * 0.5f;
			var x34 = (x3 + x4) * 0.5f;
			var y34 = (y3 + y4) * 0.5f;
			var x123 = (x12 + x23) * 0.5f;
			var y123 = (y12 + y23) * 0.5f;
			var dx = x4 - x1;
			var dy = y4 - y1;
			var d2 = Math.Abs((x2 - x4) * dy - (y2 - y4) * dx);
			var d3 = Math.Abs((x3 - x4) * dy - (y3 - y4) * dx);
			if ((d2 + d3) * (d2 + d3) < tessTol * (dx * dx + dy * dy)) {
				AddPoint(x4, y4, type);
				return;
			}
			var x234 = (x23 + x34) * 0.5f;
			var y234 = (y23 + y34) * 0.5f;
			var x1234 = (x123 + x234) * 0.5f;
			var y1234 = (y123 + y234) * 0.5f;
			TesselateBezier(x1, y1, x12, y12, x123, y123, x1234, y1234, level + 1, 0);
			TesselateBezier(x1234, y1234, x234, y234, x34, y34, x4, y4, level + 1, type);
		}

		private void FlattenPaths()
		{
			Point* last;
			Point* p0;
			Point* p1;
			Point* pts;
			Path path;
			var i = 0;
			var j = 0;
			float* cp1;
			float* cp2;
			float* p;
			float area = 0;
			if (cache.Paths.Count > 0) {
				return;
			}
			i = 0;
			while (i < commandsNumber) {
				var cmd = commands[i];
				switch ((CommandType)cmd) {
					case CommandType.MoveTo:
						AddPath();
						p = &commands[i + 1];
						AddPoint(p[0], p[1], PointFlags.Corner);
						i += 3;
						break;
					case CommandType.LineTo:
						p = &commands[i + 1];
						AddPoint(p[0], p[1], PointFlags.Corner);
						i += 3;
						break;
					case CommandType.BezierTo:
						last = LastPoint();
						if (last != null) {
							cp1 = &commands[i + 1];
							cp2 = &commands[i + 3];
							p = &commands[i + 5];
							TesselateBezier(
								last->X, last->Y, cp1[0], cp1[1], cp2[0], cp2[1], p[0], p[1], 0, PointFlags.Corner);
						}
						i += 7;
						break;
					case CommandType.Close:
						InternalClosePath();
						i++;
						break;
					case CommandType.Winding:
						PathWinding((Winding)commands[i + 1]);
						i += 2;
						break;
					default:
						i++;
						break;
				}
			}
			cache.Bounds.AX = cache.Bounds.AY = 1e6f;
			cache.Bounds.BX = cache.Bounds.BY = -1e6f;
			for (j = 0; j < cache.Paths.Count; j++) {
				path = cache.Paths[j];
				pts = &cache.Points[path.First];
				p0 = &pts[path.Count - 1];
				p1 = &pts[0];
				if (PointsAreEquals(p0->X, p0->Y, p1->X, p1->Y, distTol) != 0) {
					path.Count--;
					p0 = &pts[path.Count - 1];
					path.Closed = 1;
				}
				if (path.Count > 2) {
					area = PolyArea(pts, path.Count);
					if (path.Winding == Winding.CounterClockWise && area < 0.0f) {
						PolyReverse(pts, path.Count);
					}
					if (path.Winding == Winding.ClockWise && area > 0.0f) {
						PolyReverse(pts, path.Count);
					}
				}
				for (i = 0; i < path.Count; i++) {
					p0->DeltaX = p1->X - p0->X;
					p0->DeltaY = p1->Y - p0->Y;
					p0->Length = Normalize(ref p0->DeltaX, ref p0->DeltaY);
					cache.Bounds.AX = Math.Min(cache.Bounds.AX, p0->X);
					cache.Bounds.AY = Math.Min(cache.Bounds.AY, p0->Y);
					cache.Bounds.BX = Math.Max(cache.Bounds.BX, p0->X);
					cache.Bounds.BY = Math.Max(cache.Bounds.BY, p0->Y);
					p0 = p1++;
				}
			}
		}

		private void CalculateJoins(float w, LineCap lineJoin, float miterLimit)
		{
			var iw = 0.0f;
			if (w > 0.0f) {
				iw = 1.0f / w;
			}
			for (var i = 0; i < cache.Paths.Count; i++) {
				var path = cache.Paths[i];
				var pts = &cache.Points[path.First];
				var p0 = &pts[path.Count - 1];
				var p1 = &pts[0];
				var nleft = 0;
				path.BevelCount = 0;
				for (var j = 0; j < path.Count; j++) {
					var dlx0 = p0->DeltaY;
					var dly0 = -p0->DeltaX;
					var dlx1 = p1->DeltaY;
					var dly1 = -p1->DeltaX;
					p1->Dmx = (dlx0 + dlx1) * 0.5f;
					p1->Dmy = (dly0 + dly1) * 0.5f;
					var dmr2 = p1->Dmx * p1->Dmx + p1->Dmy * p1->Dmy;
					if (dmr2 > 0.000001f) {
						var scale = 1.0f / dmr2;
						if (scale > 600.0f) {
							scale = 600.0f;
						}
						p1->Dmx *= scale;
						p1->Dmy *= scale;
					}
					p1->Flags = (byte)((p1->Flags & (byte)PointFlags.Corner) != 0 ? PointFlags.Corner : 0);
					var cross = p1->DeltaX * p0->DeltaY - p0->DeltaX * p1->DeltaY;
					if (cross > 0.0f) {
						nleft++;
						p1->Flags |= (byte)PointFlags.Left;
					}
					var limit = Math.Max(1.01f, Math.Min(p0->Length, p1->Length) * iw);
					if (dmr2 * limit * limit < 1.0f) {
						p1->Flags |= (byte)PointFlags.InnerBevel;
					}
					if ((p1->Flags & (byte)PointFlags.Corner) != 0) {
						if (
							dmr2 * miterLimit * miterLimit < 1.0f
							|| lineJoin == Lime.LineCap.Bevel
							|| lineJoin == Lime.LineCap.Round
						) {
							p1->Flags |= (byte)PointFlags.Bevel;
						}
					}
					if ((p1->Flags & (byte)(PointFlags.Bevel | PointFlags.InnerBevel)) != 0) {
						path.BevelCount++;
					}
					p0 = p1++;
				}
				path.Convex = nleft == path.Count ? 1 : 0;
			}
		}

		private int ExpandStroke(float w, float fringe, LineCap lineCap, LineCap lineJoin, float miterLimit)
		{
			var cverts = 0;
			var i = 0;
			var j = 0;
			var aa = fringe;
			var u0 = 0.0f;
			var u1 = 1.0f;
			var ncap = CurveDivs(w, (float)3.14159274, tessTol);
			w += aa * 0.5f;
			if (aa == 0.0f) {
				u0 = 0.5f;
				u1 = 0.5f;
			}
			CalculateJoins(w, lineJoin, miterLimit);
			cverts = 0;
			for (i = 0; i < cache.Paths.Count; i++) {
				var path = cache.Paths[i];
				var loop = path.Closed == 0 ? 0 : 1;
				if (lineJoin == Lime.LineCap.Round) {
					cverts += (path.Count + path.BevelCount * (ncap + 2) + 1) * 2;
				} else {
					cverts += (path.Count + path.BevelCount * 5 + 1) * 2;
				}
				if (loop == 0) {
					if (lineCap == Lime.LineCap.Round) {
						cverts += (ncap * 2 + 2) * 2;
					} else {
						cverts += (3 + 3) * 2;
					}
				}
			}
			var verts = AllocTempVerts(cverts);
			for (i = 0; i < cache.Paths.Count; i++) {
				var path = cache.Paths[i];
				var pts = &cache.Points[path.First];
				Point* p0;
				Point* p1;
				var s = 0;
				var e = 0;
				var loop = 0;
				float dx = 0;
				float dy = 0;
				path.Fill = null;
				loop = path.Closed == 0 ? 0 : 1;
				fixed (Vertex* dst2 = &verts.Array[verts.Offset]) {
					var dst = dst2;
					if (loop != 0) {
						p0 = &pts[path.Count - 1];
						p1 = &pts[0];
						s = 0;
						e = path.Count;
					} else {
						p0 = &pts[0];
						p1 = &pts[1];
						s = 1;
						e = path.Count - 1;
					}
					if (loop == 0) {
						dx = p1->X - p0->X;
						dy = p1->Y - p0->Y;
						Normalize(ref dx, ref dy);
						if (lineCap == Lime.LineCap.Butt) {
							dst = ButtCapStart(dst, p0, dx, dy, w, -aa * 0.5f, aa, u0, u1);
						} else if (lineCap == Lime.LineCap.Butt || lineCap == Lime.LineCap.Square) {
							dst = ButtCapStart(dst, p0, dx, dy, w, w - aa, aa, u0, u1);
						} else if (lineCap == Lime.LineCap.Round) {
							dst = RoundCapStart(dst, p0, dx, dy, w, ncap, aa, u0, u1);
						}
					}
					for (j = s; j < e; ++j) {
						if ((p1->Flags & (byte)(PointFlags.Bevel | PointFlags.InnerBevel)) != 0) {
							if (lineJoin == Lime.LineCap.Round) {
								dst = RoundJoin(dst, p0, p1, w, w, u0, u1, ncap, aa);
							} else {
								dst = BevelJoin(dst, p0, p1, w, w, u0, u1, aa);
							}
						} else {
							SetVertex(dst, p1->X + p1->Dmx * w, p1->Y + p1->Dmy * w, u0, 1);
							dst++;
							SetVertex(dst, p1->X - p1->Dmx * w, p1->Y - p1->Dmy * w, u1, 1);
							dst++;
						}
						p0 = p1++;
					}
					if (loop != 0) {
						SetVertex(dst, verts.Array[verts.Offset].Pos4.X, verts.Array[verts.Offset].Pos4.Y, u0, 1);
						dst++;
						SetVertex(
							dst, verts.Array[verts.Offset + 1].Pos4.X, verts.Array[verts.Offset + 1].Pos4.Y, u1, 1);
						dst++;
					} else {
						dx = p1->X - p0->X;
						dy = p1->Y - p0->Y;
						Normalize(ref dx, ref dy);
						if (lineCap == Lime.LineCap.Butt) {
							dst = ButtCapEnd(dst, p1, dx, dy, w, -aa * 0.5f, aa, u0, u1);
						} else if (lineCap == Lime.LineCap.Butt || lineCap == Lime.LineCap.Square) {
							dst = ButtCapEnd(dst, p1, dx, dy, w, w - aa, aa, u0, u1);
						} else if (lineCap == Lime.LineCap.Round) {
							dst = RoundCapEnd(dst, p1, dx, dy, w, ncap, aa, u0, u1);
						}
					}
					path.Stroke = new ArraySegment<Vertex>(verts.Array, verts.Offset, (int)(dst - dst2));
					var newPos = verts.Offset + path.Stroke.Value.Count;
					verts = new ArraySegment<Vertex>(verts.Array, newPos, verts.Array.Length - newPos);
				}
			}
			return 1;
		}

		private int ExpandFill(float w, LineCap lineJoin, float miterLimit)
		{
			var i = 0;
			var j = 0;
			var aa = fringeWidth;
			var fringe = w > 0.0f ? 1 : 0;
			CalculateJoins(w, lineJoin, miterLimit);
			var cverts = 0;
			for (i = 0; i < cache.Paths.Count; i++) {
				var path = cache.Paths[i];
				cverts += path.Count + path.BevelCount + 1;
				if (fringe != 0) {
					cverts += (path.Count + path.BevelCount * 5 + 1) * 2;
				}
			}
			var verts = AllocTempVerts(cverts);
			var convex = cache.Paths.Count == 1 && cache.Paths[0].Convex != 0 ? 1 : 0;
			for (i = 0; i < cache.Paths.Count; i++) {
				var path = cache.Paths[i];
				var pts = &cache.Points[path.First];
				Point* p0;
				Point* p1;
				float woff = 0;
				woff = 0.5f * aa;
				fixed (Vertex* dst2 = &verts.Array[verts.Offset]) {
					var dst = dst2;
					if (fringe != 0) {
						p0 = &pts[path.Count - 1];
						p1 = &pts[0];
						for (j = 0; j < path.Count; ++j) {
							if ((p1->Flags & (byte)PointFlags.Bevel) != 0) {
								var dlx0 = p0->DeltaY;
								var dly0 = -p0->DeltaX;
								var dlx1 = p1->DeltaY;
								var dly1 = -p1->DeltaX;
								if ((p1->Flags & (byte)PointFlags.Left) != 0) {
									var lx = p1->X + p1->Dmx * woff;
									var ly = p1->Y + p1->Dmy * woff;
									SetVertex(dst, lx, ly, 0.5f, 1);
									dst++;
								} else {
									var lx0 = p1->X + dlx0 * woff;
									var ly0 = p1->Y + dly0 * woff;
									var lx1 = p1->X + dlx1 * woff;
									var ly1 = p1->Y + dly1 * woff;
									SetVertex(dst, lx0, ly0, 0.5f, 1);
									dst++;
									SetVertex(dst, lx1, ly1, 0.5f, 1);
									dst++;
								}
							} else {
								SetVertex(dst, p1->X + p1->Dmx * woff, p1->Y + p1->Dmy * woff, 0.5f, 1);
								dst++;
							}
							p0 = p1++;
						}
					} else {
						for (j = 0; j < path.Count; ++j) {
							SetVertex(dst, pts[j].X, pts[j].Y, 0.5f, 1);
							dst++;
						}
					}
					path.Fill = new ArraySegment<Vertex>(verts.Array, verts.Offset, (int)(dst - dst2));
					var newPos = verts.Offset + path.Fill.Value.Count;
					verts = new ArraySegment<Vertex>(verts.Array, newPos, verts.Array.Length - newPos);
				}
				if (fringe != 0) {
					var lw = w + woff;
					var rw = w - woff;
					float lu = 0;
					float ru = 1;
					fixed (Vertex* dst2 = &verts.Array[verts.Offset]) {
						var dst = dst2;
						if (convex != 0) {
							lw = woff;
							lu = 0.5f;
						}
						p0 = &pts[path.Count - 1];
						p1 = &pts[0];
						for (j = 0; j < path.Count; ++j) {
							if ((p1->Flags & (byte)(PointFlags.Bevel | PointFlags.InnerBevel)) != 0) {
								dst = BevelJoin(dst, p0, p1, lw, rw, lu, ru, fringeWidth);
							} else {
								SetVertex(dst, p1->X + p1->Dmx * lw, p1->Y + p1->Dmy * lw, lu, 1);
								dst++;
								SetVertex(dst, p1->X - p1->Dmx * rw, p1->Y - p1->Dmy * rw, ru, 1);
								dst++;
							}
							p0 = p1++;
						}
						SetVertex(dst, verts.Array[verts.Offset].Pos4.X, verts.Array[verts.Offset].Pos4.Y, lu, 1);
						dst++;
						SetVertex(
							dst, verts.Array[verts.Offset + 1].Pos4.X, verts.Array[verts.Offset + 1].Pos4.Y, ru, 1);
						dst++;
						path.Stroke = new ArraySegment<Vertex>(verts.Array, verts.Offset, (int)(dst - dst2));
						var newPos = verts.Offset + path.Stroke.Value.Count;
						verts = new ArraySegment<Vertex>(verts.Array, newPos, verts.Array.Length - newPos);
					}
				} else {
					path.Stroke = null;
				}
			}
			return 1;
		}

		private static float Triarea2(float ax, float ay, float bx, float by, float cx, float cy)
		{
			var abx = bx - ax;
			var aby = by - ay;
			var acx = cx - ax;
			var acy = cy - ay;
			return acx * aby - abx * acy;
		}

		private static float PolyArea(Point* pts, int npts)
		{
			var area = (float)0;
			for (int i = 2; i < npts; i++) {
				var a = &pts[0];
				var b = &pts[i - 1];
				var c = &pts[i];
				area += Triarea2(a->X, a->Y, b->X, b->Y, c->X, c->Y);
			}
			return area * 0.5f;
		}

		internal static void PolyReverse(Point* pts, int npts)
		{
			var tmp = new Point();
			var i = 0;
			var j = npts - 1;
			while (i < j) {
				tmp = pts[i];
				pts[i] = pts[j];
				pts[j] = tmp;
				i++;
				j--;
			}
		}

		private static void SetVertex(Vertex* vtx, float x, float y, float u, float v)
		{
			SetVertex(ref *vtx, x, y, u, v);
		}

		private static void SetVertex(ref Vertex vtx, float x, float y, float u, float v)
		{
			vtx.Pos4.X = x;
			vtx.Pos4.Y = y;
			vtx.UV1.X = u;
			vtx.UV1.Y = v;
		}

		private static void IntersectRectangles(
			float* dst,
			float ax,
			float ay,
			float aw,
			float ah,
			float bx,
			float by,
			float bw,
			float bh
		) {
			var minx = Math.Max(ax, bx);
			var miny = Math.Max(ay, by);
			var maxx = Math.Min(ax + aw, bx + bw);
			var maxy = Math.Min(ay + ah, by + bh);
			dst[0] = minx;
			dst[1] = miny;
			dst[2] = Math.Max(0.0f, maxx - minx);
			dst[3] = Math.Max(0.0f, maxy - miny);
		}

		private static float GetAverageScale(ref Matrix32 t)
		{
			return (t.U.Length + t.V.Length) * 0.5f;
		}

		private static int CurveDivs(float r, float arc, float tol)
		{
			var da = MathF.Acos(r / (r + tol)) * 2.0f;
			return Math.Max(2, (int)MathF.Ceiling(arc / da));
		}

		private static void ChooseBevel(
			int bevel,
			Point* p0,
			Point* p1,
			float w,
			float* x0,
			float* y0,
			float* x1,
			float* y1
		) {
			if (bevel != 0) {
				*x0 = p1->X + p0->DeltaY * w;
				*y0 = p1->Y - p0->DeltaX * w;
				*x1 = p1->X + p1->DeltaY * w;
				*y1 = p1->Y - p1->DeltaX * w;
			} else {
				*x0 = p1->X + p1->Dmx * w;
				*y0 = p1->Y + p1->Dmy * w;
				*x1 = p1->X + p1->Dmx * w;
				*y1 = p1->Y + p1->Dmy * w;
			}
		}

		private static Vertex* RoundJoin(
			Vertex* dst, Point* p0, Point* p1, float lw, float rw, float lu, float ru, int ncap, float fringe)
		{
			var dlx0 = p0->DeltaY;
			var dly0 = -p0->DeltaX;
			var dlx1 = p1->DeltaY;
			var dly1 = -p1->DeltaX;
			if ((p1->Flags & (byte)PointFlags.Left) != 0) {
				float lx0 = 0;
				float ly0 = 0;
				float lx1 = 0;
				float ly1 = 0;
				float a0 = 0;
				float a1 = 0;
				ChooseBevel(p1->Flags & (byte)PointFlags.InnerBevel, p0, p1, lw, &lx0, &ly0, &lx1, &ly1);
				a0 = MathF.Atan2(-dly0, -dlx0);
				a1 = MathF.Atan2(-dly1, -dlx1);
				if (a1 > a0) {
					a1 -= (float)(3.14159274 * 2);
				}
				SetVertex(dst, lx0, ly0, lu, 1);
				dst++;
				SetVertex(dst, p1->X - dlx0 * rw, p1->Y - dly0 * rw, ru, 1);
				dst++;
				var n = Mathf.Clamp((int)MathF.Ceiling((float)((a0 - a1) / 3.14159274 * ncap)), 2, ncap);
				for (var i = 0; i < n; i++) {
					var u = i / (float)(n - 1);
					var a = a0 + u * (a1 - a0);
					var rx = p1->X + MathF.Cos(a) * rw;
					var ry = p1->Y + MathF.Sin(a) * rw;
					SetVertex(dst, p1->X, p1->Y, 0.5f, 1);
					dst++;
					SetVertex(dst, rx, ry, ru, 1);
					dst++;
				}
				SetVertex(dst, lx1, ly1, lu, 1);
				dst++;
				SetVertex(dst, p1->X - dlx1 * rw, p1->Y - dly1 * rw, ru, 1);
				dst++;
			} else {
				float rx0 = 0;
				float ry0 = 0;
				float rx1 = 0;
				float ry1 = 0;
				float a0 = 0;
				float a1 = 0;
				ChooseBevel(p1->Flags & (byte)PointFlags.InnerBevel, p0, p1, -rw, &rx0, &ry0, &rx1, &ry1);
				a0 = MathF.Atan2(dly0, dlx0);
				a1 = MathF.Atan2(dly1, dlx1);
				if (a1 < a0) {
					a1 += (float)(3.14159274 * 2);
				}
				SetVertex(dst, p1->X + dlx0 * rw, p1->Y + dly0 * rw, lu, 1);
				dst++;
				SetVertex(dst, rx0, ry0, ru, 1);
				dst++;
				var n = Mathf.Clamp((int)MathF.Ceiling((float)((a1 - a0) / 3.14159274 * ncap)), 2, ncap);
				for (var i = 0; i < n; i++) {
					var u = i / (float)(n - 1);
					var a = a0 + u * (a1 - a0);
					var lx = p1->X + MathF.Cos(a) * lw;
					var ly = p1->Y + MathF.Sin(a) * lw;
					SetVertex(dst, lx, ly, lu, 1);
					dst++;
					SetVertex(dst, p1->X, p1->Y, 0.5f, 1);
					dst++;
				}
				SetVertex(dst, p1->X + dlx1 * rw, p1->Y + dly1 * rw, lu, 1);
				dst++;
				SetVertex(dst, rx1, ry1, ru, 1);
				dst++;
			}
			return dst;
		}

		private static Vertex* BevelJoin(
			Vertex* dst,
			Point* p0,
			Point* p1,
			float lw,
			float rw,
			float lu,
			float ru,
			float fringe
		) {
			float rx0 = 0;
			float ry0 = 0;
			float rx1 = 0;
			float ry1 = 0;
			float lx0 = 0;
			float ly0 = 0;
			float lx1 = 0;
			float ly1 = 0;
			var dlx0 = p0->DeltaY;
			var dly0 = -p0->DeltaX;
			var dlx1 = p1->DeltaY;
			var dly1 = -p1->DeltaX;
			if ((p1->Flags & (byte)PointFlags.Left) != 0) {
				ChooseBevel(p1->Flags & (byte)PointFlags.InnerBevel, p0, p1, lw, &lx0, &ly0, &lx1, &ly1);
				SetVertex(dst, lx0, ly0, lu, 1);
				dst++;
				SetVertex(dst, p1->X - dlx0 * rw, p1->Y - dly0 * rw, ru, 1);
				dst++;
				if ((p1->Flags & (byte)PointFlags.Bevel) != 0) {
					SetVertex(dst, lx0, ly0, lu, 1);
					dst++;
					SetVertex(dst, p1->X - dlx0 * rw, p1->Y - dly0 * rw, ru, 1);
					dst++;
					SetVertex(dst, lx1, ly1, lu, 1);
					dst++;
					SetVertex(dst, p1->X - dlx1 * rw, p1->Y - dly1 * rw, ru, 1);
					dst++;
				} else {
					rx0 = p1->X - p1->Dmx * rw;
					ry0 = p1->Y - p1->Dmy * rw;
					SetVertex(dst, p1->X, p1->Y, 0.5f, 1);
					dst++;
					SetVertex(dst, p1->X - dlx0 * rw, p1->Y - dly0 * rw, ru, 1);
					dst++;
					SetVertex(dst, rx0, ry0, ru, 1);
					dst++;
					SetVertex(dst, rx0, ry0, ru, 1);
					dst++;
					SetVertex(dst, p1->X, p1->Y, 0.5f, 1);
					dst++;
					SetVertex(dst, p1->X - dlx1 * rw, p1->Y - dly1 * rw, ru, 1);
					dst++;
				}
				SetVertex(dst, lx1, ly1, lu, 1);
				dst++;
				SetVertex(dst, p1->X - dlx1 * rw, p1->Y - dly1 * rw, ru, 1);
				dst++;
			} else {
				ChooseBevel(p1->Flags & (byte)PointFlags.InnerBevel, p0, p1, -rw, &rx0, &ry0, &rx1, &ry1);
				SetVertex(dst, p1->X + dlx0 * lw, p1->Y + dly0 * lw, lu, 1);
				dst++;
				SetVertex(dst, rx0, ry0, ru, 1);
				dst++;
				if ((p1->Flags & (byte)PointFlags.Bevel) != 0) {
					SetVertex(dst, p1->X + dlx0 * lw, p1->Y + dly0 * lw, lu, 1);
					dst++;
					SetVertex(dst, rx0, ry0, ru, 1);
					dst++;
					SetVertex(dst, p1->X + dlx1 * lw, p1->Y + dly1 * lw, lu, 1);
					dst++;
					SetVertex(dst, rx1, ry1, ru, 1);
					dst++;
				} else {
					lx0 = p1->X + p1->Dmx * lw;
					ly0 = p1->Y + p1->Dmy * lw;
					SetVertex(dst, p1->X + dlx0 * lw, p1->Y + dly0 * lw, lu, 1);
					dst++;
					SetVertex(dst, p1->X, p1->Y, 0.5f, 1);
					dst++;
					SetVertex(dst, lx0, ly0, lu, 1);
					dst++;
					SetVertex(dst, lx0, ly0, lu, 1);
					dst++;
					SetVertex(dst, p1->X + dlx1 * lw, p1->Y + dly1 * lw, lu, 1);
					dst++;
					SetVertex(dst, p1->X, p1->Y, 0.5f, 1);
					dst++;
				}
				SetVertex(dst, p1->X + dlx1 * lw, p1->Y + dly1 * lw, lu, 1);
				dst++;
				SetVertex(dst, rx1, ry1, ru, 1);
				dst++;
			}
			return dst;
		}

		private static Vertex* ButtCapStart(
			Vertex* dst,
			Point* p,
			float dx,
			float dy,
			float w,
			float d,
			float aa,
			float u0,
			float u1
		) {
			var px = p->X - dx * d;
			var py = p->Y - dy * d;
			var dlx = dy;
			var dly = -dx;
			SetVertex(dst, px + dlx * w - dx * aa, py + dly * w - dy * aa, u0, 0);
			dst++;
			SetVertex(dst, px - dlx * w - dx * aa, py - dly * w - dy * aa, u1, 0);
			dst++;
			SetVertex(dst, px + dlx * w, py + dly * w, u0, 1);
			dst++;
			SetVertex(dst, px - dlx * w, py - dly * w, u1, 1);
			dst++;
			return dst;
		}

		private static Vertex* ButtCapEnd(
			Vertex* dst,
			Point* p,
			float dx,
			float dy,
			float w,
			float d,
			float aa,
			float u0,
			float u1
		) {
			var px = p->X + dx * d;
			var py = p->Y + dy * d;
			var dlx = dy;
			var dly = -dx;
			SetVertex(dst, px + dlx * w, py + dly * w, u0, 1);
			dst++;
			SetVertex(dst, px - dlx * w, py - dly * w, u1, 1);
			dst++;
			SetVertex(dst, px + dlx * w + dx * aa, py + dly * w + dy * aa, u0, 0);
			dst++;
			SetVertex(dst, px - dlx * w + dx * aa, py - dly * w + dy * aa, u1, 0);
			dst++;
			return dst;
		}

		private static Vertex* RoundCapStart(
			Vertex* dst,
			Point* p,
			float dx,
			float dy,
			float w,
			int ncap,
			float aa,
			float u0,
			float u1
		) {
			var px = p->X;
			var py = p->Y;
			var dlx = dy;
			var dly = -dx;
			for (var i = 0; i < ncap; i++) {
				var a = (float)(i / (float)(ncap - 1) * 3.14159274);
				var ax = MathF.Cos(a) * w;
				var ay = MathF.Sin(a) * w;
				SetVertex(dst, px - dlx * ax - dx * ay, py - dly * ax - dy * ay, u0, 1);
				dst++;
				SetVertex(dst, px, py, 0.5f, 1);
				dst++;
			}
			SetVertex(dst, px + dlx * w, py + dly * w, u0, 1);
			dst++;
			SetVertex(dst, px - dlx * w, py - dly * w, u1, 1);
			dst++;
			return dst;
		}

		private static Vertex* RoundCapEnd(
			Vertex* dst,
			Point* p,
			float dx,
			float dy,
			float w,
			int ncap,
			float aa,
			float u0,
			float u1
		) {
			var px = p->X;
			var py = p->Y;
			var dlx = dy;
			var dly = -dx;
			SetVertex(dst, px + dlx * w, py + dly * w, u0, 1);
			dst++;
			SetVertex(dst, px - dlx * w, py - dly * w, u1, 1);
			dst++;
			for (var i = 0; i < ncap; i++) {
				var a = (float)(i / (float)(ncap - 1) * 3.14159274);
				var ax = MathF.Cos(a) * w;
				var ay = MathF.Sin(a) * w;
				SetVertex(dst, px, py, 0.5f, 1);
				dst++;
				SetVertex(dst, px - dlx * ax + dx * ay, py - dly * ax + dy * ay, u0, 1);
				dst++;
			}
			return dst;
		}

		private static int PointsAreEquals(float x1, float y1, float x2, float y2, float tol)
		{
			var dx = x2 - x1;
			var dy = y2 - y1;
			return dx * dx + dy * dy < tol * tol ? 1 : 0;
		}

		private static float CalcDistanceFromPointToSegment(float x, float y, float px, float py, float qx, float qy)
		{
			var pqx = qx - px;
			var pqy = qy - py;
			var dx = x - px;
			var dy = y - py;
			var d = pqx * pqx + pqy * pqy;
			var t = pqx * dx + pqy * dy;
			if (d > 0) {
				t /= d;
			}
			if (t < 0) {
				t = 0;
			} else if (t > 1) {
				t = 1;
			}
			dx = px + t * pqx - x;
			dy = py + t * pqy - y;
			return dx * dx + dy * dy;
		}

		private static float Cross(float dx0, float dy0, float dx1, float dy1)
		{
			return dx1 * dy0 - dx0 * dy1;
		}

		private static float Normalize(ref float x, ref float y)
		{
			float d = MathF.Sqrt(x * x + y * y);
			if (d > 1e-6f) {
				var id = 1.0f / d;
				x *= id;
				y *= id;
			}
			return d;
		}
	}
}
