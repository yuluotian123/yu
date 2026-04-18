namespace GameLogic
{
    /// <summary>
    /// 可存档组件接口。
    /// 实现此接口并注册到 SaveModule 后，该对象的 public 属性和标注了 [JsonInclude] 的字段
    /// 会被自动序列化/反序列化，无需手写任何序列化代码。
    /// 
    /// 使用方法：
    /// 1. 实现 ISaveable，提供唯一的 SaveKey
    /// 2. 向 SaveModule 注册：saveModule.Register(this)
    /// 3. 在需要保存的属性/字段上正常使用 public 属性 或 [JsonInclude] 标注
    /// 4. 调用 saveModule.Save("slot1") 即可自动保存
    /// 
    /// <code>
    /// using GameLogic;
    /// using System.Text.Json.Serialization;
    /// 
    /// public class PlayerManager : ISaveable
    /// {
    ///     public string SaveKey => "player";
    ///     
    ///     // 这些属性会自动保存
    ///     public int Health { get; set; } = 100;
    ///     public int Level { get; set; } = 1;
    ///     public string PlayerName { get; set; } = "Hero";
    ///     
    ///     // 私有字段加 [JsonInclude] 也会被保存
    ///     [JsonInclude] private float _playTime;
    ///     
    ///     // 不想保存的属性加 [JsonIgnore]
    ///     [JsonIgnore] public bool IsInBattle { get; set; }
    /// }
    /// </code>
    /// </summary>
    public interface ISaveable
    {
        /// <summary>
        /// 子系统的唯一存档 Key（如 "mission_chains"、"inventory"、"player"）。
        /// 在存档文件中作为字典的 key 使用，不可重复。
        /// </summary>
        string SaveKey { get; }
        

        /// <summary>
        /// 自定义在序列化时的行为
        /// </summary>
        void Save(){}

        /// <summary>SaveModule 完成数据回写后自动调用，用于执行运行时状态恢复。</summary>
         void Load(){}
    }
}
