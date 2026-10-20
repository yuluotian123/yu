using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using Uniforms = Godot.Collections.Array<Godot.RDUniform>;

namespace GameLogic;

/// <summary>Render-thread DDGI resources and dispatch; the EC component owns capture and lifetime.</summary>
[GlobalClass]
public partial class DdgiEffect : CompositorEffect, ISerializationListener
{
    [Export] public bool FreezeUpdates { get; set; }
    private readonly object _gate = new();
    private readonly DdgiSceneCapture _capture = new();
    private readonly GDictionary _status = new() { ["state"] = "waiting", ["updates"] = 0, ["instances"] = 0, ["blas"] = 0 };
    private DdgiPacket _pending, _packet;
    private RenderingDevice _rd;
    private bool _initialized, _released;
    private readonly Dictionary<string, Rid> _shaders = new(), _pipelines = new(), _atlas = new();
    private readonly Dictionary<string, (Rid Rid, int Size)> _buffers = new();
    private sealed class GeometryCache
    {
        public Rid Blas, Vertices, Indices;
        public byte[] Triangles;
        public int Offset;
    }
    private readonly Dictionary<string, GeometryCache> _geometry = new();
    private Rid _sampler, _materialSampler, _tlas, _colorArray, _dataArray, _skyFallback, _skyTexture;
    private int _tlasCapacity, _nextProbe, _rotation, _generation, _materialUpdates;
    private uint _lightingEpoch, _visibilityEpoch;
    private ulong _lastFrame = ulong.MaxValue, _timestampFrame = ulong.MaxValue;
    private string _topology = "";
    private List<string> _colorKeys = new(), _dataKeys = new();
    private List<DdgiInstance> _lastInstances = new();
    private byte[] _lastMaterials = System.Array.Empty<byte>(), _lastLights = System.Array.Empty<byte>();
    private byte[] _lastEmitters = System.Array.Empty<byte>();
    private (Vector3 Sky, float Distance) _lighting;
    private (Rid Texture, long Revision, Basis Rotation, float Normalization) _skyKey;
    private byte[] _renderedLights = System.Array.Empty<byte>();
    private float _renderedSkyEnergy = float.NaN;
    private Vector3 _renderedConstantSky;
    private bool _hasRenderedLighting;
    private long? _pendingSkyRelight;

    public DdgiEffect()
    {
        ResourceName = "DDGI · Ray Query probe update";
        EffectCallbackType = EffectCallbackTypeEnum.PreOpaque;
    }

    public GDictionary GetStatus() { lock (_gate) return _status.Duplicate(true); }
    private void Status(string key, Variant value) => _status[key] = value;
    private void Fail(string message, bool unsupported = false)
    { Status("state", unsupported ? "unsupported" : "failed"); Status("error", message); GD.PushError("[DDGI] " + message); }

    public void CaptureScene(Node3D root, GDictionary configuration)
    {
        if (_released || !GodotObject.IsInstanceValid(root)) return;
        var settings = DdgiSettings.From(configuration);
        bool native = ClassDB.ClassHasMethod("RenderSceneBuffersRD", "set_ddgi");
        lock (_gate)
        {
            if (_status["state"].AsString() != "unsupported" &&
                (!native || (settings.EnvironmentSky && !ClassDB.ClassHasMethod("RenderSceneBuffersRD", "get_ddgi_sky"))))
                Fail("Required DDGI engine API is unavailable. Save and reopen with scripts/tools/engine/launch_godot.ps1. Current executable: " + OS.GetExecutablePath(), true);
            Status("native_receiver_available", native); Status("native_receiver", native);
            if (_status["state"].AsString() is "unsupported" or "failed") settings = settings with { Enabled = false };
        }
        long captureStart = Stopwatch.GetTimestamp();
        var packet = _capture.Capture(root, settings);
        double captureMs = Stopwatch.GetElapsedTime(captureStart).TotalMilliseconds;
        if (configuration.ContainsKey("sky_energy")) packet.SkyEnergy = configuration["sky_energy"].AsSingle();
        lock (_gate)
        {
            _pending = packet;
            Status("capture_cpu_ms", captureMs);
        }
        // Allocate before depth/opaque lists are prepared; only update existing caches in PreOpaque.
        RenderingServer.CallOnRenderThread(Callable.From(PrepareRender));
    }

