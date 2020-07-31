using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI.AnimeshEditor.Operations;
using Tangerine.UI.AnimeshEditor.Topology.HalfEdgeTopology;
using SkinnedVertex = Lime.Animesh.SkinnedVertex;

namespace Tangerine.UI.AnimeshEditor
{
	internal static class TopologyPrimitiveExtensions
	{
		public static bool IsVertex(this ITopologyPrimitive self) => self.Count == 1;
		public static bool IsEdge(this ITopologyPrimitive self) => self.Count == 2;
		public static bool IsFace(this ITopologyPrimitive self) => self.Count == 3;
	}

	public struct AnimeshSlice
	{
		public AnimeshTools.ModificationState State;
		public List<SkinnedVertex> Vertices;
		public List<TopologyFace> IndexBuffer;
		public List<TopologyEdge> ConstrainedVertices;
		public List<IKeyframe> Keyframes;
	}

	[YuzuDontGenerateDeserializer]
	[NodeComponentDontSerialize]
	[AllowedComponentOwnerTypes(typeof(Lime.Animesh))]
	public sealed class AnimeshController<T> : NodeComponent where T : ITopology
	{
		public Animesh Mesh { get; private set; }
		public ITopology Topology { get; private set; }
		public IList<SkinnedVertex> Vertices => AnimeshTools.Mode == AnimeshTools.ModificationMode.Animation
			? Mesh.TransientVertices
			: Mesh.Vertices;

		private readonly ISceneView sv;

		public AnimeshController() { }

