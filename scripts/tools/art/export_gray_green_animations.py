"""Produce native idle/walk sprites from the selected, unmodified ComfyUI videos."""
import json
from pathlib import Path

import av
import numpy as np
from PIL import Image
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "assets/textures/sprites/npc/gray_green_resident"
META = ROOT / "assets/resources/npc/gray_green_resident"
CLIPS = {
    "idle": {"video": "5b7f9bac_000.mp4", "indices": list(range(0, 72, 3)), "fps": 8},
    "walk": {"video": "5aa63960_000.mp4", "indices": list(range(12, 44, 2)), "fps": 12},
}


def alpha_from_pink(image):
    rgb = np.asarray(image).astype(np.int16)
    foreground = ~((rgb[:, :, 0] - rgb[:, :, 1] > 18) & (rgb[:, :, 2] - rgb[:, :, 1] > 12))
    labels, _ = ndimage.label(foreground)
    sizes = np.bincount(labels.ravel())
    sizes[0] = 0
    foreground = labels == int(sizes.argmax())
    rgba = np.asarray(image.convert("RGBA")).copy()
    rgba[:, :, 3] = foreground.astype(np.uint8) * 255
    rgba[~foreground, :3] = 0  # Hidden RGB only; visible pixels remain byte-identical.
    return Image.fromarray(rgba)


def export_clip(clip, config):
    video_path = ROOT / "assets/videos/npc/gray_green_resident" / config["video"]
    decoded = []
    with av.open(str(video_path)) as video:
        info = {"size": [video.streams.video[0].width, video.streams.video[0].height],
                "fps": float(video.streams.video[0].average_rate)}
        for frame in video.decode(video=0):
            decoded.append(frame.to_image().convert("RGB").resize((256, 256), Image.Resampling.NEAREST))
    (ART / clip).mkdir(parents=True, exist_ok=True)
    raw = ART / "source" / f"{clip}_rgb_v6"
    raw.mkdir(parents=True, exist_ok=True)
    frames = []
    for i, source_index in enumerate(config["indices"]):
        image = decoded[source_index]
        image.save(raw / f"{clip}_{i:02d}_source_{source_index:03d}.png")
        rgba = alpha_from_pink(image)
        rgba.save(ART / clip / f"{clip}_{i:02d}.png")
        frames.append(rgba)
    columns = min(8, len(frames))
    atlas = Image.new("RGBA", (columns * 256, ((len(frames) + columns - 1) // columns) * 256))
    for i, frame in enumerate(frames):
        atlas.paste(frame, (i % columns * 256, i // columns * 256))
    atlas.save(ART / f"{clip}_atlas_v6.png")
    frames[0].save(ART / f"{clip}_preview_v6.png", format="PNG", save_all=True, append_images=frames[1:],
                   duration=1000 / config["fps"], loop=0, disposal=0, blend=0)
    manifest = {
        "video": "../../../videos/npc/gray_green_resident/" + config["video"],
        "frames": f"../../../textures/sprites/npc/gray_green_resident/{clip}/{clip}_*.png",
        "source_indices": config["indices"], "source": info, "source_frame_count": len(decoded),
        "frame_size": [256, 256], "output_fps": config["fps"], "binary_alpha": True,
        "foot_anchor": [128, 240], "transform": "entire source frame to 256x256, nearest; no per-frame crop, translation or palette quantization",
        "rgb_tolerance": 0, "preserve_inset": 3,
        "alpha_method": "pink r-g>18 and b-g>12; largest connected foreground; hidden RGB zeroed, visible RGB untouched",
        "regions": [
            {"name": "face", "bounds": [118, 30, 154, 69]},
            {"name": "hair", "bounds": [85, 12, 160, 78]},
            {"name": "torso", "bounds": [82, 64, 168, 169]},
            {"name": "hands", "bounds": [70, 101, 177, 179]},
            {"name": "bag", "bounds": [137, 108, 182, 167]},
            {"name": "legs", "bounds": [70, 155, 195, 246]}
        ]
    }
    (META / f"{clip}_audit.json").write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps({"clip": clip, "source": info, "count": len(frames), "indices": config["indices"]}))
    return columns


def main():
    atlas_columns = {clip: export_clip(clip, config) for clip, config in CLIPS.items()}
    lines = ['[gd_resource type="SpriteFrames" format=3]', '']
    for clip in CLIPS:
        lines += [f'[ext_resource type="Texture2D" path="res://assets/textures/sprites/npc/gray_green_resident/{clip}_atlas_v6.png" id="{clip}"]']
    lines.append('')
    for clip, config in CLIPS.items():
        for i in range(len(config['indices'])):
            columns = atlas_columns[clip]
            lines += [f'[sub_resource type="AtlasTexture" id="{clip}_{i}"]', f'atlas = ExtResource("{clip}")',
                      f'region = Rect2({i % columns*256}, {i // columns*256}, 256, 256)', 'filter_clip = true', '']
    animations = []
    for clip, config in CLIPS.items():
        frames = ',\n'.join('{"duration": 1.0, "texture": SubResource("'+clip+'_'+str(i)+'")}' for i in range(len(config['indices'])))
        animations.append('{"frames": [\n'+frames+'\n], "loop": 1, "name": &"'+clip+'", "speed": '+str(float(config['fps']))+'}')
    lines += ['[resource]', 'animations = ['+',\n'.join(animations)+']', '']
    (META / 'sprite_frames.tres').write_text('\n'.join(lines), encoding='utf-8')


if __name__ == "__main__":
    main()
