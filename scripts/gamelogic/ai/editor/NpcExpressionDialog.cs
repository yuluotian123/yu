#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
namespace GameLogic.AI;

/// <summary>A local draft; applying creates one native Inspector undo entry, cancellation writes nothing.</summary>
[Tool]
public partial class NpcExpressionDialog : Window
{
    readonly NpcGraphAsset _graph;
    readonly GodotObject _owner;
    readonly CharacterValueType _expected;
    readonly Action<CharacterExpressionDefinition> _commit;
    readonly Func<bool> _isCurrent;
    readonly bool _required;
    readonly VBoxContainer _editor=new();
    readonly Label _preview=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};
    readonly Label _status=new(){AutowrapMode=TextServer.AutowrapMode.WordSmart};
    readonly Label _targetHint=new(){Text="先点击公式中的参数或 ⋯，再从这里填入。",AutowrapMode=TextServer.AutowrapMode.WordSmart};
    readonly LineEdit _search=new(){PlaceholderText="搜索名称、分类或参数 ID"};
    readonly Tree _parameters=new(){HideRoot=true,SizeFlagsVertical=Control.SizeFlags.ExpandFill};
    readonly Button _apply=new(){Text="应用公式"},_undoButton=new(){Text="撤销编辑"},_redoButton=new(){Text="重做编辑"};
    readonly Stack<CharacterExpressionDefinition> _undo=new(),_redo=new();
    readonly Dictionary<CharacterValueType,Button> _constants=new();
    Button _calculation;
    IReadOnlyList<NpcEditorSymbol> _symbols=Array.Empty<NpcEditorSymbol>();
    Action<CharacterExpressionDefinition> _insert;
    CharacterValueType _slotType;
    bool _anyType,_refreshing,_rebuildQueued;
    public CharacterExpressionDefinition Draft {get;private set;}
    public NpcExpressionDialog(){Visible=false;}
    public NpcExpressionDialog(NpcGraphAsset graph,GodotObject owner,CharacterValueType expected,CharacterExpressionDefinition value,string title,
        Action<CharacterExpressionDefinition> commit,Func<bool> isCurrent=null,bool required=false)
    {
        _graph=graph;_owner=owner;_expected=expected;_commit=commit;_isCurrent=isCurrent;_required=required;
        Draft=value==null?null:(CharacterExpressionDefinition)value.DuplicateDeep(Resource.DeepDuplicateMode.All);
        Visible=false;Title=title+" · 公式编辑器";MinSize=new(720,460);Unresizable=false;Transient=true;Exclusive=true;
        GraphEditorTranslationService.DisableAutoTranslate(this);
        CloseRequested+=QueueFree;
        var margin=new MarginContainer();margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        foreach(string edge in new[]{"left","right","top","bottom"})margin.AddThemeConstantOverride("margin_"+edge,14);
        AddChild(margin);var shell=new VBoxContainer();shell.AddThemeConstantOverride("separation",10);margin.AddChild(shell);
        var toolbar=new HFlowContainer();toolbar.AddThemeConstantOverride("h_separation",8);
        _undoButton.Pressed+=UndoDraft;_redoButton.Pressed+=RedoDraft;toolbar.AddChild(_undoButton);toolbar.AddChild(_redoButton);
        var clear=new Button{Text="清空公式"};clear.Pressed+=()=>Change(null,true);toolbar.AddChild(clear);shell.AddChild(toolbar);
        var previewPanel=new PanelContainer();previewPanel.AddChild(_preview);shell.AddChild(previewPanel);
        var split=new HSplitContainer{SizeFlagsVertical=Control.SizeFlags.ExpandFill,SplitOffsets=new[]{250}};shell.AddChild(split);
        var library=new VBoxContainer{CustomMinimumSize=new(210,0)};split.AddChild(library);
        library.AddChild(new Label{Text="参数库"});library.AddChild(_targetHint);library.AddChild(_search);library.AddChild(_parameters);
        var constants=new HFlowContainer();
        foreach(var type in new[]{CharacterValueType.Number,CharacterValueType.Boolean,CharacterValueType.Symbol})
        {
            var button=new Button{Text=type switch{CharacterValueType.Number=>"数字",CharacterValueType.Boolean=>"真 / 假",_=>"文本"}};
            button.Pressed+=()=>Insert(NpcExpressionEditor.Constant(type));_constants.Add(type,button);constants.AddChild(button);
        }
        library.AddChild(constants);_calculation=new Button{Text="数值计算"};_calculation.Pressed+=()=>Insert(NpcExpressionEditor.Operation(CharacterExpressionKind.Multiply,new(){Number=1},new(){Number=1}));library.AddChild(_calculation);
        var scroll=new ScrollContainer{CustomMinimumSize=new(390,0),HorizontalScrollMode=ScrollContainer.ScrollMode.Auto,VerticalScrollMode=ScrollContainer.ScrollMode.Auto};split.AddChild(scroll);
        _editor.SizeFlagsHorizontal=Control.SizeFlags.ExpandFill;scroll.AddChild(_editor);
        shell.AddChild(_status);var footer=new HBoxContainer();footer.AddSpacer(false);
        var cancel=new Button{Text="取消",CustomMinimumSize=new(88,0)};cancel.Pressed+=QueueFree;footer.AddChild(cancel);
        _apply.CustomMinimumSize=new(110,0);_apply.Pressed+=ApplyDraft;footer.AddChild(_apply);shell.AddChild(footer);
        _search.TextChanged+=_=>RefreshParameters();
        _parameters.ItemSelected+=()=>
        {
            if(_refreshing)return;var item=_parameters.GetSelected();if(item==null)return;
            if(_insert==null){_targetHint.Text="请先点击右侧要替换的参数或 ⋯。";return;}
            int index=item.GetMetadata(0).AsInt32();if(index>=0&&index<_symbols.Count)Insert(NpcExpressionEditor.Reference(_symbols[index]));
        };
        Rebuild();
    }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if(@event is InputEventKey{Pressed:true,Keycode:Key.Escape}){SetInputAsHandled();QueueFree();}
    }
    void Change(CharacterExpressionDefinition value,bool structural)
    {
        _undo.Push(Draft);_redo.Clear();Draft=value;RefreshPreview();
        if(structural&&!_rebuildQueued)
        {
            _insert=null;_rebuildQueued=true;
            Callable.From(()=>{_rebuildQueued=false;if(GodotObject.IsInstanceValid(this)&&!IsQueuedForDeletion())Rebuild();}).CallDeferred();
        }
    }
    void UndoDraft(){if(_undo.Count==0)return;_redo.Push(Draft);Draft=_undo.Pop();Rebuild();}
    void RedoDraft(){if(_redo.Count==0)return;_undo.Push(Draft);Draft=_redo.Pop();Rebuild();}
    void Rebuild()
    {
        foreach(Node child in _editor.GetChildren()){GraphEditorSignalCleanup.PrepareForRemoval(child);_editor.RemoveChild(child);}
        _insert=null;_targetHint.Text="先点击公式中的参数或 ⋯，再从这里填入。";
        _editor.AddChild(NpcExpressionEditor.Build(_graph,_owner,_expected,Draft,Change,SelectSlot));
        RefreshPreview();RefreshParameters();
    }
    void SelectSlot(Control anchor,CharacterValueType expected,bool anyType,Action<CharacterExpressionDefinition> selected)
    {
        _slotType=expected;_anyType=anyType;_insert=selected;
        _targetHint.Text="正在替换："+(anchor.TooltipText.Length>0?anchor.TooltipText.Split('\n')[0]:"选中的值");
        RefreshParameters();if(_search.IsVisibleInTree()){_search.GrabFocus();_search.SelectAll();}
    }
    void Insert(CharacterExpressionDefinition expression)
    {
        // The operand callback queues a rebuild; do not clear a Tree from inside ItemSelected.
        var insert=_insert;if(insert==null)return;_insert=null;insert(expression);
    }
    void RefreshParameters()
    {
        _symbols=NpcExpressionEditorCatalog.For(_graph,_owner);_refreshing=true;_parameters.Clear();var root=_parameters.CreateItem();
        var groups=new Dictionary<string,TreeItem>();string query=_search.Text.Trim();
        for(int i=0;i<_symbols.Count;i++)
        {
            var symbol=_symbols[i];if(_insert!=null&&!_anyType&&symbol.Type!=_slotType)continue;
            if(query.Length>0&&!(symbol.Label+" "+symbol.Key+" "+symbol.Group).Contains(query,StringComparison.OrdinalIgnoreCase))continue;
            if(!groups.TryGetValue(symbol.Group,out var group))
            {group=_parameters.CreateItem(root);group.SetText(0,symbol.Group);group.SetSelectable(0,false);groups.Add(symbol.Group,group);}
            var item=_parameters.CreateItem(group);item.SetText(0,symbol.Label);item.SetTooltipText(0,symbol.Key+" · "+symbol.Type);item.SetMetadata(0,i);
        }
        foreach(var pair in _constants)pair.Value.Disabled=_insert==null||!_anyType&&pair.Key!=_slotType;
        _calculation.Disabled=_insert==null||!_anyType&&_slotType!=CharacterValueType.Number;
        _refreshing=false;
    }
    void RefreshPreview()
    {
        var symbols=NpcExpressionEditorCatalog.For(_graph,_owner);
        string formatted=NpcExpressionPresentation.Format(Draft,symbols);
        _preview.Text=formatted.Length>500?formatted[..500]+"…":formatted;_preview.TooltipText=formatted;
        string error=Draft==null&&_required?"此处必须配置公式。":NpcExpressionPresentation.Validate(Draft,_expected,symbols);
        bool current=_isCurrent?.Invoke()??true;
        _status.Text=!current?"原公式已在其他位置修改，请关闭后重新打开。":error.Length>0?error:"修改保存在草稿中；点击“应用公式”保存，取消则放弃。";
        _apply.Disabled=!current||error.Length>0;_undoButton.Disabled=_undo.Count==0;_redoButton.Disabled=_redo.Count==0;
    }
    public void ApplyDraft()
    {
        RefreshPreview();if(_apply.Disabled)return;
        _commit(Draft);QueueFree();
    }
}
#endif
