using System;

namespace Framework.UI
{
    /// <summary>
    /// UI 窗口属性标记。
    /// <para>
    /// 用于标注 <see cref="UIWindow"/> 子类的层级、资源路径及全屏标志，
    /// 由 <see cref="UIModule"/> 在创建实例时自动读取。
    /// </para>
    /// <example>
    /// <code>
    /// [Window(UILayer.Normal, "res://assets/ui/main_menu.tscn", fullScreen: true)]
    /// public class MainMenuWindow : UIWindow { }
    /// </code>
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class WindowAttribute : Attribute
    {
        /// <summary>窗口所在层级。</summary>
        public readonly UILayer Layer;

        /// <summary>PackedScene 资源路径（res:// 格式）。</summary>
        public readonly string AssetPath;

        /// <summary>
        /// 是否为全屏窗口。
        /// <para>全屏窗口打开时，同层及以下的所有非全屏窗口 Visible 将被设置为 false。</para>
        /// </summary>
        public readonly bool FullScreen;

        /// <summary>
        /// 隐藏后自动关闭的等待时间（秒）。
        /// <para>小于等于 0 表示隐藏时立即销毁，不缓存；大于 0 则缓存节点，超时后自动销毁。</para>
        /// </summary>
        public readonly float HideTimeToClose;

        /// <summary>
        /// 标记 UI 窗口属性。
        /// </summary>
        /// <param name="layer">层级。</param>
        /// <param name="assetPath">PackedScene 资源路径（res:// 格式）。</param>
        /// <param name="fullScreen">是否全屏（默认 false）。</param>
        /// <param name="hideTimeToClose">隐藏后自动关闭延迟秒数（默认 10 秒）。</param>
        public WindowAttribute(
            UILayer layer,
            string assetPath = "",
            bool fullScreen = false,
            float hideTimeToClose = 10f)
        {
            Layer = layer;
            AssetPath = assetPath;
            FullScreen = fullScreen;
            HideTimeToClose = hideTimeToClose;
        }
    }

    /// <summary>
    /// UI 节点自动绑定特性。
    /// <para>
    /// 标记在 <see cref="UIBase"/> 子类的字段上，框架在 <c>InternalCreate</c> 阶段
    /// 通过反射自动从场景树中查找对应节点并赋值，无需手动编写绑定代码。
    /// </para>
    /// <example>
    /// <code>
    /// // 方式1：指定精确路径
    /// [UIBind("Panel/Header/Title")]
    /// private Label _titleLabel;
    ///
    /// // 方式2：路径为空，用字段名推断（_titleLabel → TitleLabel）
    /// [UIBind]
    /// private Label _titleLabel;
    ///
    /// // 方式3：Godot UniqueNode语法（场景中右键节点→Access as Unique Name）
    /// [UIBind("%")]
    /// private Button _btnStart;   // 等价于 GetNode("%BtnStart")
    /// </code>
    /// </example>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = true)]
    public sealed class UIBindAttribute : Attribute
    {
        /// <summary>
        /// 节点路径。
        /// <list type="bullet">
        ///   <item>指定路径字符串：直接用该路径调用 <c>GetNodeOrNull</c>。</item>
        ///   <item>空字符串（默认）：从字段名推断节点名（去掉 <c>_</c>/<c>m_</c> 前缀并首字母大写）。</item>
        ///   <item><c>"%"</c>：使用 Godot UniqueNode 语法，节点名同样由字段名推断。</item>
        /// </list>
        /// </summary>
        public readonly string Path;

        /// <summary>
        /// 创建 UIBind 特性。
        /// </summary>
        /// <param name="path">
        /// 节点路径。为空时用字段名推断；为 <c>"%"</c> 时使用 Godot UniqueNode 语法。
        /// </param>
        public UIBindAttribute(string path = "")
        {
            Path = path;
        }
    }
}
