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
		public Func<Node> RetrieveNode { get; set; }
		public string DocumentPath { get; set; }
		public string TargetProperty { get; set; }
		public string AnimationId { get; set; }
	}

	[AllowedComponentOwnerTypes(typeof(ThemedCaption))]
	public class NavigationComponent : NodeBehavior
	{
		private static readonly object invalidationTaskTag = new object();

		private bool wasMouseOver;
		private bool savedHitTestTarget;
		private TextStyle savedTextStyle;

		private readonly NavigationInfo info;
		private readonly TextStyle animationLinkStyle;

		public event Action MouseMovedOver;
		public event Action MouseMovedAway;

		private ThemedCaption Caption => Owner as ThemedCaption;

		public NavigationComponent(NavigationInfo info)
		{
			this.info = info;
			animationLinkStyle = TextStylePool.Get(TextStyleIdentifiers.AnimationLink);
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
				caption.Clicked += OnCaptionClick;
				caption.Tasks.Add(Theme.MouseHoverInvalidationTask(caption), invalidationTaskTag);
			}
		}

		private void DetachFrom(Node node)
		{
			var caption = node as ThemedCaption;
			if (caption != null) {
				caption.HitTestTarget = savedHitTestTarget;
				caption.SetDefaultStyle(savedTextStyle);
				caption.Clicked -= OnCaptionClick;
				caption.Tasks.StopByTag(invalidationTaskTag);
			}
		}

		public override void Update(float delta)
		{
			base.Update(delta);
			if (Owner == null) {
				return;
			}
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

		private void OnMouseOver() => Caption?.SetDefaultStyle(animationLinkStyle);

		private void OnMouseAway() => Caption?.SetDefaultStyle(Caption?.BoldStyle);

		private void OnCaptionClick()
		{
			try {
				Project.Current.OpenDocument(info.DocumentPath);
			} catch (System.Exception exception) {
				System.Console.WriteLine(exception);
				var message = $"Couldn't open {info.DocumentPath}\n\n{exception.Message}";
				new AlertDialog(message, "Ok").Show();
				return;
			}
			try {
				var node = info.RetrieveNode();
				NavigateToNode.Perform(node, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
				NavigateToAnimation.Perform(GetAnimation(node));
				SelectPropertyRow(node);
			} catch (AnimationNotFoundException exception) {
				HandleException(exception);
			} catch (CannotSelectPropertyRowException exception) {
				HandleException(exception);
			} catch (System.Exception exception) {
				HandleException(exception);
			}

			void HandleException(System.Exception exception)
			{
				System.Console.WriteLine(exception);
				new AlertDialog($"Couldn't perform navigation\n\n{exception.Message}", "Ok").Show();
			}
		}

		private Animation GetAnimation(Node node)
		{
			foreach (var a in node.Ancestors) {
				if (a.Animations.TryFind(info.AnimationId, out var animation)) {
					return animation;
				}
			}
			throw new AnimationNotFoundException(info.AnimationId);
		}

		private void SelectPropertyRow(Node node)
		{
			try {
				Document.Current.History.DoTransaction(() => {
					ClearSceneItemSelection.Perform();
					SelectNode.Perform(node);
					DelegateOperation.Perform(null, Document.Current.BumpSceneTreeVersion, false);
					SetProperty.Perform(
						obj: Document.Current.GetSceneItemForObject(node).GetTimelineSceneItemState(),
						propertyName: nameof(TimelineSceneItemStateComponent.AnimatorsExpanded),
						value: true,
						isChangingDocument: false
					);
					DelegateOperation.Perform(Document.Current.BumpSceneTreeVersion, null, false);
					var item = Document.Current.GetSceneItemForObject(node).SceneItems
						.Where(i => i.Id == info.TargetProperty)
						.First(
							i => i.Components.Get<CommonPropertySceneItemData>().Animator.AnimationId
							== info.AnimationId
						);
					SelectSceneItem.Perform(item);
				});
			} catch (System.Exception exception) {
				throw new CannotSelectPropertyRowException(exception);
			}
		}

		private class AnimationNotFoundException : System.Exception
		{
			public AnimationNotFoundException(string animationId)
				: base($"Neither this node nor its ancestors contain animation with \"{animationId}\" id!")
			{ }
		}

		private class CannotSelectPropertyRowException : System.Exception
		{
			public CannotSelectPropertyRowException(System.Exception inner)
				: base("Cannot select property scene item!", inner)
			{ }
		}
	}
}
