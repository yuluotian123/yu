#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>连线选择、标签、方向箭头和创建转换预览。</summary>
public sealed class GraphConnectionEditorService
{
    private const float ConnectionPickDistance = 12f;
    private static readonly Color Accent = new(0.4f, 0.78f, 1f);
    private readonly Node _owner;
    private readonly GraphEdit _graphEdit;
    private readonly Func<GraphAsset> _getCurrentGraph;
    private readonly Action<GraphConnection> _deleteConnection;
    private readonly Action<GraphConnection> _selectConnection;
    private readonly Dictionary<string, Label> _connectionLabels = new();
    private readonly Control _overlay;
    private readonly Label _hint;
    private GraphConnection _selectedConnection;
    private GraphConnection _hoveredConnection;
    private string _previewSource;
    private int _previewPort;
    private Vector2 _previewTarget;

    public GraphConnection SelectedConnection => _selectedConnection;

    public GraphConnectionEditorService(Node owner, GraphEdit graphEdit, Func<GraphAsset> getCurrentGraph,
        Action<GraphConnection> deleteConnection, Action<GraphConnection> selectConnection = null)
    {
        _owner = owner;
        _graphEdit = graphEdit;
        _getCurrentGraph = getCurrentGraph;
        _deleteConnection = deleteConnection;
        _selectConnection = selectConnection;
        _overlay = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        _graphEdit.AddChild(_overlay);
        _overlay.Draw += DrawConnections;
        _graphEdit.MouseExited += () => _hoveredConnection = null;
        _hint = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        _hint.AddThemeFontSizeOverride("font_size", 13);
        _hint.AddThemeStyleboxOverride("normal", CreateLabelBackground());
        _overlay.AddChild(_hint);
    }

    public void Reset()
    {
        foreach (Label label in _connectionLabels.Values)
        {
            label.MouseFilter = Control.MouseFilterEnum.Ignore;
            label.QueueFree();
        }
        _connectionLabels.Clear();
        _selectedConnection = null;
        _hoveredConnection = null;
        SetTransitionPreview(null, 0, Vector2.Zero);
    }

    public void ClearSelection() => _selectedConnection = null;

    public void SelectConnection(GraphConnection connection)
    {
        if (connection != null && _getCurrentGraph()?.Connections.Contains(connection) != true) return;
        _selectedConnection = connection;
        _graphEdit.GrabFocus();
        _selectConnection?.Invoke(connection);
    }

