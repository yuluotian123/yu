"""无损封装三段待机并只读核验；所有人物像素由 Aseprite 编辑。"""
import binascii
import hashlib
import json
import struct
import zlib
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
GROUP = 'amber_fox_style_v2/greatsword_stance_idle'
TEX = ROOT / 'assets/textures/sprites' / GROUP
META = ROOT / 'assets/resources/sprites' / GROUP
DOC = ROOT / 'docs/characters/media/protagonist'
POSES = ('high', 'mid', 'low')


def write_json(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def apng(path, frames):
    """封装原始 RGBA 帧，不绘图、不重采样、不合并相同帧。"""
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', binascii.crc32(kind + data) & 0xffffffff)
    width, height = frames[0].size
    output = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
    output += chunk(b'acTL', struct.pack('>II', len(frames), 0))
    sequence = 0
    for n, frame in enumerate(frames):
        output += chunk(b'fcTL', struct.pack('>IIIIIHHBB', sequence, width, height, 0, 0, 1, 24, 0, 0))
        sequence += 1
        data = frame.tobytes()
        compressed = zlib.compress(b''.join(b'\x00' + data[y*width*4:(y+1)*width*4] for y in range(height)), 9)
        if n == 0:
            output += chunk(b'IDAT', compressed)
        else:
            output += chunk(b'fdAT', struct.pack('>I', sequence) + compressed)
            sequence += 1
    path.write_bytes(output + chunk(b'IEND', b''))
    with Image.open(path) as check:
        assert check.n_frames == 72 and check.info['loop'] == 0
        for n, expected in enumerate(frames):
            check.seek(n)
            assert np.array_equal(np.array(check.convert('RGBA')), np.array(expected))


def package():
    cfg = json.loads((META / 'motion.json').read_text(encoding='utf-8'))
    sources = json.loads((META / 'sources.json').read_text(encoding='utf-8'))
    transforms = json.loads((META / 'transforms.json').read_text(encoding='utf-8'))
    clips, checks = [], {}
    lines = ['[gd_resource type="SpriteFrames" format=3]', '']
    animations = []
    for pose in POSES:
        path = TEX / pose
        frames = [Image.open(path / 'frames' / f'idle_{n:03d}.png').convert('RGBA') for n in range(72)]
        arrays = np.stack([np.array(frame) for frame in frames])
        assert arrays.shape == (72, 384, 320, 4)
        assert np.isin(arrays[:, :, :, 3], [0, 255]).all()
        atlas = np.array(Image.open(path / 'idle-atlas.png').convert('RGBA'))
        records = []
        for n, frame in enumerate(frames):
            x, y = n % 8 * 320, n // 8 * 384
            assert np.array_equal(atlas[y:y+384, x:x+320], arrays[n])
            bounds = frame.getbbox()
            assert bounds[0] > 0 and bounds[1] > 0 and bounds[2] < 320 and bounds[3] < 384
            records.append({'frame': n, 'source_frame': sources[pose]['source_indices'][n],
                            'upper_lift_px': transforms[pose][n]['upper_lift_px'],
                            'tail_extra_tip_dx_px': transforms[pose][n]['tail_extra_tip_dx_px'],
                            'isolated_tail_pixels_removed': transforms[pose][n]['isolated_tail_pixels_removed'],
                            'bounds': bounds, 'atlas_region': [x, y, 320, 384],
                            'sha256': hashlib.sha256((path / 'frames' / f'idle_{n:03d}.png').read_bytes()).hexdigest()})
        apng(path / 'idle.apng', frames)
        # 审核基准由 Aseprite 从 source.aseprite 导出，平时保存在忽略的临时目录。
        baseline_dir = ROOT / '.godot/protagonist-idle-cleanup' / pose / 'baseline'
        before = [baseline_dir / f'idle_{n:03d}.png' for n in range(72)]
        compared = 0
        if all(p.exists() for p in before):
            for n, file in enumerate(before):
                original = np.array(Image.open(file).convert('RGBA'))
                lift = records[n]['upper_lift_px']
                # 此区域覆盖脸、头发、肩、手和上中段剑；仅整体位移，无脸部重绘。
                assert np.array_equal(original[lift:245+lift], arrays[n, :245]), (pose, n, '上身')
                # 下段剑尖位于更低的行，单独核对其完整刀身，避免只检查肩部以上。
                if pose == 'low':
                    assert np.array_equal(original[210+lift:292+lift, 170:260], arrays[n, 210:292, 170:260]), (pose, n, '下段刀身')
                assert np.array_equal(original[330:], arrays[n, 330:]), (pose, n, '脚底')
                compared += 1
        duration = 0
        with Image.open(DOC / f'greatsword-idle-{pose}.gif') as gif:
            assert gif.n_frames == 72 and gif.info['loop'] == 0
            for n in range(72):
                gif.seek(n)
                duration += gif.info['duration']
                actual = np.array(gif.convert('RGBA'))
                expected_alpha = np.repeat(np.repeat(arrays[n, :, :, 3], 2, axis=0), 2, axis=1)
                assert np.array_equal(actual[:, :, 3], expected_alpha)
        assert duration == 3000
        adjacent = [int(np.any(arrays[n] != arrays[(n+1) % 72], axis=2).sum()) for n in range(72)]
        checks[pose] = {'frames': 72, 'binary_alpha': True, 'atlas_rgba_exact': True, 'apng_rgba_exact': True,
                        'inside_canvas': True, 'gif_duration_ms': duration, 'gif_alpha_exact': True,
                        'baseline_compared_frames': compared, 'upper_region_rigid_translation': compared == 72,
                        'isolated_tail_pixels_removed': sum(item['isolated_tail_pixels_removed'] for item in records),
                        'feet_unchanged_from_source': compared == 72, 'loop_seam_changed_pixels': adjacent[-1],
                        'largest_adjacent_change_pixels': max(adjacent), 'independent_full_silhouette_mask': False}
        write_json(META / f'{pose}-frames.json', records)
        name = 'idle_' + pose
        texture = (path / 'idle-atlas.png').relative_to(ROOT).as_posix()
        lines.append(f'[ext_resource type="Texture2D" path="res://{texture}" id="{name}"]')
        clips.append({'key': pose, 'animation': name, 'frames': 72, 'fps': 24, 'loop': True,
                      'duration_seconds': 3, 'prefix': 'idle', 'texture_directory': path.relative_to(ROOT).as_posix()})
    for clip in clips:
        name = clip['animation']
        for n in range(72):
            lines += ['', f'[sub_resource type="AtlasTexture" id="{name}_{n}"]', f'atlas = ExtResource("{name}")',
                      f'region = Rect2({n%8*320}, {n//8*384}, 320, 384)', 'filter_clip = true']
        entries = ',\n'.join('{"duration": 1.0, "texture": SubResource("%s_%d")}' % (name, n) for n in range(72))
        animations.append('{"frames": [\n' + entries + '], "loop": true, "name": &"' + name + '", "speed": 24.0}')
    lines += ['', '[resource]', 'animations = [' + ',\n'.join(animations) + ']', '']
    (META / 'sprite_frames.tres').write_text('\n'.join(lines), encoding='utf-8')
    write_json(META / 'clips.json', clips)
    write_json(META / 'media-validation.json', checks)
    write_json(META / 'manifest.json', {
        'date': '2026-11-08', 'status': '三段待机已接入武器架势切换',
        'attacks': '本次三段攻击被否定并删除，待重新设计',
        'method': '沿用选定的 ComfyUI 视频整理稿，在 Aseprite 中加大呼吸与尾摆',
        'canvas': cfg['canvas'], 'foot_anchor': cfg['foot_anchor'], 'fps': 24, 'frames_per_pose': 72,
        'duration_seconds': 3, 'loop': True, 'clips': clips, 'runtime_replaced': True,
        'previews': {'idle': 'greatsword-stance-idle-current.gif', 'static': 'greatsword-stances.png'},
        'sources': 'sources.json', 'motion': 'motion.json', 'verification': 'media-validation.json',
        'note': 'PNG/APNG 为像素基准；GIF 仅供观看。自动核验没有独立全轮廓标注。'})
    print(json.dumps({'frames': 216, 'clips': list(checks), 'checks': checks}, ensure_ascii=True))


if __name__ == '__main__':
    package()
