using System.Collections.Generic;
using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

[GlobalClass]
public partial class SelectableManagerComponent : Component2D
{
    private IEventModule _eventModule;
    private IInputModule _inputModule;

    public override int Priority => ComponentPriority.Input + 1;


    private readonly List<SelectionComponent> _selectables = new();
    private SelectionComponent _selected;
    public SelectionComponent Selected => _selected;
    public GameObject2D SelectedUnit => _selected?.Owner;

    public override void OnInit()
    {
        _eventModule = ModuleSystem.GetModule<IEventModule>();
        _inputModule = ModuleSystem.GetModule<IInputModule>();
    }

    public override void OnUpdate(double delta)
    {
        if (!_inputModule.IsJustPressed("combat_select"))
            return;

        var viewport = Owner.GetViewport();
        if (ViewportInputUtility.IsPointerBlockedByUI(viewport))
            return;

        var mouseScreenPosition = viewport?.GetMousePosition() ?? Vector2.Zero;
        var mouseWorldPosition = ViewportInputUtility.ScreenToWorld(viewport, mouseScreenPosition);

        var target = FindTopSelectableAt(mouseWorldPosition);
        if (target == null)
            return;

        Select(target);
        _inputModule.TryConsumePressed("combat_select");
    }
    private SelectionComponent FindTopSelectableAt(Vector2 worldPoint)
    {
        SelectionComponent bestSelection = null;
        float bestDistanceSquared = float.MaxValue;

        for (int i = _selectables.Count - 1; i >= 0; i--)
        {
            var selection = _selectables[i];
            if (selection == null || selection.Owner == null)
            {
                _selectables.RemoveAt(i);
                continue;
            }

            if (!selection.ContainsWorldPoint(worldPoint))
                continue;

            float distanceSquared = selection.Owner.WorldPosition2D.DistanceSquaredTo(worldPoint);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestSelection = selection;
        }

        return bestSelection;
    }


    public override void OnDestroy()
    {
        _selected?.SetSelected(false);
        _selected = null;
        _inputModule = null;
        _eventModule = null;
    }

    public void RegisterSelectable(SelectionComponent selection)
    {
        if (selection == null || _selectables.Contains(selection))
            return;

        _selectables.Add(selection);
    }

    public void UnregisterSelectable(SelectionComponent selection)
    {
        if (selection == null)
            return;

        if (ReferenceEquals(_selected, selection))
            _selected = null;

        _selectables.Remove(selection);
    }


    public void Select(SelectionComponent selection)
    {
        if (_selected == selection)
            return;

        _selected?.SetSelected(false);
        _selected = selection;

        if (_selected != null)
            _selected.SetSelected(true);

        NotifySelectionChanged();
    }

    public void ClearSelection()
    {
        Select(null);
    }

    private void NotifySelectionChanged()
    {
        _eventModule?.Send(GameRtsEvents.ArmySelectionChanged);
    }
}
