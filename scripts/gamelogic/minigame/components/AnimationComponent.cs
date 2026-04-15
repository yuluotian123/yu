using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class AnimationComponent : Component2D
{
    public override int Priority => ComponentPriority.Animation;


    public override void OnInit()
    {
        // 在这里初始化动画相关的资源和状态

    }


    /// <param name="animationName"></param>
    public void PlayAnimation(string animationName)
    {
    }
}
