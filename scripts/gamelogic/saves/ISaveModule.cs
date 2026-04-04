namespace Framework
{
    /// <summary>
    /// 存档管理模块接口。
    /// 负责将所有注册的 <see cref="GameLogic.Save.ISaveable"/> 对象序列化到文件，
    /// 以及从文件反序列化并回写到对应对象。
    /// 
    /// 存档文件格式为 JSON，路径为 res://saves/{slot}.json。
    /// 每个 ISaveable 以其 SaveKey 为 key 存储在顶层 JSON 对象中。
    /// </summary>
    public interface ISaveModule
    {
        /// <summary>注册一个可存档对象。同一 SaveKey 只能注册一次。</summary>
        void Register(GameLogic.Save.ISaveable saveable);

        /// <summary>取消注册。</summary>
        void Unregister(GameLogic.Save.ISaveable saveable);

        /// <summary>将所有已注册对象保存到指定存档槽。</summary>
        void Save(string slot = "default");

        /// <summary>
        /// 从指定存档槽加载数据，并将数据回写到所有已注册对象。
        /// 若存档文件不存在则静默返回 false。
        /// </summary>
        bool Load(string slot = "default");

        /// <summary>删除指定存档槽文件。</summary>
        void Delete(string slot = "default");

        /// <summary>判断指定存档槽是否存在。</summary>
        bool Exists(string slot = "default");
    }
}
