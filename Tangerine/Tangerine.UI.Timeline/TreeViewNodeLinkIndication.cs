using System.Collections.Generic;
using System.Linq;
using Lime;
using System.Text;
using Tangerine.Core;

namespace Tangerine.UI.Timeline
{
	public class LinkIndicationCleanerProcessor : ITreeViewItemPresentationProcessor
	{
		public void Process(ITreeViewItemPresentation presentation)
		{
			if (presentation is NodeTreeViewItemPresentation p) {
				p.LinkIndicatorButtonContainer?.Clear();
			}
		}
	}

	public class ImageCombinerLinkIndicationProcessor : ITreeViewItemPresentationProcessor
	{
		public void Process(ITreeViewItemPresentation presentation)
		{
			if (presentation is NodeTreeViewItemPresentation p && p.Node is ImageCombiner ic) {
				if (p.Minimalistic) {
				} else if (ic.GetArgs(out var a1, out var a2)) {
					p.LinkIndicatorButtonContainer.GetOrAdd<ImageCombinerLinkIndicatorButton>().ShowNormal();
					var p1 = (NodeTreeViewItemPresentation) TreeViewComponent
						.GetTreeViewItem(Document.Current.GetSceneItemForObject(a1)).Presentation;
					p1.LinkIndicatorButtonContainer.GetOrAdd<ImageCombinerLinkIndicatorButton>();
					var p2 = (NodeTreeViewItemPresentation) TreeViewComponent
						.GetTreeViewItem(Document.Current.GetSceneItemForObject(a2)).Presentation;
					p2.LinkIndicatorButtonContainer.GetOrAdd<ImageCombinerLinkIndicatorButton>();
				} else {
					p.Label.Color = Theme.Colors.RedText;
					p.LinkIndicatorButtonContainer.GetOrAdd<ImageCombinerLinkIndicatorButton>().ShowError();
				}
			}
		}

		private class ImageCombinerLinkIndicatorButton : LinkIndicatorButton
		{
			public ImageCombinerLinkIndicatorButton() : base(NodeIconPool.GetTexture(typeof(ImageCombiner)))
			{
				Tooltip = "Linked to ImageCombiner";
			}

			private void SetTooltipAndTexture(string tooltip, ITexture texture)
			{
				Tooltip = tooltip;
				Texture = texture;
			}

			public void ShowNormal(string tooltip = "Has linked arguments") =>
				SetTooltipAndTexture(tooltip, NodeIconPool.GetTexture(typeof(ImageCombiner)));

			public void ShowError(string tooltip = "No linked arguments") =>
				SetTooltipAndTexture(tooltip, IconPool.GetTexture("Timeline.NoEntry"));
		}
	}

	public class BoneLinkIndicationProcessor : ITreeViewItemPresentationProcessor
	{
		private readonly StringBuilder labelBuilder = new StringBuilder();

		public void Process(ITreeViewItemPresentation presentation)
		{
			labelBuilder.Clear();
			if (presentation is NodeTreeViewItemPresentation p) {
				if (!p.Minimalistic) {
					if (p.Node is Bone bone) {
						var nodes = GetLinkedNodes(bone).ToList();
						if (nodes.Count > 0) {
							labelBuilder.Append(p.Label.Text);
							labelBuilder.Append(" : ");
							var b = p.LinkIndicatorButtonContainer.GetOrAdd<BoneLinkIndicatorButton>();
							foreach (var ln in nodes) {
								b.AddLinkedNode(ln);
								if (ln != nodes[0]) {
									labelBuilder.Append(", ");
								}
								labelBuilder.Append(ln.Id);
							}
							p.Label.Text = labelBuilder.ToString();
						}
					} else {
						var bones = GetLinkedBones(p.Node).ToList();
						if (bones.Count > 0) {
							var b = p.LinkIndicatorButtonContainer.GetOrAdd<BoneLinkIndicatorButton>();
							foreach (var lb in bones) {
								b.AddLinkedNode(lb);
							}
						}
					}
 				}
			}
		}

		private IEnumerable<Node> GetLinkedNodes(Bone bone)
		{
			foreach (var node in bone.Parent.Nodes) {
				if (node is DistortionMesh mesh) {
					if (mesh.Nodes.Any(n => IsBoneBound(bone, ((DistortionMeshPoint) n).SkinningWeights))) {
						yield return mesh;
					}
				} else if (node is Animesh animesh) {
					foreach (var v in animesh.Vertices) {
						if (IsBoneBound(bone, v.BlendIndices)) {
							yield return animesh;
							break;
						}
					}
				} else if (node is Widget widget) {
					if (IsBoneBound(bone, widget.SkinningWeights)) {
						yield return node;
					}
				}
			}
		}

