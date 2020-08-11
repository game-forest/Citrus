using System;
using System.Collections.Generic;
using Lime;

using System.Linq;

namespace Tangerine.Core.Components
{
	public sealed class CurveRow : Component
	{
		public Node Node { get; }
		public IAnimator Animator { get; }
		public CurveEditorState State { get; }

		public CurveRow(Node node, IAnimator animator, CurveEditorState state)
		{
			Node = node;
			Animator = animator;
			State = state;
		}
	}

	public sealed class NodeRow : Component
	{
		public Node Node { get; set; }
	}

	public sealed class FolderRow : Component
	{
		public Folder.Descriptor Folder { get; set; }
	}

	public sealed class PropertyRow : Component
	{
		public Node Node { get; set; }
		public IAnimator Animator { get; set; }
	}

	public sealed class BoneRow : Component
	{
		public Bone Bone { get; set; }
	}
	
	public sealed class AnimationRow : Component
	{
		public Animation Animation { get; set; }
	}

	public sealed class AnimationTrackRow : Component
	{
		public AnimationTrack Track { get; set; }
	}
	
	[MutuallyExclusiveDerivedComponents]
	public abstract class CommonRowData : Component
	{
		public abstract string Id { get; set; }
	}

	public sealed class CommonNodeRowData : CommonRowData
	{
		public Node Node { get; set; }

		public override string Id
		{
			get => Node.Id;
			set => Node.Id = value;
		}
	}

	public sealed class CommonFolderRowData : CommonRowData
	{
		public Folder.Descriptor Folder { get; set; }
		
		public override string Id
		{
			get => Folder.Id;
			set => Folder.Id = value;
		}
	}
	
	public sealed class CommonPropertyRowData : CommonRowData
	{
		public IAnimator Animator { get; set; }
		
		public override string Id
		{
			get => Animator.TargetPropertyPath;
			set { }
		}
	}

	public class CommonAnimationRowData : CommonRowData
	{
		public Animation Animation { get; set; }
		
		public override string Id { get; set; }
	}

	public sealed class CommonAnimationTrackRowData : CommonRowData
	{
		public AnimationTrack Track { get; set; }

		public override string Id
		{
			get => Track.Id;
			set => Track.Id = value;
		}
	}

	[NodeComponentDontSerialize]
	public class TimelineOffset : NodeComponent
	{
		public Vector2 Offset { get; set; }
	}
}