		public AnimeshController(ISceneView sv)
		{
			this.sv = sv;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (Owner is Lime.Animesh mesh) {
				if (mesh.Vertices.Count == 0) {
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = Vector2.Zero,
						UV1 = Vector2.Zero,
						Color = mesh.Color,
						BlendWeights = new Mesh3D.BlendWeights { Weight0 = 1f, },
					});
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = new Vector2(mesh.Size.X, 0.0f),
						UV1 = new Vector2(1.0f, 0.0f),
						Color = mesh.Color,
						BlendWeights = new Mesh3D.BlendWeights { Weight0 = 1f, },
					});
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = new Vector2(0.0f, mesh.Size.Y),
						UV1 = new Vector2(0.0f, 1.0f),
						Color = mesh.Color,
						BlendWeights = new Mesh3D.BlendWeights { Weight0 = 1f, },
					});
					mesh.Vertices.Add(new SkinnedVertex {
						Pos = mesh.Size,
						UV1 = Vector2.One,
						Color = mesh.Color,
						BlendWeights = new Mesh3D.BlendWeights { Weight0 = 1f, },
					});
				}
				Topology = (T)Activator.CreateInstance(typeof(T), mesh.Vertices);
				if (mesh.Faces.Count > 0) {
					Topology.ConstructFrom(mesh.Vertices, mesh.ConstrainedEdges, mesh.Faces);
				} else {
					mesh.Faces.AddRange(Topology.Faces);
				}
				mesh.OnBoneArrayChanged = RecalcVertexBoneTies;
				Mesh = mesh;
				Topology.OnTopologyChanged += UpdateMeshFaces;
				if (mesh.TransientVertices == null) {
					mesh.TransientVertices = new List<SkinnedVertex>(mesh.Vertices);
				}
				AnimeshTools.State = AnimeshTools.ModificationState.Animation;
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
					Mesh.ConstrainedEdges.Add(new TopologyEdge((ushort)edge.Item1, (ushort)edge.Item2));
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
			return AnimeshTools.State == AnimeshTools.ModificationState.Animation ?
				// Parent to render context.
				(Mesh.ParentWidget.LocalToWorldTransform * sceneToRenderContextTransform).TransformVector(Mesh.TransformedVertexPosition(i)) :
				// Local to render context.
				(Mesh.LocalToWorldTransform * sceneToRenderContextTransform).TransformVector(Mesh.CalcVertexPositionInCurrentSpace(i));
		}

		private bool HitTest(Vector2 position, float scale, out TopologyHitTestResult result)
		{
			if (AnimeshTools.Mode == AnimeshTools.ModificationMode.Animation) {
				result = new TopologyHitTestResult();
				position = sv.CalcTransitionFromSceneSpace(sv.Frame).TransformVector(position);
				var transformedVertices = new Vector2[3];
				foreach (var (face, info) in Topology.FacesWithInfo) {
					transformedVertices[0] = CalcTransformedVertexPosition(face[0]);
					transformedVertices[1] = CalcTransformedVertexPosition(face[1]);
					transformedVertices[2] = CalcTransformedVertexPosition(face[2]);
					if (
						AnimeshUtils.PointTriangleIntersection(position, transformedVertices[0],
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
							Theme.Metrics.AnimeshVertexHitTestRadius
						) {
							result.Target = new TopologyVertex { Index = face[i], };
							result.Info = null;
							break;
						}
						if (
							AnimeshUtils.SqrDistanceFromPointToSegment(s1, s2, position) <=
							Theme.Metrics.AnimeshEdgeHitTestRadius * Theme.Metrics.AnimeshEdgeHitTestRadius
						) {
							result.Target = new TopologyEdge(face[i], face[(i + 1) % 3]);
							result.Info = new TopologyEdge.EdgeInfo {
								IsConstrained = info[i].IsConstrained,
								IsFraming = info[i].IsFraming,
							};
						}
					}
				}
				result = result.Target == null ? null : result;
				return result != null;
			}
			var vertexHitRadius = Theme.Metrics.AnimeshVertexHitTestRadius / scale;
			var edgeHitRadius = Theme.Metrics.AnimeshEdgeHitTestRadius / scale;
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			position = transform.TransformVector(position);
			return Topology.HitTest(position, vertexHitRadius, edgeHitRadius, out result);
		}

		private bool ValidateHitTestResult(TopologyHitTestResult result, bool ignoreState)
		{
			if (result == null) {
				return false;
			}

			if (ignoreState) {
				return true;
			}

			switch (AnimeshTools.State) {
				case AnimeshTools.ModificationState.Animation:
				case AnimeshTools.ModificationState.Creation:
					return true;
				case AnimeshTools.ModificationState.Modification:
					return result.Target.IsVertex();
				case AnimeshTools.ModificationState.Removal:
					return result.Target.IsVertex() || result.Target.IsEdge() &&
							(result.Info as TopologyEdge.EdgeInfo).IsConstrained;
				default:
					return false;
			}
		}

		public bool HitTest(Vector2 position, float scale, bool ignoreState = false)
		{
			HitTest(position, scale, out var result);
			return ValidateHitTestResult(result, ignoreState);
		}

		public void Render(Widget renderContext)
		{
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var hitTestResult);
			var isHitTestSuccessful = ValidateHitTestResult(hitTestResult, ignoreState: false);
			var hitTestTarget = isHitTestSuccessful ? hitTestResult.Target : null;

			if (hitTestTarget != null && hitTestTarget.IsFace()) {
				AnimeshUtils.RenderTriangle(
					CalcTransformedVertexPosition(hitTestTarget[0]),
					CalcTransformedVertexPosition(hitTestTarget[1]),
					CalcTransformedVertexPosition(hitTestTarget[2]),
					AnimeshTools.State == AnimeshTools.ModificationState.Removal ?
						Theme.Colors.AnimeshRemovalColor :
						Theme.Colors.AnimeshHoverColor
				);
			}
			var isHitTestResultTargetEdge = hitTestTarget != null && hitTestTarget.IsEdge();
			var isHitTestResultTargetVertex = hitTestTarget != null && hitTestTarget.IsVertex();
			if (Window.Current.Input.IsKeyPressed(Key.Alt)) {
				var transform = Mesh.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame);
				var bbox = Rectangle.Empty;
				foreach (var vertex in Vertices) {
					bbox = bbox.IncludingPoint(vertex.Pos);
				}
				var bfv = new[] { Vector2.Zero, bbox.A, new Vector2(bbox.BX, bbox.AY), new Vector2(bbox.AX, bbox.BY), bbox.B, };
				foreach (var (i1, i2, i3, t) in ((HalfEdgeTopology)Topology).DebugTriangles()) {
					var v1 = transform.TransformVector(i1 >= 0 ? Vertices[i1].Pos : bfv[-i1]);
					var v2 = transform.TransformVector(i2 >= 0 ? Vertices[i2].Pos : bfv[-i2]);
					var v3 = transform.TransformVector(i3 >= 0 ? Vertices[i3].Pos : bfv[-i3]);
					render(v1, v2);
					render(v2, v3);
					render(v3, v1);
				}
				AnimeshUtils.RenderLine(transform.TransformVector(bfv[1]), transform.TransformVector(bfv[2]), new Vector2(2f), new Vector2(2f), Color4.Red, Color4.Red);
				AnimeshUtils.RenderLine(transform.TransformVector(bfv[2]), transform.TransformVector(bfv[4]), new Vector2(2f), new Vector2(2f), Color4.Red, Color4.Red);
				AnimeshUtils.RenderLine(transform.TransformVector(bfv[4]), transform.TransformVector(bfv[3]), new Vector2(2f), new Vector2(2f), Color4.Red, Color4.Red);
				AnimeshUtils.RenderLine(transform.TransformVector(bfv[3]), transform.TransformVector(bfv[1]), new Vector2(2f), new Vector2(2f), Color4.Red, Color4.Red);
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
							AnimeshTools.State == AnimeshTools.ModificationState.Removal &&
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
			if (AnimeshTools.State == AnimeshTools.ModificationState.Creation && hitTestTarget != null && !hitTestTarget.IsVertex()) {
				AnimeshUtils.RenderVertex(
					(Mesh.LocalToWorldTransform * sv.CalcTransitionFromSceneSpace(sv.Frame))
						.TransformVector(SnapMousePositionToTopologyPrimitiveIfPossible(hitTestTarget)),
					Theme.Metrics.AnimeshBackgroundVertexRadius,
					Theme.Metrics.AnimeshVertexRadius,
					Color4.White.Transparentify(0.5f),
					Color4.DarkGray.Lighten(0.5f).Transparentify(0.5f)
				);
			}
		}

		private void RenderEdge(Vector2 start, Vector2 end, bool isFraming, bool isConstrained, bool isHovered = false)
		{
			var foregroundColor = Theme.Colors.AnimeshInnerEdgeColor;
			var backgroundColor = Theme.Colors.AnimeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.AnimeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.AnimeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.AnimeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.AnimeshBackgroundEdgeThickness
			);
			if (isFraming) {
				foregroundColor = Theme.Colors.AnimeshFramingEdgeColor;
				backgroundColor = Theme.Colors.AnimeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.AnimeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.AnimeshBackgroundEdgeThickness * 1.7f);
			} else if (isConstrained) {
				foregroundColor = Theme.Colors.AnimeshFixedEdgeColor;
				backgroundColor = Theme.Colors.AnimeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.AnimeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.AnimeshBackgroundEdgeThickness);
			}
			AnimeshUtils.RenderLine(
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
			var foregroundColor = AnimeshTools.State == AnimeshTools.ModificationState.Removal ?
				Theme.Colors.AnimeshRemovalColor : Theme.Colors.AnimeshHoverColor;
			var backgroundColor = Theme.Colors.AnimeshInnerEdgeBackgroundColor;
			var foregroundSize = new Vector2(
				Theme.Metrics.AnimeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.AnimeshEdgeThickness
			);
			var backgroundSize = new Vector2(
				Theme.Metrics.AnimeshBackgroundEdgeThickness * 2.0f,
				Theme.Metrics.AnimeshBackgroundEdgeThickness
			);
			if (isFraming) {
				backgroundColor = Theme.Colors.AnimeshFramingEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.AnimeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.AnimeshBackgroundEdgeThickness * 1.7f);
			} else if (isConstrained) {
				backgroundColor = Theme.Colors.AnimeshFixedEdgeBackgroundColor;
				foregroundSize = new Vector2(Theme.Metrics.AnimeshEdgeThickness);
				backgroundSize = new Vector2(Theme.Metrics.AnimeshBackgroundEdgeThickness);
			}
			AnimeshUtils.RenderLine(
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
			AnimeshUtils.RenderVertex(
				position,
				Theme.Metrics.AnimeshBackgroundVertexRadius,
				Theme.Metrics.AnimeshVertexRadius,
				Theme.Colors.AnimeshVertexBackgroundColor,
				Theme.Colors.AnimeshVertexColor
			);

		private void RenderVertexHovered(Vector2 position) =>
			AnimeshUtils.RenderVertex(
				position,
				1.3f * Theme.Metrics.AnimeshBackgroundVertexRadius,
				1.3f * Theme.Metrics.AnimeshVertexRadius,
				Theme.Colors.AnimeshHoverColor.Darken(0.7f),
				AnimeshTools.State == AnimeshTools.ModificationState.Removal ?
					Theme.Colors.AnimeshRemovalColor :
					Theme.Colors.AnimeshHoverColor
			);

		public void TieVertexWithBones(List<Bone> bones)
		{
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result);
			if (result.Target.IsVertex()) {
				Document.Current.History.DoTransaction(() => {
					List<IKeyframe> keyframes = null;
					Mesh.Animators.TryFind(
						nameof(Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new AnimeshSlice {
						State = AnimeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					keyframes = animator?.Keys.ToList();
					TieSkinnedVerticesWithBones.Perform(bones, Mesh, result.Target[0]);
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
					var sliceAfter = new AnimeshSlice {
						State = AnimeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					AnimeshModification.Slice.Perform(Mesh, sliceBefore, sliceAfter);
				});
			}
		}

		public void UntieVertexFromBones(List<Bone> bones)
		{
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result);
			if (result.Target.IsVertex()) {
				Document.Current.History.DoTransaction(() => {
					List<IKeyframe> keyframes = null;
					Mesh.Animators.TryFind(
						nameof(Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new AnimeshSlice {
						State = AnimeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
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
					var sliceAfter = new AnimeshSlice {
						State = AnimeshTools.State,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					AnimeshModification.Slice.Perform(Mesh, sliceBefore, sliceAfter);
				});
			}
		}

		protected void RecalcVertexBoneTies()
		{
			List<IKeyframe> keyframes = null;
			Mesh.Animators.TryFind(
				nameof(Mesh.TransientVertices),
				out var animator
			);
			var sliceBefore = new AnimeshSlice {
				State = AnimeshTools.State,
				Vertices = new List<SkinnedVertex>(Mesh.Vertices),
				IndexBuffer = new List<TopologyFace>(Mesh.Faces),
				ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
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
			var sliceAfter = new AnimeshSlice {
				State = AnimeshTools.State,
				Vertices = new List<SkinnedVertex>(Mesh.Vertices),
				IndexBuffer = new List<TopologyFace>(Mesh.Faces),
				ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
				Keyframes = keyframes
			};
			AnimeshModification.Slice.Perform(Mesh, sliceBefore, sliceAfter);
		}

		public IEnumerator<object> AnimationTask()
		{
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			var cursor = WidgetContext.Current.MouseCursor;
			var lastPosLocalPosition = transform.TransformVector(sv.MousePosition);
			var lastPositionsBeforeBonesApplication = new [] { lastPosLocalPosition, lastPosLocalPosition, lastPosLocalPosition, };
			var hitTestTarget = HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result) ? result.Target : null;
			if (hitTestTarget == null) {
				yield break;
			}
			var invSkinningMatrices = new[] { Matrix32.Identity, Matrix32.Identity, Matrix32.Identity, };
			for (int i = 0; i < hitTestTarget.Count; i++) {
				invSkinningMatrices[i] = Mesh.ParentWidget.BoneArray.
					CalcWeightedRelativeTransform(Mesh.Vertices[hitTestTarget[i]].SkinningWeights).CalcInversed();
				lastPositionsBeforeBonesApplication[i] = invSkinningMatrices[i].TransformVector(lastPositionsBeforeBonesApplication[i]);
			}
			var positionDeltas = new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero, };
			var mousePositionsBeforeBonesApplication = new[] { Vector2.Zero, Vector2.Zero, Vector2.Zero, };
			using (Document.Current.History.BeginTransaction()) {
				Core.Operations.SetAnimableProperty.Perform(
					Mesh,
					nameof(Lime.Animesh.TransientVertices),
					new List<SkinnedVertex>(Vertices),
					createAnimatorIfNeeded: true,
					createInitialKeyframeForNewAnimator: true
				);
				while (sv.Input.IsMousePressed()) {
					UI.Utils.ChangeCursorIfDefault(cursor);
					var mousePositionInMeshSpace = transform.TransformVector(sv.MousePosition);
					for (int i = 0; i < hitTestTarget.Count; i++) {
						mousePositionsBeforeBonesApplication[i] =
							invSkinningMatrices[i].TransformVector(mousePositionInMeshSpace);
						positionDeltas[i] = mousePositionsBeforeBonesApplication[i] - lastPositionsBeforeBonesApplication[i];
						lastPositionsBeforeBonesApplication[i] = mousePositionsBeforeBonesApplication[i];
					}
					TranslateTransientVertices(hitTestTarget, positionDeltas);
					Core.Operations.SetAnimableProperty.Perform(
						Mesh,
						nameof(Lime.Animesh.TransientVertices),
						new List<SkinnedVertex>(Vertices),
						createAnimatorIfNeeded: true,
						createInitialKeyframeForNewAnimator: true
					);
					Mesh.Invalidate();
					yield return null;
				}
				Mesh.Animators.Invalidate();
				AnimeshModification.Animate.Perform();
				Document.Current.History.CommitTransaction();
			}
		}

		public IEnumerator<object> ModificationTask()
		{
			var transform = Mesh.LocalToWorldTransform.CalcInversed();
			var cursor = WidgetContext.Current.MouseCursor;
			var lastPos = transform.TransformVector(sv.MousePosition);
			HitTest(sv.MousePosition, sv.Scene.Scale.X, out var result);
			if (result == null || !result.Target.IsVertex()) {
				yield break;
			}
			var target = (TopologyVertex)result.Target;
			using (Document.Current.History.BeginTransaction()) {
				Mesh.Animators.TryFind(
					nameof(Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new AnimeshSlice {
					State = AnimeshTools.ModificationState.Modification,
					Vertices = new List<SkinnedVertex>(Mesh.Vertices),
					IndexBuffer = new List<TopologyFace>(Mesh.Faces),
					ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
					Keyframes = animator?.Keys.ToList()
				};
				var lastValidDelta = Vector2.Zero;
				var lastValidUVDelta = Vector2.Zero;
				Topology.VertexHitTestRadius = Topology.EdgeHitTestDistance = 0f;
				while (sv.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					var keyframes = animator?.Keys.ToList();
					UI.Utils.ChangeCursorIfDefault(cursor);
					var delta = (transform.TransformVector(sv.MousePosition) - lastPos);
					var uvDelta = delta / Mesh.Size;
					if (Topology.TranslateVertex(target.Index, delta, uvDelta, out var removedVertices)) {
						lastValidDelta = delta;
						lastValidUVDelta = uvDelta;
					} else if (lastValidDelta != Vector2.Zero) {
						Topology.TranslateVertex(target.Index, lastValidDelta, lastValidUVDelta, out removedVertices);
					}
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						var targetIndex = target.Index;
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var vertices = new List<SkinnedVertex>((List<SkinnedVertex>)newKey.Value);
							if (removedVertices != null) {
								foreach (var removedIndex in removedVertices) {
									vertices[removedIndex] = vertices[vertices.Count - 1];
									if (vertices.Count - 1 == targetIndex) {
										targetIndex = (ushort)removedIndex;
									}
									vertices.RemoveAt(vertices.Count - 1);
								}
							}
							var v = vertices[targetIndex];
							v.UV1 = Mesh.Vertices[targetIndex].UV1;
							vertices[targetIndex] = v;
							newKey.Value = vertices;
							keyframes.Add(newKey);
							animator.ResetCache();
						}
					}
					var sliceAfter = new AnimeshSlice {
						State = AnimeshTools.ModificationState.Modification,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					AnimeshModification.Slice.Perform(
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
			var mousePos = Mesh.LocalToWorldTransform.CalcInversed().TransformVector(sv.MousePosition);
			if (primitive != null) {
				if (primitive.IsEdge()) {
					var v1 = Vertices[primitive[0]].Pos;
					var v2 = Vertices[primitive[1]].Pos;
					return AnimeshUtils.PointProjectionToLine(mousePos, v1, v2, out _);
				}
			}
			return mousePos;
		}

		private void UpdateHitTestMetrics()
		{
			var vertexHitRadius = Theme.Metrics.AnimeshVertexHitTestRadius / sv.Scene.Scale.X;
			var edgeHitRadius = Theme.Metrics.AnimeshEdgeHitTestRadius / sv.Scene.Scale.X;
			Topology.VertexHitTestRadius = vertexHitRadius;
			Topology.EdgeHitTestDistance = edgeHitRadius;
		}

		private Vector2 InterpolateUV(ITopologyPrimitive primitive, Vector2 position)
		{
			if (primitive.IsFace()) {
				var v1 = Vertices[primitive[0]];
				var v2 = Vertices[primitive[1]];
				var v3 = Vertices[primitive[2]];
				var weights = AnimeshUtils.CalcTriangleRelativeBarycentricCoordinates(position, v1.Pos, v2.Pos, v3.Pos);
				return weights[0] * v1.UV1 + weights[1] * v2.UV1 + weights[2] * v3.UV1;
			}
			if (primitive.IsEdge()) {
				var v1 = Vertices[primitive[0]];
				var v2 = Vertices[primitive[1]];
				var weights = AnimeshUtils.CalcSegmentRelativeBarycentricCoordinates(position, v1.Pos, v2.Pos);
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

		public void TranslateTransientVertices(ITopologyPrimitive primitive, params Vector2[] delta)
		{
			for (var i = 0; i < primitive.Count; i++) {
				TranslateTransientVertex(primitive[i], delta[i]);
			}
		}

		private void TranslateTransientVertex(int index, Vector2 delta)
		{
			var v = Mesh.TransientVertices[index];
			v.Pos += delta;
			Mesh.TransientVertices[index] = v;
		}

		public IEnumerator<object> CreationTask()
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
					var sliceBefore = new AnimeshSlice {
						State = AnimeshTools.ModificationState.Creation,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = animator?.Keys.ToList()
					};
					var pos = SnapMousePositionToTopologyPrimitiveIfPossible(initialHitTestTarget);
					var vertex = new SkinnedVertex {
						Pos = pos,
						Color = Mesh.Color,
						UV1 = InterpolateUV(initialHitTestTarget, pos),
						BlendWeights = new Mesh3D.BlendWeights { Weight0 = 1f, },
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
					var sliceAfter = new AnimeshSlice {
						State = AnimeshTools.ModificationState.Creation,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					AnimeshModification.Slice.Perform(
						Mesh,
						sliceBefore,
						sliceAfter
					);
					Window.Current.Invalidate();
					Document.Current.History.CommitTransaction();
				}
				// Perhaps Topology.AddVertex should return an index where new vertex was placed or -1
				// if it wasn't inserted. Now we assume it's always pushed back.
				initialHitTestTarget = new TopologyVertex { Index = (ushort)(Vertices.Count - 1), };
			}

			yield return ConstrainingTask((TopologyVertex)initialHitTestTarget);
		}

		private IEnumerator<object> ConstrainingTask(TopologyVertex initialHitTestTarget)
		{
			yield return null;
			using (Document.Current.History.BeginTransaction()) {
				List<IKeyframe> keyframes = null;
				Mesh.Animators.TryFind(
					nameof(Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new AnimeshSlice {
					State = AnimeshTools.ModificationState.Creation,
					Vertices = new List<SkinnedVertex>(Mesh.Vertices),
					IndexBuffer = new List<TopologyFace>(Mesh.Faces),
					ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
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
							BlendWeights = new Mesh3D.BlendWeights { Weight0 = 1f, },
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
						Topology.InsertConstrainedEdge(startIndex, endIndex);
						var sliceAfter = new AnimeshSlice {
							State = AnimeshTools.ModificationState.Creation,
							Vertices = new List<SkinnedVertex>(Mesh.Vertices),
							IndexBuffer = new List<TopologyFace>(Mesh.Faces),
							ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
							Keyframes = keyframes
						};

						AnimeshModification.Slice.Perform(
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

		public  IEnumerator<object> RemovalTask()
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
					var sliceBefore = new AnimeshSlice {
						State = AnimeshTools.ModificationState.Removal,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
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
					} else if (hitTestTarget.IsEdge() && result.Info is TopologyEdge.EdgeInfo ei && ei.IsConstrained) {
						var edge = (TopologyEdge)hitTestTarget;
						Topology.RemoveConstrainedEdge(edge.Index0, edge.Index1);
					}
					var sliceAfter = new AnimeshSlice {
						State = AnimeshTools.ModificationState.Removal,
						Vertices = new List<SkinnedVertex>(Mesh.Vertices),
						IndexBuffer = new List<TopologyFace>(Mesh.Faces),
						ConstrainedVertices = new List<TopologyEdge>(Mesh.ConstrainedEdges),
						Keyframes = keyframes
					};
					AnimeshModification.Slice.Perform(
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