		private bool IsBoneBound(Bone bone, in Mesh3D.BlendIndices indices)
		{
			return
				indices.Index0 == bone.Index ||
				indices.Index1 == bone.Index ||
				indices.Index2 == bone.Index ||
				indices.Index3 == bone.Index;
		}

		private bool IsBoneBound(Bone bone, SkinningWeights sw)
		{
			return sw != null && !sw.IsEmpty() && (
				sw.Bone0.Index == bone.Index || sw.Bone1.Index == bone.Index ||
				sw.Bone2.Index == bone.Index || sw.Bone3.Index == bone.Index);
		}

		private IEnumerable<Bone> GetLinkedBones(Node node)
		{
			if (node is DistortionMesh mesh) {
				foreach (var n in node.Parent.Nodes) {
					if (n is Bone bone) {
						foreach (var p in mesh.Nodes) {
							if (IsBoneBound(bone, ((DistortionMeshPoint) p).SkinningWeights)) {
								yield return bone;
								break;
							}
						}
					}
				}
			} else if (node is Animesh animesh) {
				foreach (var n in node.Parent.Nodes) {
					if (n is Bone bone) {
						foreach (var v in animesh.Vertices) {
							if (IsBoneBound(bone, v.BlendIndices)) {
								yield return bone;
								break;
							}
						}
					}
				}
			} else if (node is Widget widget) {
				foreach (var n in node.Parent.Nodes) {
					if (n is Bone bone && IsBoneBound(bone, widget.SkinningWeights)) {
						yield return bone;
					}
				}
			}
		}

		private class BoneLinkIndicatorButton : LinkIndicatorButton
		{
			public BoneLinkIndicatorButton() : base(NodeIconPool.GetTexture(typeof(Bone)))
			{
				Tooltip = "Linked to Bone(s)";
			}
		}
	}

	public class SplineGearLinkIndicationProcessor : ITreeViewItemPresentationProcessor
	{
		public void Process(ITreeViewItemPresentation presentation)
		{
			if (presentation is NodeTreeViewItemPresentation p) {
				if (p.Minimalistic) {
					return;
				}
				if (p.Node is SplineGear sg1) {
					if (sg1.Spline != null) {
						p.LinkIndicatorButtonContainer.GetOrAdd<SplineGearLinkIndicatorButton>().AddLinkedNode(sg1.Spline);
					}
					if (sg1.Widget != null) {
						p.LinkIndicatorButtonContainer.GetOrAdd<SplineGearLinkIndicatorButton>().AddLinkedNode(sg1.Widget);
					}
				} else {
					foreach (var sibling in p.Node.Parent.Nodes) {
						if (sibling is SplineGear sg2 && (sg2.Widget == p.Node || sg2.Spline == p.Node)) {
							var b = p.LinkIndicatorButtonContainer.GetOrAdd<SplineGearLinkIndicatorButton>();
							b.AddLinkedNode(sibling);
						}
					}
				}
			}
		}

		private class SplineGearLinkIndicatorButton : LinkIndicatorButton
		{
			public SplineGearLinkIndicatorButton() : base(NodeIconPool.GetTexture(typeof(SplineGear)))
			{
				Tooltip = "Linked to Widget(s)";
			}
		}
	}

	public class SplineGear3DLinkIndicationProcessor : ITreeViewItemPresentationProcessor
	{
		public void Process(ITreeViewItemPresentation presentation)
		{
			if (presentation is NodeTreeViewItemPresentation p) {
				if (p.Minimalistic) {
					return;
				}
				if (p.Node is SplineGear3D sg1) {
					if (sg1.Spline != null) {
						p.LinkIndicatorButtonContainer.GetOrAdd<SplineGear3DLinkIndicatorButton>().AddLinkedNode(sg1.Spline);
					}
					if (sg1.Node != null) {
						p.LinkIndicatorButtonContainer.GetOrAdd<SplineGear3DLinkIndicatorButton>().AddLinkedNode(sg1.Node);
					}
				} else {
					foreach (var sibling in p.Node.Parent.Nodes) {
						if (sibling is SplineGear3D sg2 && (sg2.Node == p.Node || sg2.Spline == p.Node)) {
							var b = p.LinkIndicatorButtonContainer.GetOrAdd<SplineGear3DLinkIndicatorButton>();
							b.AddLinkedNode(sibling);
						}
					}
				}
			}
		}

		private class SplineGear3DLinkIndicatorButton : LinkIndicatorButton
		{
			public SplineGear3DLinkIndicatorButton() : base(NodeIconPool.GetTexture(typeof(SplineGear)))
			{
				Tooltip = "Linked to Node3D(s)";
			}
		}
	}
}
