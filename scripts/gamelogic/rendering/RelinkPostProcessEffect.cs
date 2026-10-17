using System;
using Godot;
using Godot.Collections;

namespace GameLogic;

/// <summary>渲染资源，不是独立的游戏节点。LUT 在透明之前，电影曲线和泛光在透明之后。</summary>
[Tool, GlobalClass]
public partial class RelinkPostProcessEffect : CompositorEffect, ISerializationListener
{
    public enum PassKind { ColorGrade, FilmAndBloom }
    private PassKind _pass = PassKind.FilmAndBloom;
    [Export] public PassKind Pass
    {
        get => _pass;
        set
        {
            _pass = value;
            EffectCallbackType = value == PassKind.ColorGrade ? EffectCallbackTypeEnum.PreTransparent : EffectCallbackTypeEnum.PostTransparent;
            AccessResolvedDepth = value == PassKind.ColorGrade;
            ResourceName = value == PassKind.ColorGrade ? "Relink · HDR LUT (Before Transparency)" : "Relink · Film + Bloom (After Transparency)";
        }
    }
    [Export] public RelinkPostProcessProfile Profile { get; set; }
    private RenderingDevice _rd;
    private Rid _gradeShader, _gradePipeline, _bloomShader, _bloomPipeline, _filmShader, _filmPipeline, _sampler;
    private Rid _copyShader;
    private readonly System.Collections.Generic.Dictionary<long, Rid> _copyPipelines = new();
    private bool _failed;
    private long _renderCount;
    private string _renderStatus = "尚未执行";
    private StringName _context;
    private readonly System.Collections.Generic.Dictionary<ulong, WeakRef> _viewports = new();

    public bool HasShaderError => _failed;
    public long GetRenderCount() => System.Threading.Interlocked.Read(ref _renderCount);
    public string GetRenderStatus() => _renderStatus;

    /// <summary>运行时隔离当前合成器的可变参数，同时保留两个阶段对同一 Profile 的共享。</summary>
    public static void MakeProfilesLocal(Compositor compositor)
    {
        if (compositor == null) return;
        const string ownerKey = "yu_relink_profile_owner";
        using var owner = compositor.GetMeta(ownerKey, 0UL);
        ulong id = compositor.GetInstanceId();
        if (owner.AsUInt64() == id) return;
        // C# 导出资源在场景实例化后仍可能引用外部 Profile；仅靠 Local To Scene 不足以隔离。
        var copies = new System.Collections.Generic.Dictionary<ulong, RelinkPostProcessProfile>();
        var effects = compositor.CompositorEffects;
        using var effectsOwner = (Godot.Collections.Array)effects;
        foreach (CompositorEffect effect in effects)
        {
            if (effect is not RelinkPostProcessEffect relink || relink.Profile == null) continue;
            ulong profileId = relink.Profile.GetInstanceId();
            if (!copies.TryGetValue(profileId, out var copy))
            {
                copy = (RelinkPostProcessProfile)relink.Profile.Duplicate();
                copies.Add(profileId, copy);
            }
            relink.Profile = copy;
        }
        compositor.SetMeta(ownerKey, id);
    }

    public static bool HasActiveFilm(Compositor compositor)
    {
        if (compositor == null) return false;
        var effects = compositor.CompositorEffects;
        using var effectsOwner = (Godot.Collections.Array)effects;
        foreach (CompositorEffect effect in effects)
            if (effect is RelinkPostProcessEffect relink && relink.Enabled && !relink.HasShaderError
                && relink.Pass == PassKind.FilmAndBloom && relink.Profile?.Active == true) return true;
        return false;
    }

    public RelinkPostProcessEffect()
    {
        Pass = PassKind.FilmAndBloom;
        AccessResolvedColor = true;
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        System.Threading.Interlocked.Increment(ref _renderCount);
        if (effectCallbackType != (int)EffectCallbackType || Profile == null || !Profile.Active || _failed) return;
        if (renderData.GetRenderSceneBuffers() is not RenderSceneBuffersRD buffers) return;
        Vector2I size = buffers.GetInternalSize();
        if (size.X < 1 || size.Y < 1 || !Initialize()) return;
        // 两个阶段都登记，包括关闭 Bloom 或只启用 LUT 的情况。
        ulong id = buffers.GetInstanceId();
        if (!_viewports.ContainsKey(id)) _viewports[id] = GodotObject.WeakRef(buffers);
        for (uint view = 0; view < buffers.GetViewCount(); view++)
        {
            Rid color = buffers.GetColorLayer(view, false);
            using var format = _rd.TextureGetFormat(color);
            if ((format.UsageBits & RenderingDevice.TextureUsageBits.StorageBit) == 0)
            {
                // Reflection/probe viewports may not expose writable HDR color.
                _renderStatus = $"颜色缓冲不可写 ({format.Format} / {format.UsageBits})";
                return;
            }
            if (Pass == PassKind.ColorGrade)
            {
                if (Grade(color, buffers.GetDepthLayer(view, false), size, renderData.GetRenderSceneData().GetViewProjection(view))
                    && buffers.GetMsaa3D() != RenderingServer.ViewportMsaa.Disabled)
                    CopyToMsaa(color, buffers.GetColorLayer(view, true));
            }
            else
                FilmAndBloom(buffers, color, size, view);
        }
    }

