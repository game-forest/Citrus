using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Lime;
using Orange;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Node = Lime.Node;

namespace Tangerine
{
	using TimelineAnimationCommandData = Tuple<Node, Action<SceneItem, SceneItem>>;

	public class GroupNodes : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var items = Document.Current.SelectedSceneItems();
			if (items.Any()) {
				var group = Common.Operations.GroupSceneItems.Perform(items);
				ClearSceneItemSelection.Perform();
				SelectNode.Perform(group);
			}
		}
	}

	public class UngroupNodes : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var groups = Document.Current.SelectedSceneItems()
				.Select(i => i.GetNode()).OfType<Frame>().ToList();
			if (groups.Count == 0) {
				return;
			}
			ClearSceneItemSelection.Perform();
			var items = Common.Operations.UngroupSceneItems.Perform(groups);
			foreach (var i in items) {
				SelectSceneItem.Perform(i, true);
			}
		}

		public override bool GetEnabled() => Core.Document.Current.SelectedNodes().Any(i => i is Frame);
	}

	public class InsertTimelineColumn : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			TimelineHorizontalShift.Perform(UI.Timeline.Timeline.Instance.CurrentColumn, 1);
		}
	}

	public class RemoveTimelineColumn : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			TimelineColumnRemove.Perform(UI.Timeline.Timeline.Instance.CurrentColumn);
		}
	}

	public class AddAnimation : DocumentCommandHandler
	{
		public enum AnimationType
		{
			Default,
			Compound,
			ZeroPose,
		}

		private AnimationType type;

		private Node Node
		{
			get
			{
				switch (type) {
					case AnimationType.ZeroPose:
						return (TimelineCommands.AddZeroPoseAnimation.UserData as TimelineAnimationCommandData)?.Item1;
					case AnimationType.Compound:
						return (TimelineCommands.AddCompoundAnimation.UserData as TimelineAnimationCommandData)?.Item1;
					case AnimationType.Default:
						return (TimelineCommands.AddAnimation.UserData as TimelineAnimationCommandData)?.Item1;
				}
				throw new InvalidDataException();
			}
		}

		private Action<SceneItem, SceneItem> Callback
		{
			get
			{
				switch (type) {
					case AnimationType.ZeroPose:
						return (TimelineCommands.AddZeroPoseAnimation.UserData as TimelineAnimationCommandData)?.Item2;
					case AnimationType.Compound:
						return (TimelineCommands.AddCompoundAnimation.UserData as TimelineAnimationCommandData)?.Item2;
					case AnimationType.Default:
						return (TimelineCommands.AddAnimation.UserData as TimelineAnimationCommandData)?.Item2;
				}
				throw new InvalidDataException();
			}
		}

		public override bool GetEnabled() =>
			Node != null &&
			string.IsNullOrEmpty(Node.ContentsPath) &&
			(type != AnimationType.ZeroPose || !Node.Animations.TryFind(Animation.ZeroPoseId, out _));

		public AddAnimation(AnimationType animationType)
		{
			type = animationType;
		}

		public override void ExecuteTransaction()
		{
			var node = Node;
			var animation = new Animation {
				Id = type == AnimationType.ZeroPose ? Animation.ZeroPoseId : GenerateAnimationId(node, "NewAnimation"),
				IsCompound = type == AnimationType.Compound,
			};
			var nodeSceneItem = Document.Current.GetSceneItemForObject(node);
			var animationSceneItem = LinkSceneItem.Perform(nodeSceneItem, new SceneTreeIndex(0), animation);
			Callback?.Invoke(nodeSceneItem, animationSceneItem);
			switch (type) {
				case AnimationType.Compound:
					var track = new AnimationTrack { Id = "Track1" };
					var animationTrackSceneItem = LinkSceneItem.Perform(
						animationSceneItem, new SceneTreeIndex(0), track);
					SelectSceneItem.Perform(animationTrackSceneItem);
					break;
				case AnimationType.ZeroPose:
					foreach (var a in node.Descendants.SelectMany(n => n.Animators).ToList()) {
						var (propertyData, animable, index) =
							AnimationUtils.GetPropertyByPath(a.Owner, a.TargetPropertyPath);
						var zeroPoseKey = Keyframe.CreateForType(propertyData.Info.PropertyType);
						zeroPoseKey.Value = index == -1
							? propertyData.Info.GetValue(animable)
							: propertyData.Info.GetValue(animable, new object[] { index });
						zeroPoseKey.Function = KeyFunction.Step;
						SetKeyframe.Perform(a.Owner, a.TargetPropertyPath, animation, zeroPoseKey);
					}
					break;
			}
			NavigateToAnimation.Perform(animation);
		}

		private static string GenerateAnimationId(Node node, string prefix)
		{
			var animations = GetAnimations(node);
			for (int i = 1; ; i++) {
				var id = prefix + (i > 1 ? i.ToString() : string.Empty);
				if (animations.All(a => a.Id != id)) {
					return id;
				}
			}
		}

		private static List<Animation> GetAnimations(Node node)
		{
			var usedAnimations = new HashSet<string>();
			var animations = new List<Animation>();
			var ancestor = node;
			while (true) {
				foreach (var a in ancestor.Animations) {
					if (!a.IsLegacy && usedAnimations.Add(a.Id)) {
						animations.Add(a);
					}
				}
				if (ancestor == Document.Current.RootNode) {
					return animations;
				}
				ancestor = ancestor.Parent;
			}
		}
	}

	public class ExportScene : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var nodes = Document.Current?.SelectedNodes().Editable().ToList();
			if (nodes.Count != 1) {
				AlertDialog.Show("Please, select a single node");
				return;
			}
			if (!(nodes[0] is Widget w && NodeCompositionValidator.CanHaveChildren(w.GetType()))) {
				AlertDialog.Show($"Can't export {nodes[0].GetType()}");
				return;
			}
			Export(w);
		}

		public static void Export(Node node)
		{
			var dlg = new FileDialog {
				AllowedFileTypes = new string[] { Document.Current.GetFileExtension() },
				Mode = FileDialogMode.Save,
				InitialDirectory = Path.GetDirectoryName(Document.Current.FullPath),
			};
			if (dlg.RunModal()) {
				if (!Project.Current.TryGetAssetPath(dlg.FileName, out var assetPath)) {
					AlertDialog.Show("Can't save the document outside the project directory");
				} else {
					try {
						var clone = node.Clone().AsWidget;
						clone.Position = Vector2.Zero;
						clone.Visible = true;
						clone.LoadExternalScenes();
						clone.ContentsPath = null;
						int removedAnimatorsCount = clone.RemoveDanglingAnimators();
						Document.ExportNodeToFile(dlg.FileName, assetPath, clone);
						if (removedAnimatorsCount != 0) {
							var message =
								"Your exported content has references to external animations. It's forbidden.\n";
							if (removedAnimatorsCount == 1) {
								message += "1 dangling animator has been removed!";
							} else {
								message += $"{removedAnimatorsCount} dangling animators have been removed!";
							}
							Document.Current.ShowWarning(message);
						}
					} catch (System.Exception e) {
						AlertDialog.Show(e.Message);
					}
				}
			}
		}
	}

	public class InlineExternalScene : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var nodes = Document.Current?.SelectedNodes().Editable().ToList();
			if (nodes.Count != 1) {
				AlertDialog.Show("Please, select a single node");
				return;
			}
			if (
				!(
					nodes[0] is Widget w
					&& NodeCompositionValidator.CanHaveChildren(w.GetType())
					&& Document.Current != null
				)
			) {
				AlertDialog.Show($"Can't inline {nodes[0].GetType()}");
				return;
			}
			var node = nodes[0];
			var clone = node.Clone();
			clone.ContentsPath = null;
			var nodeItem = Document.Current.GetSceneItemForObject(node);
			var parentItem = nodeItem.Parent;
			var index = parentItem.SceneItems.IndexOf(nodeItem);
			UnlinkSceneItem.Perform(nodeItem);
			LinkSceneItem.Perform(parentItem, new SceneTreeIndex(index), clone);
		}
	}

	public class UpsampleAnimationTwice : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			foreach (var n in Document.Current.Container.Nodes) {
				UpsampleNodeAnimation(n);
			}
		}

		protected void UpsampleNodeAnimation(Node node)
		{
			foreach (var a in node.Animations) {
				foreach (var m in a.Markers) {
					SetProperty.Perform(m, "Frame", m.Frame * 2);
				}
			}
			foreach (var a in node.Animators) {
				foreach (var k in a.Keys) {
					SetProperty.Perform(k, "Frame", k.Frame * 2);
				}
			}
			foreach (var n in node.Nodes) {
				UpsampleNodeAnimation(n);
			}
		}
	}

	public class GeneratePreview : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			if (Document.Current.Format == DocumentFormat.Tan) {
				DocumentPreview.Generate(CompressionFormat.Png);
			}
		}
	}
}
