namespace GameLogic
{
    /// <summary>
    /// 存档管理模块接口。
    /// 负责将所有注册的 <see cref="ISaveable"/> 对象序列化到文件，
    /// 以及从文件反序列化并回写到对应对象。
    /// 
    /// JSON saves use user://saves/{slot}.json, with legacy objects and versioned sections.
    /// Loading falls back to the backup and then the legacy res://saves directory.
    /// </summary>
    public interface ISaveModule
    {
        string[] ListSlots();
        string ReadSlot(string slot, out string sourcePath);
        void WriteSlot(string slot, string json, string expectedJson);
        void DeleteSlot(string slot, string expectedJson);

        /// <summary>注册一个可存档对象。同一 SaveKey 只能注册一次。</summary>
        void Register(ISaveable saveable);

        /// <summary>取消注册。</summary>
        void Unregister(ISaveable saveable);

        void RegisterSection(ISaveSection section);
        void UnregisterSection(ISaveSection section);

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
