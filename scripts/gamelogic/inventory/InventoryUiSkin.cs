using Godot;

namespace GameLogic;

// Native controls and square borders keep the parchment/wood UI crisp at small sizes.
internal static class InventoryUiSkin
{
    public static readonly Color Ink = new("493429");
    public static readonly Color Muted = new("94785b");
    public static readonly Color Paper = new("f1dfbc");
    public static readonly Color Gold = new("e8b65e");
    public static readonly Color Teal = new("55766a");

    public static StyleBoxFlat Box(string fill, string border, int width = 2)
    {
        var style = new StyleBoxFlat { BgColor = new Color(fill), BorderColor = new Color(border), AntiAliasing = false };
        style.SetBorderWidthAll(width);
        style.SetContentMarginAll(8);
        return style;
    }

    public static Theme Theme()
    {
        var theme = new Theme
        {
            DefaultFont = new SystemFont { FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Noto Sans CJK SC" } },
            DefaultFontSize = 18
        };
        theme.SetColor("font_color", "Label", Ink);
        theme.SetColor("font_color", "Button", Ink);
        theme.SetColor("font_hover_color", "Button", Ink);
        theme.SetColor("font_pressed_color", "Button", Ink);
        theme.SetColor("font_disabled_color", "Button", new Color("a38c70"));
        theme.SetStylebox("normal", "Button", Box("e6cfa5", "9f7d50"));
        theme.SetStylebox("hover", "Button", Box("ffe6ab", "b87937"));
        theme.SetStylebox("pressed", "Button", Box("d9b779", "875d33"));
        theme.SetStylebox("disabled", "Button", Box("e0cfb1", "bba484"));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        return theme;
    }

    public static Button Button(string text) => new()
    {
        Text = text, Theme = Theme(), FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand
    };

    public static Label Text(string text, int size = 18, Color? color = null) => new()
    {
        Text = text, MouseFilter = Control.MouseFilterEnum.Ignore,
        Modulate = color ?? Colors.White, VerticalAlignment = VerticalAlignment.Center,
        Theme = new Theme { DefaultFontSize = size }
    };
}
