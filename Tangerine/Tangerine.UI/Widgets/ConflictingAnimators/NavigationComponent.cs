using System;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Components;
using Tangerine.Core.Operations;

namespace Tangerine.UI.Widgets.ConflictingAnimators
{
	public class NavigationInfo
	{
		public delegate Node RetrieveNodeDelegate();

		public RetrieveNodeDelegate RetrieveNode { get; set; }
		public string DocumentPath { get; set; }
		public string TargetProperty { get; set; }
		public string AnimationId { get; set; }
	}

	[AllowedComponentOwnerTypes(typeof(ThemedCaption))]
	public class NavigationComponent : NodeBehavior
	{
		private static object invalidationTaskTag = new object();

		private bool wasMouseOver;
		private bool savedHitTestTarget;
		private TextStyle savedTextStyle;

		public readonly NavigationInfo Info;
		public readonly TextStyle AnimationLinkStyle;
		public event Action MouseMovedOver;
		public event Action MouseMovedAway;

		public ThemedCaption Caption => Owner as ThemedCaption;

		public NavigationComponent(NavigationInfo info)
		{
			Info = info;
			AnimationLinkStyle = TextStylePool.Get(TextStyleIdentifiers.AnimationLink);
			MouseMovedOver += OnMouseOver;
			MouseMovedAway += OnMouseAway;
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			if (oldOwner == null) {
				AttachTo(Owner);
			} else {
				DetachFrom(oldOwner);
			}
		}

		private void AttachTo(Node node)
		{
			var caption = node as ThemedCaption;
			if (caption != null) {
				savedHitTestTarget = caption.HitTestTarget;
				savedTextStyle = caption.GetDefaultStyle();
				caption.SetDefaultStyle(caption.BoldStyle);
				caption.HitTestTarget = true;
				caption.Clicked += OnClick;
				caption.Tasks.Add(Theme.MouseHoverInvalidationTask(caption), invalidationTaskTag);
			}
		}

		private void DetachFrom(Node node)
		{
			var caption = node as ThemedCaption;
			if (caption != null) {
				caption.HitTestTarget = savedHitTestTarget;
				caption.SetDefaultStyle(savedTextStyle);
				caption.Clicked -= OnClick;
				caption.Tasks.StopByTag(invalidationTaskTag);
			}
		}

		public override void Update(float delta)
		{
			base.Update(delta);
			if (Owner == null) return;

			if (Owner.IsMouseOver()) {
				WidgetContext.Current.MouseCursor = MouseCursor.Hand;
				if (!wasMouseOver) {
					wasMouseOver = true;
					MouseMovedOver?.Invoke();
				}
			} else if (wasMouseOver) {
				wasMouseOver = false;
				MouseMovedAway?.Invoke();
			}
		}

		private void OnMouseOver() => Caption?.SetDefaultStyle(AnimationLinkStyle);

		private void OnMouseAway() => Caption?.SetDefaultStyle(Caption?.BoldStyle);

		private void OnClick()
		{
			try {
				Project.Current.OpenDocument(Info.DocumentPath);
			} catch (System.Exception e) {
				var message = $"Couldn't open {Info.DocumentPath}\n{e.Message}";
				Logger.Write(message);
				new AlertDialog(message, "Ok").Show();
				return;
			}

			try {
				var node = Info.RetrieveNode();
				NavigateToNode.Perform(node, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
				NavigateToAnimation.Perform(GetAnimation(node));
				SelectPropertyRow(Document.Current.GetSceneItemForObject(node));
			} catch (System.Exception e) {
				var message = $"Couldn't perform navigation\n{e.Message}";
				Logger.Write(message);
				new AlertDialog(message, "Ok").Show();
			}
		}

		private Animation GetAnimation(Node node)
		{
			foreach (var a in node.Ancestors) {
				if (a.Animations.TryFind(Info.AnimationId, out var animation)) {
					return animation;
				}
			}

			return null;
		}

		private void SelectPropertyRow(Row item)
		{
			var row = item.Rows
				.Where(i => i.Id == Info.TargetProperty)
				.First(i => i.Components.Get<CommonPropertyRowData>().Animator.AnimationId == Info.AnimationId);
			var state = item.GetTimelineItemState();

			using (Document.Current.History.BeginTransaction()) {
				state.ShowAnimators = true;
				state.Expanded = true;
				ClearRowSelection.Perform();
				SelectRow.Perform(row);
				Document.Current.BumpSceneTreeVersion();
				Document.Current.History.CommitTransaction();
			}
		}
	}
}