    private bool Initialize()
    {
        if (_rd != null) return _initialized;
        Status("engine_executable", OS.GetExecutablePath());
        _rd = RenderingServer.GetRenderingDevice();
        if (_rd == null || !_rd.HasFeature(RenderingDevice.Features.RayQuery))
        { Fail("Forward+ Vulkan with Ray Query support is required.", true); return false; }
        foreach (string name in new[] { "trace", "classify", "integrate", "temporal", "debug_sample" })
        {
            using var file = ResourceLoader.Load<RDShaderFile>($"res://assets/shaders/rendering/ddgi/ddgi_{name}.glsl");
            using var spirv = file?.GetSpirV();
            string error = spirv?.GetStageCompileError(RenderingDevice.ShaderStage.Compute) ?? "Missing shader import";
            if (!string.IsNullOrEmpty(error)) { Fail(name + ": " + error); return false; }
            _shaders[name] = _rd.ShaderCreateFromSpirV(spirv);
            _pipelines[name] = _rd.ComputePipelineCreate(_shaders[name]);
            if (!_pipelines[name].IsValid) { Fail(name + " pipeline creation failed"); return false; }
        }
        using var sampler = new RDSamplerState
        {
            MinFilter = RenderingDevice.SamplerFilter.Linear, MagFilter = RenderingDevice.SamplerFilter.Linear,
            RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge, RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge
        };
        _sampler = _rd.SamplerCreate(sampler);
        sampler.RepeatU = sampler.RepeatV = RenderingDevice.SamplerRepeatMode.Repeat;
        _materialSampler = _rd.SamplerCreate(sampler);
        _skyFallback = NewTexture(1, 1, RenderingDevice.DataFormat.R16G16B16A16Sfloat);
        Buffer("temporal", new byte[32 + 32 * 16]);
        return _initialized = true;
    }

    private static RDUniform Uniform(int binding, RenderingDevice.UniformType type, params Rid[] ids)
    {
        var uniform = new RDUniform { Binding = binding, UniformType = type };
        foreach (Rid id in ids) uniform.AddId(id);
        return uniform;
    }
    private RDUniform Storage(int binding, string name) => Uniform(binding, RenderingDevice.UniformType.StorageBuffer, _buffers[name].Rid);
    private RDUniform Image(int binding, string name) => Uniform(binding, RenderingDevice.UniformType.Image, _atlas[name]);
    private static RDUniform Sample(int binding, Rid texture, Rid sampler) => Uniform(binding, RenderingDevice.UniformType.SamplerWithTexture, sampler, texture);

    private Rid Buffer(string name, byte[] bytes)
    {
        if (bytes.Length == 0) bytes = new byte[DdgiSceneCapture.MaterialStride];
        if (!_buffers.TryGetValue(name, out var buffer) || buffer.Size != bytes.Length)
        {
            if (buffer.Rid.IsValid) _rd.FreeRid(buffer.Rid);
            buffer = (_rd.StorageBufferCreate((uint)bytes.Length, bytes), bytes.Length);
            _buffers[name] = buffer;
        }
        else _rd.BufferUpdate(buffer.Rid, 0, (uint)bytes.Length, bytes);
        return buffer.Rid;
    }

    private Rid NewTexture(int width, int height, RenderingDevice.DataFormat format)
    {
        using var description = new RDTextureFormat
        {
            Width = (uint)width, Height = (uint)height, Format = format,
            UsageBits = RenderingDevice.TextureUsageBits.SamplingBit | RenderingDevice.TextureUsageBits.StorageBit |
                RenderingDevice.TextureUsageBits.CanCopyFromBit | RenderingDevice.TextureUsageBits.CanCopyToBit | RenderingDevice.TextureUsageBits.CanUpdateBit
        };
        using var view = new RDTextureView();
        Rid texture = _rd.TextureCreate(description, view);
        _rd.TextureClear(texture, new Color(0, 0, 0, 0), 0, 1, 0, 1);
        return texture;
    }

