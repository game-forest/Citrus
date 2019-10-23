using Lime;
using Lime.PolygonMesh.Topology;
using System;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Edge = Lime.PolygonMesh.PolygonMesh.Edge;
using Face = Lime.PolygonMesh.PolygonMesh.Face;
using ModificationMode = Lime.PolygonMesh.PolygonMesh.ModificationMode;

namespace Tangerine.UI.SceneView.PolygonMesh
{
	[YuzuDontGenerateDeserializer]
	[AllowedComponentOwnerTypes(typeof(Lime.PolygonMesh.PolygonMesh))]
	public abstract class PolygonMeshController : NodeComponent
	{
		protected static SceneView sv => SceneView.Instance;

		public enum ModificationState
		{
			Animation,
			Triangulation,
			Creation,
			Removal
		}

		private ModificationState state;
		internal ModificationState State
		{
			get => state;
			set
			{
				state = value;
				switch (value) {
					case ModificationState.Animation:
						Mode = ModificationMode.Animation;
						break;
					case ModificationState.Triangulation:
					case ModificationState.Creation:
					case ModificationState.Removal:
						Mode = ModificationMode.Setup;
						break;
				}
			}
		}

		public struct PolygonMeshSlice
		{
			public ModificationState State;
			public List<Vertex> Vertices;
			public List<Face> IndexBuffer;
			public List<Edge> ConstrainedVertices;
			public List<IKeyframe> Keyframes;
		}

		private ModificationMode mode;
		protected ModificationMode Mode
		{
			get => mode;
			set
			{
				mode = value;
				Update();
			}
		}

		public abstract void Update();
		public abstract bool HitTest(Widget context, Vector2 position, float scale);
		public abstract void Render(Widget renderContext);
		public abstract void Render(Widget renderContext, Widget hitTestContext, Vector2 position, float scale);
		public abstract IEnumerator<object> AnimationTask();
		public abstract IEnumerator<object> TriangulationTask();
		public abstract IEnumerator<object> CreationTask();
		public abstract IEnumerator<object> RemovalTask();
	}

	[YuzuDontGenerateDeserializer]
	[AllowedComponentOwnerTypes(typeof(Lime.PolygonMesh.PolygonMesh))]
	public abstract class TopologyController : PolygonMeshController
	{
		public static class Policies
		{
			public static class Rendering
			{
				public static readonly TopologyDataType[] Order = new[] {
					TopologyDataType.Edge,
					TopologyDataType.Vertex
				};
			}

			public static class HitTesting
			{
				public static readonly Dictionary<ModificationState, TopologyDataType[]> Order =
					new Dictionary<ModificationState, TopologyDataType[]> {
						[ModificationState.Animation] = new[] {
							TopologyDataType.Vertex,
							TopologyDataType.Edge,
							TopologyDataType.Face
						},

						[ModificationState.Triangulation] = new[] {
							TopologyDataType.Vertex,
						},

						[ModificationState.Creation] = new[] {
							TopologyDataType.Vertex,
							TopologyDataType.Edge,
							TopologyDataType.Face
						},

						[ModificationState.Removal] = new[] {
							TopologyDataType.Vertex,
						}
					};
			}
		}

		public ITopology Topology { get; protected set; }

		public ITopologyAggregator TopologyAggregator { get; protected set; }

		public ITopologyModificator TopologyModificator { get; protected set; }

		public (TopologyDataType Type, int Index) HitTestTarget { get; protected set; }

		public (TopologyDataType Type, int Index) NullHitTestTarget => (TopologyDataType.None, -1);

		public override void Update()
		{
			switch (Mode) {
				case ModificationMode.Animation:
					Topology.EmplaceVertices(Topology.Mesh.TransientVertices);
					break;
				case ModificationMode.Setup:
					Topology.EmplaceVertices(Topology.Mesh.Vertices);
					break;
			}
			Topology.Mesh.Mode = Mode;
		}
	}

