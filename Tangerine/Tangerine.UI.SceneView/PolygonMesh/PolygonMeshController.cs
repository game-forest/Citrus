using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Lime.PolygonMesh.Topology;
using Lime.PolygonMesh.Utils;
using Lime.Widgets.PolygonMesh.Topology;
using Tangerine.Core;
using Vertex = Lime.Vertex;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	internal static class TopologyPrimitiveExtensions
	{
		public static bool IsVertex(this ITopologyPrimitive self) => self.Count == 1;

		public static bool IsEdge(this ITopologyPrimitive self) => self.Count == 2;

		public static bool IsFace(this ITopologyPrimitive self) => self.Count == 3;
	}

	[YuzuDontGenerateDeserializer]
	[NodeComponentDontSerialize]
	[AllowedComponentOwnerTypes(typeof(Lime.Widgets.PolygonMesh.PolygonMesh))]
	public abstract class PolygonMeshController : NodeComponent
	{
		protected static SceneView sv => SceneView.Instance;

		public struct PolygonMeshSlice
		{
			public PolygonMeshTools.ModificationState State;
			public List<Vertex> Vertices;
			public List<Face> IndexBuffer;
			public List<Edge> ConstrainedVertices;
			public List<IKeyframe> Keyframes;
		}

		protected abstract bool ValidateHitTestResult(TopologyHitTestResult result, bool ignoreState);

		public abstract void Render(Widget renderContext);
		public abstract bool HitTest(Vector2 position, float scale, bool ignoreState = false);
		public abstract IEnumerator<object> AnimationTask();
		public abstract IEnumerator<object> TriangulationTask();
		public abstract IEnumerator<object> CreationTask();
		public abstract IEnumerator<object> RemovalTask();
		public abstract IEnumerator<object> ConcaveTask();
	}

	[YuzuDontGenerateDeserializer]
	[AllowedComponentOwnerTypes(typeof(Lime.Widgets.PolygonMesh.PolygonMesh))]
	public abstract class TopologyController : PolygonMeshController
	{
		public Lime.Widgets.PolygonMesh.PolygonMesh Mesh { get; protected set; }

		public ITopology Topology { get; protected set; }

		public IList<Vertex> Vertices => PolygonMeshTools.Mode == PolygonMeshTools.ModificationMode.Animation
			? Mesh.TransientVertices
			: Mesh.Vertices;
	}

	[YuzuDontGenerateDeserializer]
	[AllowedComponentOwnerTypes(typeof(Lime.Widgets.PolygonMesh.PolygonMesh))]
	public sealed class TopologyController<T> : TopologyController where T : ITopology
	{
		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (Owner is Lime.Widgets.PolygonMesh.PolygonMesh mesh) {
				if (mesh.Vertices.Count == 0) {
					mesh.Vertices.Add(new Vertex {
						Pos = Vector2.Zero,
						UV1 = Vector2.Zero,
						Color = mesh.Color
					});
					mesh.Vertices.Add(new Vertex {
						Pos = new Vector2(1.0f, 0.0f),
						UV1 = new Vector2(1.0f, 0.0f),
						Color = mesh.Color
					});
					mesh.Vertices.Add(new Vertex {
						Pos = new Vector2(0.0f, 1.0f),
						UV1 = new Vector2(0.0f, 1.0f),
						Color = mesh.Color
					});
					mesh.Vertices.Add(new Vertex {
						Pos = Vector2.One,
						UV1 = Vector2.One,
						Color = mesh.Color
					});
				}
				Topology = (T)Activator.CreateInstance(typeof(T), mesh.Vertices);
				if (mesh.Faces.Count > 0) {
					Topology.Sync(mesh.Vertices, mesh.ConstrainedEdges, mesh.Faces);
				}
				Mesh = mesh;
				Mesh.Faces.AddRange(Topology.Faces);
				Topology.OnTopologyChanged += UpdateMeshFaces;
				if (mesh.TransientVertices == null) {
					mesh.TransientVertices = new List<Vertex>(mesh.Vertices);
				}
				PolygonMeshTools.State = PolygonMeshTools.ModificationState.Animation;
			} else if (oldOwner != null) {
				oldOwner.Components.Remove(this);
				Topology.OnTopologyChanged -= UpdateMeshFaces;
			}
		}

		private void UpdateMeshFaces(ITopology topology)
		{
			if (Mesh != null) {
				Mesh.Faces.Clear();
				Mesh.ConstrainedEdges.Clear();
				Mesh.Faces.AddRange(topology.Faces);
				foreach (var edge in topology.ConstrainedEdges) {
					Mesh.ConstrainedEdges.Add(new Edge((ushort)edge.Item1, (ushort)edge.Item2));
				}
			}
		}

		private bool HitTest(Vector2 position, float scale, out TopologyHitTestResult result)
		{
			if (PolygonMeshTools.Mode == PolygonMeshTools.ModificationMode.Animation) {
				Mesh.Update();
			}
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			position = transform.TransformVector(position);
			var normalizedPosition = position / Mesh.Size;
			var vertexHitRadius = Theme.Metrics.PolygonMeshVertexHitTestRadius / scale / Mesh.Size.Length;
			var edgeHitRadius = Theme.Metrics.PolygonMeshEdgeHitTestRadius / scale / Mesh.Size.Length;
			return Topology.HitTest(normalizedPosition, vertexHitRadius, edgeHitRadius, out result);
		}

		protected override bool ValidateHitTestResult(TopologyHitTestResult result, bool ignoreState)
		{
			if (result == null) {
				return false;
			}

			if (ignoreState) {
				return true;
			}

			switch (PolygonMeshTools.State) {
				case PolygonMeshTools.ModificationState.Animation:
				case PolygonMeshTools.ModificationState.Creation:
					return true;
				case PolygonMeshTools.ModificationState.Triangulation:
				case PolygonMeshTools.ModificationState.Removal:
					return result.Target.IsVertex();
				default:
					return false;
			}
		}

		public override bool HitTest(Vector2 position, float scale, bool ignoreState = false)
		{
			HitTest(position, scale, out var result);
			return ValidateHitTestResult(result, ignoreState);
		}

		public override void Render(Widget renderContext)
		{
			if (PolygonMeshTools.Mode == PolygonMeshTools.ModificationMode.Animation) {
				Mesh.Update();
			}
			var transform = Mesh.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(renderContext);
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var hitTestResult);
			var isHitTestSuccessful = ValidateHitTestResult(hitTestResult, ignoreState: false);
			var hitTestTarget = isHitTestSuccessful ? hitTestResult.Target : null;
			if (hitTestTarget != null && hitTestTarget.IsFace()) {
				Utils.RenderTriangle(
					transform.TransformVector(Vertices[hitTestTarget[0]].Pos * Mesh.Size),
					transform.TransformVector(Vertices[hitTestTarget[1]].Pos * Mesh.Size),
					transform.TransformVector(Vertices[hitTestTarget[2]].Pos * Mesh.Size),
					PolygonMeshTools.State == PolygonMeshTools.ModificationState.Removal ?
						Theme.Colors.PolygonMeshRemovalColor :
						Theme.Colors.PolygonMeshHoverColor
				);
			}
			var isHitTestResultTargetEdge = hitTestTarget != null && hitTestTarget.IsEdge();
			var isHitTestResultTargetVertex = hitTestTarget != null && hitTestTarget.IsVertex();
			foreach (var (face, info) in Topology.FacesWithInfo) {
				var prevVertex = transform.TransformVector(Vertices[face.Index0].Pos * Mesh.Size);
				for (int i = 0; i < 3; i++) {
					var prevVertexIndex = face[i];
					var nextVertexIndex = face[(i + 1) % 3];
					var nextVertex = transform.TransformVector(Vertices[nextVertexIndex].Pos * Mesh.Size);
					var edgeInfo = info[i];
					var v1 = prevVertex;
					var v2 = nextVertex;
					// Keep the same render order for each edge.
					if (v2.X < v1.X || (v2.X == v1.X && v2.Y < v1.Y)) {
						Toolbox.Swap(ref v1, ref v2);
					}
					var isEdgeHovered = isHitTestResultTargetEdge &&
										(hitTestTarget[0] == prevVertexIndex && hitTestTarget[1] == nextVertexIndex ||
										hitTestTarget[0] == nextVertexIndex && hitTestTarget[1] == prevVertexIndex);
					var isEdgePossiblyWillBeRemoved =
						isHitTestResultTargetVertex &&
						PolygonMeshTools.State == PolygonMeshTools.ModificationState.Removal &&
						(hitTestTarget[0] == prevVertexIndex || hitTestTarget[0] == nextVertexIndex);
					if (isEdgeHovered || isEdgePossiblyWillBeRemoved) {
						RenderEdgeHovered(v1, v2, edgeInfo.IsFraming, edgeInfo.IsConstrained);
					} else {
						RenderEdge(v1, v2, edgeInfo.IsFraming, edgeInfo.IsConstrained);
					}
					prevVertex = nextVertex;
				}
			}
			for (int i = 0; i < Vertices.Count; i++) {
				var v = transform.TransformVector(Vertices[i].Pos * Mesh.Size);
				if (isHitTestResultTargetVertex && i == hitTestTarget[0]) {
					RenderVertexHovered(v);
				} else {
					RenderVertex(v);
				}
			}
			if (PolygonMeshTools.State == PolygonMeshTools.ModificationState.Creation && hitTestTarget != null && !hitTestTarget.IsVertex()) {
				Utils.RenderVertex(
					transform.TransformVector(SnapMousePositionToTopologyPrimitiveIfPossible(hitTestTarget) * Mesh.Size),
					Theme.Metrics.PolygonMeshBackgroundVertexRadius,
					Theme.Metrics.PolygonMeshVertexRadius,
					Color4.White.Transparentify(0.5f),
					Color4.DarkGray.Lighten(0.5f).Transparentify(0.5f)
				);
			}
		}

		private void RenderEdge(Vector2 start, Vector2 end, bool isFraming, bool isConstrained, bool isHovered = false)
		{
			var foregroundColor = Theme.Colors.PolygonMeshInnerEdgeColor;
			var backgroundColor = Theme.Colors.PolygonMeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness
			);
			if (isFraming) {
				foregroundColor = Theme.Colors.PolygonMeshFramingEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 1.7f);
			} else if (isConstrained) {
				foregroundColor = Theme.Colors.PolygonMeshFixedEdgeColor;
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			Utils.RenderLine(
				start,
				end,
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(isFraming || isConstrained)
			);
		}

		private void RenderEdgeHovered(Vector2 start, Vector2 end, bool isFraming, bool isConstrained)
		{
			var foregroundColor = PolygonMeshTools.State == PolygonMeshTools.ModificationState.Removal ?
				Theme.Colors.PolygonMeshRemovalColor : Theme.Colors.PolygonMeshHoverColor;
			var backgroundColor = Theme.Colors.PolygonMeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.PolygonMeshBackgroundEdgeThickness
			);
			if (isFraming) {
				backgroundColor = Theme.Colors.PolygonMeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness * 1.7f);
			} else if (isConstrained) {
				backgroundColor = Theme.Colors.PolygonMeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.PolygonMeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.PolygonMeshBackgroundEdgeThickness);
			}
			Utils.RenderLine(
				start,
				end,
				backgroundSize,
				foregroundSize,
				backgroundColor,
				foregroundColor,
				!(isFraming || isConstrained)
			);
		}

		private void RenderVertex(Vector2 position) =>
			Utils.RenderVertex(
				position,
				Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshVertexBackgroundColor,
				Theme.Colors.PolygonMeshVertexColor
			);

		private void RenderVertexHovered(Vector2 position) =>
			Utils.RenderVertex(
				position,
				1.3f * Theme.Metrics.PolygonMeshBackgroundVertexRadius,
				1.3f * Theme.Metrics.PolygonMeshVertexRadius,
				Theme.Colors.PolygonMeshHoverColor.Darken(0.7f),
				PolygonMeshTools.State == PolygonMeshTools.ModificationState.Removal ?
					Theme.Colors.PolygonMeshRemovalColor :
					Theme.Colors.PolygonMeshHoverColor
			);

		// TODO: That is incorrect and should be improved later.
		public override IEnumerator<object> AnimationTask()
		{
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			var cursor = WidgetContext.Current.MouseCursor;
			var lastPos = transform.TransformVector(sv.MousePosition);
			var hitTestTarget = HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result) ? result.Target : null;
			if (hitTestTarget == null) {
				yield break;
			}
			using (Document.Current.History.BeginTransaction()) {
				Core.Operations.SetAnimableProperty.Perform(
					Mesh,
					nameof(Lime.Widgets.PolygonMesh.PolygonMesh.TransientVertices),
					new List<Vertex>(Vertices),
					createAnimatorIfNeeded: true,
					createInitialKeyframeForNewAnimator: true
				);
				while (sv.Input.IsMousePressed()) {
					UI.Utils.ChangeCursorIfDefault(cursor);
					var positionDelta = (transform.TransformVector(sv.MousePosition) - lastPos) / Mesh.Size;
					lastPos = transform.TransformVector(sv.MousePosition);
					TranslateTransientVertices(hitTestTarget, positionDelta);
					Core.Operations.SetAnimableProperty.Perform(
						Mesh,
						nameof(Lime.Widgets.PolygonMesh.PolygonMesh.TransientVertices),
						new List<Vertex>(Vertices),
						createAnimatorIfNeeded: true,
						createInitialKeyframeForNewAnimator: true
					);
					yield return null;
				}
				Mesh.Animators.Invalidate();
				PolygonMeshModification.Animate.Perform();
				Document.Current.History.CommitTransaction();
			}
		}

		public override IEnumerator<object> TriangulationTask()
		{
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			var cursor = WidgetContext.Current.MouseCursor;
			var lastPos = transform.TransformVector(sv.MousePosition);
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result);
			if (result == null || !result.Target.IsVertex()) {
				yield break;
			}
			var target = ((Lime.Widgets.PolygonMesh.Topology.Vertex)result.Target);
			using (Document.Current.History.BeginTransaction()) {
				List<IKeyframe> keyframes = null;
				Mesh.Animators.TryFind(
					nameof(Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new PolygonMeshSlice {
					State = PolygonMeshTools.ModificationState.Triangulation,
					Vertices = new List<Vertex>(Mesh.Vertices),
					IndexBuffer = new List<Face>(Mesh.Faces),
					ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
					Keyframes = animator?.Keys.ToList()
				};
				while (sv.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					keyframes = animator?.Keys.ToList();
					UI.Utils.ChangeCursorIfDefault(cursor);
					var delta = (transform.TransformVector(sv.MousePosition) - lastPos) / Mesh.Size;
					Topology.TranslateVertex(target.Index, delta, delta);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var v = (newKey.Value as List<Vertex>)[target.Index];
							v.UV1 = Mesh.Vertices[target.Index].UV1;
							(newKey.Value as List<Vertex>)[target.Index] = v;
							keyframes.Add(newKey);
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Triangulation,
						Vertices = new List<Vertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(
						Mesh,
						sliceBefore,
						sliceAfter
					);
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private Vector2 SnapMousePositionToTopologyPrimitiveIfPossible(ITopologyPrimitive primitive)
		{
			var mousePos = Mesh.LocalToWorldTransform.CalcInversed().TransformVector(sv.MousePosition) / Mesh.Size;
			if (primitive != null) {
				if (primitive.IsEdge()) {
					var v1 = Vertices[primitive[0]].Pos;
					var v2 = Vertices[primitive[1]].Pos;
					return PolygonMeshUtils.PointProjectionToLine(mousePos, v1, v2, out _);
				}
			}
			return mousePos;
		}

		private Vector2 InterpolateUV(ITopologyPrimitive primitive, Vector2 position)
		{
			if (primitive.IsFace()) {
				var v1 = Vertices[primitive[0]];
				var v2 = Vertices[primitive[1]];
				var v3 = Vertices[primitive[2]];
				var weights = PolygonMeshUtils.CalcTriangleRelativeBarycentricCoordinates(position, v1.Pos, v2.Pos, v3.Pos);
				return weights[0] * v1.UV1 + weights[1] * v2.UV1 + weights[2] * v3.UV1;
			}
			if (primitive.IsEdge()) {
				var v1 = Vertices[primitive[0]];
				var v2 = Vertices[primitive[1]];
				var weights = PolygonMeshUtils.CalcSegmentRelativeBarycentricCoordinates(position, v1.Pos, v2.Pos);
				return weights[0] * v1.UV1 + weights[1] * v2.UV1;
			}
			if (primitive.IsVertex()) {
				return Vertices[primitive[0]].UV1;
			}
			throw new InvalidOperationException();
		}

		public void TranslateTransientVertices(ITopologyPrimitive primitive, Vector2 delta)
		{
			for (var i = 0; i < primitive.Count; i++) {
				TranslateTransientVertex(primitive[i], delta);
			}
		}

		private void TranslateTransientVertex(int index, Vector2 delta)
		{
			var v = Mesh.TransientVertices[index];
			v.Pos += delta;
			Mesh.TransientVertices[index] = v;
		}

		public override IEnumerator<object> CreationTask()
		{
			if (!HitTest(sv.MousePosition, sv.Scene.Scale.X, out var initialHitTestResult)) {
				yield break;
			}
			var initialHitTestTarget = initialHitTestResult.Target;
			if (!initialHitTestTarget.IsVertex()) {
				using (Document.Current.History.BeginTransaction()) {
					List<IKeyframe> keyframes = null;
					Mesh.Animators.TryFind(
						nameof(Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Creation,
						Vertices = new List<Vertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					var pos = SnapMousePositionToTopologyPrimitiveIfPossible(initialHitTestTarget);
					var vertex = new Vertex {
						Pos = pos,
						Color = Mesh.Color,
						UV1 = InterpolateUV(initialHitTestTarget, pos),
					};
					Topology.AddVertex(vertex);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var vertices = new List<Vertex>(newKey.Value as List<Vertex>) { vertex };
							newKey.Value = vertices;
							keyframes.Add(newKey);
							animator.ResetCache();
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Creation,
						Vertices = new List<Vertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(
						Mesh,
						sliceBefore,
						sliceAfter
					);
					Window.Current.Invalidate();
					Document.Current.History.CommitTransaction();
				}
				// Perhaps Topology.AddVertex should return an index where new vertex was placed or -1
				// if it wasn't inserted. Now we assume it's always pushed back.
				initialHitTestTarget = new Lime.Widgets.PolygonMesh.Topology.Vertex { Index = (ushort)(Vertices.Count - 1), };
			}

			yield return ConstrainingTask((Lime.Widgets.PolygonMesh.Topology.Vertex)initialHitTestTarget);
		}

		private IEnumerator<object> ConstrainingTask(Lime.Widgets.PolygonMesh.Topology.Vertex initialHitTestTarget)
		{
			yield return null;
			using (Document.Current.History.BeginTransaction()) {
				List<IKeyframe> keyframes = null;
				Mesh.Animators.TryFind(
					nameof(Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new PolygonMeshSlice {
					State = PolygonMeshTools.ModificationState.Creation,
					Vertices = new List<Vertex>(Mesh.Vertices),
					IndexBuffer = new List<Face>(Mesh.Faces),
					ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
					Keyframes = animator?.Keys.ToList()
				};
				while (sv.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					keyframes = animator?.Keys.ToList();
					var hitTestTarget = HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result)
						? result.Target
						: null;
					if (
						hitTestTarget == null ||
					    hitTestTarget.IsVertex() && initialHitTestTarget.Index == hitTestTarget[0]
					) {
						yield return null;
						continue;
					}
					var startIndex = initialHitTestTarget.Index;
					var endIndex = hitTestTarget[0];
					if (!hitTestTarget.IsVertex()) {
						var pos = SnapMousePositionToTopologyPrimitiveIfPossible(hitTestTarget);
						var vertex = new Vertex {
							Pos = pos,
							Color = Mesh.Color,
							UV1 = InterpolateUV(hitTestTarget, pos),
						};
						Topology.AddVertex(vertex);
						if (animator != null) {
							keyframes = new List<IKeyframe>();
							foreach (var key in animator.Keys.ToList()) {
								var newKey = key.Clone();
								var vertices = new List<Vertex>(newKey.Value as List<Vertex>) {
									vertex
								};
								newKey.Value = vertices;
								keyframes.Add(newKey);
								animator.ResetCache();
							}
						}
						endIndex = (ushort)(Mesh.Vertices.Count - 1);
					}
					Topology.ConstrainEdge(startIndex, endIndex);
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Creation,
						Vertices = new List<Vertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};

					PolygonMeshModification.Slice.Perform(
						Mesh,
						sliceBefore,
						sliceAfter
					);
					Window.Current.Invalidate();
					sv.Input.ConsumeKeyPress(Key.Mouse0);
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		public override IEnumerator<object> RemovalTask()
		{
			UI.Utils.ChangeCursorIfDefault(WidgetContext.Current.MouseCursor);
			if (Topology.Vertices.Count == 3) {
				new AlertDialog("Mesh can't contain less than 3 vertices", "Ok :(").Show();
				yield return null;
			} else {
				using (Document.Current.History.BeginTransaction()) {
					var hitTestTarget = HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result)
						? result.Target
						: null;
					if (hitTestTarget == null != hitTestTarget.IsFace()) {
						yield break;
					}
					List<IKeyframe> keyframes = null;
					Mesh.Animators.TryFind(
						nameof(Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Removal,
						Vertices = new List<Vertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					if (hitTestTarget.IsVertex()) {
						Topology.RemoveVertex(hitTestTarget[0]);
						if (animator != null) {
							keyframes = new List<IKeyframe>();
							foreach (var key in animator.Keys.ToList()) {
								var newKey = key.Clone();
								var vertices = new List<Vertex>(newKey.Value as List<Vertex>);
								vertices[hitTestTarget[0]] = vertices[vertices.Count - 1];
								vertices.RemoveAt(vertices.Count - 1);
								newKey.Value = vertices;
								keyframes.Add(newKey);
								animator.ResetCache();
							}
						}
					} else {
						// It's an edge. If edge is constrain than try to deconstrain it.
						// TODO
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Removal,
						Vertices = new List<Vertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(
						Mesh,
						sliceBefore,
						sliceAfter
					);
					Document.Current.History.CommitTransaction();
				}
				yield return null;
			}
		}

		// TODO: provide implementation
		public override IEnumerator<object> ConcaveTask()
		{
			yield break;
		}
	}
}
