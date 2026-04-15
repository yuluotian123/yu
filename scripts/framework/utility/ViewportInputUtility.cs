using Godot;

namespace Framework
{
    public static class ViewportInputUtility
    {
        public static bool IsPointerBlockedByUI(Node node)
        {
            return IsPointerBlockedByUI(node?.GetViewport());
        }

        public static bool IsPointerBlockedByUI(Viewport viewport)
        {
            var hoveredControl = viewport?.GuiGetHoveredControl();
            return hoveredControl != null && hoveredControl.MouseFilter != Control.MouseFilterEnum.Ignore;
        }

        public static Vector2 ScreenToWorld(Node node, Vector2 screenPosition)
        {
            return ScreenToWorld(node?.GetViewport(), screenPosition);
        }

        public static Vector2 ScreenToWorld(Viewport viewport, Vector2 screenPosition)
        {
            if (viewport == null)
                return screenPosition;

            return viewport.GetCanvasTransform().AffineInverse() * screenPosition;
        }

        public static Rect2 ScreenRectToWorld(Viewport viewport, Rect2 screenRect)
        {
            var topLeft = ScreenToWorld(viewport, screenRect.Position);
            var bottomRight = ScreenToWorld(viewport, screenRect.End);
            return new Rect2(topLeft, bottomRight - topLeft);
        }
    }
}
