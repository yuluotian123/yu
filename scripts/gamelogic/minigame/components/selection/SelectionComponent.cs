using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class SelectionComponent : Component2D
{
    [Export] public NodePath SelectionIndicatorPath { get; set; } = new NodePath("SelectionIndicator");
    [Export] public float SelectionRadius { get; set; } = 36f;
    [Export] public Color SelectedColor { get; set; } = new Color(0.3f, 1f, 0.45f, 1f);
    [Export] public float SelectedWidth { get; set; } = 3f;
    [Export] public Color ContextTargetColor { get; set; } = new Color(1f, 0.85f, 0.25f, 1f);
    [Export] public float ContextTargetWidth { get; set; } = 4f;
    [Export] public Color CombinedStateColor { get; set; } = new Color(1f, 0.75f, 0.2f, 1f);
    [Export] public float CombinedStateWidth { get; set; } = 5f;

    private CanvasItem _selectionIndicator;
    private SelectableManagerComponent _selectableManager => RootModule.Instance.GameState?.PlayerState.GetSelectableManager();

    private FactionComponent _factionComponent;
    public FactionComponent Faction => _factionComponent ??= Owner?.GetComponent<FactionComponent>();

    public bool CanReceivePlayerCommands => Faction?.IsPlayerFaction ?? false;

    private bool _isSelected;
    public bool IsSelected => _isSelected;

    private bool _isContextTargeted;
    public bool IsContextTargeted => _isContextTargeted;

    public override int Priority => ComponentPriority.Interaction;

    public override void OnInit()
    {
        _selectionIndicator = Owner?.GetNodeOrNull<CanvasItem>(SelectionIndicatorPath);
        _factionComponent = Owner?.GetComponent<FactionComponent>();
        _selectableManager?.RegisterSelectable(this);

        RefreshIndicatorVisual();
    }

    public override void OnDestroy()
    {
        _selectableManager?.UnregisterSelectable(this);
        _factionComponent = null;
        _selectionIndicator = null;
    }

    public void SetSelected(bool selected)
    {
        if (_isSelected == selected)
            return;

        _isSelected = selected;
        RefreshIndicatorVisual();
    }

    public void SetContextTargeted(bool targeted)
    {
        if (_isContextTargeted == targeted)
            return;

        _isContextTargeted = targeted;
        RefreshIndicatorVisual();
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (Owner == null)
            return false;

        return Owner.WorldPosition2D.DistanceTo(worldPoint) <= SelectionRadius;
    }

    public bool IntersectsWorldRect(Rect2 worldRect)
    {
        if (Owner == null)
            return false;

        Rect2 normalizedRect = worldRect.Abs();
        Vector2 worldPosition = Owner.WorldPosition2D;
        Vector2 closestPoint = worldPosition.Clamp(normalizedRect.Position, normalizedRect.End);
        return worldPosition.DistanceSquaredTo(closestPoint) <= SelectionRadius * SelectionRadius;
    }

    private void RefreshIndicatorVisual()
    {
        if (_selectionIndicator == null)
            return;

        bool visible = _isSelected || _isContextTargeted;
        _selectionIndicator.Visible = visible;

        if (!visible || _selectionIndicator is not Line2D line)
            return;

        if (_isSelected && _isContextTargeted)
        {
            line.DefaultColor = CombinedStateColor;
            line.Width = CombinedStateWidth;
        }
        else if (_isContextTargeted)
        {
            line.DefaultColor = ContextTargetColor;
            line.Width = ContextTargetWidth;
        }
        else
        {
            line.DefaultColor = SelectedColor;
            line.Width = SelectedWidth;
        }
    }
}
