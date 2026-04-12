using System.Diagnostics;
using Godot;

/// <summary>
/// 无限平铺背景组件 Godot 4.6 优化版
/// 根据玩家/相机位置自动填充可见区域，永远不会出现空白边界
/// 支持视差滚动，性能友好
/// </summary>
public partial class BackGroundLayer : TileMapLayer
{

    [ExportGroup("瓦片设置")]
    [Export] public int TileSourceId = 0;
    [Export] public Vector2I AtlasCoordinate = Vector2I.Zero;
    [Export] public int ExtraMarginTiles = 1;

    private Camera2D _camera;
    private Vector2I _tileSize;
    private Rect2I _lastViewportBounds;

    public override void _Ready()
    {
        _camera = GetViewport().GetCamera2D();
        _tileSize = TileSet.TileSize;
        ZIndex = -100;

        // 初始化时立即填充
        UpdateVisibleTiles();
    }

    public override void _Process(double delta)
    {
        UpdateVisibleTiles();
    }

    private void UpdateVisibleTiles()
    {
        if (!IsInstanceValid(_camera))
        {
            return; 
        }

        // Godot 4.6 正确API: 屏幕坐标转世界坐标
        Rect2 viewRect = _camera.GetViewportRect();
        Transform2D canvasXform = _camera.GetCanvasTransform().AffineInverse();
        Vector2 viewTopLeft = canvasXform * viewRect.Position;
        Vector2 viewBottomRight = canvasXform * viewRect.End;

        // 转换为瓦片网格坐标
        Vector2I tileStart = (Vector2I)(viewTopLeft / _tileSize).Floor();
        Vector2I tileEnd = (Vector2I)(viewBottomRight / _tileSize).Ceil();

        // 边缘扩展防止相机移动时边界闪烁
        tileStart -= new Vector2I(ExtraMarginTiles, ExtraMarginTiles);
        tileEnd += new Vector2I(ExtraMarginTiles, ExtraMarginTiles);

        Rect2I newBounds = new Rect2I(tileStart, tileEnd - tileStart);

        // 只有可见区域跨越瓦片边界时才更新 绝大多数帧直接跳过
        if (newBounds == _lastViewportBounds)
            return;

        _lastViewportBounds = newBounds;

        Clear();

        // 批量填充可见区域
        int startX = newBounds.Position.X;
        int endX = newBounds.End.X;
        int startY = newBounds.Position.Y;
        int endY = newBounds.End.Y;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                // Godot 4.6 正确API
                SetCell(new Vector2I(x, y), TileSourceId, AtlasCoordinate);
            }
        }
    }
}
