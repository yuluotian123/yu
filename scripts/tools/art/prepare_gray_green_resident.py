"""Export the NPC's unquantized native idle and integer-scaled video input."""
import argparse
import json
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
ART = ROOT / "assets/textures/sprites/npc/gray_green_resident"
META = ROOT / "assets/resources/npc/gray_green_resident"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", default="keyframe_rgb_v6.png")
    parser.add_argument("--input-name", default="life_input_v6.png")
    parser.add_argument("--chroma", action="store_true", help="Derive binary alpha from a pink RGB animation input")
    options = parser.parse_args()
    source = Image.open(ART / "source" / options.source).convert("RGBA")
    if options.chroma:
        import numpy as np
        from scipy import ndimage
        rgb = np.asarray(source.convert("RGB")).astype(np.int16)
        foreground = ~((rgb[:, :, 0] - rgb[:, :, 1] > 18) & (rgb[:, :, 2] - rgb[:, :, 1] > 12))
        labels, _ = ndimage.label(foreground)
        sizes = np.bincount(labels.ravel())
        sizes[0] = 0
        source.putalpha(Image.fromarray((labels == int(sizes.argmax())).astype(np.uint8) * 255))
    mask = source.getchannel("A").point(lambda a: 255 if a >= 128 else 0)
    bounds = mask.getbbox()
    crop = source.crop(bounds)
    size = (round(crop.width * 224 / crop.height), 224)
    crop = crop.resize(size, Image.Resampling.NEAREST)
    crop.putalpha(crop.getchannel("A").point(lambda a: 255 if a >= 128 else 0))
    native = Image.new("RGBA", (256, 256))
    position = (128 - size[0] // 2, 240 - size[1])
    native.paste(crop, position, crop.getchannel("A"))
    META.mkdir(parents=True, exist_ok=True)
    native.save(ART / "source/keyframe_native_v6.png")
    model = Image.new("RGB", native.size, (216, 164, 204))
    model.paste(native, mask=native.getchannel("A"))
    model.resize((768, 768), Image.Resampling.NEAREST).save(ART / "source" / options.input_name)
    metadata = {
        "source": "source/" + options.source, "source_size": source.size,
        "source_crop": bounds, "canvas_size": [256, 256], "character_height": 224,
        "foot_anchor": [128, 240], "crop_position": position,
        "resize": "nearest", "palette_quantization": False, "alpha_threshold": 128,
        "alpha_method": "pink chroma, largest connected foreground" if options.chroma else "source alpha",
        "model_input": "source/" + options.input_name, "model_size": [768, 768],
        "background": [216, 164, 204], "model_transform": "native canvas scaled by 3; no recentering"
    }
    (META / "idle_export.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    print(json.dumps(metadata))


if __name__ == "__main__":
    main()
