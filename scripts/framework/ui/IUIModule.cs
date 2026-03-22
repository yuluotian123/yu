using System;

namespace Framework.UI
{
    /// <summary>
    /// UI 模块接口。
    /// <para>
    /// 通过 <see cref="ModuleSystem.GetModule{T}"/> 以本接口获取 <see cref="UIModule"/> 实例。
    /// </para>
    /// <example>
    /// <code>
    /// var ui = ModuleSystem.GetModule&lt;IUIModule&gt;();
    ///
    /// // 同步打开（资源已缓存时即时显示，否则等异步加载完成后显示）
    /// ui.ShowUI&lt;MainMenuWindow&gt;("PlayerName");
    ///
    /// // 异步打开（带回调）
    /// ui.ShowUIAsync&lt;BagWindow&gt;(onComplete: win =&gt; { }, userData: bagData);
    ///
    /// // 关闭
    /// ui.CloseUI&lt;MainMenuWindow&gt;();
    ///
    /// // 隐藏（缓存节点，超时后自动关闭）
    /// ui.HideUI&lt;BagWindow&gt;();
    /// </code>
    /// </example>
    /// </summary>
    public interface IUIModule
    {
        // ──────────────────────────────────────────────
        //  打开窗口
        // ──────────────────────────────────────────────

        /// <summary>
        /// 打开 UI 窗口（推荐方式）。
        /// <para>
        /// 若窗口已存在且已加载，立即刷新并置顶；
        /// 若首次打开，异步加载资源，加载完成后自动显示。
        /// </para>
        /// </summary>
        /// <typeparam name="T">UIWindow 子类，需标记 <see cref="WindowAttribute"/>。</typeparam>
        /// <param name="userData">传递给 <see cref="UIBase.UserDatas"/> 的数据。</param>
        void ShowUI<T>(params object[] userData) where T : UIWindow, new();

        /// <summary>
        /// 异步打开 UI 窗口，加载完成后回调。
        /// </summary>
        /// <typeparam name="T">UIWindow 子类。</typeparam>
        /// <param name="onComplete">加载并初始化完成后的回调（参数为窗口实例）。</param>
        /// <param name="userData">用户数据。</param>
        void ShowUIAsync<T>(Action<T> onComplete = null, params object[] userData) where T : UIWindow, new();

        // ──────────────────────────────────────────────
        //  关闭 / 隐藏
        // ──────────────────────────────────────────────

        /// <summary>
        /// 关闭指定类型的 UI 窗口（销毁节点）。
        /// </summary>
        void CloseUI<T>() where T : UIWindow;

        /// <summary>
        /// 关闭指定实例的 UI 窗口。
        /// </summary>
        void CloseUI(UIWindow window);

        /// <summary>
        /// 隐藏指定类型的 UI 窗口（节点保留，缓存一段时间后自动关闭）。
        /// </summary>
        void HideUI<T>() where T : UIWindow;

        /// <summary>
        /// 关闭所有 UI 窗口。
        /// </summary>
        /// <param name="layer">若指定，则只关闭该层级的窗口；否则关闭全部。</param>
        void CloseAll(UILayer? layer = null);

        // ──────────────────────────────────────────────
        //  查询
        // ──────────────────────────────────────────────

        /// <summary>是否存在指定类型的窗口（含隐藏/加载中状态）。</summary>
        bool HasWindow<T>() where T : UIWindow;

        /// <summary>获取指定类型窗口实例，不存在时返回 null。</summary>
        T GetWindow<T>() where T : UIWindow;

        /// <summary>获取所有层中最顶层窗口的名称。</summary>
        string GetTopWindowName();

        /// <summary>获取指定层中最顶层窗口的名称。</summary>
        string GetTopWindowName(UILayer layer);

        /// <summary>是否有窗口正在异步加载中。</summary>
        bool IsAnyLoading();

        /// <summary>当前已打开的窗口总数（含隐藏状态）。</summary>
        int WindowCount { get; }
    }
}
