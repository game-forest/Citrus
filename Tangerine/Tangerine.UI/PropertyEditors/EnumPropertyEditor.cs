using System;
using System.Linq;
using System.Reflection;
using Lime;
using Tangerine.Core;

namespace Tangerine.UI
{
	public class EnumPropertyEditor<T> : CommonPropertyEditor<T>
	{
		public DropDownList Selector { get; }

		public EnumPropertyEditor(IPropertyEditorParams editorParams) : base(editorParams)
		{
			Selector = editorParams.DropDownListFactory();
			Selector.LayoutCell = new LayoutCell(Alignment.Center);
			ContainerWidget.AddNode(Selector);
			var propType = editorParams.PropertyInfo.PropertyType;
			var fields = propType.GetFields(BindingFlags.Public | BindingFlags.Static);
			var allowedFields = fields.Where(f => !Attribute.IsDefined((MemberInfo)f, typeof(TangerineIgnoreAttribute)));
			foreach (var field in allowedFields) {
				Selector.Items.Add(new CommonDropDownList.Item(field.Name, field.GetValue(null)));
			}
			Selector.Changed += a => {
				if (a.ChangedByUser)
					SetProperty((T)Selector.Items[a.Index].Value);
			};
			Selector.AddChangeWatcher(CoalescedPropertyValue(), v => Selector.Value = v);
		}
	}
}