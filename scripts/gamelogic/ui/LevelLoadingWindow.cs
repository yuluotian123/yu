using Framework.UI;
using Godot;

namespace GameLogic;

[Window(UILayer.Loading,"res://assets/scenes/ui/level_loading_window.tscn",fullScreen:true)]
public class LevelLoadingWindow : UIWindow
{
    IWorldModule _world;
    Label _levelName,_stage,_percentage;
    ProgressBar _progress;

    public override void BindMemberProperty()
    {
        _levelName=Owner.GetNode<Label>("%LevelName");
        _stage=Owner.GetNode<Label>("%Stage");
        _percentage=Owner.GetNode<Label>("%Percentage");
        _progress=Owner.GetNode<ProgressBar>("%Progress");
    }
    protected override void OnRefresh()
    {
        _world=(IWorldModule)UserDatas[0];
        _levelName.Text=(string)UserDatas[1];
        RefreshProgress();
    }
    protected override void OnUpdate(double delta)=>RefreshProgress();
    internal void RefreshProgress()
    {
        float progress=_world.Progress;
        _progress.Value=progress*100;
        _percentage.Text=$"{Mathf.FloorToInt(progress*100)}%";
        _stage.Text=_world.Status switch
        {
            "加载关卡"=>"正在载入场景",
            "创建关卡"=>"正在准备场景",
            "加载玩家"=>"正在载入角色",
            "交接关卡"=>"正在进入关卡",
            "等待物理与导航"=>"即将就绪",
            "加载完成" or "关卡运行中"=>"加载完成",
            _=>"正在准备"
        };
    }
}
