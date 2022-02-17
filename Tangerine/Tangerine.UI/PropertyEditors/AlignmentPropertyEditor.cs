using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class AlignmentPropertyEditor : CommonPropertyEditor<Alignment>
	{
		private readonly DropDownList selectorH;
		private readonly DropDownList selectorV;

		private static readonly Dictionary<Type, IEnumerable<FieldInfo>> allowedFields =
			new Dictionary<Type, IEnumerable<FieldInfo>>();

		static AlignmentPropertyEditor()
		{
			var items = new[] { typeof(HAlignment), typeof(VAlignment) };
			foreach (var type in items) {
				allowedFields[type] = type.GetFields(BindingFlags.Public | BindingFlags.Static)
					.Where(f => !Attribute.IsDefined(f, typeof(TangerineIgnoreAttribute)));
			}
		}

		public AlignmentPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			EditorContainer.AddNode(new Widget {
				Layout = new HBoxLayout { DefaultCell = new DefaultLayoutCell(Alignment.Center), Spacing = 4 },
				Nodes = {
					(selectorH = editorParams.DropDownListFactory()),
					(selectorV = editorParams.DropDownListFactory()),
				},
			});
			var items = new[] {
				(type: typeof(HAlignment), selector: selectorH),
				(type: typeof(VAlignment), selector: selectorV),
			};
			foreach (var (type, selector) in items) {
				var fields = allowedFields[type];
				foreach (var field in fields) {
					selector.Items.Add(new CommonDropDownList.Item(field.Name, field.GetValue(null)));
				}
				selector.Changed += a => {
					if (a.ChangedByUser) {
						SetComponent(type);
					}
				};
			}
			var currentX = CoalescedPropertyComponentValue(v => v.X);
			var currentY = CoalescedPropertyComponentValue(v => v.Y);
			selectorH.AddLateChangeWatcher(currentX, v => {
				if (v.IsDefined) {
					selectorH.Value = v.Value;
				} else {
					selectorH.Text = ManyValuesText;
				}
			});
			selectorV.AddLateChangeWatcher(currentY, v => {
				if (v.IsDefined) {
					selectorV.Value = v.Value;
				} else {
					selectorV.Text = ManyValuesText;
				}
			});
		}

		private void SetComponent(Type t)
		{
			DoTransaction(() => {
				SetProperty<Alignment>((current) => {
					if (t == typeof(HAlignment)) {
						current.X = (HAlignment)selectorH.Value;
					} else if (t == typeof(VAlignment)) {
						current.Y = (VAlignment)selectorV.Value;
					}
					return current;
				});
			});
		}

		public override void Submit()
		{
			var currentX = CoalescedPropertyComponentValue(v => v.X);
			var currentY = CoalescedPropertyComponentValue(v => v.Y);
			SetComponent(typeof(HAlignment));
			SetComponent(typeof(VAlignment));
		}
	}
}