    private bool Initialize()
    {
        _rd ??= RenderingServer.GetRenderingDevice();
        if (_rd == null) return false;
        if (_sampler.IsValid) return true;
        _context = new StringName($"relink_post_{GetInstanceId()}");
        using var sampler = new RDSamplerState
        {
            MinFilter = RenderingDevice.SamplerFilter.Linear,
            MagFilter = RenderingDevice.SamplerFilter.Linear,
            RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatW = RenderingDevice.SamplerRepeatMode.ClampToEdge
        };
        _sampler = _rd.SamplerCreate(sampler);
        if (Pass == PassKind.ColorGrade) CreatePipeline("grade", out _gradeShader, out _gradePipeline);
        else
        {
            CreatePipeline("bloom", out _bloomShader, out _bloomPipeline);
            CreatePipeline("film", out _filmShader, out _filmPipeline);
        }
        return !_failed;
    }

    private void CreatePipeline(string name, out Rid shader, out Rid pipeline)
    {
        using var file = ResourceLoader.Load<RDShaderFile>($"res://assets/shaders/rendering/relink_post/{name}.glsl");
        using var spirv = file?.GetSpirV();
        string error = spirv?.GetStageCompileError(RenderingDevice.ShaderStage.Compute);
        shader = default;
        pipeline = default;
        if (spirv == null || !string.IsNullOrEmpty(error))
        {
            GD.PushError($"[RelinkPost] {name}: {error ?? "missing compute shader"}");
            _failed = true;
            return;
        }
        shader = _rd.ShaderCreateFromSpirV(spirv);
        pipeline = _rd.ComputePipelineCreate(shader);
        if (!pipeline.IsValid) _failed = true;
    }

    private bool Grade(Rid color, Rid depth, Vector2I size, Projection projection)
    {
        if (Profile.NearLut == null || Profile.FarLut == null || Profile.LutStrength <= 0)
        {
            _renderStatus = Profile.LutStrength <= 0 ? "LUT 强度为 0" : "缺少 LUT";
            return false;
        }
        Rid near = RenderingServer.TextureGetRdTexture(Profile.NearLut.GetRid());
        Rid far = RenderingServer.TextureGetRdTexture(Profile.FarLut.GetRid());
        if (!near.IsValid || !far.IsValid || !depth.IsValid)
        {
            _renderStatus = "LUT 或深度缓冲无效";
            return false;
        }
        var uniforms = new Array<RDUniform> { ImageUniform(0, color), SampleUniform(1, depth), SampleUniform(2, near), SampleUniform(3, far) };
        Vector4 ranges = Profile.DepthRanges;
        Projection inverse = projection.Inverse();
        float[] data = { size.X, size.Y, Mathf.Max(.01f, Profile.Exposure), Mathf.Max(.01f, Profile.CapturedExposure),
            ranges.X, Mathf.Max(ranges.X + .001f, ranges.Y), ranges.Z, Mathf.Max(ranges.Z + .001f, ranges.W),
            Mathf.Clamp(Profile.LutStrength, 0, 1), Profile.GradeSky ? 1 : 0, 0, 0,
            inverse.X.X, inverse.X.Y, inverse.X.Z, inverse.X.W,
            inverse.Y.X, inverse.Y.Y, inverse.Y.Z, inverse.Y.W,
            inverse.Z.X, inverse.Z.Y, inverse.Z.Z, inverse.Z.W,
            inverse.W.X, inverse.W.Y, inverse.W.Z, inverse.W.W };
        Dispatch(_gradeShader, _gradePipeline, uniforms, size, data);
        return true;
    }

