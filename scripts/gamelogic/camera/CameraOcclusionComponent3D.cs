using System.Collections.Generic;
using Godot;

namespace GameLogic;

/// <summary>保持场景镜头构图，处理角色遮挡、房屋/树干穿入保护和正交镜头的近处表面渐隐。</summary>
[Tool, GlobalClass]
public partial class CameraOcclusionComponent3D : Component3D
{
    // 同一宿主内，在镜头完成跟随后检测最终视线。
    public override int Priority => ComponentPriority.VFX - 1;
    /// <summary>遮挡模型的搜索范围；仅处理 camera_occluder 分组节点及其子网格。</summary>
    [Export] public NodePath OccluderRootPath { get; set; } = new("..");
    /// <summary>相对跟随目标原点的检测中心，单位米。</summary>
    [Export] public Vector3 TargetOffset { get; set; } = new(0f, 0.1f, 0f);
    /// <summary>角色中心及屏幕左右、上下边缘必须全部被遮挡才淡出；这些遮挡可以来自不同模型。</summary>
    [Export] public Vector2 TargetHalfSize { get; set; } = new(0.32f, 0.4f);
    /// <summary>遮挡物的整体隐藏程度，1 完全隐藏整栋房屋或整棵树；保留碰撞、阴影和 GI。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float OccludedTransparency { get; set; } = 1f;
    /// <summary>五个检测点连续受挡达到此时长才开始渐隐；任一点恢复可见就重新计时。0 表示立即触发。</summary>
    [Export(PropertyHint.Range, "0,3,0.01")] public float OcclusionDelay { get; set; } = 0.2f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float FadeSeconds { get; set; } = 0.12f;
    [Export(PropertyHint.Range, "0,1,0.01")] public float RestoreSeconds { get; set; } = 0.25f;
    /// <summary>视线畅通后稍等再恢复，避免经过屋檐或树叶边缘时闪烁。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")] public float RestoreDelay { get; set; } = 0.15f;
    /// <summary>正交镜头前的表面渐隐距离，单位米；0 关闭。仅作用于 camera_near_fade 分组及其子网格，保留阴影。可在编辑器预览。</summary>
    [Export(PropertyHint.Range, "0,10,0.05,or_greater")] public float NearFadeDistance { get; set; }
    /// <summary>相机或近裁面进入房屋、实体树干时立即隐去该物体的全部表面，保留阴影、GI 和碰撞；离开后按恢复延迟和时长还原。识别 building/tree 资产类别或 camera_solid_occluder 分组，不移动镜头。</summary>
    [Export] public bool HideCameraInteriors { get; set; } = true;

    private static readonly StringName NearFadeGroup = "camera_near_fade";
    private static readonly StringName NearFadeParameter = "camera_near_fade_distance";
    private static readonly StringName SolidHideParameter = "camera_solid_hide";
    private readonly Dictionary<MeshInstance3D, Variant> _nearFadeOriginals = new();
    private readonly HashSet<MeshInstance3D> _nearFadeSeen = new();
    private readonly List<MeshInstance3D> _nearFadeRemoved = new();
    private float _appliedNearFadeDistance = -1f;

    private sealed class Occluder
    {
        public MeshInstance3D Mesh;
        public float OriginalTransparency;
        public float Fade;
        public float Hold;
        public int HitMask;
        public readonly float[] HitDistances = new float[5];
        public bool BlocksLayer;
        public CameraSolid Solid;
        public bool SupportsSolidHide;
        public bool SolidHideApplied;
        public Variant OriginalSolidHide;
    }

    private sealed class CameraSolid
    {
        public readonly List<Occluder> Meshes = new();
        public readonly List<CollisionShape3D> Colliders = new();
        public bool IsTree;
        public float Hide;
        public float Hold;
        public float OcclusionFade;
    }

    private sealed class TreeVolume
    {
        public TriangleMesh Triangles;
        public Vector3[] Faces;
        public Aabb Bounds;
    }

