using System.Collections.Generic;
using Framework;
using GameLogic;
using GameLogic.Input;
using Godot;

[GlobalClass]
public partial class SelectableManagerComponent : Component2D
{
    [Export] public NodePath SelectionBoxOverlayPath { get; set; } = new NodePath("SelectionBoxLayer/SelectionBoxOverlay");
    [Export] public float BoxSelectionThreshold { get; set; } = 8f;

    private readonly List<SelectionComponent> _selectables = new();
    private readonly List<SelectionComponent> _selectedSelections = new();
    private readonly List<GameObject2D> _selectedUnits = new();

    private IEventModule _eventModule;
    private IInputModule _inputModule;
    private SelectionBoxOverlay _selectionBoxOverlay;
    private bool _isSelectionGestureActive;
    private bool _isBoxSelecting;
    private Vector2 _selectionStartScreenPosition;

    public override int Priority => ComponentPriority.Input + 1;

    public IReadOnlyList<SelectionComponent> SelectedSelections => _selectedSelections;
    public IReadOnlyList<GameObject2D> SelectedUnits => _selectedUnits;

    public override void OnInit()
    {
        _eventModule = ModuleSystem.GetModule<IEventModule>();
        _inputModule = ModuleSystem.GetModule<IInputModule>();
        _selectionBoxOverlay = Owner?.GetNodeOrNull<SelectionBoxOverlay>(SelectionBoxOverlayPath);
        _selectionBoxOverlay?.HideRect();
    }

    public override void OnUpdate(double delta)
    {
        PruneInvalidSelections();
        HandleSelectionInput();
    }

    public override void OnDestroy()
    {
        ClearSelection();
        ResetSelectionGesture();
        _selectionBoxOverlay = null;
        _inputModule = null;
        _eventModule = null;
    }

    public void RegisterSelectable(SelectionComponent selection)
    {
        if (!IsSelectableValid(selection) || _selectables.Contains(selection))
            return;

        _selectables.Add(selection);
    }

    public void UnregisterSelectable(SelectionComponent selection)
    {
        if (selection == null)
            return;

        _selectables.Remove(selection);
        if (RemoveSelectionInternal(selection, false))
            NotifySelectionChanged();
    }

    public void SetSelection(IEnumerable<SelectionComponent> selections)
    {
        List<SelectionComponent> nextSelections = CollectValidSelections(selections);
        if (SelectionSetsEqual(_selectedSelections, nextSelections))
            return;

        for (int i = 0; i < _selectedSelections.Count; i++)
        {
            SelectionComponent currentSelection = _selectedSelections[i];
            if (!nextSelections.Contains(currentSelection))
                currentSelection.SetSelected(false);
        }

        for (int i = 0; i < nextSelections.Count; i++)
        {
            SelectionComponent nextSelection = nextSelections[i];
            if (!_selectedSelections.Contains(nextSelection))
                nextSelection.SetSelected(true);
        }

        _selectedSelections.Clear();
        _selectedUnits.Clear();

        for (int i = 0; i < nextSelections.Count; i++)
        {
            SelectionComponent selection = nextSelections[i];
            _selectedSelections.Add(selection);
            _selectedUnits.Add(selection.Owner);
        }

        NotifySelectionChanged();
    }

    public void SetSingleSelection(SelectionComponent selection)
    {
        if (selection == null)
        {
            ClearSelection();
            return;
        }

        SetSelection(new[] { selection });
    }

    public void AddSelection(SelectionComponent selection)
    {
        if (!IsSelectableEligible(selection) || _selectedSelections.Contains(selection))
            return;

        _selectedSelections.Add(selection);
        _selectedUnits.Add(selection.Owner);
        selection.SetSelected(true);
        NotifySelectionChanged();
    }

    public void RemoveSelection(SelectionComponent selection)
    {
        if (!RemoveSelectionInternal(selection, true))
            return;

        NotifySelectionChanged();
    }

    public void ClearSelection()
    {
        if (_selectedSelections.Count == 0)
            return;

        for (int i = 0; i < _selectedSelections.Count; i++)
            _selectedSelections[i].SetSelected(false);

        _selectedSelections.Clear();
        _selectedUnits.Clear();
        NotifySelectionChanged();
    }

    private void HandleSelectionInput()
    {
        Viewport viewport = Owner?.GetViewport();
        if (viewport == null)
            return;

        if (_inputModule.IsJustPressed("combat_select"))
            BeginSelectionGesture(viewport);

        if (!_isSelectionGestureActive)
            return;

        UpdateSelectionGesture(viewport);

        if (_inputModule.IsJustReleased("combat_select"))
            EndSelectionGesture(viewport);
    }

    private void BeginSelectionGesture(Viewport viewport)
    {
        if (ViewportInputUtility.IsPointerBlockedByUI(viewport))
            return;

        if (!_inputModule.TryConsumePressed("combat_select"))
            return;

        _isSelectionGestureActive = true;
        _isBoxSelecting = false;
        _selectionStartScreenPosition = viewport.GetMousePosition();
        _selectionBoxOverlay?.HideRect();
    }

    private void UpdateSelectionGesture(Viewport viewport)
    {
        if (!_isSelectionGestureActive || !_inputModule.IsPressed("combat_select"))
            return;

        Vector2 currentMousePosition = viewport.GetMousePosition();
        if (!_isBoxSelecting && currentMousePosition.DistanceTo(_selectionStartScreenPosition) >= BoxSelectionThreshold)
            _isBoxSelecting = true;

        if (!_isBoxSelecting)
            return;

        _selectionBoxOverlay?.ShowRect(CreateScreenRect(_selectionStartScreenPosition, currentMousePosition));
    }

