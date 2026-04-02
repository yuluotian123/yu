using System.Text.Json.Serialization;
using Godot;

/// <summary>
/// 测试 GraphJsonHelper 的 Attribute 支持
/// </summary>
public class JsonHelperAttributeTest
{
    public class TestClass
    {
        // 公开属性，默认序列化
        public string PublicProperty { get; set; } = "public";

        // 私有属性，标记 [JsonInclude] 后会序列化
        [JsonInclude]
        private string PrivateProperty { get; set; } = "private";

        // 私有字段，标记 [JsonInclude] 后会序列化
        [JsonInclude]
        private int _privateField = 42;

        // 公开属性，标记 [JsonIgnore] 不会序列化
        [JsonIgnore]
        public string IgnoredProperty { get; set; } = "ignored";

        // 公开字段，没有 [JsonInclude] 不会序列化
        public int PublicField = 100;
    }

    public static void Run()
    {
        var obj = new TestClass();
        
        // 序列化
        string json = GraphJsonHelper.Serialize(obj);
        GD.Print("序列化结果：");
        GD.Print(json);
        
        // 反序列化
        var restored = GraphJsonHelper.Deserialize<TestClass>(json);
        GD.Print("\n反序列化成功！");
        
        // 验证：应该包含 PublicProperty, PrivateProperty, _privateField
        // 不应该包含 IgnoredProperty, PublicField
    }
}