    private SceneCameraComponent3D _controller;
    private Node3D _timedTarget;
    private Camera3D _timedCamera;
    private float _occlusionElapsed;
    private readonly List<Occluder> _occluders = new();
    private readonly Dictionary<Mesh, TriangleMesh> _triangles = new();
    private readonly Dictionary<Node3D, CameraSolid> _cameraSolids = new();
    private readonly Dictionary<Mesh, TreeVolume> _treeVolumes = new();
    private readonly Vector3[] _cameraSamples = new Vector3[5];
    private readonly Vector3[] _samples = new Vector3[5];
    private readonly Vector3[] _starts = new Vector3[5];
    private readonly Occluder[] _nearest = new Occluder[5];

    public override void OnInit()
    {
        _controller = Owner.GetComponent<SceneCameraComponent3D>();
        var root = Owner.GetNodeOrNull<Node>(OccluderRootPath);
        if (root != null) Collect(root, false);
        RefreshNearFadeTargets(root);
    }

    private void Collect(Node node, bool included)
    {
        included |= node.IsInGroup("camera_occluder");
        if (included && node is MeshInstance3D mesh && mesh.Mesh != null)
        {
            var entry = new Occluder { Mesh = mesh, OriginalTransparency = mesh.Transparency };
            foreach (var property in mesh.GetPropertyList())
            {
                if (property["name"].AsString() == "instance_shader_parameters/" + SolidHideParameter)
                    entry.SupportsSolidHide = true;
            }
            if (entry.SupportsSolidHide) entry.OriginalSolidHide = GetStoredParameter(mesh, SolidHideParameter);
            if (FindCameraSolid(mesh) is Node3D buildingRoot)
            {
                if (!_cameraSolids.TryGetValue(buildingRoot, out var building))
                {
                    building = new CameraSolid { IsTree = buildingRoot.GetMeta("asset_category", "").AsString() == "tree" };
                    foreach (Node child in buildingRoot.FindChildren("*", "CollisionShape3D", true, false))
                        if (child is CollisionShape3D collider && collider.Shape is BoxShape3D)
                            building.Colliders.Add(collider);
                    _cameraSolids.Add(buildingRoot, building);
                }
                entry.Solid = building;
                building.Meshes.Add(entry);
            }
            _occluders.Add(entry);
        }
        foreach (Node child in node.GetChildren()) Collect(child, included);
    }

    private static Node3D FindCameraSolid(Node node)
    {
        for (Node ancestor = node; ancestor != null; ancestor = ancestor.GetParent())
            if (ancestor is Node3D building && (building.IsInGroup("camera_solid_occluder")
                || building.GetMeta("asset_category", "").AsString() is "building" or "tree")) return building;
        return null;
    }