    private void SetupAtlas(DdgiSettings settings)
    {
        string key = $"{settings.Origin}:{settings.Counts}:{settings.Spacing}:{settings.Rays}";
        if (key == _topology) return;
        foreach (Rid texture in _atlas.Values) _rd.FreeRid(texture);
        Vector3I c = settings.Counts;
        _atlas["irradiance"] = NewTexture(c.X * c.Z * 10, c.Y * 10, RenderingDevice.DataFormat.R16G16B16A16Sfloat);
        _atlas["distance"] = NewTexture(c.X * c.Z * 18, c.Y * 18, RenderingDevice.DataFormat.R32G32Sfloat);
        _atlas["state"] = NewTexture(c.X * c.Z, c.Y, RenderingDevice.DataFormat.R32G32B32A32Sfloat);
        _atlas["epochs"] = NewTexture(c.X * c.Z, c.Y, RenderingDevice.DataFormat.R32G32Uint);
        _topology = key; _nextProbe = 0; _lightingEpoch++; _visibilityEpoch++; _generation++;
    }

    private bool UpdateGeometry(DdgiPacket packet, out bool changed)
    {
        var obsolete = _geometry.Keys.Where(k => !packet.Geometry.ContainsKey(k)).ToArray();
        changed = obsolete.Length > 0;
        if (changed && _tlas.IsValid) { _rd.FreeRid(_tlas); _tlas = default; _tlasCapacity = 0; }
        foreach (string key in obsolete)
        {
            var entry = _geometry[key]; _rd.FreeRid(entry.Blas); _rd.FreeRid(entry.Vertices); _rd.FreeRid(entry.Indices); _geometry.Remove(key);
        }
        foreach (var (key, source) in packet.Geometry)
        {
            if (_geometry.ContainsKey(key)) continue;
            var flags = RenderingDevice.BufferCreationBits.AccelerationStructureBuildInputReadOnlyBit;
            Rid vertices = _rd.VertexBufferCreate((uint)source.Positions.Length, source.Positions, flags);
            Rid indices = _rd.IndexBufferCreate((uint)source.IndexCount, RenderingDevice.IndexBufferFormat.Uint32, source.Indices, false, flags);
            using var description = new RDAccelerationStructureGeometry
            {
                VertexBuffer = vertices, VertexCount = (uint)source.VertexCount, VertexStride = 12,
                VertexFormat = RenderingDevice.DataFormat.R32G32B32Sfloat, IndexBuffer = indices, IndexCount = (uint)source.IndexCount
            };
            var geometries = new Godot.Collections.Array<RDAccelerationStructureGeometry> { description };
            using var geometriesOwner = (Godot.Collections.Array)geometries;
            Rid blas = _rd.BlasCreate(geometries, RenderingDevice.AccelerationStructureFlagBits.PreferFastTraceBit);
            if (!blas.IsValid || _rd.BlasBuild(blas) != Error.Ok)
            {
                if (blas.IsValid) _rd.FreeRid(blas); _rd.FreeRid(vertices); _rd.FreeRid(indices);
                Fail("BLAS creation/build failed"); return false;
            }
            _geometry[key] = new() { Blas = blas, Vertices = vertices, Indices = indices, Triangles = source.Triangles }; changed = true;
        }
        if (changed || !_buffers.ContainsKey("triangles"))
        {
            using var stream = new MemoryStream();
            foreach (var entry in _geometry.Values) { entry.Offset = (int)stream.Length / DdgiSceneCapture.TriangleStride; stream.Write(entry.Triangles); }
            Buffer("triangles", stream.ToArray());
        }
        return true;
    }

    private Rid MaterialArray(List<byte[]> layers, bool srgb)
    {
        using var format = new RDTextureFormat
        {
            Width = DdgiSceneCapture.TextureSize, Height = DdgiSceneCapture.TextureSize, ArrayLayers = (uint)layers.Count,
            TextureType = RenderingDevice.TextureType.Type2DArray,
            Format = srgb ? RenderingDevice.DataFormat.R8G8B8A8Srgb : RenderingDevice.DataFormat.R8G8B8A8Unorm,
            UsageBits = RenderingDevice.TextureUsageBits.SamplingBit
        };
        var data = new Godot.Collections.Array<byte[]>();
        using var dataOwner = (Godot.Collections.Array)data;
        foreach (byte[] layer in layers) data.Add(layer);
        using var view = new RDTextureView();
        return _rd.TextureCreate(format, view, data);
    }

    private static bool Discontinuous(byte[] previous, byte[] current)
    {
        if (previous.Length != current.Length) return true;
        var a = MemoryMarshal.Cast<byte, float>(previous);
        var b = MemoryMarshal.Cast<byte, float>(current);
        for (int i = 0; i < a.Length; i++)
            if (Mathf.Abs(a[i] - b[i]) > Mathf.Max(.02f, .25f * Mathf.Max(Mathf.Abs(a[i]), Mathf.Abs(b[i])))) return true;
        return false;
    }

