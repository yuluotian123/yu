"""三段移动的无损封装、来源归档和只读像素核验；不绘制或修整人物。"""
import binascii
import argparse
import hashlib
import json
import os
import shutil
import struct
import zlib
from fractions import Fraction
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
GROUP = 'amber_fox_style_v2/greatsword_stance_movement'
TEX = ROOT / 'assets/textures/sprites' / GROUP
META = ROOT / 'assets/resources/sprites' / GROUP


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


def pack_apng(path, frames, fps, loop=True):
    def chunk(kind, data):
        return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', binascii.crc32(kind + data) & 0xffffffff)
    width, height = frames[0].size
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0))
    data += chunk(b'acTL', struct.pack('>II', len(frames), 0 if loop else 1))
    seq = 0
    delay = (Fraction(1) / Fraction(str(fps))).limit_denominator(65535)
    for n, image in enumerate(frames):
        data += chunk(b'fcTL', struct.pack('>IIIIIHHBB', seq, width, height, 0, 0, delay.numerator, delay.denominator, 0, 0))
        seq += 1
        rgba = image.tobytes()
        compressed = zlib.compress(b''.join(b'\x00' + rgba[y*width*4:(y+1)*width*4] for y in range(height)), 9)
        if n == 0:
            data += chunk(b'IDAT', compressed)
        else:
            data += chunk(b'fdAT', struct.pack('>I', seq) + compressed)
            seq += 1
    path.write_bytes(data + chunk(b'IEND', b''))
    with Image.open(path) as check:
        assert check.n_frames == len(frames) and check.info['loop'] == (0 if loop else 1)
        for n, expected in enumerate(frames):
            check.seek(n)
            assert np.array_equal(np.array(check.convert('RGBA')), np.array(expected))
            assert abs(check.info['duration'] - 1000 / fps) < 0.001


def copy_source(source, target):
    if source.resolve() != target.resolve():
        shutil.copy2(source, target)