    public override void OnUpdate(double delta)
    {
        var camera = _controller?.SceneCamera;
        var target = _controller?.FollowTarget;
        bool activeCamera = _controller?.HasShot == true && camera != null && camera.IsCurrent();
        ApplyNearFade(activeCamera && camera.Projection == Camera3D.ProjectionType.Orthogonal
            ? Mathf.Max(0f, NearFadeDistance) : -1f);
        if (!activeCamera || target == null)
        {
            Restore();
            return;
        }
        float dt = Mathf.Max(0f, (float)delta);
        UpdateCameraInteriors(camera, dt);
        if (target != _timedTarget || camera != _timedCamera)
        {
            _occlusionElapsed = 0f;
            _timedTarget = target;
            _timedCamera = camera;
        }
        Vector3 center = (target.IsPhysicsInterpolatedAndEnabled()
            ? target.GetGlobalTransformInterpolated().Origin : target.GlobalPosition) + TargetOffset;
        Vector3 right = camera.GlobalBasis.X * Mathf.Max(0f, TargetHalfSize.X);
        Vector3 up = camera.GlobalBasis.Y * Mathf.Max(0f, TargetHalfSize.Y);
        _samples[0] = center;
        _samples[1] = center - right;
        _samples[2] = center + right;
        _samples[3] = center - up;
        _samples[4] = center + up;
        for (int i = 0; i < _samples.Length; i++)
            _starts[i] = camera.ProjectPosition(camera.UnprojectPosition(_samples[i]), camera.Near);
        foreach (var entry in _occluders)
        {
            var mesh = entry.Mesh;
            entry.BlocksLayer = false;
            entry.HitMask = GodotObject.IsInstanceValid(mesh) && mesh.IsVisibleInTree() && mesh.Mesh != null
                && (mesh.Layers & camera.CullMask) != 0 ? GetBlockedSamples(entry) : 0;
        }
        bool fullyBlocked = SelectOccludingLayers();
        float delay = Mathf.Max(0f, OcclusionDelay);
        float remainingDelay = Mathf.Max(0f, delay - _occlusionElapsed);
        _occlusionElapsed = fullyBlocked ? Mathf.Min(delay, _occlusionElapsed + dt) : 0f;
        bool sustained = fullyBlocked && _occlusionElapsed >= delay;
        // 跨过时长阈值的帧，只用等待结束后的剩余时间推进渐隐。
        float fadeDt = Mathf.Max(0f, dt - remainingDelay);
        foreach (var entry in _occluders)
        {
            var mesh = entry.Mesh;
            if (!GodotObject.IsInstanceValid(mesh)) continue;
            bool blocked = sustained && entry.BlocksLayer;
            entry.Hold = blocked ? Mathf.Max(0f, RestoreDelay) : Mathf.Max(0f, entry.Hold - dt);
            bool fade = blocked || entry.Hold > 0f;
            float seconds = fade ? FadeSeconds : RestoreSeconds;
            float transitionDt = blocked ? fadeDt : dt;
            entry.Fade = Mathf.MoveToward(entry.Fade, fade ? 1f : 0f, seconds <= 0f ? 1f : transitionDt / seconds);
        }
        // 同一栋房屋或同一棵树的所有子网格同步隐藏，避免露出内部或留下树冠。
        foreach (var solid in _cameraSolids.Values)
        {
            solid.OcclusionFade = 0f;
            foreach (var entry in solid.Meshes) solid.OcclusionFade = Mathf.Max(solid.OcclusionFade, entry.Fade);
        }
        foreach (var entry in _occluders)
            if (GodotObject.IsInstanceValid(entry.Mesh)) ApplyVisibility(entry);
    }

    private void ApplyVisibility(Occluder entry)
    {
        float fade = entry.Solid?.OcclusionFade ?? entry.Fade;
        float solidHide = Mathf.Max(entry.Solid?.Hide ?? 0f, fade * Mathf.Clamp(OccludedTransparency, 0f, 1f));
        if (entry.SupportsSolidHide)
        {
            if (solidHide > 0f)
            {
                entry.Mesh.SetInstanceShaderParameter(SolidHideParameter, solidHide);
                entry.SolidHideApplied = true;
            }
            else RestoreSolidHide(entry);
        }
        else
        {
            // 未接入共享隐藏 shader 的模型保留实例透明度兼容路径。
            entry.Mesh.Transparency = Mathf.Max(entry.OriginalTransparency, solidHide);
        }
    }

    private static void RestoreSolidHide(Occluder entry)
    {
        if (!entry.SolidHideApplied || !GodotObject.IsInstanceValid(entry.Mesh)) return;
        entry.Mesh.SetInstanceShaderParameter(SolidHideParameter, entry.OriginalSolidHide);
        entry.SolidHideApplied = false;
    }

