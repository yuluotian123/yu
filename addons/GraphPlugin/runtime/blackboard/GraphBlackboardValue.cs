using System;
using System.Text.Json.Serialization;
using Godot;

public abstract class GraphBlackboardValue
{
    [JsonIgnore]
    public virtual string DisplayName => GetType().Name;

    [JsonIgnore]
    public abstract Type ValueType { get; }

    public abstract object GetObjectValue();

    public abstract bool SetObjectValue(object value);

    public bool TryGetValue<T>(out T value)
    {
        if (this is T self)
        {
            value = self;
            return true;
        }

        object objectValue = GetObjectValue();
        if (objectValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        try
        {
            Type targetType = typeof(T);
            if (objectValue is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
            {
                value = (T)Convert.ChangeType(objectValue, targetType);
                return true;
            }
        }
        catch
        {
        }

        value = default;
        return false;
    }

    public bool SetValue<T>(T value) => SetObjectValue(value);

    public GraphBlackboardValue Clone()
    {
        return GraphJsonHelper.Deserialize<GraphBlackboardValue>(GraphJsonHelper.Serialize(this));
    }

    public virtual string GetPreviewText()
    {
        object value = GetObjectValue();
        return value?.ToString() ?? "null";
    }

    public virtual Control CreateEditUI(GraphEditorContext context)
    {
        return new Label { Text = GetPreviewText() };
    }
}

public abstract class GraphBlackboardValue<T> : GraphBlackboardValue
{
    public T Value { get; set; }

    [JsonIgnore]
    public override Type ValueType => typeof(T);

    public override object GetObjectValue() => Value;

    public override bool SetObjectValue(object value)
    {
        if (value is T typedValue)
        {
            Value = typedValue;
            return true;
        }

        if (value == null)
        {
            if (!typeof(T).IsValueType)
            {
                Value = default;
                return true;
            }

            return false;
        }

        try
        {
            Type targetType = typeof(T);
            if (targetType.IsEnum && value is string enumName)
            {
                Value = (T)Enum.Parse(targetType, enumName);
                return true;
            }

            if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(targetType))
            {
                Value = (T)Convert.ChangeType(value, targetType);
                return true;
            }
        }
        catch
        {
        }

        return false;
    }
}

public sealed class GraphBoolBlackboardValue : GraphBlackboardValue<bool>
{
    public override string DisplayName => "Bool";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var checkBox = new CheckBox
        {
            Text = "Value",
            ButtonPressed = Value
        };
        checkBox.Toggled += value => Value = value;
        return checkBox;
    }
}

public sealed class GraphIntBlackboardValue : GraphBlackboardValue<int>
{
    public override string DisplayName => "Int";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var spinBox = new SpinBox
        {
            MinValue = -999999,
            MaxValue = 999999,
            Step = 1,
            Value = Value
        };
        spinBox.ValueChanged += value => Value = (int)Math.Round(value);
        return spinBox;
    }
}

public sealed class GraphFloatBlackboardValue : GraphBlackboardValue<float>
{
    public override string DisplayName => "Float";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var spinBox = new SpinBox
        {
            MinValue = -999999,
            MaxValue = 999999,
            Step = 0.01,
            Value = Value
        };
        spinBox.ValueChanged += value => Value = (float)value;
        return spinBox;
    }
}

public sealed class GraphStringBlackboardValue : GraphBlackboardValue<string>
{
    public override string DisplayName => "String";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var lineEdit = new LineEdit
        {
            Text = Value ?? string.Empty,
            PlaceholderText = "Value"
        };
        lineEdit.TextChanged += value => Value = value;
        return lineEdit;
    }
}

public sealed class GraphVector2BlackboardValue : GraphBlackboardValue<Vector2>
{
    public override string DisplayName => "Vector2";

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label { Text = "X" });

        var xSpin = CreateSpinBox(Value.X);
        row.AddChild(xSpin);

        row.AddChild(new Label { Text = "Y" });
        var ySpin = CreateSpinBox(Value.Y);
        row.AddChild(ySpin);

        xSpin.ValueChanged += value => Value = new Vector2((float)value, Value.Y);
        ySpin.ValueChanged += value => Value = new Vector2(Value.X, (float)value);
        return row;
    }

    private static SpinBox CreateSpinBox(float value)
    {
        return new SpinBox
        {
            MinValue = -999999,
            MaxValue = 999999,
            Step = 0.01,
            Value = value,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
    }
}

public sealed class GraphColorBlackboardValue : GraphBlackboardValue<Color>
{
    public GraphColorBlackboardValue()
    {
        Value = Colors.White;
    }

    public override string DisplayName => "Color";

    public override string GetPreviewText()
    {
        return $"RGBA({Value.R:0.###}, {Value.G:0.###}, {Value.B:0.###}, {Value.A:0.###})";
    }

    public override Control CreateEditUI(GraphEditorContext context)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var picker = new ColorPickerButton
        {
            Color = Value,
            EditAlpha = true,
            CustomMinimumSize = new Vector2(96, 32)
        };
        row.AddChild(picker);

        var preview = new Label
        {
            Text = GetPreviewText(),
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        row.AddChild(preview);

        picker.ColorChanged += value =>
        {
            Value = value;
            preview.Text = GetPreviewText();
        };

        return row;
    }
}
