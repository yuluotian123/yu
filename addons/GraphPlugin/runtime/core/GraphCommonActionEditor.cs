#if TOOLS
using System;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Godot;

// Shared plain-data parameter editor, used by actions and conditions in both graph families.
public static class GraphCommonActionEditor
{
    public static Control Build(object definition, GraphEditorContext context)
    {
        var root = new VBoxContainer();
        foreach (var property in definition.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!property.CanRead || !property.CanWrite) continue;
            string label = Regex.Replace(property.Name, "([a-z])([A-Z])", "$1 $2");
            object value = property.GetValue(definition);
            Action<object> set = next => { property.SetValue(definition, next); context?.CurrentGraph?.MarkDirty(); };
            Control field = null;
            if (value is GraphActionValue parameter) field = parameter.CreateEditUI(context);
            else if (value is GraphBlackboardKeyReference key) field = key.CreateEditUI(context, label);
            else if (value is GraphActionComponentReference component)
                field = component.CreateEditUI(label, context, () => context?.CurrentGraph?.MarkDirty());
            else if (property.PropertyType == typeof(string))
            {
                var edit = new LineEdit { Text = (string)value ?? "" };
                edit.TextChanged += text => set(text); field = edit;
            }
            else if (property.PropertyType == typeof(bool))
            {
                var check = new CheckBox { Text = label, ButtonPressed = (bool)value };
                check.Toggled += flag => set(flag); field = check;
            }
            else if (property.PropertyType.IsEnum)
            {
                var option = new OptionButton();
                var values = Enum.GetValues(property.PropertyType);
                foreach (object choice in values) option.AddItem(choice.ToString());
                option.Selected = Array.IndexOf(values.Cast<object>().ToArray(), value);
                option.ItemSelected += index => set(values.GetValue((int)index)); field = option;
            }
            else if (property.PropertyType == typeof(float) || property.PropertyType == typeof(int))
            {
                var spin = new SpinBox { MinValue = -1000000, MaxValue = 1000000,
                    Step = property.PropertyType == typeof(int) ? 1 : 0.05, Value = Convert.ToDouble(value) };
                spin.ValueChanged += number => set(Convert.ChangeType(number, property.PropertyType)); field = spin;
            }
            if (field == null) continue;
            root.AddChild(new Label { Text = label });
            root.AddChild(field);
        }
        return root;
    }
}
#endif
