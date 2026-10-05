"""Slice generated sprites by alpha islands, align them, and build Godot SpriteFrames.

Requires Pillow. No model calls; source art is preserved. Run with --project PATH.
"""
import argparse
import json
from collections import deque
from pathlib import Path
from PIL import Image, ImageChops, ImageFilter

SIZE = 192
ORIGIN = (96, 168)
# One scale per sheet preserves crouches and airborne poses. Anchors are the
# character's horizontal body center and ground reference, not the weapon bbox.
SHEETS = {
    "idle": (0.160, [(320,614), (920,614), (320,1220), (920,1220)]),
    "run": (0.155, [(447,615), (1045,616), (474,1188), (1084,1201)]),
    "jump": (0.150, [(335,662), (894,660), (346,1238), (935,1242)]),
    "attack": (0.170, [(337,662), (958,661), (377,1224), (961,1228)]),
    "hurt": (0.145, [(302,650), (922,649), (336,1243), (978,1245)]),
    "death": (0.160, [(286,615), (936,629), (335,1090), (1006,1101)]),
}
# Existing graph/ability names remain unchanged. Attack duration matches the
# 0.34s ability; dash reuses the forward running poses at the existing 1.25 speed.
ANIMATIONS = {
    "idle": (5, True, [("idle", i, 1) for i in range(4)]),
    "run": (10, True, [("run", i, 1) for i in range(4)]),
    "jump": (12, False, [("jump", i, 1) for i in range(4)]),
    "jumpup": (12, False, [("jump", 1, 1)]),
    "inair": (8, True, [("jump", 2, 1)]),
    "isfalling": (8, True, [("jump", 1, 1)]),
    "land": (20, False, [("jump", 3, 1), ("idle", 0, 1)]),
    "attack": (100, False, [("attack", 0, 8), ("attack", 1, 7), ("attack", 2, 9), ("attack", 3, 10)]),
    "dash": (16, False, [("run", 1, 1), ("run", 3, 1), ("run", 1, 1), ("run", 3, 1)]),
    "hurt": (10, False, [("hurt", i, 1) for i in range(4)]),
    "death": (6, False, [("death", i, 1) for i in range(4)]),
}

def find_sprites(image):
    width, height = image.size
    pixels = bytearray(image.getchannel("A").tobytes())
    regions = []
    for start in range(len(pixels)):
        if pixels[start] < 32:
            continue
        pending = deque([start])
        pixels[start] = 0
        indices = []
        x0 = x1 = start % width
        y0 = y1 = start // width
        while pending:
            pos = pending.popleft()
            indices.append(pos)
            x, y = pos % width, pos // width
            x0, x1 = min(x0, x), max(x1, x)
            y0, y1 = min(y0, y), max(y1, y)
            for nx, ny in ((x-1,y), (x+1,y), (x,y-1), (x,y+1),
                           (x-1,y-1), (x+1,y-1), (x-1,y+1), (x+1,y+1)):
                if 0 <= nx < width and 0 <= ny < height:
                    n = ny * width + nx
                    if pixels[n] >= 32:
                        pixels[n] = 0
                        pending.append(n)
        if len(indices) > 1000:
            regions.append(((x0, y0, x1+1, y1+1), indices))
    assert len(regions) == 4, f"Expected 4 separate sprites, got {len(regions)}"
    regions.sort(key=lambda r: r[0][1])
    return sorted(regions[:2], key=lambda r: r[0][0]) + sorted(regions[2:], key=lambda r: r[0][0])

def build(project):
    folder = project / "assets/sprites/greatsword"
    folder.mkdir(parents=True, exist_ok=True)
    atlas = Image.new("RGBA", (SIZE * 4, SIZE * len(SHEETS)))
    metadata = {"frame_size": SIZE, "origin": ORIGIN, "sprite_position": [0, -36], "frames": {}}
    resource = ['[gd_resource type="SpriteFrames" format=3]', '',
                '[ext_resource type="Texture2D" path="res://assets/sprites/greatsword/atlas.png" id="1_atlas"]', '']
    for row, (name, (scale, anchors)) in enumerate(SHEETS.items()):
        source = Image.open(folder / "sources" / f"{name}.png").convert("RGBA")
        for i, ((box, indices), anchor) in enumerate(zip(find_sprites(source), anchors)):
            # Isolate the connected sprite before cropping: rectangular crops
            # alone would include neighboring swords that cross the grid.
            data = bytearray(source.width * source.height)
            for pixel in indices:
                data[pixel] = 255
            mask = Image.frombytes("L", source.size, bytes(data)).filter(ImageFilter.MaxFilter(5))
            isolated = source.copy()
            isolated.putalpha(ImageChops.darker(source.getchannel("A"), mask))
            crop_box = (max(0, box[0]-2), max(0, box[1]-2), min(source.width,box[2]+2), min(source.height,box[3]+2))
            crop = isolated.crop(crop_box)
            scaled = crop.resize((round(crop.width*scale), round(crop.height*scale)), Image.Resampling.NEAREST)
            x = ORIGIN[0] - round((anchor[0]-crop_box[0])*scale)
            y = ORIGIN[1] - round((anchor[1]-crop_box[1])*scale)
            assert x >= 0 and y >= 0 and x+scaled.width <= SIZE and y+scaled.height <= SIZE, (name,i,x,y,scaled.size)
            frame = Image.new("RGBA", (SIZE, SIZE))
            frame.alpha_composite(scaled, (x,y))
            bbox = frame.getbbox()
            assert bbox and min(bbox[:2]) > 0 and max(bbox[2:]) < SIZE, (name,i,bbox)
            atlas.alpha_composite(frame, (SIZE*i, SIZE*row))
            frame_id = f"{name}_{i}"
            metadata["frames"][frame_id] = {"source_box": box, "source_anchor": anchor, "scale": scale, "bounds": bbox}
            resource += [f'[sub_resource type="AtlasTexture" id="{frame_id}"]',
                         'atlas = ExtResource("1_atlas")',
                         f'region = Rect2({SIZE*i}, {SIZE*row}, {SIZE}, {SIZE})', 'filter_clip = true', '']
    atlas.save(folder / "atlas.png")
    resource += ['[resource]', 'animations = [']
    for index, (name, (fps, loop, frames)) in enumerate(ANIMATIONS.items()):
        resource += ['{', '"frames": [']
        resource += [f'{{"duration": {duration:.1f}, "texture": SubResource("{sheet}_{frame}")}}' + (',' if i < len(frames)-1 else '')
                     for i, (sheet,frame,duration) in enumerate(frames)]
        resource += ['],', f'"loop": {str(loop).lower()},', f'"name": &"{name}",', f'"speed": {fps:.1f}',
                     '}' + (',' if index < len(ANIMATIONS)-1 else '')]
    resource += [']', '']
    (folder / "sprite_frames.tres").write_text('\n'.join(resource), encoding="utf-8")
    metadata["animations"] = {name: {"fps": fps, "loop": loop, "frames": frames} for name,(fps,loop,frames) in ANIMATIONS.items()}
    (folder / "alignment.json").write_text(json.dumps(metadata, indent=2)+"\n", encoding="utf-8")
    print(f"Built {len(metadata['frames'])} aligned frames and {len(ANIMATIONS)} animations in {folder}")

if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project", type=Path, default=Path(__file__).resolve().parents[2])
    build(parser.parse_args().project.resolve())
