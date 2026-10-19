"""Export a fixed-canvas pixel clip; RGB is preserved, only the pink background alpha changes.

Use a JSON job containing video, output, indices and fps. Never crops or recentres individual frames.
"""
import argparse
import json
from pathlib import Path
import av
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

parser = argparse.ArgumentParser()
parser.add_argument("job", type=Path)
args = parser.parse_args()
job = json.loads(args.job.read_text(encoding="utf-8"))
root = Path.cwd()
output = root / job["output"]
output.mkdir(parents=True, exist_ok=True)
source = output.parent / "source" / (job["name"] + "_rgb_v1")
source.mkdir(parents=True, exist_ok=True)
decoded = [f.to_image().convert("RGB").resize((256, 256), Image.Resampling.NEAREST)
           for f in av.open(str(root / job["video"])).decode(video=0)]
frames = []
for index, source_index in enumerate(job["indices"]):
    rgb = decoded[source_index]
    rgb.save(source / f"{job['name']}_{index:02d}_source_{source_index:03d}.png")
    values = np.array(rgb).astype(np.int16)
    r, g, b = values.transpose(2, 0, 1)
    background = (b > r - 28) & (r > b - 28) & (b > g + 12) & (r > g + 22)
    mask = ~background
    labels, count = ndimage.label(mask)
    sizes = np.bincount(labels.ravel())
    mask &= sizes[labels] >= 8
    rgba = np.dstack((np.array(rgb), mask.astype(np.uint8) * 255))
    image = Image.fromarray(rgba)
    image.save(output / f"{job['name']}_{index:02d}.png")
    frames.append(image)
count = len(frames)
cols = 8
atlas = Image.new("RGBA", (256 * cols, 256 * ((count + cols - 1) // cols)))
for i, frame in enumerate(frames):
    atlas.alpha_composite(frame, (i % cols * 256, i // cols * 256))
atlas.save(output.parent / (job["name"] + "_atlas_v1.png"))
frames[0].save(output.parent / (job["name"] + "_preview_v1.png"), save_all=True,
               append_images=frames[1:], duration=1000 / job["fps"], loop=0, disposal=0, blend=0)
review = []
for frame in frames:
    bg = Image.new("RGBA", (512, 512), (45, 49, 58, 255))
    bg.alpha_composite(frame.resize((512, 512), Image.Resampling.NEAREST))
    review.append(bg.convert("RGB"))
review[0].save(output.parent / (job["name"] + "_review_v1.gif"), save_all=True,
               append_images=review[1:], duration=1000 / job["fps"], loop=0)
print(json.dumps({"name": job["name"], "frames": count, "fps": job["fps"], "source_frames": len(decoded),
                  "canvas": [256, 256], "mapping": job["indices"], "rgb": "unaltered source RGB", "alpha": "binary chroma segmentation; visual review required"}))
