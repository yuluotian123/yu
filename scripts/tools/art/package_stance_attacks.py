"""归档攻击、核对源像素与待机端点，封装带逐帧时间的 APNG；不绘制角色。"""
import argparse
import binascii
import hashlib
import json
import os
import struct
import zlib
from pathlib import Path

import av
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
GROUP = 'amber_fox_style_v2/greatsword_stance_attack'
META = ROOT / 'assets/resources/sprites' / GROUP
TEX = ROOT / 'assets/textures/sprites' / GROUP


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def prepare(config):
    for spec in config['clips']:
        out = TEX / spec['stance'] / 'source-rgb'
        out.mkdir(parents=True, exist_ok=True)
        (out / '.gdignore').touch()
        wanted = set(spec['indices'])
        with av.open(ROOT / spec['video']) as video:
            for n, frame in enumerate(video.decode(video=0)):
                if n in wanted:
                    frame.to_image().convert('RGB').resize(tuple(config['canvas']), Image.Resampling.NEAREST).save(out / f'{n:03d}.png')
                    wanted.remove(n)
        assert not wanted, f'Missing source frames: {wanted}'


def assemble(config):
    """Join accepted source poses losslessly; never synthesize or repaint pixels."""
    for spec in config['clips']:
        if not spec.get('source_parts'):
            continue
        frames = []
        for part in spec['source_parts']:
            with av.open(ROOT / part['video']) as source:
                decoded = [frame.to_ndarray(format='rgb24') for frame in source.decode(video=0)]
            frames.extend(decoded[index] for index in part['indices'])
        target = ROOT / spec['video']
        with av.open(target, 'w') as output:
            stream = output.add_stream('ffv1', rate=24)
            stream.width, stream.height = frames[0].shape[1], frames[0].shape[0]
            stream.pix_fmt = 'bgr0'
            for pixels in frames:
                for packet in stream.encode(av.VideoFrame.from_ndarray(pixels, format='rgb24')):
                    output.mux(packet)
            for packet in stream.encode():
                output.mux(packet)
        with av.open(target) as check:
            decoded = list(check.decode(video=0))
            assert len(decoded) == len(frames)
            assert all(np.array_equal(frame.to_ndarray(format='rgb24'), expected) for frame, expected in zip(decoded, frames))
        spec['source_video_sha256'] = hashlib.sha256(target.read_bytes()).hexdigest()
        print(f'{spec["stance"]}: assembled {len(frames)} exact RGB frames')


def pack_apng(path, frames, times):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', binascii.crc32(kind + data) & 0xffffffff)
    w, h = frames[0].size
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0))
    data += chunk(b'acTL', struct.pack('>II', len(frames), 1))
    seq = 0
    for n, (im, ms) in enumerate(zip(frames, times)):
        data += chunk(b'fcTL', struct.pack('>IIIIIHHBB', seq, w, h, 0, 0, ms, 1000, 0, 0)); seq += 1
        rgba = im.tobytes()
        compressed = zlib.compress(b''.join(b'\x00' + rgba[y*w*4:(y+1)*w*4] for y in range(h)), 9)
        data += chunk(b'IDAT' if n == 0 else b'fdAT', compressed if n == 0 else struct.pack('>I', seq) + compressed)
        if n: seq += 1
    path.write_bytes(data + chunk(b'IEND', b''))
    with Image.open(path) as check:
        assert check.n_frames == len(frames) and check.info['loop'] == 1
        for n, expected in enumerate(frames):
            check.seek(n)
            assert np.array_equal(np.array(check.convert('RGBA')), np.array(expected))
            assert abs(check.info['duration'] - times[n]) < 0.001


