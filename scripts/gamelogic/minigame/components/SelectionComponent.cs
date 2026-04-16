using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

[GlobalClass]
public partial class SelectionComponent : Component2D
{
    [Export] public NodePath SelectionIndicatorPath { get; set; } = new NodePath("SelectionIndicator");
    [Export] public float SelectionRadius { get; set; } = 36f;

    private CanvasItem _selectionIndicator;
    private SelectableManagerComponent _selectableManager =>RootModule.Instance.GameState?.PlayerState.GetSelectableManager();

    private FactionComponent _factionComponent;
    public FactionComponent Faction => _factionComponent ??= Owner?.GetComponent<FactionComponent>();

    public bool CanReceivePlayerCommands => Faction?.IsPlayerFaction ?? false;

    private bool _isSelected;
    public bool IsSelected => _isSelected;

    public override int Priority => ComponentPriority.Interaction;

    public override void OnInit()
    {
        _selectionIndicator = Owner?.GetNodeOrNull<CanvasItem>(SelectionIndicatorPath);
        _factionComponent = Owner?.GetComponent<FactionComponent>();
        _selectableManager?.RegisterSelectable(this);

        ApplySelectionVisual();
    }

    public override void OnDestroy()
    {
        _factionComponent = null;
        _selectionIndicator = null;
        _selectableManager?.UnregisterSelectable(this);
    }

    public void SetSelected(bool selected)
    {
        _isSelected = selected;
        ApplySelectionVisual();
    }

    public bool ContainsWorldPoint(Vector2 worldPoint)
    {
        if (Owner == null)
            return false;

        return Owner.WorldPosition2D.DistanceTo(worldPoint) <= SelectionRadius;
    }

    private void ApplySelectionVisual()
    {
        if (_selectionIndicator != null)
            _selectionIndicator.Visible = _isSelected;
    }
}
