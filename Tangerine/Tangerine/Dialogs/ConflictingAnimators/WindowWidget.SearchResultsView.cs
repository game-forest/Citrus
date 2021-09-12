using System;
using System.Collections.Generic;
using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public partial class WindowWidget
	{
		private sealed class SearchResultsView : ThemedScrollView
		{
			private readonly ITexture sectionIcon = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;
			private readonly Dictionary<string, SectionWidget> sections = new Dictionary<string, SectionWidget>();
			private readonly Queue<ConflictInfo> pending = new Queue<ConflictInfo>();

			public SearchResultsView()
			{
				Content.Layout = new VBoxLayout { Spacing = 16 };
				Content.Padding = new Thickness(8);
				Tasks.Add(PendingInfoProcessor);
				Decorate();
			}

			private void Decorate()
			{
				Content.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = w.CalcRect();
					Renderer.DrawRect(rect.A, rect.B, Theme.Colors.WhiteBackground);
				}));
				Content.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = w.CalcRect();
					Renderer.DrawRectOutline(rect.A, rect.B, Theme.Colors.ControlBorder);
				}));
			}

			private IEnumerator<object> PendingInfoProcessor()
			{
				while (true) {
					for (var i = 0; i < Math.Min(pending.Count, 100); ++i) {
						var info = pending.Dequeue();
						if (!sections.TryGetValue(info.DocumentPath, out var section)) {
							section = new SectionWidget(info.DocumentPath, sectionIcon);
							sections[info.DocumentPath] = section;
							Content.AddNode(section);
						}
						section.AddItem(new SectionItemWidget(info));
					}
					yield return null;
				}
			}

			public void Enqueue(ConflictInfo info) => pending.Enqueue(info);

			public void Clear()
			{
				Content.Nodes.Clear();
				sections.Clear();
				pending.Clear();
			}
		}
	}
}
