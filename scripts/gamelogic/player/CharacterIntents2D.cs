using Godot;

namespace GameLogic
{
    /// <summary>
    /// 具备 intent 输入协议的角色能力组件。
    /// RawIntent 表示 controller/AI 发出的请求，ApprovedIntent 表示 FSM 批准后的请求。
    /// </summary>
    public interface ICharacterIntentAbility2D<TIntent> where TIntent : struct
    {
        /// <summary>本帧由 controller 或 AI 写入的原始意图。</summary>
        TIntent RawIntent { get; }

        /// <summary>本帧由 FSM 批准后，能力组件实际执行的意图。</summary>
        TIntent ApprovedIntent { get; }

        /// <summary>写入原始意图。通常只由 controller 或 AI 调用。</summary>
        void SetIntent(TIntent intent);

        /// <summary>写入已批准意图。通常只由 FSM 调用。</summary>
        void ApproveIntent(TIntent intent);

        /// <summary>清理本帧的原始意图和已批准意图。</summary>
        void ClearFrameIntents();
    }

    /// <summary>水平移动意图。</summary>
    public readonly struct MoveIntent2D
    {
        public MoveIntent2D(float axisX)
        {
            AxisX = Mathf.Clamp(axisX, -1f, 1f);
            HasInput = !Mathf.IsZeroApprox(AxisX);
        }

        /// <summary>水平输入轴，范围为 -1 到 1。负数向左，正数向右。</summary>
        public float AxisX { get; }

        /// <summary>是否存在有效水平输入。</summary>
        public bool HasInput { get; }

        /// <summary>无移动输入。</summary>
        public static MoveIntent2D None => new(0f);
    }

    /// <summary>跳跃意图。</summary>
    public readonly struct JumpIntent2D
    {
        public JumpIntent2D(bool startRequested, bool sustainRequested)
        {
            StartRequested = startRequested;
            SustainRequested = sustainRequested;
        }

        /// <summary>本帧是否刚按下跳跃。</summary>
        public bool StartRequested { get; }

        /// <summary>本帧是否持续按住跳跃。</summary>
        public bool SustainRequested { get; }

        /// <summary>无跳跃输入。</summary>
        public static JumpIntent2D None => new(false, false);
    }

    /// <summary>攻击意图，预留给后续攻击能力使用。</summary>
    public readonly struct AttackIntent2D
    {
        public AttackIntent2D(bool startRequested, bool sustainRequested)
        {
            StartRequested = startRequested;
            SustainRequested = sustainRequested;
        }

        /// <summary>本帧是否刚按下攻击。</summary>
        public bool StartRequested { get; }

        /// <summary>本帧是否持续按住攻击。</summary>
        public bool SustainRequested { get; }

        /// <summary>无攻击输入。</summary>
        public static AttackIntent2D None => new(false, false);
    }
}
