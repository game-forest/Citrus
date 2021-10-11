using System.Collections.Generic;
using System.Collections.Concurrent;
using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	internal sealed partial class WindowWidget
	{
		private sealed class SearchResultsView : ThemedScrollView
		{
			private readonly ITexture sectionIcon;
			private readonly Dictionary<string, SectionWidget> sections;
			private readonly ConcurrentQueue<ConflictInfo> pending;

			public SearchResultsView()
			{
				sectionIcon = IconPool.GetIcon("Lookup.SceneFileIcon").AsTexture;
				sections = new Dictionary<string, SectionWidget>();
				pending = new ConcurrentQueue<ConflictInfo>();
				Content.Layout = new VBoxLayout { Spacing = 0 };
				Content.Padding = new Thickness(8);
				Tasks.Add(PendingInfoProcessor);
				Decorate();
			}

			private void Decorate()
			{
				Content.CompoundPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = CalcRect(w);
					Renderer.DrawRect(rect.A, rect.B, Theme.Colors.WhiteBackground);
				}));
				Content.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = CalcRect(w);
					Renderer.DrawRectOutline(rect.A, rect.B, Theme.Colors.ControlBorder);
				}));

				static Rectangle CalcRect(Widget w)
				{
					var wp = w.ParentWidget;
					var p = wp.Padding;
					return new Rectangle(
						-w.Position + Vector2.Zero - new Vector2(p.Left, p.Top),
						-w.Position + wp.Size + new Vector2(p.Right, p.Bottom)
					);
				}
			}

			private IEnumerator<object> PendingInfoProcessor()
			{
				while (true) {
					int processedPerIteration = 0;
					while (!pending.IsEmpty && processedPerIteration < 100) {
						if (pending.TryDequeue(out var conflict)) {
							++processedPerIteration;
							var path = conflict.DocumentPath;
							if (!sections.TryGetValue(path, out var section)) {
								sections.Add(path, section = new SectionWidget(path, sectionIcon));
								Content.AddNode(section);
							}
							section.AddItem(new SectionItemWidget(conflict));
						}
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