	[YuzuDontGenerateDeserializer]
	[AllowedComponentOwnerTypes(typeof(Lime.PolygonMesh.PolygonMesh))]
	public sealed class TopologyController<T> : TopologyController where T : ITopology
	{
		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (Owner is Lime.PolygonMesh.PolygonMesh mesh) {
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
				Topology = (T)Activator.CreateInstance(typeof(T), Owner);
				switch (Topology) {
					case HalfEdgeTopology het:
						TopologyAggregator = new HalfEdgeTopologyAggregator(het);
						TopologyModificator = new HalfEdgeTopologyModificator(het);
						break;
				}
				if (mesh.TransientVertices == null) {
					mesh.TransientVertices = new List<Vertex>(mesh.Vertices);
				}
				State = PolygonMeshTools.ControllerStateBeforeClone;
			} else if (oldOwner != null) {
				oldOwner.Components.Remove(this);
			}
		}

		protected override void OnBeforeNodeSerialization()
		{
			base.OnBeforeNodeSerialization();
			PolygonMeshTools.ControllerStateBeforeClone = State;
			if (Owner != null) {
				Owner.Components.Remove(this);
			}
		}

		public override bool HitTest(Widget context, Vector2 position, float scale)
		{
			Update();
			HitTestTarget = NullHitTestTarget;
			position = context.CalcTransitionToSpaceOf(Owner.AsWidget).TransformVector(position);
			foreach (var type in Policies.HitTesting.Order[State]) {
				if (HitTestHelper(position, scale, type)) {
					return true;
				}
			}
			return false;
		}

		private bool HitTestHelper(Vector2 position, float scale, TopologyDataType type)
		{
			var index = -1;
			var success = false;
			var minDistance = float.MaxValue;
			var data = TopologyAggregator[type];
			for (var i = 0; i < data.Count; ++i) {
				if (data[i].HitTest(Topology, position, out var distance, Owner.AsWidget.Size, scale)) {
					if (distance < minDistance) {
						minDistance = distance;
						index = i;
					}
				}
			}
			if (success = index != -1) {
				HitTestTarget = (type, index);
			}
			return success;
		}

		public override void Render(Widget renderContext, Widget hitTestContext, Vector2 position, float scale)
		{
			HitTest(hitTestContext, position, scale);
			Render(renderContext);
		}

		public override void Render(Widget renderContext)
		{
			Update();
			Renderer.Transform1 = renderContext.LocalToWorldTransform;
			Renderer.Blending = renderContext.GlobalBlending;
			Renderer.Shader = renderContext.GlobalShader;
			var transform = Owner.AsWidget.CalcTransitionToSpaceOf(renderContext);

			if (HitTestTarget.Type == TopologyDataType.Face) {
				TopologyAggregator[HitTestTarget.Type][HitTestTarget.Index].RenderHovered(
					Topology,
					transform,
					State,
					Owner.AsWidget.Size
				);
			}

			foreach (var type in Policies.Rendering.Order) {
				for (var i = 0; i < TopologyAggregator[type].Count; ++i) {
					if (
						HitTestTarget.Type == type &&
						HitTestTarget.Index == i &&
						Policies.HitTesting.Order[State].Contains(type) ||
						State == ModificationState.Removal &&
						HitTestTarget != NullHitTestTarget &&
						TopologyAggregator[HitTestTarget.Type, HitTestTarget.Index].Contains((type, i))
					) {
						TopologyAggregator[type][i].RenderHovered(Topology, transform, State, Owner.AsWidget.Size);
						continue;
					}
					TopologyAggregator[type][i].Render(Topology, transform, Owner.AsWidget.Size);
				}
			}
			if (
				State == ModificationState.Creation &&
				(
					HitTestTarget.Type == TopologyDataType.Edge ||
					HitTestTarget.Type == TopologyDataType.Face
				)
			) {
				Utils.RenderVertex(
					transform.TransformVector(ClampMousePositionToHitTestTarget() * Owner.AsWidget.Size),
					Theme.Metrics.PolygonMeshBackgroundVertexRadius,
					Theme.Metrics.PolygonMeshVertexRadius,
					Color4.White.Transparentify(0.5f),
					Color4.Yellow.Lighten(0.5f).Transparentify(0.5f)
				);
			}
		}

