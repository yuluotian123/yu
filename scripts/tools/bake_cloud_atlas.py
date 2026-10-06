"""Pack the original grayscale cloud artwork into shader data (Pillow + NumPy).
Run from the repository root: python scripts/tools/bake_cloud_atlas.py
The input painting remains unmodified; output channels are light/rim/SDF/bounds.
"""
from pathlib import Path
import numpy as np
from PIL import Image, ImageFilter

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "assets/environment/cloud_painting_source.png"
OUTPUT = ROOT / "assets/environment/cloud_atlas.png"
# Authored sprite bounds in the 1254 square source (not a generated runtime texture).
BOUNDS = [(0, 0, 636, 346), (636, 0, 1254, 340),
          (0, 350, 636, 640), (636, 340, 1254, 645),
          (0, 650, 636, 922), (636, 650, 1254, 918),
          (0, 922, 636, 1254), (636, 938, 1254, 1237)]


def distance_to(mask):
    """Truncated chamfer distance; 128 pixels retain gradients through the cores for growth/dissolve animation."""
    distance = np.where(mask, 0.0, 129.0).astype(np.float32)
    for _ in range(128):
        old = np.pad(distance, 1, constant_values=129.0)
        distance = np.minimum.reduce([
            distance, old[:-2, 1:-1] + 1, old[2:, 1:-1] + 1,
            old[1:-1, :-2] + 1, old[1:-1, 2:] + 1,
            old[:-2, :-2] + 1.414214, old[:-2, 2:] + 1.414214,
            old[2:, :-2] + 1.414214, old[2:, 2:] + 1.414214])
    return distance


def bake_flow_noise():
    # Reference repository Noise_091.png is 16-bit data, not a display-color image.
    source = Image.open(ROOT / "assets/environment/cloud_flow_noise_source.png")
    values = np.asarray(source).astype(np.float32) / 65535.0
    channel = Image.fromarray(values, "F").resize((1024, 1024), Image.Resampling.LANCZOS)
    # Gentle low-pass removes subpixel grain without changing the broad flow pattern.
    channel = Image.fromarray(np.rint(np.clip(np.asarray(channel), 0, 1) * 255).astype(np.uint8))
    channel = channel.filter(ImageFilter.GaussianBlur(1.0))
    Image.merge("RGB", (channel, channel, channel)).save(
        ROOT / "assets/environment/cloud_flow_noise.png", optimize=True)


def main():
    source = Image.open(SOURCE).convert("RGBA")
    if source.size != (1254, 1254):
        raise ValueError("Update authored sprite bounds for a different source painting.")
    atlas = Image.new("RGBA", (1024, 1024))
    for i, bounds in enumerate(BOUNDS):
        sprite = source.crop(bounds)
        sprite.thumbnail((464, 222), Image.Resampling.LANCZOS)
        tile = Image.new("RGBA", (512, 256))
        tile.paste(sprite, ((512 - sprite.width) // 2, (256 - sprite.height) // 2))
        values = np.asarray(tile).astype(np.float32) / 255.0
        alpha = values[:, :, 3]
        mask_image = Image.fromarray((alpha > 0.35).astype(np.uint8) * 255).filter(ImageFilter.MedianFilter(3))
        mask = np.asarray(mask_image) > 127
        inside = distance_to(~mask)
        # Reference thresholds assume zero outside and a continuous gradient through the core.
        sdf = np.clip(inside / 80.0, 0, 1)
        luminance = values[:, :, :3].mean(axis=2)
        light = np.clip((luminance - 0.55) / 0.44, 0, 1)
        # Extend light data into the transparent rim, avoiding dark SDF growth edges.
        light = np.where(alpha > 0.05, light, 1.0)
        rim = np.exp(-np.maximum(inside - 1, 0) / 2.5) * mask
        gate = np.asarray(mask_image.filter(ImageFilter.MaxFilter(19))).astype(np.float32) / 255.0
        data = np.stack([light, rim, sdf, gate], axis=2)
        encoded = Image.fromarray(np.rint(data * 255).astype(np.uint8), "RGBA")
        atlas.paste(encoded, ((i % 2) * 512, (i // 2) * 256))
    atlas.save(OUTPUT, optimize=True)
    bake_flow_noise()
    print(f"Packed {OUTPUT}: 1024x1024, eight 512x256 cards, RGBA light/rim/SDF/bounds.")


if __name__ == "__main__":
    main()

