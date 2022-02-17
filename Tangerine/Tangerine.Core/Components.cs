using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core.Operations;

namespace Tangerine.Core.Components
{
	[NodeComponentDontSerialize]
	public class TimelineSceneItemStateComponent : Component
	{
		public bool NodesExpandable { get; set; }

		public bool NodesExpanded { get; set; }

		public bool AnimatorsExpandable { get; set; }

		public bool AnimatorsExpanded { get; set; }

		public bool Selected
		{
			get => SelectionOrder > 0;
			set
			{
				if (Selected != value) {
					SelectionOrder = value ? selectionCounter++ : 0;
				}
			}
		}

		private static int selectionCounter = 1;

		public int SelectionOrder { get; private set; }

		public int Index { get; set; }
	}

	public sealed class CurveSceneItem : Component
	{
		public Node Node { get; }
		public IAnimator Animator { get; }
		public CurveEditorState State { get; }

		public CurveSceneItem(Node node, IAnimator animator, CurveEditorState state)
		{
			Node = node;
			Animator = animator;
			State = state;
		}
	}

	public sealed class NodeSceneItem : Component
	{
		public Node Node { get; set; }
	}

	public sealed class FolderSceneItem : Component
	{
		public Folder.Descriptor Folder { get; set; }
	}

	public sealed class AnimatorSceneItem : Component
	{
		public Node Node { get; set; }
		public IAnimator Animator { get; set; }
	}

	public sealed class BoneSceneItem : Component
	{
		public Bone Bone { get; set; }
	}

	public sealed class AnimationSceneItem : Component
	{
		public Animation Animation { get; set; }
	}

	public sealed class MarkerSceneItem : Component
	{
		public Marker Marker { get; set; }
	}

	public sealed class AnimationTrackSceneItem : Component
	{
		public AnimationTrack Track { get; set; }
	}

	[AllowOnlyOneComponent]
	public abstract class CommonSceneItemData : Component
	{
		public abstract string Id { get; set; }
	}

	public sealed class CommonNodeSceneItemData : CommonSceneItemData
	{
		public Node Node { get; set; }

		public override string Id
		{
			get => Node.Id;
			set => Document.Current.History.DoTransaction(
				() => SetProperty.Perform(Node, nameof(Node.Id), value)
			);
		}
	}

	public sealed class CommonFolderSceneItemData : CommonSceneItemData
	{
		public Folder.Descriptor Folder { get; set; }

		public override string Id
		{
			get => Folder.Id;
			set => Document.Current.History.DoTransaction(
				() => SetProperty.Perform(Folder, nameof(Lime.Folder.Descriptor.Id), value)
			);
		}
	}

	public sealed class CommonPropertySceneItemData : CommonSceneItemData
	{
		public IAnimator Animator { get; set; }

		public override string Id
		{
			get => Animator.TargetPropertyPath;
			set { }
		}
	}

	public class CommonAnimationSceneItemData : CommonSceneItemData
	{
		public Animation Animation { get; set; }

		public override string Id { get; set; }
	}

	public sealed class CommonMarkerSceneItemData : CommonSceneItemData
	{
		public Marker Marker { get; set; }

		public override string Id
		{
			get => Marker.Id;
			set => Document.Current.History.DoTransaction(
				() => SetProperty.Perform(Marker, nameof(Marker.Id), value)
			);
		}
	}

	public sealed class CommonAnimationTrackSceneItemData : CommonSceneItemData
	{
		public AnimationTrack Track { get; set; }

		public override string Id
		{
			get => Track.Id;
			set => Document.Current.History.DoTransaction(
				() => SetProperty.Perform(Track, nameof(AnimationTrack.Id), value)
			);
		}
	}

	[NodeComponentDontSerialize]
	public class TimelineOffset : NodeComponent
	{
		public Vector2 Offset { get; set; }
	}
}