    private void EndSelectionGesture(Viewport viewport)
    {
        if (!_isSelectionGestureActive)
            return;

        Vector2 endScreenPosition = viewport.GetMousePosition();
        if (_isBoxSelecting)
            ApplyBoxSelection(viewport, endScreenPosition);
        else
            ApplyPointSelection(viewport, endScreenPosition);

        ResetSelectionGesture();
    }

    private void ApplyPointSelection(Viewport viewport, Vector2 screenPosition)
    {
        Vector2 worldPosition = ViewportInputUtility.ScreenToWorld(viewport, screenPosition);
        SetSingleSelection(FindTopSelectableAt(worldPosition));
    }

    private void ApplyBoxSelection(Viewport viewport, Vector2 endScreenPosition)
    {
        Rect2 screenRect = CreateScreenRect(_selectionStartScreenPosition, endScreenPosition);
        Rect2 worldRect = ViewportInputUtility.ScreenRectToWorld(viewport, screenRect).Abs();
        SetSelection(FindSelectablesInWorldRect(worldRect));
    }

    private void ResetSelectionGesture()
    {
        _isSelectionGestureActive = false;
        _isBoxSelecting = false;
        _selectionStartScreenPosition = Vector2.Zero;
        _selectionBoxOverlay?.HideRect();
    }

    private SelectionComponent FindTopSelectableAt(Vector2 worldPoint)
    {
        SelectionComponent bestSelection = null;
        float bestDistanceSquared = float.MaxValue;

        for (int i = _selectables.Count - 1; i >= 0; i--)
        {
            SelectionComponent selection = _selectables[i];
            if (!IsSelectableValid(selection))
            {
                _selectables.RemoveAt(i);
                continue;
            }

            if (!selection.CanReceivePlayerCommands || !selection.ContainsWorldPoint(worldPoint))
                continue;

            float distanceSquared = selection.Owner.WorldPosition2D.DistanceSquaredTo(worldPoint);
            if (distanceSquared >= bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestSelection = selection;
        }

        return bestSelection;
    }

    private List<SelectionComponent> FindSelectablesInWorldRect(Rect2 worldRect)
    {
        List<SelectionComponent> result = new();

        for (int i = _selectables.Count - 1; i >= 0; i--)
        {
            SelectionComponent selection = _selectables[i];
            if (!IsSelectableValid(selection))
            {
                _selectables.RemoveAt(i);
                continue;
            }

            if (!selection.CanReceivePlayerCommands || !selection.IntersectsWorldRect(worldRect))
                continue;

            result.Add(selection);
        }

        return result;
    }

    private void PruneInvalidSelections()
    {
        bool selectionChanged = false;

        for (int i = _selectables.Count - 1; i >= 0; i--)
        {
            if (IsSelectableValid(_selectables[i]))
                continue;

            _selectables.RemoveAt(i);
        }

        for (int i = _selectedSelections.Count - 1; i >= 0; i--)
        {
            if (IsSelectableValid(_selectedSelections[i]))
                continue;

            RemoveSelectionAt(i, false);
            selectionChanged = true;
        }

        if (selectionChanged)
            NotifySelectionChanged();
    }

    private List<SelectionComponent> CollectValidSelections(IEnumerable<SelectionComponent> selections)
    {
        List<SelectionComponent> result = new();
        if (selections == null)
            return result;

        foreach (SelectionComponent selection in selections)
        {
            if (!IsSelectableEligible(selection) || result.Contains(selection))
                continue;

            result.Add(selection);
        }

        return result;
    }

    private bool RemoveSelectionInternal(SelectionComponent selection, bool setDeselected)
    {
        int index = _selectedSelections.IndexOf(selection);
        if (index < 0)
            return false;

        RemoveSelectionAt(index, setDeselected);
        return true;
    }

    private void RemoveSelectionAt(int index, bool setDeselected)
    {
        SelectionComponent selection = _selectedSelections[index];
        if (setDeselected && selection != null)
            selection.SetSelected(false);

        _selectedSelections.RemoveAt(index);
        _selectedUnits.RemoveAt(index);
    }

    private void NotifySelectionChanged()
    {
        _eventModule?.Send(GameRtsEvents.ArmySelectionChanged);
    }

    private static Rect2 CreateScreenRect(Vector2 start, Vector2 end)
    {
        Vector2 topLeft = new Vector2(Mathf.Min(start.X, end.X), Mathf.Min(start.Y, end.Y));
        Vector2 bottomRight = new Vector2(Mathf.Max(start.X, end.X), Mathf.Max(start.Y, end.Y));
        return new Rect2(topLeft, bottomRight - topLeft);
    }

    private static bool SelectionSetsEqual(IReadOnlyList<SelectionComponent> current, List<SelectionComponent> next)
    {
        if (current.Count != next.Count)
            return false;

        for (int i = 0; i < current.Count; i++)
        {
            if (!next.Contains(current[i]))
                return false;
        }

        return true;
    }

    private static bool IsSelectableEligible(SelectionComponent selection)
    {
        return IsSelectableValid(selection) && selection.CanReceivePlayerCommands;
    }

    private static bool IsSelectableValid(SelectionComponent selection)
    {
        return selection != null && selection.Owner != null;
    }
}
