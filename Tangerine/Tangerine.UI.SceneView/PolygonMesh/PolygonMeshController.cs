using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Lime.PolygonMesh.Topology;
using Lime.PolygonMesh.Utils;
using Lime.Widgets.PolygonMesh.Topology;
using Lime.Widgets.PolygonMesh.Topology.HalfEdgeTopology;
using Tangerine.Core;
using Vertex = Lime.Vertex;
using SkinnedVertex = Lime.Widgets.PolygonMesh.PolygonMesh.SkinnedVertex;
using static Lime.Widgets.PolygonMesh.Topology.Edge;

// Note: This will be heavily refactored once the necessary
// functionality is implemented within both frontend and backend.

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
			public List<SkinnedVertex> Vertices;
			public List<Face> IndexBuffer;
			public List<Edge> ConstrainedVertices;
			public List<IKeyframe> Keyframes;
		}

		protected abstract bool ValidateHitTestResult(TopologyHitTestResult result, bool ignoreState);
		protected abstract void RecalcVertexBoneTies();

		public abstract void Render(Widget renderContext);
		public abstract bool HitTest(Vector2 position, float scale, bool ignoreState = false);
		public abstract void TieVertexWithBones(List<Bone> bones);
		public abstract void UntieVertexFromBones(List<Bone> bones);
		public abstract IEnumerator<object> AnimationTask();
		public abstract IEnumerator<object> TriangulationTask();
		public abstract IEnumerator<object> CreationTask();
		public abstract IEnumerator<object> RemovalTask();
	}

	[YuzuDontGenerateDeserializer]
	[AllowedComponentOwnerTypes(typeof(Lime.Widgets.PolygonMesh.PolygonMesh))]
	public abstract class TopologyController : PolygonMeshController
	{
		public Lime.Widgets.PolygonMesh.PolygonMesh Mesh { get; protected set; }

		public ITopology Topology { get; protected set; }

		public IList<SkinnedVertex> Vertices => PolygonMeshTools.Mode == PolygonMeshTools.ModificationMode.Animation
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
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = Vector2.Zero,
						UV1 = Vector2.Zero,
						Color = mesh.Color
					});
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = new Vector2(1.0f, 0.0f),
						UV1 = new Vector2(1.0f, 0.0f),
						Color = mesh.Color
					});
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = new Vector2(0.0f, 1.0f),
						UV1 = new Vector2(0.0f, 1.0f),
						Color = mesh.Color
					});
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = Vector2.One,
						UV1 = Vector2.One,
						Color = mesh.Color
					});
				}
				Topology = (T)Activator.CreateInstance(typeof(T), mesh.Vertices);
				if (mesh.Faces.Count > 0) {
					Topology.Sync(mesh.Vertices, mesh.ConstrainedEdges, mesh.Faces);
				} else {
					mesh.Faces.AddRange(Topology.Faces);
				}
				mesh.OnBoneArrayChanged = RecalcVertexBoneTies;
				Mesh = mesh;
				Topology.OnTopologyChanged += UpdateMeshFaces;
				if (mesh.TransientVertices == null) {
					mesh.TransientVertices = new List<SkinnedVertex>(mesh.Vertices);
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

		// TODO: Refactor.
		// Since transformation strictly depends on the modification state and
		// current workspace context, recalculating matrices and transforming positions
		// must be done once per modification demand, thus implying some dirty mask implementation.
		private Vector2 CalcTransformedVertexPosition(int i)
		{
			var sceneToRenderContextTransform = sv.CalcTransitionFromSceneSpace(sv.Frame);
			return PolygonMeshTools.State == PolygonMeshTools.ModificationState.Animation ?
				// Parent to render context.
				(Mesh.ParentWidget.LocalToWorldTransform * sceneToRenderContextTransform).TransformVector(Mesh.TransformedVertexPosition(i)) :
				// Local to render context.
				(Mesh.LocalToWorldTransform * sceneToRenderContextTransform).TransformVector(Mesh.CalcVertexPositionInCurrentSpace(i));
		}

		private bool HitTest(Vector2 position, float scale, out TopologyHitTestResult result)
		{
			if (PolygonMeshTools.Mode == PolygonMeshTools.ModificationMode.Animation) {
				result = new TopologyHitTestResult();
				position = sv.CalcTransitionFromSceneSpace(sv.Frame).TransformVector(position);
				var transformedVertices = new Vector2[3];
				foreach (var (face, info) in Topology.FacesWithInfo) {
					transformedVertices[0] = CalcTransformedVertexPosition(face[0]);
					transformedVertices[1] = CalcTransformedVertexPosition(face[1]);
					transformedVertices[2] = CalcTransformedVertexPosition(face[2]);
					if (
						PolygonMeshUtils.PointTriangleIntersection(position, transformedVertices[0],
							transformedVertices[1], transformedVertices[2])
					) {
						result.Target = face;
						result.Info = info;
					}
					for (ushort i = 0; i < 3; i++) {
						var s1 = transformedVertices[i];
						var s2 = transformedVertices[(i + 1) % 3];
						if (
							Vector2.Distance(transformedVertices[i], position) <=
							Theme.Metrics.PolygonMeshVertexHitTestRadius
						) {
							result.Target = new Lime.Widgets.PolygonMesh.Topology.Vertex { Index = face[i], };
							result.Info = null;
							break;
						}
						if (
							PolygonMeshUtils.SqrDistanceFromPointToSegment(s1, s2, position) <=
							Theme.Metrics.PolygonMeshEdgeHitTestRadius * Theme.Metrics.PolygonMeshEdgeHitTestRadius
						) {
							result.Target = new Edge(face[i], face[(i + 1) % 3]);
							result.Info = new EdgeInfo {
								IsConstrained = info[i].IsConstrained,
								IsFraming = info[i].IsFraming,
							};
						}
					}
				}
				result = result.Target == null ? null : result;
				return result != null;
			}
			var vertexHitRadius = Theme.Metrics.PolygonMeshVertexHitTestRadius / scale / Mesh.Size.Length;
			var edgeHitRadius = Theme.Metrics.PolygonMeshEdgeHitTestRadius / scale / Mesh.Size.Length;
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			position = transform.TransformVector(position);
			var normalizedPosition = position / Mesh.Size;
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
					return result.Target.IsVertex();
				case PolygonMeshTools.ModificationState.Removal:
					return result.Target.IsVertex() || result.Target.IsEdge() &&
							(result.Info as EdgeInfo).IsConstrained;
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
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var hitTestResult);
			var isHitTestSuccessful = ValidateHitTestResult(hitTestResult, ignoreState: false);
			var hitTestTarget = isHitTestSuccessful ? hitTestResult.Target : null;

			if (hitTestTarget != null && hitTestTarget.IsFace()) {
				Utils.RenderTriangle(
					CalcTransformedVertexPosition(hitTestTarget[0]),
					CalcTransformedVertexPosition(hitTestTarget[1]),
					CalcTransformedVertexPosition(hitTestTarget[2]),
					PolygonMeshTools.State == PolygonMeshTools.ModificationState.Removal ?
						Theme.Colors.PolygonMeshRemovalColor :
						Theme.Colors.PolygonMeshHoverColor
				);
			}
			var isHitTestResultTargetEdge = hitTestTarget != null && hitTestTarget.IsEdge();
			var isHitTestResultTargetVertex = hitTestTarget != null && hitTestTarget.IsVertex();
			if (Window.Current.Input.IsKeyPressed(Key.Alt)) {
				var transform = Mesh.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame);
				var bfv = new[] { Vector2.Zero, Vector2.Zero, Vector2.East, Vector2.Down, Vector2.One, };
				foreach (var (i1, i2, i3, t) in ((HalfEdgeTopology)Topology).DebugTriangles()) {
					var v1 = transform.TransformVector((i1 >= 0 ? Vertices[i1].Pos : bfv[-i1]) * Mesh.Size);
					var v2 = transform.TransformVector((i2 >= 0 ? Vertices[i2].Pos : bfv[-i2]) * Mesh.Size);
					var v3 = transform.TransformVector((i3 >= 0 ? Vertices[i3].Pos : bfv[-i3]) * Mesh.Size);
					render(v1, v2);
					render(v2, v3);
					render(v3, v1);
				}

				void render(Vector2 s, Vector2 e)
				{
					if (e.X < s.X || (e.X == s.X && e.Y < s.Y)) {
						Toolbox.Swap(ref s, ref e);
					}
					RenderEdge(s, e, false, false, false);
				}
			} else {
				foreach (var (face, info) in Topology.FacesWithInfo) {
					var prevVertex = CalcTransformedVertexPosition(face.Index0);
					for (var i = 0; i < 3; i++) {
						var prevVertexIndex = face[i];
						var nextVertexIndex = face[(i + 1) % 3];
						var nextVertex = CalcTransformedVertexPosition(nextVertexIndex);
						var (isFraming, isConstrained) = info[i];
						var v1 = prevVertex;
						var v2 = nextVertex;
						// Keep the same render order for each edge.
						if (v2.X < v1.X || (v2.X == v1.X && v2.Y < v1.Y)) {
							Toolbox.Swap(ref v1, ref v2);
						}
						var isEdgeHovered =
							isHitTestResultTargetEdge &&
							(hitTestTarget[0] == prevVertexIndex && hitTestTarget[1] == nextVertexIndex ||
							 hitTestTarget[0] == nextVertexIndex && hitTestTarget[1] == prevVertexIndex);
						var isEdgePossiblyWillBeRemoved =
							isHitTestResultTargetVertex &&
							PolygonMeshTools.State == PolygonMeshTools.ModificationState.Removal &&
							(hitTestTarget[0] == prevVertexIndex || hitTestTarget[0] == nextVertexIndex);
						if (isEdgeHovered || isEdgePossiblyWillBeRemoved) {
							RenderEdgeHovered(v1, v2, isFraming, isConstrained);
						} else {
							RenderEdge(v1, v2, isFraming, isConstrained);
						}
						prevVertex = nextVertex;
					}
				}
			}
			for (var i = 0; i < Vertices.Count; i++) {
				var v = CalcTransformedVertexPosition(i);
				if (isHitTestResultTargetVertex && i == hitTestTarget[0]) {
					RenderVertexHovered(v);
				} else {
					RenderVertex(v);
				}
			}
			if (PolygonMeshTools.State == PolygonMeshTools.ModificationState.Creation && hitTestTarget != null && !hitTestTarget.IsVertex()) {
				Utils.RenderVertex(
					(Mesh.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame)).TransformVector(SnapMousePositionToTopologyPrimitiveIfPossible(hitTestTarget) * Mesh.Size),
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

		public override void TieVertexWithBones(List<Bone> bones)
		{
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result);
			if (result.Target.IsVertex()) {
				Document.Current.History.DoTransaction(() => {
					List<IKeyframe> keyframes = null;
					Mesh.Animators.TryFind(
						nameof(Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new PolygonMeshSlice {
						State = PolygonMeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					keyframes = animator?.Keys.ToList();
					Core.Operations.TieSkinnedVerticesWithBones.Perform(bones, Mesh, result.Target[0]);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var v = (newKey.Value as List<SkinnedVertex>)[result.Target[0]];
							v.BlendIndices = Mesh.Vertices[result.Target[0]].BlendIndices;
							v.BlendWeights = Mesh.Vertices[result.Target[0]].BlendWeights;
							(newKey.Value as List<SkinnedVertex>)[result.Target[0]] = v;
							keyframes.Add(newKey);
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(Mesh, sliceBefore, sliceAfter);
				});
			}
		}

		public override void UntieVertexFromBones(List<Bone> bones)
		{
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result);
			if (result.Target.IsVertex()) {
				Document.Current.History.DoTransaction(() => {
					List<IKeyframe> keyframes = null;
					Mesh.Animators.TryFind(
						nameof(Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new PolygonMeshSlice {
						State = PolygonMeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					keyframes = animator?.Keys.ToList();
					Core.Operations.UntieSkinnedVerticesFromBones.Perform(bones, Mesh, result.Target[0]);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var v = (newKey.Value as List<SkinnedVertex>)[result.Target[0]];
							v.BlendIndices = Mesh.Vertices[result.Target[0]].BlendIndices;
							v.BlendWeights = Mesh.Vertices[result.Target[0]].BlendWeights;
							(newKey.Value as List<SkinnedVertex>)[result.Target[0]] = v;
							keyframes.Add(newKey);
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(Mesh, sliceBefore, sliceAfter);
				});
			}
		}

		protected override void RecalcVertexBoneTies()
		{
			List<IKeyframe> keyframes = null;
			Mesh.Animators.TryFind(
				nameof(Mesh.TransientVertices),
				out var animator
			);
			var sliceBefore = new PolygonMeshSlice {
				State = PolygonMeshTools.State,
				Vertices = new List<SkinnedVertex>(Mesh.Vertices),
				IndexBuffer = new List<Face>(Mesh.Faces),
				ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
				Keyframes = animator?.Keys.ToList()
			};
			keyframes = animator?.Keys.ToList();
			var missingBones = new HashSet<byte>();
			var vertexBoneMap = new List<HashSet<int>>();
			for (var i = 0; i < Vertices.Count; ++i) {
				vertexBoneMap.Add(new HashSet<int>());
				foreach (var index in Vertices[i].BlendIndices.ToArray()) {
					if (index == 0) {
						continue;
					}
					var bone = Mesh.ParentWidget.Nodes.GetBone(index);
					if (bone == null) {
						missingBones.Add(index);
					}
					vertexBoneMap[i].Add(bone?.Index ?? index);
				}
			}
			for (var i = 0; i < Vertices.Count; ++i) {
				var boneIndices = vertexBoneMap[i].Where(_ => missingBones.Contains((byte)_)).ToArray();
				if (boneIndices.Length > 0) {
					var v = Mesh.Vertices[i];
					var sw = v.SkinningWeights.Release(boneIndices);
					v.SkinningWeights = sw;
					Mesh.Vertices[i] = v;
					v = Mesh.TransientVertices[i];
					v.SkinningWeights = sw;
					Mesh.TransientVertices[i] = v;
				}
			}
			if (animator != null) {
				keyframes = new List<IKeyframe>();
				foreach (var key in animator.Keys.ToList()) {
					var newKey = key.Clone();
					for (var i = 0; i < Vertices.Count; ++i) {
						var v = (newKey.Value as List<SkinnedVertex>)[i];
						v.BlendIndices = Mesh.Vertices[i].BlendIndices;
						v.BlendWeights = Mesh.Vertices[i].BlendWeights;
						(newKey.Value as List<SkinnedVertex>)[i] = v;
					}
					keyframes.Add(newKey);
				}
			}
			var sliceAfter = new PolygonMeshSlice {
				State = PolygonMeshTools.State,
				Vertices = new List<SkinnedVertex>(Mesh.Vertices),
				IndexBuffer = new List<Face>(Mesh.Faces),
				ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
				Keyframes = keyframes
			};
			PolygonMeshModification.Slice.Perform(Mesh, sliceBefore, sliceAfter);
		}

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
					new List<SkinnedVertex>(Vertices),
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
						new List<SkinnedVertex>(Vertices),
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
			var target = (Lime.Widgets.PolygonMesh.Topology.Vertex)result.Target;
			using (Document.Current.History.BeginTransaction()) {
				List<IKeyframe> keyframes = null;
				Mesh.Animators.TryFind(
					nameof(Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new PolygonMeshSlice {
					State = PolygonMeshTools.ModificationState.Triangulation,
					Vertices = new List<SkinnedVertex>(Mesh.Vertices),
					IndexBuffer = new List<Face>(Mesh.Faces),
					ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
					Keyframes = animator?.Keys.ToList()
				};
				var lastValidDelta = Vector2.Zero;
				Topology.VertexHitTestRadius = Topology.EdgeHitTestDistance = 0f;
				while (sv.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					keyframes = animator?.Keys.ToList();
					UI.Utils.ChangeCursorIfDefault(cursor);
					var delta = (transform.TransformVector(sv.MousePosition) - lastPos) / Mesh.Size;
					if (Topology.TranslateVertex(target.Index, delta, delta)) {
						lastValidDelta = delta;
					} else if (lastValidDelta != Vector2.Zero) {
						Topology.TranslateVertex(target.Index, lastValidDelta, lastValidDelta);
					}
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var v = (newKey.Value as List<SkinnedVertex>)[target.Index];
							v.UV1 = Mesh.Vertices[target.Index].UV1;
							(newKey.Value as List<SkinnedVertex>)[target.Index] = v;
							keyframes.Add(newKey);
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Triangulation,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
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

		private void UpdateHitTestMetrics()
		{
			var vertexHitRadius = Theme.Metrics.PolygonMeshVertexHitTestRadius / sv.Scene.Scale.X / Mesh.Size.Length;
			var edgeHitRadius = Theme.Metrics.PolygonMeshEdgeHitTestRadius / sv.Scene.Scale.X / Mesh.Size.Length;
			Topology.VertexHitTestRadius = vertexHitRadius;
			Topology.EdgeHitTestDistance = edgeHitRadius;
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
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<Face>(Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					var pos = SnapMousePositionToTopologyPrimitiveIfPossible(initialHitTestTarget);
					var vertex = new SkinnedVertex {
						Pos = pos,
						Color = Mesh.Color,
						UV1 = InterpolateUV(initialHitTestTarget, pos),
					};
					UpdateHitTestMetrics();
					Topology.AddVertex(vertex);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var vertices = new List<SkinnedVertex>(newKey.Value as List<SkinnedVertex>) { vertex };
							newKey.Value = vertices;
							keyframes.Add(newKey);
							animator.ResetCache();
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Creation,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
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
					Vertices = new List<SkinnedVertex>(Mesh.Vertices),
					IndexBuffer = new List<Face>(Mesh.Faces),
					ConstrainedVertices = new List<Edge>(Mesh.ConstrainedEdges),
					Keyframes = animator?.Keys.ToList()
				};
				while (sv.Input.IsMousePressed()) {
					UpdateHitTestMetrics();
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
					int endIndex = hitTestTarget[0];
					if (!hitTestTarget.IsVertex()) {
						var pos = SnapMousePositionToTopologyPrimitiveIfPossible(hitTestTarget);
						var vertex = new SkinnedVertex {
							Pos = pos,
							Color = Mesh.Color,
							UV1 = InterpolateUV(hitTestTarget, pos),
						};
						endIndex = Topology.AddVertex(vertex);
						if (animator != null) {
							keyframes = new List<IKeyframe>();
							foreach (var key in animator.Keys.ToList()) {
								var newKey = key.Clone();
								var vertices = new List<SkinnedVertex>(newKey.Value as List<SkinnedVertex>) {
									vertex
								};
								newKey.Value = vertices;
								keyframes.Add(newKey);
								animator.ResetCache();
							}
						}
					}
					if (endIndex >= 0) {
						Topology.ConstrainEdge(startIndex, endIndex);
						var sliceAfter = new PolygonMeshSlice {
							State = PolygonMeshTools.ModificationState.Creation,
							Vertices = new List<SkinnedVertex>(Mesh.Vertices),
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
					}
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
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
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
								var vertices = new List<SkinnedVertex>(newKey.Value as List<SkinnedVertex>);
								vertices[hitTestTarget[0]] = vertices[vertices.Count - 1];
								vertices.RemoveAt(vertices.Count - 1);
								newKey.Value = vertices;
								keyframes.Add(newKey);
								animator.ResetCache();
							}
						}
					} else if (hitTestTarget.IsEdge() && result.Info is EdgeInfo ei && ei.IsConstrained) {
						var edge = (Edge)hitTestTarget;
						Topology.DeconstrainEdge(edge.Index0, edge.Index1);
					}
					var sliceAfter = new PolygonMeshSlice {
						State = PolygonMeshTools.ModificationState.Removal,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
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
	}
}
