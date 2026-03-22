namespace Framework.UI
{
    /// <summary>
    /// UI 层级枚举。
    /// <para>
    /// 每个层级对应场景树中的一个 <c>CanvasLayer</c> 节点，Layer 值越大渲染越靠前。
    /// </para>
    /// </summary>
    public enum UILayer
    {
        /// <summary>背景层（CanvasLayer = 0）：用于全屏背景、天空盒 UI 等。</summary>
        Background = 0,

        /// <summary>普通层（CanvasLayer = 10）：常规游戏面板、HUD 等。</summary>
        Normal = 10,

        /// <summary>高层（CanvasLayer = 20）：二级弹窗、提示面板等。</summary>
        High = 20,

        /// <summary>遮罩层（CanvasLayer = 30）：模态对话框、半透明遮罩等。</summary>
        Modal = 30,

        /// <summary>系统层（CanvasLayer = 40）：加载界面、系统提示等，覆盖所有 UI。</summary>
        System = 40,

        /// <summary>顶层提示（CanvasLayer = 50）：飘字、Toast、引导遮罩等最顶层内容。</summary>
        Tips = 50,
    }
}