    private void UpdateCameraInteriors(Camera3D camera, float dt)
    {
        Vector2 screen = camera.GetViewport().GetVisibleRect().Size;
        _cameraSamples[0] = camera.GlobalPosition;
        _cameraSamples[1] = camera.ProjectPosition(Vector2.Zero, camera.Near);
        _cameraSamples[2] = camera.ProjectPosition(new Vector2(screen.X, 0f), camera.Near);
        _cameraSamples[3] = camera.ProjectPosition(new Vector2(0f, screen.Y), camera.Near);
        _cameraSamples[4] = camera.ProjectPosition(screen, camera.Near);
        foreach (var building in _cameraSolids.Values)
        {
            bool inside = false;
            if (HideCameraInteriors)
                foreach (var point in _cameraSamples)
                {
                    if (!IsInsideSolid(building, point, camera.CullMask)) continue;
                    inside = true;
                    break;
                }
            building.Hold = inside ? Mathf.Max(0f, RestoreDelay) : Mathf.Max(0f, building.Hold - dt);
            // A smoothed approach would expose the interior for several frames after crossing a wall.
            building.Hide = inside || building.Hold > 0f ? 1f : Mathf.MoveToward(building.Hide, 0f,
                RestoreSeconds <= 0f ? 1f : dt / RestoreSeconds);
        }
    }

    private bool IsInsideSolid(CameraSolid building, Vector3 point, uint cullMask)
    {
        if (building.IsTree)
        {
            foreach (var entry in building.Meshes)
                if (GodotObject.IsInstanceValid(entry.Mesh) && entry.Mesh.IsVisibleInTree()
                    && (entry.Mesh.Layers & cullMask) != 0 && IsInsideTrunk(entry.Mesh, point)) return true;
            return false;
        }
        bool underRoof = false;
        foreach (var collider in building.Colliders)
        {
            if (!GodotObject.IsInstanceValid(collider) || collider.Disabled || collider.Shape is not BoxShape3D box) continue;
            Vector3 local = collider.GlobalTransform.AffineInverse() * point;
            Vector3 half = box.Size * 0.5f;
            // Use actual house footprints, not the larger AABB containing eaves and window boxes.
            if (Mathf.Abs(local.X) > half.X || Mathf.Abs(local.Z) > half.Z || local.Y < -half.Y) continue;
            if (local.Y <= half.Y) return true;
            underRoof = true;
        }
        if (!underRoof) return false;
        // The walk collider ends at the eaves. Confirm an actual roof above the camera,
        // so attics count as inside, while empty corners of a pitched roof's AABB do not.
        foreach (var entry in building.Meshes)
        {
            var mesh = entry.Mesh;
            if (!GodotObject.IsInstanceValid(mesh) || !mesh.IsVisibleInTree() || (mesh.Layers & cullMask) == 0) continue;
            Vector3 local = mesh.GlobalTransform.AffineInverse() * point;
            Aabb bounds = mesh.GetAabb();
            if (!bounds.HasPoint(local) || mesh.Mesh == null) continue;
            if (!_triangles.TryGetValue(mesh.Mesh, out var triangles))
                _triangles[mesh.Mesh] = triangles = mesh.Mesh.GenerateTriangleMesh();
            if (triangles == null) continue;
            using var hit = triangles.IntersectSegment(local,
                new Vector3(local.X, bounds.End.Y + 0.1f, local.Z));
            if (hit.Count > 0) return true;
        }
        return false;
    }

