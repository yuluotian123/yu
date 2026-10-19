#if TOOLS
using Godot;
using GameLogic;

[Tool]
public partial class CharacterDataEditorPlugin : EditorPlugin
{
    CharacterDataInspector _inspector;
    public override void _EnterTree(){_inspector=new();AddInspectorPlugin(_inspector);}
    public override void _ExitTree(){if(_inspector!=null)RemoveInspectorPlugin(_inspector);_inspector=null;}
}
#endif