    public bool HandleShortcut(InputEvent @event)
    {
        return @event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Delete } key &&
            !key.CtrlPressed && !key.AltPressed && !key.MetaPressed && DeleteSelectedConnection();
    }

    public bool HandleGraphEditInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton { Pressed: true } mb &&
            mb.ButtonIndex is MouseButton.Left or MouseButton.Right)
        {
            if (IsPositionInsideGraphNode(mb.GlobalPosition))
            {
                _hoveredConnection = null;
                if (mb.ButtonIndex == MouseButton.Left) ClearSelection();
                return false;
            }
            GraphConnection connection = FindConnectionAtPosition(mb.Position);
            if (connection != null)
            {
                SelectConnection(connection);
                if (mb.ButtonIndex == MouseButton.Right)
                    ShowConnectionMenu(_graphEdit.GetScreenPosition() + mb.Position);
                return true;
            }
            if (mb.ButtonIndex == MouseButton.Left) ClearSelection();
        }
        else if (@event is InputEventMouseMotion mm)
            _hoveredConnection = IsPositionInsideGraphNode(mm.GlobalPosition) ? null : FindConnectionAtPosition(mm.Position);
        return false;
    }

    public void SetTransitionPreview(string source, int port, Vector2 target, string message = null)
    {
        _previewSource = source;
        _previewPort = port;
        _previewTarget = target;
        _hint.Text = message ?? "右键状态创建转换 · 点击连线或标签编辑 · Delete 删除选中连线";
        _hint.Modulate = source == null ? Colors.White : Accent;
    }

    public void UpdateConnectionLabels()
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null) return;
        if (_selectedConnection != null && !graph.Connections.Contains(_selectedConnection)) SelectConnection(null);
        if (_hoveredConnection != null && !graph.Connections.Contains(_hoveredConnection)) _hoveredConnection = null;
        var connections = new HashSet<string>(graph.Connections.Select(GetConnectionKey));
        foreach (string key in new List<string>(_connectionLabels.Keys))
            if (!connections.Contains(key))
            {
                _connectionLabels[key].MouseFilter = Control.MouseFilterEnum.Ignore;
                _connectionLabels[key].QueueFree();
                _connectionLabels.Remove(key);
            }
        foreach (GraphConnection connection in graph.Connections)
        {
            string key = GetConnectionKey(connection);
            if (!_connectionLabels.TryGetValue(key, out Label label)) label = CreateConnectionLabel(connection);
            string text = connection.GetDisplayName();
            label.Text = text.Length > 32 ? text[..31] + "…" : text;
            float textWidth = label.GetThemeFont("font").GetStringSize(label.Text, fontSize: label.GetThemeFontSize("font_size")).X;
            label.Size = new Vector2(Mathf.Min(220, textWidth + 12), label.GetCombinedMinimumSize().Y);
            label.TooltipText = $"{graph.FindNodeById(connection.FromNode)?.GetDisplayName()} → {graph.FindNodeById(connection.ToNode)?.GetDisplayName()}\n{text}\n单击编辑 · Delete 删除";
            label.Modulate = connection == _selectedConnection ? Accent : Colors.White;
            Vector2[] points = GetConnectionPoints(connection);
            label.Visible = points.Length >= 2;
            if (label.Visible)
            {
                SampleLine(points, 0.5f, out Vector2 middle, out Vector2 direction);
                // Keep the two directions on opposite sides, including backward curves.
                Vector2 normal = new(-direction.Y, direction.X);
                label.Position = middle + normal * (label.Size.Y * 0.5f + 10f) - label.Size * 0.5f;
            }
        }
        _overlay.Size = _graphEdit.Size;
        _hint.Visible = graph is StateGraphAsset;
        _hint.Position = new Vector2(12, Mathf.Max(0, _overlay.Size.Y - _hint.Size.Y - 12));
        _overlay.QueueRedraw();
    }

    private Label CreateConnectionLabel(GraphConnection connection)
    {
        Label label = connection.CreateConnectionLabel();
        string key = GetConnectionKey(connection);
        label.MouseFilter = Control.MouseFilterEnum.Stop;
        label.ClipText = true;
        label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        label.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        label.AddThemeFontSizeOverride("font_size", 13);
        label.AddThemeStyleboxOverride("normal", CreateLabelBackground());
        label.GuiInput += @event =>
        {
            if (@event is not InputEventMouseButton { Pressed: true } mb ||
                mb.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
            // Resolve by endpoints: undo may have replaced the original connection object.
            GraphConnection current = _getCurrentGraph()?.Connections.FirstOrDefault(c => GetConnectionKey(c) == key);
            if (current == null) return;
            SelectConnection(current);
            if (mb.ButtonIndex == MouseButton.Right) ShowConnectionMenu(label.GetScreenPosition() + mb.Position);
            label.AcceptEvent();
        };
        label.MouseEntered += () => _hoveredConnection = _getCurrentGraph()?.Connections.FirstOrDefault(c => GetConnectionKey(c) == key);
        label.MouseExited += () => _hoveredConnection = null;
        _connectionLabels[key] = label;
        _graphEdit.AddChild(label);
        return label;
    }

    private static StyleBoxFlat CreateLabelBackground() => new()
    {
        BgColor = new Color(0.09f, 0.12f, 0.17f, 0.94f),
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
        ContentMarginLeft = 6, ContentMarginRight = 6, ContentMarginTop = 3, ContentMarginBottom = 3
    };

    private void DrawConnections()
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null) return;
        foreach (GraphConnection connection in graph.Connections)
        {
            Vector2[] points = GetConnectionPoints(connection);
            if (points.Length < 2) continue;
            bool highlighted = connection == _selectedConnection || connection == _hoveredConnection;
            Color color = highlighted ? Accent : new Color(0.82f, 0.88f, 0.96f);
            if (highlighted) _overlay.DrawPolyline(points, color, connection == _selectedConnection ? 4f : 3f, true);
            if (graph is StateGraphAsset)
            {
                SampleLine(points, 0.65f, out Vector2 point, out Vector2 direction);
                DrawArrow(point, direction, color);
            }
        }
        if (_previewSource != null && TryGetPortPosition(_previewSource, true, _previewPort, out Vector2 source))
        {
            _overlay.DrawDashedLine(source, _previewTarget, Accent, 2f, 8f);
            DrawArrow(_previewTarget, (_previewTarget - source).Normalized(), Accent);
        }
    }

    private void DrawArrow(Vector2 point, Vector2 direction, Color color)
    {
        Vector2 side = new(-direction.Y, direction.X);
        _overlay.DrawColoredPolygon(new[] { point + direction * 6, point - direction * 6 + side * 5, point - direction * 6 - side * 5 }, color);
    }

    private Vector2[] GetConnectionPoints(GraphConnection connection)
    {
        if (!TryGetPortPosition(connection.FromNode, true, connection.FromPort, out Vector2 from) ||
            !TryGetPortPosition(connection.ToNode, false, connection.ToPort, out Vector2 to)) return Array.Empty<Vector2>();
        return _graphEdit.GetConnectionLine(from, to);
    }

    private bool TryGetPortPosition(string id, bool output, int port, out Vector2 position)
    {
        position = Vector2.Zero;
        var node = _graphEdit.GetNodeOrNull<GraphNode>(id);
        if (node == null || port < 0 || port >= (output ? node.GetOutputPortCount() : node.GetInputPortCount())) return false;
        position = node.Position + (output ? node.GetOutputPortPosition(port) : node.GetInputPortPosition(port)) * _graphEdit.Zoom;
        return true;
    }

    internal static void SampleLine(Vector2[] points, float ratio, out Vector2 point, out Vector2 direction)
    {
        float length = 0;
        for (int i = 1; i < points.Length; i++) length += points[i - 1].DistanceTo(points[i]);
        float remaining = length * ratio;
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 delta = points[i] - points[i - 1];
            float segment = delta.Length();
            if (segment <= 0.001f) continue;
            if (remaining <= segment)
            {
                direction = delta / segment;
                point = points[i - 1] + direction * remaining;
                return;
            }
            remaining -= segment;
        }
        point = points.Length > 0 ? points[^1] : Vector2.Zero;
        direction = Vector2.Right;
    }

    private GraphConnection FindConnectionAtPosition(Vector2 position)
    {
        GraphAsset graph = _getCurrentGraph();
        if (graph == null) return null;
        var closest = _graphEdit.GetClosestConnectionAtPoint(position, ConnectionPickDistance);
        if (closest.Count == 0) return null;
        return graph.Connections.FirstOrDefault(c => c.Matches(closest["from_node"].AsString(), closest["from_port"].AsInt32(),
            closest["to_node"].AsString(), closest["to_port"].AsInt32()));
    }

    private bool IsPositionInsideGraphNode(Vector2 position)
    {
        foreach (Node child in _graphEdit.GetChildren())
            if (child is GraphNode node && node.Visible && node.GetGlobalRect().HasPoint(position)) return true;
        return false;
    }

    private void ShowConnectionMenu(Vector2 position)
    {
        var popup = new PopupMenu();
        popup.AddItem("删除连线 / Delete Connection", 1);
        popup.Position = (Vector2I)position;
        popup.IdPressed += _ => DeleteSelectedConnection();
        popup.PopupHide += popup.QueueFree;
        _owner.AddChild(popup);
        popup.Popup();
    }

    private bool DeleteSelectedConnection()
    {
        GraphConnection connection = _selectedConnection;
        if (connection == null || _getCurrentGraph()?.Connections.Contains(connection) != true) return false;
        ClearSelection();
        _deleteConnection?.Invoke(connection);
        _selectConnection?.Invoke(null);
        return true;
    }

    private static string GetConnectionKey(GraphConnection connection) =>
        $"{connection.FromNode}:{connection.FromPort}->{connection.ToNode}:{connection.ToPort}";
}
#endif