    private bool IsInsideTrunk(MeshInstance3D instance, Vector3 point)
    {
        Mesh mesh = instance.Mesh;
        if (mesh == null) return false;
        if (!_treeVolumes.TryGetValue(mesh, out var volume))
        {
            var faces = new List<Vector3>();
            for (int surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                Material material = instance.GetActiveMaterial(surface);
                // Leaves are alpha cards, not closed solids. Only opaque bark contributes to containment.
                bool opaque = material is ShaderMaterial shader
                    ? shader.Shader?.ResourcePath == "res://assets/shaders/town/town_toon.gdshader"
                    : material is StandardMaterial3D standard && standard.Transparency == BaseMaterial3D.TransparencyEnum.Disabled;
                if (!opaque) continue;
                using var arrays = mesh.SurfaceGetArrays(surface);
                Vector3[] vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
                int[] indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
                if (indices.Length == 0) faces.AddRange(vertices);
                else foreach (int index in indices) faces.Add(vertices[index]);
            }
            volume = new TreeVolume { Faces = faces.ToArray() };
            if (faces.Count > 0)
            {
                volume.Triangles = new TriangleMesh();
                volume.Triangles.CreateFromFaces(volume.Faces);
                volume.Bounds = new Aabb(faces[0], Vector3.Zero);
                foreach (Vector3 vertex in faces) volume.Bounds = volume.Bounds.Expand(vertex);
            }
            _treeVolumes.Add(mesh, volume);
        }
        if (volume.Triangles == null) return false;
        Vector3 local = instance.GlobalTransform.AffineInverse() * point;
        if (!volume.Bounds.HasPoint(local)) return false;
        Vector3 direction = new Vector3(0.619f, 0.347f, 0.704f).Normalized();
        Vector3 end = local + direction * (volume.Bounds.Size.Length() * 2f + 1f);
        int winding = 0;
        // Signed crossings handle overlapping trunk/branch shells; parity alone cancels their interiors.
        for (int crossing = 0; crossing < 64; crossing++)
        {
            using var hit = volume.Triangles.IntersectSegment(local, end);
            if (hit.Count == 0) return winding != 0;
            int face = hit["face_index"].AsInt32() * 3;
            Vector3 normal = (volume.Faces[face] - volume.Faces[face + 2])
                .Cross(volume.Faces[face] - volume.Faces[face + 1]);
            winding += normal.Dot(direction) > 0f ? 1 : -1;
            local = hit["position"].AsVector3() + direction * 0.0002f;
        }
        return winding != 0;
    }

    private bool SelectOccludingLayers()
    {
        bool blocked = false;
        while (true)
        {
            for (int i = 0; i < _samples.Length; i++)
            {
                _nearest[i] = null;
                float distance = float.PositiveInfinity;
                foreach (var entry in _occluders)
                {
                    if (entry.BlocksLayer || (entry.HitMask & (1 << i)) == 0
                        || entry.HitDistances[i] >= distance) continue;
                    _nearest[i] = entry;
                    distance = entry.HitDistances[i];
                }
                // 前层移除后只剩局部遮挡，就保留这些物件；不能连带隐藏旁边的房屋。
                if (_nearest[i] == null) return blocked;
            }
            // 五条射线最近的命中物件共同组成一层完整遮挡，再检查它们后面的剩余层。
            foreach (var entry in _nearest) entry.BlocksLayer = true;
            blocked = true;
        }
    }

    private int GetBlockedSamples(Occluder entry)
    {
        var mesh = entry.Mesh;
        Transform3D inverse = mesh.GlobalTransform.AffineInverse();
        Aabb bounds = mesh.GetAabb();
        int hitMask = 0;
        for (int i = 0; i < _samples.Length; i++)
        {
            // 从近裁面出发，同时支持透视与正交，且不把角色身后的物件算成遮挡。
            Vector3 from = inverse * _starts[i];
            Vector3 to = inverse * _samples[i];
            if (!bounds.IntersectsSegment(from, to)) continue;
            if (!_triangles.TryGetValue(mesh.Mesh, out var triangles))
                _triangles[mesh.Mesh] = triangles = mesh.Mesh.GenerateTriangleMesh();
            if (triangles == null) continue;
            // 屋顶和树冠的包围盒有大量空隙，必须再检查真实三角面。
            using var hit = triangles.IntersectSegment(from, to);
            if (hit.Count > 0)
            {
                hitMask |= 1 << i;
                // 各网格可能有不同缩放，必须在同一世界空间比较最近命中。
                entry.HitDistances[i] = _starts[i].DistanceSquaredTo(mesh.GlobalTransform * hit["position"].AsVector3());
            }
        }
        return hitMask;
    }