    private static bool AlphaChanged(byte[] previous, byte[] current)
    {
        if (previous.Length != current.Length) return true;
        var a = MemoryMarshal.Cast<byte, float>(previous);
        var b = MemoryMarshal.Cast<byte, float>(current);
        int stride = DdgiSceneCapture.MaterialStride / sizeof(float);
        for (int i = 0; i < a.Length; i += stride)
        {
            if (a[i + 12] < 0 && b[i + 12] < 0) continue;
            if (a[i + 3] != b[i + 3]) return true;
            for (int j = 8; j < 16; j++) if (a[i + j] != b[i + j]) return true;
        }
        return false;
    }

    private bool UpdateScene(DdgiPacket packet)
    {
        if (!UpdateGeometry(packet, out bool geometryChanged)) return false;
        bool instancesChanged = geometryChanged || !_lastInstances.SequenceEqual(packet.Instances) || !_tlas.IsValid;
        bool materialsChanged = !_lastMaterials.AsSpan().SequenceEqual(packet.Materials), lightsChanged = !_lastLights.AsSpan().SequenceEqual(packet.Lights);
        bool colorsChanged = !_colorKeys.SequenceEqual(packet.TextureKeys), dataChanged = !_dataKeys.SequenceEqual(packet.DataKeys);
        bool visibilityChanged = instancesChanged || colorsChanged || AlphaChanged(_lastMaterials, packet.Materials) || _lighting.Distance != packet.Settings.MaxDistance;
        bool lightingJump = visibilityChanged || dataChanged || Discontinuous(_lastMaterials, packet.Materials) || Discontinuous(_lastLights, packet.Lights);
        if (instancesChanged)
        {
            int count = packet.Instances.Count;
            if (!_tlas.IsValid || count > _tlasCapacity)
            {
                if (_tlas.IsValid) _rd.FreeRid(_tlas);
                _tlasCapacity = Math.Max(count, 1);
                _tlas = _rd.TlasCreate((uint)_tlasCapacity, RenderingDevice.AccelerationStructureFlagBits.PreferFastTraceBit);
            }
            var instances = new Godot.Collections.Array<RDAccelerationStructureInstance>();
            using var instancesOwner = (Godot.Collections.Array)instances;
            var data = new int[count * 4];
            for (int i = 0; i < count; i++)
            {
                var source = packet.Instances[i];
                instances.Add(new RDAccelerationStructureInstance
                {
                    Blas = _geometry[source.Key].Blas, Transform = source.Transform, Id = (uint)i,
                    Flags = RenderingDevice.AccelerationStructureInstanceFlagBits.TriangleFacingCullDisableBit
                });
                data[i * 4] = _geometry[source.Key].Offset; data[i * 4 + 1] = source.Material;
            }
            Error result = _rd.TlasBuild(_tlas, instances);
            foreach (var instance in instances) instance.Dispose();
            if (result != Error.Ok) { Fail("TLAS build failed. Reopen the patched engine using scripts/tools/engine/launch_godot.ps1. Current executable: " + OS.GetExecutablePath()); return false; }
            Buffer("instances", DdgiBytes.Of(data)); _lastInstances = packet.Instances;
        }
        if (materialsChanged || !_buffers.ContainsKey("materials"))
        { Buffer("materials", packet.Materials); _lastMaterials = packet.Materials; Status("material_updates", ++_materialUpdates); }
        if (lightsChanged || !_buffers.ContainsKey("lights")) { Buffer("lights", packet.Lights); _lastLights = packet.Lights; }
        if (!_lastEmitters.AsSpan().SequenceEqual(packet.Emitters) || !_buffers.ContainsKey("emitters"))
        { Buffer("emitters", packet.Emitters); _lastEmitters = packet.Emitters; }
        if (colorsChanged || !_colorArray.IsValid)
        {
            if (_colorArray.IsValid) _rd.FreeRid(_colorArray);
            _colorArray = MaterialArray(packet.TextureData, true); _colorKeys = packet.TextureKeys;
        }
        if (dataChanged || !_dataArray.IsValid)
        {
            if (_dataArray.IsValid) _rd.FreeRid(_dataArray);
            _dataArray = MaterialArray(packet.DataTextures, false); _dataKeys = packet.DataKeys;
        }
        if (!_colorArray.IsValid || !_dataArray.IsValid) { Fail("GI material texture array creation failed"); return false; }
        var lighting = (packet.Settings.Sky, packet.Settings.MaxDistance);
        if (instancesChanged || materialsChanged || lightsChanged || colorsChanged || dataChanged || _lighting != lighting)
        { _generation++; _lighting = lighting; }
        if (visibilityChanged) _visibilityEpoch++;
        if (lightingJump) _lightingEpoch++;
        return true;
    }

