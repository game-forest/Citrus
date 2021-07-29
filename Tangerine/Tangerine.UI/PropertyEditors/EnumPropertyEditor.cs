using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class EnumPropertyEditor<T> : CommonPropertyEditor<T>
	{
		protected DropDownList Selector { get; }

		private static Dictionary<Type, IEnumerable<FieldInfo>> allowedFields;

		static EnumPropertyEditor()
		{
			allowedFields = new Dictionary<Type, IEnumerable<FieldInfo>>();
			var type = typeof(T);
			var fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);
			var allowed = fields.Where(f => !Attribute.IsDefined((MemberInfo)f, typeof(TangerineIgnoreAttribute)));
			allowedFields[type] = allowed;
		}

		public EnumPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			Selector = editorParams.DropDownListFactory();
			Selector.LayoutCell = new LayoutCell(Alignment.Center);
			EditorContainer.AddNode(Selector);
			var propType = editorParams.PropertyInfo.PropertyType;
			var fields = allowedFields[propType];
			foreach (var field in fields) {
				Selector.Items.Add(new CommonDropDownList.Item(field.Name, field.GetValue(null)));
			}
			Selector.Changed += a => {
				if (a.ChangedByUser)
					SetProperty((T)Selector.Items[a.Index].Value);
			};
			Selector.AddLateChangeWatcher(CoalescedPropertyValue(), v => {
				if (v.IsDefined) {
					Selector.Value = v.Value;
				} else {
					Selector.Text = ManyValuesText;
				}
			});
		}
	}
}