    private void Restore()
    {
        _occlusionElapsed = 0f;
        _timedTarget = null;
        _timedCamera = null;
        foreach (var entry in _occluders)
        {
            if (GodotObject.IsInstanceValid(entry.Mesh)) entry.Mesh.Transparency = entry.OriginalTransparency;
            RestoreSolidHide(entry);
            entry.Fade = entry.Hold = 0f;
        }
        foreach (var building in _cameraSolids.Values) building.Hide = building.Hold = building.OcclusionFade = 0f;
    }

    private void RefreshNearFadeTargets(Node root)
    {
        _nearFadeSeen.Clear();
        if (root != null)
            foreach (Node groupRoot in root.GetTree().GetNodesInGroup(NearFadeGroup))
            {
                if (groupRoot != root && !root.IsAncestorOf(groupRoot)) continue;
                if (groupRoot is MeshInstance3D mesh) TrackNearFade(mesh);
                foreach (Node child in groupRoot.FindChildren("*", "MeshInstance3D", true, false))
                    TrackNearFade((MeshInstance3D)child);
            }
        _nearFadeRemoved.Clear();
        foreach (var pair in _nearFadeOriginals)
            if (!_nearFadeSeen.Contains(pair.Key))
            {
                if (GodotObject.IsInstanceValid(pair.Key))
                    pair.Key.SetInstanceShaderParameter(NearFadeParameter, pair.Value);
                _nearFadeRemoved.Add(pair.Key);
            }
        foreach (var mesh in _nearFadeRemoved) _nearFadeOriginals.Remove(mesh);
    }

    private void TrackNearFade(MeshInstance3D mesh)
    {
        _nearFadeSeen.Add(mesh);
        if (_nearFadeOriginals.ContainsKey(mesh)) return;
        // Getter 返回的可能只是 shader 默认值；恢复时须区分“未覆盖”和显式实例值。
        _nearFadeOriginals.Add(mesh, GetStoredParameter(mesh, NearFadeParameter));
        _appliedNearFadeDistance = float.NaN;
    }

    private static Variant GetStoredParameter(MeshInstance3D mesh, StringName parameter)
    {
        foreach (var property in mesh.GetPropertyList())
            if (property["name"].AsString() == "instance_shader_parameters/" + parameter
                && (property["usage"].AsInt64() & (long)PropertyUsageFlags.Storage) != 0)
                return mesh.GetInstanceShaderParameter(parameter);
        return default;
    }

    private void ApplyNearFade(float distance)
    {
        if (_appliedNearFadeDistance == distance) return;
        foreach (var pair in _nearFadeOriginals)
            if (GodotObject.IsInstanceValid(pair.Key))
                pair.Key.SetInstanceShaderParameter(NearFadeParameter, distance < 0f ? pair.Value : Variant.From(distance));
        _appliedNearFadeDistance = distance;
    }

    public override void OnEditorUpdate(GameObject3D host)
    {
        RefreshNearFadeTargets(host.GetNodeOrNull<Node>(OccluderRootPath));
        ApplyNearFade(IsActive ? Mathf.Max(0f, NearFadeDistance) : -1f);
    }

    // 预览参数不写回网格实例；场景只保存组件上的统一配置。
    public override void OnEditorPreSave(GameObject3D host) => ApplyNearFade(-1f);

    public override void OnEditorExit(GameObject3D host) => ClearNearFade();

    private void ClearNearFade()
    {
        ApplyNearFade(-1f);
        _nearFadeOriginals.Clear();
        _nearFadeSeen.Clear();
        _nearFadeRemoved.Clear();
    }

    public override void OnDestroy()
    {
        Restore();
        ClearNearFade();
        _occluders.Clear();
        _triangles.Clear();
        _cameraSolids.Clear();
        foreach (var volume in _treeVolumes.Values) volume.Triangles?.Dispose();
        _treeVolumes.Clear();
        _controller = null;
    }
}
