using System;
using System.Collections.Generic;
using System.Linq;
using Lime;
using Tangerine.Core;
using Tangerine.Core.Operations;
using Tangerine.UI;
using Tangerine.UI.Docking;

namespace Tangerine
{
	public class ConflictingAnimatorsDialog
	{

		public static class ConflictingAnimatorsWindowWidgetProvider {
			private const string AppIconPath = @"Tangerine.Resources.Icons.icon.ico";
			private const string Title = @"Conflicting Animators";
			private const WindowStyle Style = WindowStyle.Regular;

			private static Vector2? position;
			private static WindowWidget instance;

			public static WindowWidget Get()
			{
				if (instance == null) {
					var display = DockManager.Instance.MainWindowWidget.Window.Display;
					var displayCenter = display.Position + display.Size / 2;

					var window = new Window(new WindowOptions {
						Title = Title,
						Style = Style,
						Icon = new System.Drawing.Icon(new EmbeddedResource(AppIconPath, "Tangerine").GetResourceStream()),
					});
					window.DecoratedPosition = position ?? (displayCenter - window.DecoratedSize / 2f);
					window.Closing += OnClose;

					var scrollView = new ThemedScrollView {
						Content = {
							Layout = new VBoxLayout(),
						},
					};
					instance = new ThemedInvalidableWindowWidget(window) {
						Padding = new Thickness(8),
						Layout = new VBoxLayout {
							Spacing = 8
						},
						Nodes = {
							scrollView,
							new Widget {
								Padding = new Thickness(8),
								Layout = new HBoxLayout {
									Spacing = 8
								},
								LayoutCell = new LayoutCell(Alignment.LeftCenter),
								Nodes = {
									new ThemedButton {
										Text = "Search",
										Clicked = () => {
											var doc = Document.Current;
											//var queue = new Queue<Node>(doc.RootNode.Nodes);
											var queue = new Queue<Node>(doc.RootNode.Descendants);
											while (queue.Count > 0) {
												var node = queue.Dequeue();
												//if (node.ContentsPath != null) continue;
												//foreach (var child in node.Nodes) {
												//	queue.Enqueue(child);
												//}
												var props = new Dictionary<string, HashSet<string>>();
												foreach (var animator in node.Animators) {
													var id = animator.AnimationId;
													var prop = animator.TargetPropertyPath;
													if (!props.TryGetValue(animator.TargetPropertyPath, out var hash)) {
														props[prop] = new HashSet<string>();
													}
													props[prop].Add(id);
												}
												foreach (var (property, animations) in props) {
													if (animations.Count > 1) {
														scrollView.Content.AddNode(
															new ThemedButton {
																Text = "Navigate",
																Clicked = () => {
																	//Project.Current.OpenDocument(doc.Path);
																	NavigateToNode.Perform(node, enterInto: false, turnOnInspectRootNodeIfNeeded: true);
																}
															}
														);
														scrollView.Content.AddNode(
															new ThemedSimpleText($"{node}:\n[{property}] {string.Join(',', animations)}\n")
														);

													}
												}
											}
										},
									},
									new ThemedCheckBox {
										Checked = false,
									},
									new ThemedSimpleText("Global"),
								},
							},
							new ThemedSimpleText($"Scenes: {Project.Current.AssetDatabase.Count(i => i.Value.Type == ".tan").ToString()}"),
							new ThemedSimpleText($"Documents: {Project.Current.Documents.Count.ToString()}"),
							new ThemedSimpleText($"Nodes: {Document.Current.RootNodeUnwrapped.Descendants.Count().ToString()}"),
						},
					};
					instance.FocusScope = new KeyboardFocusScope(instance);
				}
				return instance;
			}

			public static bool OnClose(CloseReason reason)
			{
				position = instance.Window.DecoratedPosition;
				instance = null;
				return true;
			}
		}

		public ConflictingAnimatorsDialog()
		{
			var windowWidget = ConflictingAnimatorsWindowWidgetProvider.Get();
			windowWidget.Window.Activate();
			windowWidget.SetFocus();
		}
	}
}
