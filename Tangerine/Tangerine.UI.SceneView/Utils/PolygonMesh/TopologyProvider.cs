//using Lime.PolygonMesh;
//using System;
//using System.Collections.Generic;

//namespace Tangerine.UI.SceneView.Utils.PolygonMesh
//{
//	// Redundant once Topology is stored AS a component
//	internal class TopologyProvider
//	{
//		private static readonly Dictionary<int, ITopology> topologiesCache = new Dictionary<int, ITopology>();

//		public ITopology TryGetCachedTopology(Lime.PolygonMesh.PolygonMesh mesh)
//		{
//			//mesh.Components.GetOrAdd<MeshTopologyComponent>()
//			var key = mesh.GetHashCode();
//			if (!topologiesCache.TryGetValue(key, out var topology)) {
//				topology = CreateTopology(mesh);
//				topologiesCache[key] = topology;
//			}
//			switch (mesh.CurrentModificationContext) {
//				case Lime.PolygonMesh.PolygonMesh.ModificationContext.Animation:
//					topology.Vertices = mesh.AnimatorVertices ?? mesh.TriangulationVertices;
//					break;
//				case Lime.PolygonMesh.PolygonMesh.ModificationContext.Setup:
//					topology.Vertices = mesh.TriangulationVertices;
//					break;
//				default:
//					throw new NotSupportedException();
//			}
//			return topology;
//		}

//		public ITopology CreateTopology(Lime.PolygonMesh.PolygonMesh mesh)
//		{
//			if (mesh.TriangulationVertices.Count == 0) {
//				mesh.TriangulationVertices.Add(new Lime.Vertex {
//					Pos = Lime.Vector2.Zero,
//					UV1 = Lime.Vector2.Zero,
//					Color = mesh.Color
//				});
//				mesh.TriangulationVertices.Add(new Lime.Vertex {
//					Pos = new Lime.Vector2(1.0f, 0.0f),
//					UV1 = new Lime.Vector2(1.0f, 0.0f),
//					Color = mesh.Color
//				});
//				mesh.TriangulationVertices.Add(new Lime.Vertex {
//					Pos = new Lime.Vector2(0.0f, 1.0f),
//					UV1 = new Lime.Vector2(0.0f, 1.0f),
//					Color = mesh.Color
//				});
//				mesh.TriangulationVertices.Add(new Lime.Vertex {
//					Pos = new Lime.Vector2(1.0f, 1.0f),
//					UV1 = new Lime.Vector2(1.0f, 1.0f),
//					Color = mesh.Color
//				});
//			} else {
//				// I dunno, prolly ask triangulator
//			}
//			return new HalfEdgeTopology(mesh.TriangulationVertices, mesh.IndexBuffer, mesh);
//		}
//	}
//}
