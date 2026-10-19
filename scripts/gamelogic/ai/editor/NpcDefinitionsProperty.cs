#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

/// <summary>A named, categorized entry point into the existing native definition inspectors.</summary>
[Tool]
public partial class NpcDefinitionsProperty : EditorProperty
{
    readonly NpcGraphAsset _graph;
    readonly VBoxContainer _body=new(),_list=new();
    readonly LineEdit _search=new(){PlaceholderText="搜索定义名称或分类"};
    string _message="";
    readonly Dictionary<string,bool> _expanded=new();
    string _stamp;
    public NpcDefinitionsProperty(){AddChild(_body);SetBottomEditor(_body);_body.AddChild(_search);_body.AddChild(_list);}
    public NpcDefinitionsProperty(NpcGraphAsset graph):this()
    {
        _graph=graph;_search.TextChanged+=_=>Rebuild();
    }
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_graph))return;
        string stamp=string.Join("|",_graph.Definitions.Where(d=>d!=null).Select(d=>d.DefinitionId+":"+d.DisplayName+":"+d.Enabled+":"+NpcResourcePresentation.Description(_graph,d)));
        if(stamp==_stamp)return;_stamp=stamp;Rebuild();
    }
    void Rebuild()
    {
        foreach(Node child in _list.GetChildren())
        {GraphEditorSignalCleanup.PrepareForRemoval(child);_list.RemoveChild(child);}
        Label="人物定义库";
        string query=_search.Text.Trim();
        foreach(var group in _graph.Definitions.Where(d=>d!=null&&(query.Length==0||(d.DisplayName+" "+d.Category).Contains(query,StringComparison.OrdinalIgnoreCase))).GroupBy(d=>d.Category))
        {
            string category=group.Key;bool expanded=query.Length>0||_expanded.GetValueOrDefault(category);
            var header=new Button{Text=(expanded?"▾ ":"▸ ")+category+" · "+group.Count(),Alignment=HorizontalAlignment.Left,ClipText=true};_list.AddChild(header);
            var content=new VBoxContainer{Visible=expanded};_list.AddChild(content);
            header.Pressed+=()=>{_expanded[category]=!content.Visible;content.Visible=!content.Visible;header.Text=(content.Visible?"▾ ":"▸ ")+category+" · "+group.Count();};
            foreach(var definition in group)
            {
                var row=new HBoxContainer();var open=new Button{Text=NpcResourcePresentation.Title(_graph,definition)+(definition.Enabled?"":"（已停用）"),TooltipText=NpcResourcePresentation.Description(_graph,definition),
                    ClipText=true,SizeFlagsHorizontal=Control.SizeFlags.ExpandFill};
                open.Pressed+=()=>EmitSignal(SignalName.ResourceSelected,nameof(NpcGraphAsset.Definitions),definition);row.AddChild(open);
                var remove=new Button{Text="×",Disabled=Referenced(definition.DefinitionId),TooltipText="被节点或其他配置引用的定义，请先通过图处理引用。"};
                remove.Pressed+=()=>{if(!Referenced(definition.DefinitionId))Commit(_graph.Definitions.Where(d=>d!=definition));};row.AddChild(remove);content.AddChild(row);
            }
        }
        var add=new Button{Text="添加已有定义资源…",ClipText=true,TooltipText="从项目中添加已有 NPC 定义资源"};
        add.Pressed+=()=>
        {
            var file=new EditorFileDialog{Title="选择 NPC 定义资源",Access=EditorFileDialog.AccessEnum.Resources,FileMode=EditorFileDialog.FileModeEnum.OpenFile};file.AddFilter("*.tres,*.res","Godot 资源");
            file.FileSelected+=path=>{AddExisting(ResourceLoader.Load(path) as NpcDefinitionResource);file.QueueFree();};file.Canceled+=file.QueueFree;AddChild(file);file.PopupCentered(new Vector2I(900,600));
        };_list.AddChild(add);
        // A fresh label avoids reusing a control already queued for removal.
        _list.AddChild(new Label{Text=_message,AutowrapMode=TextServer.AutowrapMode.WordSmart});
    }
    bool Referenced(string id)=>_graph.Nodes.OfType<NpcDefinitionNodeData>().Any(n=>n.DefinitionId==id)||
        _graph.Definitions.OfType<NpcPlanTemplateDefinition>().Any(p=>p.ActionIds.Contains(id)||p.Variables.Any(v=>v.SelectorId==id))||
        _graph.Definitions.OfType<NpcActionDefinition>().Any(a=>a.Inputs.Any(i=>i.SelectorId==id));
    internal void AddExisting(NpcDefinitionResource definition)
    {
        if(definition==null){_message="请选择有效的 NPC 定义资源。";Rebuild();return;}
        if(_graph.Definitions.Any(d=>d?.DefinitionId==definition.DefinitionId)){_message="这个定义已经在人物图中。";Rebuild();return;}
        _message="";Commit(_graph.Definitions.Append(definition));
    }
    void Commit(IEnumerable<NpcDefinitionResource> definitions)
    {
        EmitChanged(nameof(NpcGraphAsset.Definitions),Variant.From(new Godot.Collections.Array<NpcDefinitionResource>(definitions)));
        _stamp=null;Callable.From(_UpdateProperty).CallDeferred();
    }
}
#endif
