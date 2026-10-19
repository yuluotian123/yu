#if TOOLS
using Godot;
/// <summary>Optional editor supplied by a plain action parameter, without domain dependencies.</summary>
public interface IGraphParameterEditor
{
    Control CreateEditUI(GraphEditorContext context);
}
#endif
