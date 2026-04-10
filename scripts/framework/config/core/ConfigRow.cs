namespace Framework
{
    /// <summary>
    /// 配置表数据行基类。
    /// 所有通过 XlsxConverter 生成的配置类均继承此类。
    /// </summary>
    public abstract class ConfigRow
    {
        /// <summary>
        /// 主键 ID，对应表格第一列（必须为 int 类型）。
        /// </summary>
        public int Id { get; set; }
    }
}
