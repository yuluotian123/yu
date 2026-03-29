using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class AnimationComponent : Component
{
    public override int Priority => ComponentPriority.Animation;

[ExportGroup("Node References")]
    [Export] public NodePath SpritePath { get; set; } = "%CharacterSprite";

    private AnimatedSprite3D sprite3D;

    public override void OnInit()
    {
        // 在这里初始化动画相关的资源和状态
        sprite3D = Owner.GetNode<AnimatedSprite3D>(SpritePath);
        if (sprite3D == null)
            Debugger.Error("AnimationComponent requires an AnimatedSprite3D node as a child of the owner.");
    }

/// <summary>
/// 目前通过外部调用实现动画播放，后续会转到状态机
/// </summary>
/// <param name="animationName"></param>
    public void PlayAnimation(string animationName)
    {
        if (sprite3D != null)
        {
            sprite3D.Play(animationName);
        }
    }
}