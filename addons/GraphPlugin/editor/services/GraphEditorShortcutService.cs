#if TOOLS
using System;
using Godot;

/// <summary>
/// 图编辑器窗口快捷键服务。
/// </summary>
public static class GraphEditorShortcutService
{
    /// <summary>
    /// 处理保存、撤销和重做快捷键。返回 true 表示事件已被消费。
    /// </summary>
    public static bool Handle(InputEvent @event, EditorUndoRedoManager undoRedo, Action save)
    {
        if (@event is not InputEventKey key || !key.Pressed)
            return false;

        if (key.Keycode == Key.S && key.CtrlPressed)
        {
            save?.Invoke();
            return true;
        }

        if (key.Keycode == Key.Z && key.CtrlPressed && !key.ShiftPressed)
        {
            undoRedo?.GetHistoryUndoRedo((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory).Undo();
            return true;
        }

        if ((key.Keycode == Key.Z && key.CtrlPressed && key.ShiftPressed) ||
            (key.Keycode == Key.Y && key.CtrlPressed))
        {
            undoRedo?.GetHistoryUndoRedo((int)EditorUndoRedoManager.SpecialHistory.GlobalHistory).Redo();
            return true;
        }

        return false;
    }
}
#endif