def package(config):
    manifest = json.loads((META / 'manifest.json').read_text(encoding='utf-8'))
    validation = {}
    w, h = config['canvas']; ox, oy = config['idle_offset']
    for spec, clip in zip(config['clips'], manifest['clips']):
        stance = spec['stance']; path = TEX / stance
        frames = [Image.open(path / 'frames' / f'attack_{n:03d}.png').convert('RGBA') for n in range(len(spec['indices']))]
        atlas = np.array(Image.open(path / 'attack-atlas.png').convert('RGBA'))
        idle = np.array(Image.open(ROOT / spec['idle_frame']).convert('RGBA'))
        endpoint = np.zeros((h, w, 4), dtype=np.uint8)
        endpoint[oy:oy+idle.shape[0], ox:ox+idle.shape[1]] = idle
        times = [record['duration_ms'] for record in clip['records']]
        assert sum(times) == spec['duration_ms'] and clip['duration'] == sum(times)/1000
        for n, (im, record) in enumerate(zip(frames, clip['records'])):
            rgba = np.array(im); foreground = rgba[:, :, 3] > 0
            assert np.isin(rgba[:, :, 3], [0, 255]).all()
            if record['source_kind'] == 'idle_endpoint':
                assert np.array_equal(rgba[:, :, 3], endpoint[:, :, 3])
                assert np.array_equal(rgba[foreground], endpoint[foreground])
            else:
                rgb = np.array(Image.open(path / 'source-rgb' / f"{record['video_frame']:03d}.png").convert('RGB'))
                rgb16 = rgb.astype(np.int16)
                expected_alpha = ~((rgb16[:, :, 1]-rgb16[:, :, 0] > config['chroma_dominance']) & (rgb16[:, :, 1]-rgb16[:, :, 2] > config['chroma_dominance']))
                assert np.array_equal(foreground, expected_alpha)
                assert np.array_equal(rgba[foreground, :3], rgb[foreground])
            assert np.array_equal(atlas[n//6*h:(n//6+1)*h, n%6*w:(n%6+1)*w], rgba)
            record['sha256'] = hashlib.sha256((path / 'frames' / f'attack_{n:03d}.png').read_bytes()).hexdigest()
        pack_apng(path / 'attack.apng', frames, times)
        # 原视频负责中间动作；首尾另与原始 idle 的 alpha 和可见颜色逐像素核验。
        audit_path = META / 'audit' / f'{stance}.json'
        relative = lambda p: Path(os.path.relpath(p, audit_path.parent)).as_posix()
        selected = [r for r in clip['records'] if r['source_kind'] == 'video']
        regions = {
            'face': [205, 150, 365, 270], 'hair': [180, 130, 370, 270],
            'torso': [180, 190, 370, 315], 'hands_and_weapon': [45, 10, 490, 345],
            'legs': [170, 265, 365, 365], 'tail_and_pouch': [135, 225, 370, 355],
        }
        write_json(audit_path, {
            'video': relative(ROOT / spec['video']),
            'frames': [relative(path / 'frames' / f"attack_{r['frame']:03d}.png") for r in selected],
            'source_indices': [r['video_frame'] for r in selected], 'frame_size': config['canvas'],
            'binary_alpha': True, 'preserve_inset': 3, 'rgb_tolerance': 0,
            'timing_note': 'Variable frame durations; full timing is in manifest.json. Endpoints come from the original idle and are checked separately in media-validation.json.',
            'regions': [{'name': name, 'bounds': bounds} for name, bounds in regions.items()],
        })
        validation[stance] = {'frames': len(frames), 'duration_seconds': sum(times)/1000,
            'all_video_frames_retained_in_order': spec['indices'] == list(range(len(frames))),
            'visible_video_rgb_exact': True, 'binary_alpha': True,
            'idle_endpoints_alpha_and_visible_rgb_exact': True, 'atlas_rgba_exact': True,
            'apng_rgba_and_frame_times_exact': True, 'independent_full_silhouette_mask': False}
        if spec.get('blade_keys'):
            keys = spec['blade_keys']
            assert all(key[0] in spec['indices'] for key in keys)
            assert all(a[0] < b[0] for a, b in zip(keys, keys[1:]))
            # The hilt may remain near the rear hip during a thrust; only the luminous outer edge must face forward.
            assert all(key[1] * .22 + key[3] * .78 >= config['foot_anchor'][0] and key[3] >= config['foot_anchor'][0] for key in keys), 'Light must stay in front'
            frame_keys = ', '.join(str(clip['records'][spec['indices'].index(key[0])]['runtime_first_frame']) for key in keys)
            poses = ', '.join(str(value) for key in keys for value in key[1:])
            trail = ('[gd_resource type="Resource" script_class="WeaponAttackTrail" format=3]\n\n'
                     '[ext_resource type="Script" path="res://scripts/gamelogic/weapons/WeaponAttackTrail.cs" id="script"]\n\n'
                     '[resource]\nscript = ExtResource("script")\n'
                     f'AbilityId = "{spec["ability_id"]}"\nAnimation = &"attack_{stance}"\n'
                     f'Frames = PackedFloat32Array({frame_keys})\nBladePoses = PackedVector4Array({poses})\n')
            (ROOT / f'assets/resources/weapons/greatsword_attack_{stance}_trail.tres').write_text(trail, encoding='utf-8')
    write_json(META / 'manifest.json', manifest)
    write_json(META / 'media-validation.json', validation)
    print(json.dumps(validation, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=Path, default=META / 'production.json')
    parser.add_argument('--prepare', action='store_true')
    parser.add_argument('--assemble', action='store_true')
    args = parser.parse_args()
    config = json.loads(args.config.read_text(encoding='utf-8'))
    if args.assemble:
        assemble(config)
        write_json(args.config, config)
    elif args.prepare:
        prepare(config)
    else:
        package(config)
