using Godot;

namespace Framework
{
    /// <summary>
    /// 配置表模块设置资源。
    /// 在编辑器中通过 <see cref="Framework.Settings"/> 挂载后，由 <see cref="ConfigModule"/> 读取。
    /// </summary>
    [GlobalClass]
    public partial class ConfigSetting : Resource
    {
        /// <summary>
        /// 运行时 JSON 数据文件所在目录（res:// 格式）。
        /// 默认：res://assets/config/tables/
        /// </summary>
        [Export]
        public string TablePath { get; set; } = "res://assets/config/tables/";

    }
}
