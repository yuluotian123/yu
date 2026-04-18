using Godot;

[GlobalClass]
public partial class SelectionBoxOverlay : Control
{
    [Export] public Color FillColor { get; set; } = new Color(0.45f, 0.78f, 1f, 0.14f);
    [Export] public Color BorderColor { get; set; } = new Color(0.45f, 0.78f, 1f, 0.95f);
    [Export] public float BorderWidth { get; set; } = 2f;

    private bool _hasRect;
    private Rect2 _screenRect;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        Visible = false;
    }

    public void ShowRect(Rect2 screenRect)
    {
        _screenRect = screenRect.Abs();
        _hasRect = _screenRect.Size.X > 0f || _screenRect.Size.Y > 0f;
        Visible = _hasRect;
        QueueRedraw();
    }

    public void HideRect()
    {
        if (!_hasRect && !Visible)
            return;

        _hasRect = false;
        Visible = false;
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (!_hasRect)
            return;

        DrawRect(_screenRect, FillColor);
        DrawRect(_screenRect, BorderColor, false, BorderWidth);
    }
}
