using System;
#if PROFILER
using Lime.Profiler;
using Lime.Profiler.Graphics;
#endif // PROFILER

namespace Lime
{
	[TangerineRegisterComponent]
	[AllowedComponentOwnerTypes(typeof(Frame), typeof(Button), typeof(Image), typeof(SimpleText), typeof(RichText))]
	public class OverdrawForegroundComponent : OverdrawForegroundBehavior { }

	/// <summary>
	/// If Overdraw mode is enabled, adds owner to overdraw foreground RenderChain and
	/// disables rendering of a subtree (including owner) of child objects in the main render list.
	/// </summary>
	/// <remarks>
	/// If turning on overdraw mode does not turn off rendering of a subtree in the main render list,
	/// you are probably faced with a special case where RenderObjects are created bypassing presenters.
	/// As one possible solution, you can manually initialize <see cref="RenderObject.OwnerInfo"/>:
	///     #if PROFILER
	///			renderObject.OwnerInfo.Initialize(Node);
	///     #endif // PROFILER
	/// </remarks>
	public class OverdrawForegroundBehavior : NodeBehavior
	{
#if PROFILER
		private bool isEventConnectAwake;
		private NodeManager cachedNodeManager;

		private event HierarchyChangedEventHandler hierarchyChangedEvent;

		protected override void OnRegister()
		{
			base.OnRegister();
			if (isEventConnectAwake) {
				return;
			}
			ReconnectEvents();
			isEventConnectAwake = true;
		}

		private void UnsubscribeEvents(Node node)
		{
			if (cachedNodeManager != null) {
				cachedNodeManager.HierarchyChanged -= hierarchyChangedEvent;
				UpdateOverdrawForegroundFlags(node, HierarchyAction.Unlink);
				if (node.RenderChainBuilder is RenderChainBuilderProxy proxy) {
					node.RenderChainBuilder = proxy.OriginalRenderChainBuilder;
				}
			}
			isEventConnectAwake = false;
		}

		private void ReconnectEvents()
		{
			cachedNodeManager = Owner.Manager;
			// Manager is not set during object serialization.
			// There is no point in further initializing the component.
			if (cachedNodeManager == null) {
				return;
			}
			hierarchyChangedEvent = (evt) => {
				if (Owner.Manager == null) {
					UnsubscribeEvents(Owner);
				} else if (evt.Parent != null && evt.Parent.IsOverdrawForeground) {
					UpdateOverdrawForegroundFlags(evt.Child, evt.Action);
				}
			};
			UpdateOverdrawForegroundFlags(Owner, HierarchyAction.Link);
			cachedNodeManager.HierarchyChanged += hierarchyChangedEvent;
			if (!(Owner.RenderChainBuilder is RenderChainBuilderProxy)) {
				Owner.RenderChainBuilder = new RenderChainBuilderProxy(Owner);
			}
		}

		private void UpdateOverdrawForegroundFlags(Node node, HierarchyAction action)
		{
			bool flag = action == HierarchyAction.Link;
			node.IsOverdrawForeground = flag;
			foreach (var d in node.Descendants) {
				d.IsOverdrawForeground = flag;
			}
		}

		protected override void OnOwnerChanged(Node oldOwner)
		{
			base.OnOwnerChanged(oldOwner);
			UnsubscribeEvents(oldOwner);
		}

		protected internal override void Stop(Node owner)
		{
			base.Stop(owner);
			UnsubscribeEvents(owner);
		}

		/// <summary>
		/// Drawing an interface on top of overdraw is carried out by adding a node to an additional RenderChain.
		/// </summary>
		public class RenderChainBuilderProxy : IRenderChainBuilder
		{
			public IRenderChainBuilder OriginalRenderChainBuilder { get; private set; }

			public RenderChainBuilderProxy(Node node)
			{
				OriginalRenderChainBuilder = node.RenderChainBuilder;
				node.RenderChainBuilder = this;
			}

			public void AddToRenderChain(RenderChain chain)
			{
				if (Overdraw.EnabledAtUpdateThread && Window.Current == Application.MainWindow) {
					OriginalRenderChainBuilder?.AddToRenderChain(OverdrawForeground.RenderChain);
				}
				OriginalRenderChainBuilder?.AddToRenderChain(chain);
			}
		}
#endif // PROFILER
	}
}