		public override IEnumerator<object> AnimationTask()
		{
			var transform = sv.Scene.CalcTransitionToSpaceOf(Topology.Mesh);
			var cursor = WidgetContext.Current.MouseCursor;
			var lastPos = transform.TransformVector(sv.MousePosition);
			var target = HitTestTarget;
			using (Document.Current.History.BeginTransaction()) {
				while (SceneView.Instance.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					UI.Utils.ChangeCursorIfDefault(cursor);
					var positionDelta = (transform.TransformVector(sv.MousePosition) - lastPos) / Topology.Mesh.Size;
					lastPos = transform.TransformVector(sv.MousePosition);
					TopologyModificator.Translate(
						TopologyAggregator[target.Type][target.Index],
						positionDelta
					);
					Core.Operations.SetAnimableProperty.Perform(
						Topology.Mesh,
						nameof(Lime.PolygonMesh.PolygonMesh.TransientVertices),
						new List<Vertex>(Topology.Vertices),
						createAnimatorIfNeeded: true,
						createInitialKeyframeForNewAnimator: true
					);
					yield return null;
				}
				Topology.Mesh.Animators.Invalidate();
				PolygonMeshModification.Animate.Perform();
				Document.Current.History.CommitTransaction();
			}
		}

		public override IEnumerator<object> TriangulationTask()
		{
			var transform = sv.Scene.CalcTransitionToSpaceOf(Topology.Mesh);
			var cursor = WidgetContext.Current.MouseCursor;
			var lastPos = transform.TransformVector(sv.MousePosition);
			var target = HitTestTarget;
			using (Document.Current.History.BeginTransaction()) {
				List<IKeyframe> keyframes = null;
				Topology.Mesh.Animators.TryFind(
					nameof(Topology.Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new PolygonMeshSlice {
					State = ModificationState.Triangulation,
					Vertices = new List<Vertex>(Topology.Mesh.Vertices),
					IndexBuffer = new List<Face>(Topology.Mesh.Faces),
					ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
					Keyframes = animator?.Keys.ToList()
				};
				while (SceneView.Instance.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					keyframes = animator?.Keys.ToList();
					UI.Utils.ChangeCursorIfDefault(cursor);
					var delta = (transform.TransformVector(sv.MousePosition) - lastPos) / Topology.Mesh.Size;
					var modifyStructure = Topology.Mesh.Vertices.Count > 3;
					TopologyModificator.TranslateVertex(target.Index, delta, modifyStructure);
					TopologyModificator.TranslateVertexUV(target.Index, delta);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var v = (newKey.Value as List<Vertex>)[target.Index];
							v.UV1 = Topology.Mesh.Vertices[target.Index].UV1;
							(newKey.Value as List<Vertex>)[target.Index] = v;
							keyframes.Add(newKey);
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = ModificationState.Triangulation,
						Vertices = new List<Vertex>(Topology.Mesh.Vertices),
						IndexBuffer = new List<Face>(Topology.Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(
						Topology.Mesh,
						sliceBefore,
						sliceAfter
					);
					yield return null;
				}
				Document.Current.History.CommitTransaction();
			}
			yield return null;
		}

		private Vector2 ClampMousePositionToHitTestTarget()
		{
			var transform = sv.Scene.CalcTransitionToSpaceOf(Topology.Mesh);
			var pos = transform.TransformVector(sv.MousePosition) / Topology.Mesh.Size;
			var data = TopologyAggregator[HitTestTarget.Type][HitTestTarget.Index];
			if (data is EdgeData ed) {
				var v0 = Topology.Vertices[ed.TopologicalIndex0];
				var v1 = Topology.Vertices[ed.TopologicalIndex1];
				pos = Lime.PolygonMesh.Utils.PolygonMeshUtils.PointProjectionToLine(
					pos,
					v0.Pos,
					v1.Pos,
					out var isInside
				);
			}
			return pos;
		}

		private Vertex CreateVertex()
		{
			var pos = ClampMousePositionToHitTestTarget();
			var data = TopologyAggregator[HitTestTarget.Type][HitTestTarget.Index];
			return new Vertex {
				Pos = pos,
				UV1 = data.InterpolateUV(Topology, pos),
				Color = Topology.Mesh.Color
			};
		}

		public override IEnumerator<object> CreationTask()
		{
			var cursor = WidgetContext.Current.MouseCursor;
			var initialHitTestTarget = (TopologyDataType.Vertex, HitTestTarget.Index);
			if (HitTestTarget.Type != TopologyDataType.Vertex) {
				using (Document.Current.History.BeginTransaction()) {
					List<IKeyframe> keyframes = null;
					Topology.Mesh.Animators.TryFind(
						nameof(Topology.Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new PolygonMeshSlice {
						State = ModificationState.Creation,
						Vertices = new List<Vertex>(Topology.Mesh.Vertices),
						IndexBuffer = new List<Face>(Topology.Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
						Keyframes = animator?.Keys.ToList()
					};
					var vertex = CreateVertex();
					TopologyModificator.AddVertex(vertex);
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
					var sliceAfter = new PolygonMeshSlice {
						State = ModificationState.Creation,
						Vertices = new List<Vertex>(Topology.Mesh.Vertices),
						IndexBuffer = new List<Face>(Topology.Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(
						Topology.Mesh,
						sliceBefore,
						sliceAfter
					);
					Window.Current.Invalidate();
					Document.Current.History.CommitTransaction();
				}
				initialHitTestTarget.Index = Topology.Mesh.Vertices.Count - 1;
			}

			yield return ConstrainingTask(initialHitTestTarget);
		}

		private IEnumerator<object> ConstrainingTask((TopologyDataType Type, int Index) initialHitTestTarget)
		{
			yield return null;
			using (Document.Current.History.BeginTransaction()) {
				List<IKeyframe> keyframes = null;
				Topology.Mesh.Animators.TryFind(
					nameof(Topology.Mesh.TransientVertices),
					out var animator
				);
				var sliceBefore = new PolygonMeshSlice {
					State = ModificationState.Creation,
					Vertices = new List<Vertex>(Topology.Mesh.Vertices),
					IndexBuffer = new List<Face>(Topology.Mesh.Faces),
					ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
					Keyframes = animator?.Keys.ToList()
				};
				while (sv.Input.IsMousePressed()) {
					Document.Current.History.RollbackTransaction();
					keyframes = animator?.Keys.ToList();
					HitTest(sv.Scene, sv.MousePosition, sv.Scene.Scale.X);
					if (
						HitTestTarget == NullHitTestTarget ||
						HitTestTarget == initialHitTestTarget
					) {
						yield return null;
						continue;
					}
					var startIndex = initialHitTestTarget.Index;
					var endIndex = HitTestTarget.Index;
					if (HitTestTarget.Type != TopologyDataType.Vertex) {
						var vertex = CreateVertex();
						TopologyModificator.AddVertex(vertex);
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
						endIndex = Topology.Mesh.Vertices.Count - 1;
					}
					TopologyModificator.ConstrainEdge(startIndex, endIndex);
					var sliceAfter = new PolygonMeshSlice {
						State = ModificationState.Creation,
						Vertices = new List<Vertex>(Topology.Mesh.Vertices),
						IndexBuffer = new List<Face>(Topology.Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
						Keyframes = keyframes
					};

					PolygonMeshModification.Slice.Perform(
						Topology.Mesh,
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
					List<IKeyframe> keyframes = null;
					Topology.Mesh.Animators.TryFind(
						nameof(Topology.Mesh.TransientVertices),
						out var animator
					);
					var sliceBefore = new PolygonMeshSlice {
						State = ModificationState.Removal,
						Vertices = new List<Vertex>(Topology.Mesh.Vertices),
						IndexBuffer = new List<Face>(Topology.Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
						Keyframes = animator?.Keys.ToList()
					};
					TopologyModificator.RemoveVertex(HitTestTarget.Index);
					if (animator != null) {
						keyframes = new List<IKeyframe>();
						foreach (var key in animator.Keys.ToList()) {
							var newKey = key.Clone();
							var vertices = new List<Vertex>(newKey.Value as List<Vertex>);
							vertices[HitTestTarget.Index] = vertices[vertices.Count - 1];
							vertices.RemoveAt(vertices.Count - 1);
							newKey.Value = vertices;
							keyframes.Add(newKey);
							animator.ResetCache();
						}
					}
					var sliceAfter = new PolygonMeshSlice {
						State = ModificationState.Removal,
						Vertices = new List<Vertex>(Topology.Mesh.Vertices),
						IndexBuffer = new List<Face>(Topology.Mesh.Faces),
						ConstrainedVertices = new List<Edge>(Topology.Mesh.ConstrainedVertices),
						Keyframes = keyframes
					};
					PolygonMeshModification.Slice.Perform(
						Topology.Mesh,
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