    private void PrepareRender()
    {
        lock (_gate)
        {
            if (_released) return;
            if (_pending != null) { _packet = _pending; _pending = null; }
            if (_packet == null || !_packet.Settings.Enabled || !Initialize()) return;
            SetupAtlas(_packet.Settings); UpdateScene(_packet);
        }
    }

    private Rid GetUniformSet(string name, Uniforms uniforms)
    {
        using ((Godot.Collections.Array)uniforms)
        {
            Rid set = UniformSetCacheRD.GetCache(_shaders[name], 0, uniforms);
            foreach (var uniform in uniforms) uniform.Dispose();
            return set;
        }
    }

    private bool Dispatch(string name, Uniforms uniforms, byte[] constants, Vector3I groups)
        => DispatchBound(name, GetUniformSet(name, uniforms), constants, groups);

    private bool DispatchBound(string name, Rid set, byte[] constants, Vector3I groups)
    {
        if (!set.IsValid) { Fail(name + " uniform set failed"); return false; }
        long list = _rd.ComputeListBegin();
        _rd.ComputeListBindComputePipeline(list, _pipelines[name]); _rd.ComputeListBindUniformSet(list, set, 0);
        _rd.ComputeListSetPushConstant(list, constants, (uint)constants.Length);
        _rd.ComputeListDispatch(list, (uint)groups.X, (uint)groups.Y, (uint)groups.Z); _rd.ComputeListEnd();
        return true;
    }

    private byte[] Constants(DdgiSettings s, int count, int phase = 0)
    {
        using var stream = new MemoryStream(80); using var writer = new BinaryWriter(stream);
        DdgiBytes.Vector(writer, s.Origin, s.Spacing);
        writer.Write(s.Counts.X); writer.Write(s.Counts.Y); writer.Write(s.Counts.Z); writer.Write(s.Rays);
        DdgiBytes.Vector(writer, s.Sky, s.MaxDistance);
        writer.Write(_nextProbe); writer.Write(count); writer.Write(_rotation); writer.Write(_packet.Lights.Length / 32);
        writer.Write(s.History); writer.Write(_lightingEpoch); writer.Write(_visibilityEpoch); writer.Write((float)phase);
        return stream.ToArray();
    }

    private void Publish(RenderData data)
    {
        if (data.GetRenderSceneBuffers() is not RenderSceneBuffersRD buffers) return;
        var s = _packet.Settings;
        buffers.SetDdgi(_atlas["irradiance"], _atlas["distance"], _atlas["state"], s.Origin, s.Spacing, s.Counts, s.Energy, s.NormalBias, s.ViewBias);
    }

    private void PrepareSky(RenderData renderData)
    {
        using GDictionary data = _packet.Settings.EnvironmentSky && renderData.GetRenderSceneBuffers() is RenderSceneBuffersRD buffers ? buffers.GetDdgiSky() : new();
        bool active = data.Count > 0 && _rd.TextureIsValid(data["texture"].AsRid());
        _skyTexture = active ? data["texture"].AsRid() : _skyFallback;
        Basis rotation = active ? data["rotation"].AsBasis() : Basis.Identity;
        float normalization = active ? data["normalization"].AsSingle() : 1, border = active ? data["border"].AsSingle() : 0;
        long revision = active ? data["revision"].AsInt64() : 0;
        var key = (active ? _skyTexture : default, revision, rotation, normalization);
        if (key != _skyKey) { _generation++; _skyKey = key; }
        Buffer("sky", DdgiBytes.Of(new float[] { rotation.X.X, rotation.X.Y, rotation.X.Z, 0, rotation.Y.X, rotation.Y.Y, rotation.Y.Z, 0,
            rotation.Z.X, rotation.Z.Y, rotation.Z.Z, 0, active ? 1 : 0, normalization, border, 0 }));
        Status("sky_source", active ? "environment" : "constant"); Status("sky_revision", revision);
    }