    private void CopyToMsaa(Rid source, Rid destination)
    {
        if (!_copyShader.IsValid)
        {
            using var file = ResourceLoader.Load<RDShaderFile>("res://assets/shaders/rendering/relink_post/copy_msaa.glsl");
            using var spirv = file?.GetSpirV();
            if (spirv == null) { _failed = true; return; }
            _copyShader = _rd.ShaderCreateFromSpirV(spirv);
        }
        var textures = new Array<Rid> { destination };
        using var texturesOwner = (Godot.Collections.Array)textures;
        using var pass = new RDFramebufferPass { ColorAttachments = new[] { 0 } };
        var passes = new Array<RDFramebufferPass> { pass };
        using var passesOwner = (Godot.Collections.Array)passes;
        Rid framebuffer = FramebufferCacheRD.GetCacheMultipass(textures, passes, 1);
        long format = _rd.FramebufferGetFormat(framebuffer);
        if (!_copyPipelines.TryGetValue(format, out Rid pipeline))
        {
            using var textureFormat = _rd.TextureGetFormat(destination);
            using var raster = new RDPipelineRasterizationState();
            using var samples = new RDPipelineMultisampleState { SampleCount = textureFormat.Samples };
            using var depth = new RDPipelineDepthStencilState();
            using var attachment = new RDPipelineColorBlendStateAttachment();
            var attachments = new Array<RDPipelineColorBlendStateAttachment> { attachment };
            using var attachmentsOwner = (Godot.Collections.Array)attachments;
            using var blend = new RDPipelineColorBlendState { Attachments = attachments };
            pipeline = _rd.RenderPipelineCreate(_copyShader, format, -1, RenderingDevice.RenderPrimitive.Triangles,
                raster, samples, depth, blend);
            if (!pipeline.IsValid) { _failed = true; return; }
            _copyPipelines[format] = pipeline;
        }
        var uniforms = new Array<RDUniform> { SampleUniform(0, source) };
        using var uniformsOwner = (Godot.Collections.Array)uniforms;
        Rid set = UniformSetCacheRD.GetCache(_copyShader, 0, uniforms);
        foreach (RDUniform uniform in uniforms) uniform.Dispose();
        long list = _rd.DrawListBegin(framebuffer);
        _rd.DrawListBindRenderPipeline(list, pipeline);
        _rd.DrawListBindUniformSet(list, set, 0);
        _rd.DrawListDraw(list, false, 1, 3);
        _rd.DrawListEnd();
        _renderStatus = "已执行（MSAA 写回）";
    }

    private Rid Buffer(RenderSceneBuffersRD buffers, string name, Vector2I size, uint view)
    {
        // Viewport owns these buffers and clears them on resize / destruction.
        buffers.CreateTexture(_context, name, RenderingDevice.DataFormat.R16G16B16A16Sfloat,
            (uint)(RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.StorageBit),
            RenderingDevice.TextureSamples.Samples1, size, buffers.GetViewCount(), 1, true, false);
        return buffers.GetTextureSlice(_context, name, view, 0, 1, 1);
    }

    private void FilmAndBloom(RenderSceneBuffersRD buffers, Rid color, Vector2I size, uint view)
    {
        Rid[] levels = new Rid[5];
        Rid source = color;
        Vector2I levelSize = size;
        bool bloomEnabled = Profile.BloomEnabled && Profile.BloomIntensity > 0;
        for (int level = 0; level < levels.Length; level++)
        {
            // The shader's glow multiplier is zero; binding the scene sampler avoids blur dispatches.
            if (!bloomEnabled) { levels[level] = color; continue; }
            levelSize = new Vector2I(Math.Max(1, (levelSize.X + 1) / 2), Math.Max(1, (levelSize.Y + 1) / 2));
            Rid down = Buffer(buffers, $"down{level}", levelSize, view);
            Rid ping = Buffer(buffers, $"ping{level}", levelSize, view);
            Rid blur = Buffer(buffers, $"blur{level}", levelSize, view);
            BloomPass(source, down, levelSize, level == 0 ? 0 : 1);
            BloomPass(down, ping, levelSize, 2);
            BloomPass(ping, blur, levelSize, 3);
            source = down;
            levels[level] = blur;
        }
        var uniforms = new Array<RDUniform> { ImageUniform(0, color) };
        for (int i = 0; i < levels.Length; i++) uniforms.Add(SampleUniform(i + 1, levels[i]));
        Dispatch(_filmShader, _filmPipeline, uniforms, size,
            new float[] { size.X, size.Y, Mathf.Max(.01f, Profile.Exposure), Profile.BloomEnabled ? Mathf.Clamp(Profile.BloomIntensity, 0, 1) : 0 });
    }

    private void BloomPass(Rid source, Rid destination, Vector2I size, int mode)
    {
        Dispatch(_bloomShader, _bloomPipeline, new Array<RDUniform> { SampleUniform(0, source), ImageUniform(1, destination) }, size,
            new float[] { size.X, size.Y, mode, Mathf.Max(.01f, Profile.Exposure), Mathf.Max(.1f, Profile.BloomThreshold), Mathf.Clamp(Profile.BloomFloor, 0, .2f), 0, 0 });
    }

