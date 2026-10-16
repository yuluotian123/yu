using Godot;

namespace GameLogic;

/// <summary>验证临时云纹理的 GPU 绑定与资源序列化边界，不参与游戏逻辑。</summary>
public partial class CloudRadianceValidation : RefCounted
{
    private readonly SkyCloudRadiance _capture = new();

    public void Bind(Node host, ShaderMaterial sky, MeshInstance3D cloud) => _capture.Bind(host, sky, new[] { cloud });
    public void Refresh() => _capture.Refresh();
    public void Release() => _capture.Release();
}