    private Uniforms TemporalUniforms() => new()
    {
        Sample(0, _skyTexture, _sampler), Storage(1, "sky"), Storage(2, "temporal"), Image(3, "epochs"), Image(4, "state")
    };

    private Uniforms TraceUniforms(bool visibility = false) => new()
    {
        Uniform(0, RenderingDevice.UniformType.AccelerationStructure, _tlas), Storage(1, "triangles"), Storage(2, "instances"), Storage(3, "materials"),
        Sample(4, _colorArray, _materialSampler), Storage(5, visibility ? "visibility_rays" : "rays"), Image(6, "state"), Storage(7, "lights"),
        Sample(8, _skyTexture, _sampler), Storage(9, "sky"), Sample(10, _dataArray, _materialSampler), Image(11, "epochs"),
        Storage(12, "emitters"), Storage(13, "emissive_samples")
    };

    private bool NeedsLightingCatchUp(DdgiSettings settings)
    {
        bool changed = false;
        if (_hasRenderedLighting)
        {
            // More than 0.05 stops per rendered update overwhelms a budgeted history round.
            if (settings.EnvironmentSky && float.IsFinite(_packet.SkyEnergy) && float.IsFinite(_renderedSkyEnergy))
                changed = Mathf.Abs(Mathf.Log(Mathf.Max(_packet.SkyEnergy, 1e-6f) / Mathf.Max(_renderedSkyEnergy, 1e-6f)) / Mathf.Log(2)) > .05f;
            if (!settings.EnvironmentSky)
                changed |= (_renderedConstantSky - settings.Sky).Length() > .05f * Mathf.Max(1e-6f, Mathf.Max(_renderedConstantSky.Length(), settings.Sky.Length()));
            changed |= Discontinuous(_renderedLights, _packet.Lights);
        }
        if (settings.EnvironmentSky && changed) _pendingSkyRelight = _skyKey.Revision;
        else if (_pendingSkyRelight.HasValue && _pendingSkyRelight.Value != _skyKey.Revision)
        {
            // Incremental sky baking may deliver the final sky after the lighting change.
            changed = true; _pendingSkyRelight = null;
        }
        if (!settings.EnvironmentSky) _pendingSkyRelight = null;
        _renderedSkyEnergy = _packet.SkyEnergy; _renderedConstantSky = settings.Sky;
        _renderedLights = _packet.Lights; _hasRenderedLighting = true;
        return changed;
    }

