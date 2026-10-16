"""Run with RenderDoc 1.46 qrenderdoc.exe --python this_file.py.

RELINK_RDC overrides the source capture. Outputs stay in this yu checkout.
Replays the complete standard_d3d11_frame10803 capture, not the incomplete old frame.
"""
import hashlib
import json
import os
import struct
import sys
import traceback
from pathlib import Path

import renderdoc as rd

root = Path(os.environ.get("YU_PROJECT", "D:/Project/GitHub/yu"))
output = root / ".godot" / "relink-analysis"
output.mkdir(parents=True, exist_ok=True)
capture_path = Path(os.environ.get("RELINK_RDC", "D:/Project/GitHub/MyMissionSystem/Captures/Relink/standard_d3d11_frame10803.rdc"))
capture = controller = None
status = {"capture": str(capture_path), "state": "opening"}

def variable_values(variable):
    if variable.members:
        return [variable_values(v) for v in variable.members]
    return list(variable.value.f32v)[:variable.columns]

try:
    capture = rd.OpenCaptureFile()
    result = capture.OpenFile(str(capture_path), "", None)
    if result.code != rd.ResultCode.Succeeded:
        raise RuntimeError(str(result))
    result, controller = capture.OpenCapture(rd.ReplayOptions(), None)
    if result.code != rd.ResultCode.Succeeded:
        raise RuntimeError(str(result))
    textures = {str(t.resourceId): t for t in controller.GetTextures()}
    controller.SetFrameEvent(24905, True)
    pipe = controller.GetPipelineState()
    reflection = pipe.GetShaderReflection(rd.ShaderStage.Pixel)
    manifest = {"capture": capture_path.name, "renderdoc_version": "1.46", "lut_event": 24905,
                "lut_generation_events": [24874, 24881], "film_event": 31660, "luts": {}}
    for used in pipe.GetReadOnlyResources(rd.ShaderStage.Pixel, True):
        name = reflection.readOnlyResources[used.access.index].name
        if name not in ("g_NearLUT", "g_FarLUT", "g_TextureAdaptLumminance"):
            continue
        resource = used.descriptor.resource
        texture = textures[str(resource)]
        raw = bytes(controller.GetTextureData(resource, rd.Subresource()))
        if name == "g_TextureAdaptLumminance":
            manifest["adapted_exposure"] = struct.unpack_from("<f", raw)[0]
            continue
        if (texture.width, texture.height, texture.depth) != (32, 32, 32) or len(raw) != 32**3 * 8:
            raise RuntimeError("Unexpected LUT layout: " + name)
        (output / (name + ".rgba16f")).write_bytes(raw)
        values = list(struct.iter_unpack("<4e", raw))
        manifest["luts"][name] = {"resource": str(resource), "format": texture.format.Name(),
            "size": [32, 32, 32], "bytes": len(raw), "sha256": hashlib.sha256(raw).hexdigest(),
            "minimum_rgb": [min(v[i] for v in values) for i in range(3)],
            "maximum_rgb": [max(v[i] for v in values) for i in range(3)]}
    constants = {}
    for index, block in enumerate(reflection.constantBlocks):
        bound = pipe.GetConstantBlock(rd.ShaderStage.Pixel, index, 0).descriptor
        variables = controller.GetCBufferVariableContents(pipe.GetGraphicsPipelineObject(),
            pipe.GetShader(rd.ShaderStage.Pixel), rd.ShaderStage.Pixel,
            pipe.GetShaderEntryPoint(rd.ShaderStage.Pixel), index, bound.resource, bound.byteOffset, bound.byteSize)
        for v in variables:
            if v.name.startswith(("g_ColorCorrect", "g_CharaColorCorrect")):
                constants[v.name] = list(v.value.f32v)[:v.columns]
    manifest["constants"] = constants
    for event, stage in ((24874, rd.ShaderStage.Compute), (24881, rd.ShaderStage.Compute),
                         (24905, rd.ShaderStage.Pixel), (31660, rd.ShaderStage.Pixel),
                         (31079, rd.ShaderStage.Pixel), (31101, rd.ShaderStage.Pixel),
                         (31204, rd.ShaderStage.Pixel), (31342, rd.ShaderStage.Pixel), (31384, rd.ShaderStage.Pixel)):
        controller.SetFrameEvent(event, True)
        pipe = controller.GetPipelineState()
        asm = controller.DisassembleShader(pipe.GetGraphicsPipelineObject(), pipe.GetShaderReflection(stage), "")
        (output / ("event_%d.asm" % event)).write_text(asm, encoding="utf-8")
        if event >= 31079 and event != 31660:
            detail = {"event": event, "constants": {}}
            refl = pipe.GetShaderReflection(stage)
            for i, block in enumerate(refl.constantBlocks):
                bound = pipe.GetConstantBlock(stage, i, 0).descriptor
                variables = controller.GetCBufferVariableContents(pipe.GetGraphicsPipelineObject(),
                    pipe.GetShader(stage), stage, pipe.GetShaderEntryPoint(stage), i,
                    bound.resource, bound.byteOffset, bound.byteSize)
                detail["constants"][block.name] = {v.name: variable_values(v) for v in variables}
            (output / ("event_%d.json" % event)).write_text(json.dumps(detail, indent=2), encoding="utf-8")
    (output / "manifest.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    status["state"] = "complete"
except Exception:
    status.update(state="failed", error=traceback.format_exc())
finally:
    (output / "status.json").write_text(json.dumps(status, indent=2), encoding="utf-8")
    if controller is not None:
        controller.Shutdown()
    if capture is not None:
        capture.Shutdown()
sys.exit(0 if status["state"] == "complete" else 1)
