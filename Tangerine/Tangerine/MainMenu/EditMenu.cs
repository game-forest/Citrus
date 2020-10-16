using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.UI;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;
using Node = Lime.Node;
using System.IO;
using Orange;

namespace Tangerine
{
	public class GroupNodes : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var group = Common.Operations.GroupNodes.Perform(Document.Current.SelectedRows());
			ClearRowSelection.Perform();
			SelectNode.Perform(group);
		}
	}

	public class UngroupNodes : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var groups = SceneTreeUtils.EnumerateSelectedTopSceneItems()
				.Select(i => i.GetNode()).OfType<Frame>().ToList();
			if (groups.Count == 0) {
				return;
			}
			ClearRowSelection.Perform();
			var nodes = Common.Operations.UngroupNodes.Perform(groups);
			foreach (var node in nodes) {
				SelectNode.Perform(node);
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
						Document.ExportNodeToFile(dlg.FileName, assetPath, Document.Current.Format, clone);
						if (removedAnimatorsCount != 0) {
							var message = "Your exported content has references to external animations. It's forbidden.\n";
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
			if (!(nodes[0] is Widget w && NodeCompositionValidator.CanHaveChildren(w.GetType()) && Document.Current != null)) {
				AlertDialog.Show($"Can't inline {nodes[0].GetType()}");
				return;
			}
			var node = nodes[0];
			var clone = node.Clone();
			clone.ContentsPath = null;
			var nodeItem = Document.Current.GetSceneItemForObject(node);
			var parentItem = nodeItem.Parent;
			var index = parentItem.Rows.IndexOf(nodeItem);
			UnlinkSceneItem.Perform(nodeItem);
			LinkSceneItem.Perform(parentItem, index, clone);
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

	public class ConvertToButton : DocumentCommandHandler
	{
		public override void ExecuteTransaction()
		{
			var rows = Document.Current.SelectedRows().ToList();
			foreach (var row in rows) {
				if (row.Components.Get<NodeRow>()?.Node is Frame frame) {
					if (frame.DefaultAnimation.Markers.Count > 0) {
						AlertDialog.Show("It is not possible to convert a widget with existing markers");
						return;
					}
				} else {
					AlertDialog.Show("Only frames can be converted");
					return;
				}
			}
			foreach (var row in rows) {
				try {
					NodeTypeConvert.Perform(row, typeof(Button), typeof(Widget), new HashSet<string> {
						nameof(Node.Parent),
						nameof(Node.Nodes),
						nameof(Node.Animations),
						nameof(Node.Animators)
					});
				} catch (InvalidOperationException e) {
					AlertDialog.Show(e.Message);
					Document.Current.History.RollbackTransaction();
					return;
				}
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