    public override void _RenderCallback(int type, RenderData data)
    {
        lock (_gate)
        {
            if (type != (int)EffectCallbackTypeEnum.PreOpaque || _released || !_initialized || _packet == null || !_packet.Settings.Enabled || !_tlas.IsValid || _status["state"].AsString() == "failed") return;
            ulong frame = (ulong)Engine.GetFramesDrawn();
            if (_lastFrame == frame || FreezeUpdates) { Publish(data); return; }
            _lastFrame = frame; PrepareSky(data);
            var s = _packet.Settings;
            bool catchUp = NeedsLightingCatchUp(s);
            if (catchUp) s = s with { History = 0 };
            int count = Math.Min(catchUp ? Math.Max(s.Budget, 256) : s.Budget, s.Total), size = count * s.Rays * 32;
            if (!_buffers.TryGetValue("rays", out var rays) || rays.Size != size) Buffer("rays", new byte[size]);
            const int visibilityRayCount = 256;
            int visibilitySize = count * visibilityRayCount * 32;
            if (!_buffers.TryGetValue("visibility_rays", out var visibilityRays) || visibilityRays.Size != visibilitySize) Buffer("visibility_rays", new byte[visibilitySize]);
            int emissiveTriangles = BitConverter.ToInt32(_packet.Emitters, 0), emissiveSamples = emissiveTriangles * 4;
            int emissiveSize = Math.Max(16, count * emissiveSamples * 80);
            if (!_buffers.TryGetValue("emissive_samples", out var emissiveBuffer) || emissiveBuffer.Size != emissiveSize) Buffer("emissive_samples", new byte[emissiveSize]);
            Buffer("sampling_layout", DdgiBytes.Of(new[] { visibilityRayCount, emissiveSamples, 0, 0 }));
            byte[] constants = Constants(s, count);
            string tag = $"DDGI {GetInstanceId()} ";
            if (_timestampFrame != _rd.GetCapturedTimestampsFrame())
            {
                _timestampFrame = _rd.GetCapturedTimestampsFrame(); ulong begin = ulong.MaxValue;
                for (uint i = 0; i < _rd.GetCapturedTimestampsCount(); i++)
                {
                    string name = _rd.GetCapturedTimestampName(i);
                    if (name == tag + "begin") begin = _rd.GetCapturedTimestampGpuTime(i);
                    if (name == tag + "end" && begin != ulong.MaxValue) Status("gpu_update_ms", (_rd.GetCapturedTimestampGpuTime(i) - begin) / 1_000_000.0);
                }
            }
            // All batches share bindings. Avoid thousands of native Array/RDUniform wrappers during fast-forward.
            Rid temporalSet = GetUniformSet("temporal", TemporalUniforms());
            Rid traceSet = GetUniformSet("trace", TraceUniforms()), visibilitySet = GetUniformSet("trace", TraceUniforms(true));
            Rid classifySet = GetUniformSet("classify", new() { Storage(0, "rays"), Image(1, "state"), Image(2, "epochs") });
            Rid integrateSet = GetUniformSet("integrate", new() { Storage(0, "rays"), Image(1, "irradiance"), Image(2, "distance"), Image(3, "state"), Image(4, "epochs"), Storage(5, "temporal"), Storage(6, "visibility_rays"), Storage(7, "emissive_samples"), Storage(8, "sampling_layout") });
            _rd.CaptureTimestamp(tag + "begin");
            if (!DispatchBound("temporal", temporalSet, constants, Vector3I.One)) return;
            int updated = catchUp ? s.Total : count;
            // Reuse budget-sized scratch buffers; publish only after every stale probe is relit.
            for (int remaining = updated; remaining > 0;)
            {
                int batchCount = Math.Min(count, remaining);
                constants = Constants(s, batchCount);
                if (!DispatchBound("trace", traceSet, constants, new((s.Rays + 63) / 64, batchCount, 1))) return;
                if (!DispatchBound("classify", classifySet, constants, new((batchCount + 63) / 64, 1, 1))) return;
                if (!DispatchBound("trace", visibilitySet, Constants(s with { Rays = visibilityRayCount }, batchCount, 2), new(visibilityRayCount / 64, batchCount, 1))) return;
                if (emissiveSamples > 0 && !DispatchBound("trace", traceSet, Constants(s, batchCount, 3), new((emissiveSamples + 63) / 64, batchCount, 1))) return;
                if (!DispatchBound("integrate", integrateSet, constants, new(3, 3, batchCount))) return;
                // Commit after integration workgroups have read the old epochs.
                if (!DispatchBound("temporal", temporalSet, Constants(s, batchCount, 1), new((batchCount + 31) / 32, 1, 1))) return;
                _nextProbe = (_nextProbe + batchCount) % s.Total; remaining -= batchCount;
            }
            _rd.CaptureTimestamp(tag + "end");
            _rotation++; Publish(data);
            Status("state", "running"); Status("updates", _rotation); Status("generation", _generation); Status("instances", _packet.Instances.Count);
            Status("blas", _geometry.Count); Status("probes", s.Total); Status("rays_per_update", updated * s.Rays); Status("next_probe", _nextProbe); Status("ray_query", true);
            Status("lighting_epoch", _lightingEpoch); Status("visibility_epoch", _visibilityEpoch); Status("history_weight", s.History);
            Status("lighting_catch_up", catchUp); Status("updated_probes", updated);
            Status("emissive_triangles", emissiveTriangles); Status("emissive_shadow_ray_budget", updated * emissiveSamples);
            using var warnings = new GDictionary(); foreach (var (key, value) in _packet.Warnings) warnings[key] = value; Status("warnings", warnings);
            using var protocols = new GDictionary(); foreach (var (key, value) in _packet.MaterialCounts) protocols[key] = value; Status("material_counts", protocols);
        }
    }

    public void DebugReadback(Vector3[] positions, Vector3[] normals)
        => RenderingServer.CallOnRenderThread(Callable.From(() => Readback(positions, normals)));

