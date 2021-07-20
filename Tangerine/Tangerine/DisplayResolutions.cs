using Lime;
using System.Collections.Generic;
using System.Linq;
using Tangerine.Core;
using Tangerine.Core.Operations;

namespace Tangerine
{
	public static class DisplayResolutions
	{
		public static IList<ResolutionInfo> Items { get; }
		static DisplayResolutions()
		{
			Items = new List<ResolutionInfo>();
		}

		private static void SetMarker(Node rootNode, string markerId)
		{
			foreach (var node in rootNode.Nodes) {
				var animation = node.DefaultAnimation;
				var marker = animation.Markers.FirstOrDefault(m => m.Id == markerId);
				if (marker != null) {
					UI.Timeline.Operations.SetCurrentColumn.Perform(marker.Frame, animation);
				}
				SetMarker(node, markerId);
			}
		}

		public static void SetResolution(ResolutionInfo resolution)
		{
			using (Document.Current.History.BeginTransaction()) {
				SetProperty.Perform(Document.Current.RootNode, nameof(Widget.Size), resolution.Size);
				SetMarker(Document.Current.RootNode, resolution.MarkerId);
				Document.Current.History.CommitTransaction();
			}
		}
	}

	public class ResolutionInfo
	{
		public string Name { get; set; }
		public string MarkerId { get; set; }
		public Vector2 Size { get; set; }

		public ResolutionInfo(string name, string markerId, Vector2 size)
		{
			Name = name;
			MarkerId = markerId;
			Size = size;
		}
	}
}
