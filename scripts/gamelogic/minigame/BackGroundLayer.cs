using Framework;
using Godot;

/// <summary>
/// 鏃犻檺骞抽摵鑳屾櫙缁勪欢 Godot 4.6 浼樺寲鐗?/// 鏍规嵁鐜╁/鐩告満浣嶇疆鑷姩濉厖鍙鍖哄煙锛屾案杩滀笉浼氬嚭鐜扮┖鐧借竟鐣?/// 鏀寔瑙嗗樊婊氬姩锛屾€ц兘鍙嬪ソ
/// </summary>
public partial class BackGroundLayer : TileMapLayer
{

    [ExportGroup("鐡︾墖璁剧疆")]
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

        // 鍒濆鍖栨椂绔嬪嵆濉厖
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

        // Godot 4.6 姝ｇ‘API: 灞忓箷鍧愭爣杞笘鐣屽潗鏍?
        Rect2 viewRect = _camera.GetViewportRect();
        Rect2 worldRect = ViewportInputUtility.ScreenRectToWorld(_camera.GetViewport(), viewRect);
        Vector2 viewTopLeft = worldRect.Position;
        Vector2 viewBottomRight = worldRect.End;

        // 杞崲涓虹摝鐗囩綉鏍煎潗鏍?
        Vector2I tileStart = (Vector2I)(viewTopLeft / _tileSize).Floor();
        Vector2I tileEnd = (Vector2I)(viewBottomRight / _tileSize).Ceil();

        // 杈圭紭鎵╁睍闃叉鐩告満绉诲姩鏃惰竟鐣岄棯鐑?
        tileStart -= new Vector2I(ExtraMarginTiles, ExtraMarginTiles);
        tileEnd += new Vector2I(ExtraMarginTiles, ExtraMarginTiles);

        Rect2I newBounds = new Rect2I(tileStart, tileEnd - tileStart);

        // 鍙湁鍙鍖哄煙璺ㄨ秺鐡︾墖杈圭晫鏃舵墠鏇存柊 缁濆ぇ澶氭暟甯х洿鎺ヨ烦杩?
        if (newBounds == _lastViewportBounds)
            return;

        _lastViewportBounds = newBounds;

        Clear();

        // 鎵归噺濉厖鍙鍖哄煙
        int startX = newBounds.Position.X;
        int endX = newBounds.End.X;
        int startY = newBounds.Position.Y;
        int endY = newBounds.End.Y;

        for (int x = startX; x < endX; x++)
        {
            for (int y = startY; y < endY; y++)
            {
                // Godot 4.6 姝ｇ‘API
                SetCell(new Vector2I(x, y), TileSourceId, AtlasCoordinate);
            }
        }
    }
}