    private static RDUniform ImageUniform(int binding, Rid texture)
    {
        var uniform = new RDUniform { UniformType = RenderingDevice.UniformType.Image, Binding = binding };
        uniform.AddId(texture);
        return uniform;
    }

    private RDUniform SampleUniform(int binding, Rid texture)
    {
        var uniform = new RDUniform { UniformType = RenderingDevice.UniformType.SamplerWithTexture, Binding = binding };
        uniform.AddId(_sampler);
        uniform.AddId(texture);
        return uniform;
    }

    private void Dispatch(Rid shader, Rid pipeline, Array<RDUniform> uniforms, Vector2I size, float[] constants)
    {
        using var ownedUniforms = (Godot.Collections.Array)uniforms;
        Rid set = UniformSetCacheRD.GetCache(shader, 0, uniforms);
        foreach (RDUniform uniform in uniforms) uniform.Dispose();
        if (!set.IsValid) return;
        byte[] bytes = new byte[constants.Length * sizeof(float)];
        System.Buffer.BlockCopy(constants, 0, bytes, 0, bytes.Length);
        long list = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(list, pipeline);
        _rd.ComputeListBindUniformSet(list, set, 0);
        _rd.ComputeListSetPushConstant(list, bytes, (uint)bytes.Length);
        _rd.ComputeListDispatch(list, (uint)((size.X + 7) / 8), (uint)((size.Y + 7) / 8), 1);
        _rd.ComputeListEnd();
        _renderStatus = "已执行";
        // RenderingDevice inserts write/read barriers between these lists.
    }

    public void ReleaseGpuResources()
    {
        if (_rd == null) return;
        if (!_sampler.IsValid && _viewports.Count == 0) return;
        var owned = new System.Collections.Generic.List<Rid>(_copyPipelines.Values);
        owned.AddRange(new[] { _gradePipeline, _bloomPipeline, _filmPipeline, _gradeShader, _bloomShader, _filmShader, _copyShader, _sampler });
        _copyPipelines.Clear();
        WeakRef[] viewports = new WeakRef[_viewports.Count];
        _viewports.Values.CopyTo(viewports, 0);
        _viewports.Clear();
        StringName context = _context;
        _gradePipeline = _bloomPipeline = _filmPipeline = _gradeShader = _bloomShader = _filmShader = _sampler = default;
        _copyShader = default;
        _failed = false;
        using var script = ResourceLoader.Load<GDScript>("res://scripts/gamelogic/rendering/relink_gpu_cleanup.gd");
        using var instance = script.New();
        using var cleanup = instance.As<RefCounted>();
        cleanup.Set("device", _rd);
        cleanup.Set("context", context);
        // 属性保留原生数组；C# 包装器在入队前释放，避免终结器在退出后持有 GPU 缓冲。
        using (var nativeBuffers = new Godot.Collections.Array())
        {
            foreach (WeakRef weak in viewports)
            {
                // Shutdown can dispose weak wrappers before their owning effect.
                if (!GodotObject.IsInstanceValid(weak)) continue;
                using var reference = weak.GetRef();
                if (reference.AsGodotObject() is RenderSceneBuffersRD buffers)
                {
                    nativeBuffers.Add(buffers);
                    buffers.Dispose();
                }
                weak.Dispose();
            }
            cleanup.Set("buffers", nativeBuffers);
        }
        using (var nativeOwned = new Godot.Collections.Array())
        {
            foreach (Rid rid in owned) nativeOwned.Add(rid);
            cleanup.Set("owned", nativeOwned);
        }
        // GDScript 绑定自身保持回调存活，避开 C# 热重载时失效的托管委托。
        // 执行后清空数组，后续终结器不再持有 GPU 资源。
        cleanup.Call("schedule");
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) ReleaseGpuResources();
    }

    // 在热重载保存托管字段之前清空 RIDs，避免恢复已释放的 shader / pipeline。
    public void OnBeforeSerialize()
    {
        ReleaseGpuResources();
    }
    public void OnAfterDeserialize()
    {
        // GPU 资源只属于旧托管实例；不得使用热重载恢复的 RID 数值。
        _gradePipeline = _bloomPipeline = _filmPipeline = _gradeShader = _bloomShader = _filmShader = _sampler = default;
        _copyShader = default;
        _copyPipelines.Clear();
        _viewports.Clear();
        _rd = null;
        _failed = false;
    }

    protected override void Dispose(bool disposing)
    {
        // C# 热重载会先释放托管实例，原生 Resource 此时仍被场景引用。
        if (disposing) ReleaseGpuResources();
        base.Dispose(disposing);
    }
}
