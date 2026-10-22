#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic;

/// <summary>Authoring by directory labels; stored references remain plain stable IDs.</summary>
[Tool]
public partial class CharacterWorldObjectProperty : EditorProperty
{
    readonly VBoxContainer _body=new();
    CharacterWorldDefinition _owner;
    bool _bindings;
    string _stamp;
    public CharacterWorldObjectProperty(){AddChild(_body);SetBottomEditor(_body);}
    public CharacterWorldObjectProperty(CharacterWorldDefinition owner,bool bindings):this(){_owner=owner;_bindings=bindings;}
    public override void _UpdateProperty()
    {
        if(!GodotObject.IsInstanceValid(_owner))return;
        WorldCatalogEntry[] choices;
        try{choices=WorldAuthoringCompiler.EditorCatalog.Objects.Values.OrderBy(e=>e.DisplayName,StringComparer.Ordinal).ThenBy(e=>e.Id,StringComparer.Ordinal).ToArray();}
        catch(ArgumentException){choices=Array.Empty<WorldCatalogEntry>();}
        string stamp=(_bindings?string.Join(";",_owner.Objects.Select(p=>p.Key+"="+p.Value)):string.Join(";",_owner.KnownObjects))+string.Join(";",choices.Select(e=>e.Id+e.DisplayName));
        if(_stamp==stamp)return;_stamp=stamp;
        foreach(Node child in _body.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_body.RemoveChild(child);}
        var rows=_bindings?_owner.Objects.Select(p=>(Key:p.Key,Id:p.Value)).ToArray():_owner.KnownObjects.Select(id=>(Key:"",Id:id)).ToArray();
        for(int i=0;i<rows.Length;i++)
        {
            int index=i;var entry=rows[i];var row=new HBoxContainer();_body.AddChild(row);
            if(_bindings)
            {
                var key=new LineEdit{Text=entry.Key,CustomMinimumSize=new(90,0)};row.AddChild(key);
                key.TextSubmitted+=next=>
                {
                    next=next.Trim();if(next.Length==0||next!=entry.Key&&_owner.Objects.ContainsKey(next))return;
                    var values=new Godot.Collections.Dictionary<string,string>(_owner.Objects);values.Remove(entry.Key);values[next]=entry.Id;Commit(values);
                };
            }
            var picker=new OptionButton{SizeFlagsHorizontal=Control.SizeFlags.ExpandFill,TooltipText=entry.Id};row.AddChild(picker);
            picker.AddItem(entry.Id.Length==0?"请选择对象":"引用缺失 · "+entry.Id);picker.Select(0);
            for(int n=0;n<choices.Length;n++)
            {
                var choice=choices[n];picker.AddItem(choice.DisplayName+" · "+choice.Level.DisplayName+" · "+choice.Id[^Math.Min(8,choice.Id.Length)..]);
                if(choice.Id==entry.Id)picker.Select(n+1);
            }
            picker.ItemSelected+=n=>
            {
                if(n==0)return;string id=choices[(int)n-1].Id;
                if(_bindings){var values=new Godot.Collections.Dictionary<string,string>(_owner.Objects);values[entry.Key]=id;Commit(values);}
                else{var values=_owner.KnownObjects.ToArray();values[index]=id;Commit(values);}
            };
            var remove=new Button{Text="×"};row.AddChild(remove);
            remove.Pressed+=()=>
            {
                if(_bindings){var values=new Godot.Collections.Dictionary<string,string>(_owner.Objects);values.Remove(entry.Key);Commit(values);}
                else Commit(_owner.KnownObjects.Where((_,n)=>n!=index).ToArray());
            };
        }
        var add=new Button{Text=_bindings?"+ 对象绑定":"+ 已知对象",Disabled=choices.Length==0};_body.AddChild(add);
        add.Pressed+=()=>
        {
            if(_bindings)
            {
                int suffix=1;while(_owner.Objects.ContainsKey("object_"+suffix))suffix++;
                var values=new Godot.Collections.Dictionary<string,string>(_owner.Objects){{"object_"+suffix,choices[0].Id}};Commit(values);
            }
            else Commit(_owner.KnownObjects.Append(choices[0].Id).ToArray());
        };
    }
    void Commit(Godot.Collections.Dictionary<string,string> values){_stamp=null;EmitChanged(nameof(CharacterWorldDefinition.Objects),values);}
    void Commit(string[] values){_stamp=null;EmitChanged(nameof(CharacterWorldDefinition.KnownObjects),values);}
}
#endif