    private void Readback(Vector3[] positions, Vector3[] normals)
    {
        lock (_gate)
        {
            if (_released || !_initialized || _packet == null || positions.Length == 0 || positions.Length != normals.Length)
            { Status("debug_error", "DDGI is not initialized or sample arrays are invalid"); return; }
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            for (int i = 0; i < positions.Length; i++) { DdgiBytes.Vector(writer, positions[i]); DdgiBytes.Vector(writer, normals[i]); }
            Buffer("debug_samples", stream.ToArray()); Buffer("debug_results", new byte[positions.Length * 16]);
            var s = _packet.Settings;
            using var constants = new MemoryStream(); using var output = new BinaryWriter(constants);
            DdgiBytes.Vector(output, s.Origin, s.Spacing); output.Write(s.Counts.X); output.Write(s.Counts.Y); output.Write(s.Counts.Z); output.Write(positions.Length);
            output.Write(s.NormalBias); output.Write(0f); output.Write(0f); output.Write(0f);
            if (!Dispatch("debug_sample", new() { Sample(0, _atlas["irradiance"], _sampler), Sample(1, _atlas["distance"], _sampler), Sample(2, _atlas["state"], _sampler),
                Storage(3, "debug_samples"), Storage(4, "debug_results") }, constants.ToArray(), new((positions.Length + 63) / 64, 1, 1))) return;
            float[] results = MemoryMarshal.Cast<byte, float>(_rd.BufferGetData(_buffers["debug_results"].Rid)).ToArray();
            float[] rays = MemoryMarshal.Cast<byte, float>(_rd.BufferGetData(_buffers["rays"].Rid)).ToArray();
            int hits = 0, backs = 0, valid = 0, relocated = 0;
            for (int i = 0; i < rays.Length; i += 8) { if (rays[i + 3] < s.MaxDistance - .01) hits++; if (rays[i + 7] > .5) backs++; }
            float[] states = MemoryMarshal.Cast<byte, float>(_rd.TextureGetData(_atlas["state"], 0)).ToArray();
            for (int i = 0; i < states.Length; i += 4) { if (states[i + 3] > .5) valid++; if (new Vector3(states[i], states[i + 1], states[i + 2]).Length() > .001) relocated++; }
            Status("debug_values", results); Status("debug_hits", hits); Status("debug_backfaces", backs); Status("debug_valid_probes", valid); Status("debug_relocated_probes", relocated);
            byte[] temporal = _rd.BufferGetData(_buffers["temporal"].Rid);
            Status("debug_history_epoch", BitConverter.ToUInt32(temporal, 0)); Status("debug_sky_change", BitConverter.ToSingle(temporal, 16));
            Status("debug_serial", _status.ContainsKey("debug_serial") ? _status["debug_serial"].AsInt32() + 1 : 1);
        }
    }

    public void ReleaseResources()
    {
        _capture.Clear();
        lock (_gate)
        {
            if (_released) return;
            _released = true;
            // A native-script callback survives managed domain unload. It owns no GI logic.
            using var script = ResourceLoader.Load<GDScript>("res://scripts/gamelogic/rendering/ddgi/ddgi_gpu_release.gd");
            using var value = script.New(); using var cleanup = value.As<RefCounted>();
            var owned = new Godot.Collections.Array<Rid>();
            using var ownedOwner = (Godot.Collections.Array)owned;
            if (_tlas.IsValid) owned.Add(_tlas);
            foreach (var geometry in _geometry.Values) { owned.Add(geometry.Blas); owned.Add(geometry.Vertices); owned.Add(geometry.Indices); }
            foreach (Rid rid in _pipelines.Values.Concat(_shaders.Values).Concat(_buffers.Values.Select(b => b.Rid)).Concat(_atlas.Values)
                .Concat(new[] { _colorArray, _dataArray, _skyFallback, _sampler, _materialSampler })) if (rid.IsValid) owned.Add(rid);
            cleanup.Set("device", _rd); cleanup.Set("owned", owned); cleanup.Call("schedule");
            _geometry.Clear(); _pipelines.Clear(); _shaders.Clear(); _buffers.Clear(); _atlas.Clear(); _pending = _packet = null;
            _rd = null; _tlas = _colorArray = _dataArray = _skyFallback = _sampler = _materialSampler = default;
            Status("state", "released"); Status("instances", 0); Status("blas", 0);
        }
    }

    public void OnBeforeSerialize() => ReleaseResources();
    public void OnAfterDeserialize() { }
    public override void _Notification(int what) { if (what == NotificationPredelete) ReleaseResources(); }
}
