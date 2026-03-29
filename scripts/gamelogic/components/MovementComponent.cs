using Framework;
using GameLogic;
using Godot;

[GlobalClass]
public partial class MovementComponent : Component
{
    // 配置属性（在Inspector中编辑）
    [ExportGroup("Movement Settings")]
    [Export] public float Speed { get; set; } = 5.0f;
    [Export] public float SprintSpeed { get; set; } = 6f;
    [Export] public float Acceleration { get; set; } = 10.0f;
    [Export] public float Friction { get; set; } = 8.0f;
    [Export] public bool IsZMove { get; set; } = false;
    [ExportGroup("Node References")]
    [Export] public NodePath CharacterBodyPath { get; set; } = "%Player";
    [Export] public bool hasFlip { get; set; } = true;
    [Export] public NodePath SpritePath { get; set; } = "%CharacterSprite";

    private CharacterBody3D characterBody;

    public override int Priority => ComponentPriority.Movement;

    // 运行时状态（不导出）
    private Vector3 _velocity;
    private float _curSpeed;
    private Vector2 _inputDirection;

    public bool IsMoving => _velocity != Vector3.Zero;
    public bool IsSprint => _velocity.Length() >= 5.0f;

    public override void OnInit()
    {
        characterBody = Owner.GetNode<CharacterBody3D>(CharacterBodyPath);

    }

    public override void OnPhysicsUpdate(double delta)
    {
        //将输入方向转化成normalized的向量
        Vector3 dir = IsZMove ? new Vector3(_inputDirection.X, 0, _inputDirection.Y) : new Vector3(_inputDirection.X, 0, 0);   
        dir = dir.Normalized(); 
        // 计算目标速度
        Vector3 targetVelocity = dir * _curSpeed;
        // 插值当前速度 towards 目标速度
        _velocity = _velocity.MoveToward(targetVelocity, Acceleration * (float)delta);
        // 应用摩擦力
        if (dir == Vector3.Zero) _velocity = _velocity.MoveToward(Vector3.Zero, Friction * (float)delta);
        // 移动角色
        characterBody.Velocity = _velocity;
        characterBody.MoveAndSlide();

        //处理动画
        var animationComponent = Owner.GetComponent<AnimationComponent>();
        if (animationComponent != null)
        {
            if (IsMoving)
            {
                if (hasFlip)
                {
                    var sprite3D = Owner.GetNode<AnimatedSprite3D>(SpritePath);
                    if (sprite3D != null)
                    {
                        // 根据输入方向设置动画的朝向
                        if (dir.X > 0)
                            sprite3D.FlipH = false; // 朝右
                        else if (dir.X < 0)
                            sprite3D.FlipH = true;  // 朝左
                    }
                }

                if (IsSprint)
                    animationComponent.PlayAnimation("run");
                else
                    animationComponent.PlayAnimation("walk");
            }
            else
            {
                animationComponent.PlayAnimation("idle");
            }
        }

        _inputDirection = Vector2.Zero; // 重置输入方向，等待下一帧更新
    }

    public void SetMovement(Vector2 inputDirection, bool isSprint)
    {
        _inputDirection = inputDirection;
        
        if (isSprint)
            _curSpeed = SprintSpeed;
        else
            _curSpeed = Speed;
    }
}