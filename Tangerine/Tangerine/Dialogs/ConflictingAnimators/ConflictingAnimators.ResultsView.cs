using System.Collections.Generic;
using System.Collections.Concurrent;
using Lime;
using Tangerine.Core;
using Tangerine.UI;
using Tangerine.UI.Widgets.ConflictingAnimators;

namespace Tangerine.Dialogs.ConflictingAnimators
{
	public static partial class ConflictingAnimators
	{
		private class ResultsView : ThemedScrollView
		{
			private readonly ITexture sectionIcon;
			private readonly Dictionary<string, SectionWidget> sections;
			private readonly ConcurrentQueue<ConflictInfo> pending;

			public ResultsView()
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
					var rect = CalculateRect(w);
					Renderer.DrawRect(rect.A, rect.B, Theme.Colors.WhiteBackground);
				}));
				Content.CompoundPostPresenter.Add(new SyncDelegatePresenter<Widget>((w) => {
					w.PrepareRendererState();
					var rect = CalculateRect(w);
					Renderer.DrawRectOutline(rect.A, rect.B, Theme.Colors.ControlBorder);
				}));

				static Rectangle CalculateRect(Widget w)
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
								section = new SectionWidget(path, sectionIcon);
								sections.Add(path, section);
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