def package(config_path):
    config = json.loads(config_path.read_text(encoding='utf-8'))
    manifest = json.loads((META / 'manifest.json').read_text(encoding='utf-8'))
    validation = {}
    for spec, clip in zip(config['clips'], manifest['clips']):
        stance = spec['stance']
        asset = spec.get('asset', stance)
        path = TEX / asset
        frames = [Image.open(path / 'frames' / f'move_{n:03d}.png').convert('RGBA') for n in range(len(spec['indices']))]
        rgba = np.stack([np.array(im) for im in frames])
        assert np.isin(rgba[:, :, :, 3], [0, 255]).all()
        atlas = np.array(Image.open(path / 'move-atlas.png').convert('RGBA'))
        repair = json.loads((ROOT / spec['guard_repair_config']).read_text(encoding='utf-8')) if spec.get('guard_repair_config') else None
        guard = np.array(Image.open(ROOT / repair['layer']).convert('RGBA')) if repair else None
        for n, source_index in enumerate(spec['indices']):
            source = np.array(Image.open(ROOT / spec['rgb_directory'] / f'{source_index:03d}.png').convert('RGB'))
            mask = rgba[n, :, :, 3] > 0
            if repair:
                # Independently reconstruct the documented regional edit, never modify production pixels here.
                rgb16 = source.astype(np.int16)
                foreground = ~((rgb16[:, :, 1] - rgb16[:, :, 0] > 18) & (rgb16[:, :, 1] - rgb16[:, :, 2] > 18))
                generated = np.zeros_like(rgba[n])
                generated[foreground, :3] = source[foreground]
                generated[foreground, 3] = 255
                expected = generated.copy()
                dx, dy = clip['records'][n]['guard_offset']
                assert repair['tracks'][source_index]['offset'] == [dx, dy]
                for top, bottom, left in repair['erase_rows']:
                    expected[max(0, top+dy):min(384, bottom+dy), max(0, left+dx):min(320, repair['erase_right']+dx)] = 0
                sy, sx = np.where(guard[:, :, 3] > 0)
                tx, ty = sx + dx, sy + dy
                assert ((tx >= 0) & (tx < 320) & (ty >= 0) & (ty < 384)).all()
                expected[ty, tx] = guard[sy, sx]
                edited = np.array(Image.open(path / 'sources/guard-edit-masks' / f'mask_{n:03d}.png').convert('RGBA'))[:, :, 3] > 0
                assert np.array_equal(rgba[n], expected), 'Guard repair differs from the documented layer and offsets'
                assert np.array_equal(rgba[n][~edited], generated[~edited]), 'Changed outside the guard repair area'
                assert np.array_equal(rgba[n, repair['preserve_below_y']:], generated[repair['preserve_below_y']:]), 'Guard repair changed the approved lower body'
            else:
                assert np.array_equal(source[mask], rgba[n, :, :, :3][mask])
            assert np.array_equal(atlas[n//6*384:(n//6+1)*384, n%6*320:(n%6+1)*320], rgba[n])
            clip['records'][n]['sha256'] = hashlib.sha256((path / 'frames' / f'move_{n:03d}.png').read_bytes()).hexdigest()
        pack_apng(path / 'movement.apng', frames, spec['fps'], spec.get('loop', True))
        video = ROOT / spec['video']
        video.parent.mkdir(parents=True, exist_ok=True)
        copy_source(ROOT / spec['source_file'], video)
        for kind in ['workflow.json', 'prompt.txt']:
            key = 'workflow_file' if kind == 'workflow.json' else 'prompt_file'
            source = ROOT / spec[key]
            copy_source(source, META / f'{asset}-{kind}')
        copy_source(ROOT / spec['input_file'], path / 'sources/input.png')
        differences = [int(np.any(rgba[n] != rgba[(n+1) % len(frames)], axis=2).sum()) for n in range(len(frames))]
        validation[asset] = {
            'frames': len(frames), 'fps': spec['fps'], 'duration': len(frames)/spec['fps'],
            'loop': spec.get('loop', True),
            'visible_rgb_exact_from_video_decode': not bool(repair), 'binary_alpha': True,
            'visible_rgb_and_alpha_exact_outside_documented_edits': True if repair else None,
            'guard_layer_and_tracking_exact': True if repair else None,
            'approved_lower_body_unchanged': True if repair else None,
            'preserved_from_y': repair['preserve_below_y'] if repair else None,
            'atlas_rgba_exact': True, 'apng_rgba_exact': True, 'apng_frame_timing_exact': True,
            'loop_seam_changed_pixels': differences[-1], 'largest_adjacent_change_pixels': max(differences),
            'independent_full_silhouette_mask': False,
            'source_decode': 'PyAV RGB -> Pillow nearest-neighbor 640x768 to 320x384',
            'selection': spec['selection'],
        }
        regions = {
            'face': [125,170,195,235], 'hair': [108,150,204,236], 'torso': [116,210,212,289],
            'legs': [94,265,230,360], 'tail': [70,247,185,345],
            'hands': {'high':[151,150,210,245], 'mid':[96,192,178,257], 'low':[101,201,198,266]}[stance],
            'weapon': {'high':[78,30,210,185], 'mid':[96,115,245,248], 'low':[103,200,285,334]}[stance],
        }
        regions = spec.get('audit_regions', regions)
        audit_path = META / f'audit/{asset}.json'
        relative = lambda file: Path(os.path.relpath(file, audit_path.parent)).as_posix()
        audit = {
            'video': relative(video), 'frames': relative(path / 'frames/move_*.png'),
            'source_indices': spec['indices'], 'frame_size':[320,384], 'output_fps':spec['fps'],
            'binary_alpha':True, 'preserve_inset':3, 'rgb_tolerance':0,
            'regions':[{'name': name, 'bounds': bounds} for name,bounds in regions.items()],
        }
        if repair:
            audit['intentional_edit'] = {'description': 'Arms, hands and sword use a documented shoulder-following repair layer. Original video RGB differences in this area remain in the audit report; the separate numerical validation verifies untouched pixels and the full repaired layer.',
                                        'config': relative(ROOT / spec['guard_repair_config']),
                                        'separate_validation': relative(META / 'media-validation.json')}
        write_json(audit_path, audit)
    write_json(META / 'manifest.json', manifest)
    write_json(META / 'media-validation.json', validation)
    # 后续封装只需归档资源，无须依赖可清理的 .godot 目录。
    for spec in config['clips']:
        stance = spec['stance']
        asset = spec.get('asset', stance)
        spec['source_file'] = spec['video']
        spec['rgb_directory'] = (TEX / asset / 'source-rgb').relative_to(ROOT).as_posix()
        spec['input_file'] = (TEX / asset / 'sources/input.png').relative_to(ROOT).as_posix()
        spec['workflow_file'] = (META / f'{asset}-workflow.json').relative_to(ROOT).as_posix()
        spec['prompt_file'] = (META / f'{asset}-prompt.txt').relative_to(ROOT).as_posix()
    write_json(META / 'production.json', config)
    print(json.dumps(validation, ensure_ascii=False))


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--config', type=Path, default=META / 'production.json')
    package(parser.parse_args().config)
