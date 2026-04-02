using System;
using Godot;

[Tool]
[GlobalClass]
public partial class MissionGraph : GraphAsset
{
    public override string GraphType { get; set; } = "MissionGraph";
    public override string GetEditorTitle()=>ResourcePath + "_" + GraphType +"编辑器";
    public override GraphConnection CreateConnection()=> new ConnectionWithConditon();

}