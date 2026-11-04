"""Prepare the character-style swatches for the existing forest UV layouts.

Run with Python and Pillow. The source sheet is kept beside the textures.
All generated RGB values modulate the vertex palettes of the original meshes.
"""
import math
import random
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageOps

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / "assets/textures/environment/forest_village/character_style"
NEAREST = Image.Resampling.NEAREST
BOX = Image.Resampling.BOX
COLORS = {
    "bark": ("967961", "ba9880", "d4b596", "efce9e"),
    "wood": ("a58b7e", "c4a88e", "e0c5a8", "f3d8b1"),
    "plaster": ("c7bcb1", "ded3bd", "f1e6cf", "fff3da"),
    "roof": ("b49a92", "d3b0a0", "ead0b1", "f6dab2"),
    "stone": ("abaeb8", "c4c7cc", "d8d9d6", "e8e5d9"),
    "straw": ("b5a591", "d1bfa0", "e3d0a9", "f3dfb4"),
    "soil": ("978d79", "b3a78b", "cabd98", "ddd2b2"),
    "grass": ("a4ac91", "bbc4a0", "d3d8ad", "e2dfb6"),
    "leaf": ("9ba9a1", "bdc4a8", "d0d6af", "e0deb4"),
}


def swatches():
    sheet = Image.open(OUT / "source/paint_studies.png").convert("RGB")
    width, height = sheet.size
    result = {}
    for index, (kind, palette) in enumerate(COLORS.items()):
        x, y = index % 3, index // 3
        cell = sheet.crop((x*width//3+3, y*height//3+3,
                           (x+1)*width//3-3, (y+1)*height//3-3))
        value = ImageOps.autocontrast(cell.convert("L").resize((96, 96), BOX), cutoff=1)
        table = [bytes.fromhex(code) for code in palette]
        bands = [0 if i < 48 else 1 if i < 128 else 2 if i < 210 else 3 for i in range(256)]
        result[kind] = Image.merge("RGB", tuple(value.point([table[b][c] for b in bands]) for c in range(3)))
    return result


def tile(image, size):
    # Mirrored edges meet at the same value on repeating bark and terrain textures.
    half = image.resize((size//2, size//2), NEAREST)
    result = Image.new("RGB", (size, size))
    result.paste(half, (0, 0))
    result.paste(ImageOps.mirror(half), (size//2, 0))
    result.paste(ImageOps.flip(result.crop((0, 0, size, size//2))), (0, size//2))
    return result


def relief(cell):
    height = cell.convert("L").filter(ImageFilter.GaussianBlur(1.6))
    slope = lambda dx, dy: ImageChops.subtract(ImageChops.offset(height, dx, dy),
                                                ImageChops.offset(height, -dx, -dy), scale=1.5, offset=128)
    return Image.merge("RGB", (slope(1, 0), slope(0, -1), Image.new("L", cell.size, 255)))


def surface_atlas(cells, layout, name):
    atlas = Image.new("RGB", (1024, 1024))
    normals = Image.new("RGB", (2048, 2048))
    for index, kind in enumerate(layout):
        cell = cells[kind].resize((256, 256), NEAREST)
        x, y = index % 4, index // 4
        atlas.paste(cell, (x*256, y*256))
        if name == "house":
            normals.paste(relief(cell).resize((512, 512), NEAREST), (x*512, y*512))
    atlas.save(OUT / (name+".png"))
    if name == "house":
        normals.save(OUT / "house_normal.png")
        # Matte plaster and stone, slightly smoother panes and iron.
        rough = Image.new("L", (2048, 2048), 239)
        draw = ImageDraw.Draw(rough)
        for index, value in [(10, 167), (14, 150)]:
            x, y = index%4*512, index//4*512
            draw.rectangle((x, y, x+511, y+511), fill=value)
        rough.save(OUT / "house_roughness.png")


def grass_atlas():
    atlas = Image.new("RGBA", (1920, 256))
    for index in range(6):
        # Each clump is a single interlocking silhouette, with openings between tips.
        rng = random.Random(84+index)
        tuft = Image.new("RGBA", (80, 64)); draw = ImageDraw.Draw(tuft)
        for j in range(8):
            x = 15 + 7*j + rng.randrange(-2, 3)
            bottom = 63; top = rng.randrange(15, 35)
            width = rng.randrange(4, 8)
            bend = rng.randrange(-10, 13)
            shoulder = (x+bend//3, top+11)
            root = x + rng.randrange(-4,5)
            draw.polygon([(root-1, bottom), (x-width, top+19), shoulder, (x+bend, top),
                          (x+width, top+20), (root+2, bottom)],
                         fill=rng.choice(["#74a181","#83a784","#87ab89","#779c76"]))
            if j%3 == 1:
                draw.polygon([(x+bend, top),(x+bend//3, top+11),(x+bend//3+2, top+14)],
                             fill="#a8b589")
        draw.polygon([(37,63),(38,59),(41,58),(44,63)],fill="#607e5e")
        atlas.paste(tuft.resize((320,256), NEAREST), (320*index,0))
    atlas.save(OUT / "grass.png")


def meadow():
    # One eight-metre tile of interlocking, stair-stepped meadow shapes.
    size = 128
    image = Image.new("RGB", (size,size))
    mask = Image.new("L", (size,size))
    colors = ["#a3bfa9","#b7ceaf","#c9d8b3","#d7dfb6","#e1e2b8"]
    for y in range(size):
        for x in range(size):
            u, v = math.tau*x/size, math.tau*y/size
            bend = .72*math.sin(v)+.31*math.cos(u-v)
            value = (.5 + .24*math.sin(u+v+bend) + .15*math.cos(2*u-v+.6*math.sin(v))
                     + .09*math.sin(u-2*v) + .04*math.cos(3*v+u))
            band = sum(value > threshold for threshold in (.28,.46,.64,.80))
            image.putpixel((x,y),tuple(bytes.fromhex(colors[band][1:])))
            mask.putpixel((x,y),min(255,max(0,round(value*255))))
    pen = ImageDraw.Draw(image)
    rng = random.Random(739)
    for _ in range(44):
        x,y = rng.randrange(4,size-5),rng.randrange(4,size-3)
        # Broken angled dabs, only two to five ground texels per mark.
        tone = tuple(max(0,c-15) for c in image.getpixel((x,y)))
        pen.polygon([(x-2,y),(x-1,y-2),(x,y-1),(x+2,y-3),(x+1,y)],fill=tone)
    image.resize((512,512),NEAREST).save(OUT/"floor.png")
    mask.save(OUT/"meadow_mask.png")


def sprays():
    atlas = Image.new("RGBA", (512,512))
    for index in range(4):
        rng = random.Random(173+index)
        spray = Image.new("RGBA", (96,96)); pen = ImageDraw.Draw(spray)
        pen.line([(44,87),(48,60),(51,39),(52,13)], fill="#888b82", width=2)
        for j, (x,y,dx,dy) in enumerate([(46,78,-27,-17),(48,70,29,-20),(49,58,-32,-26),
                                          (50,50,29,-25),(51,39,-23,-25),(52,31,18,-19),(52,25,1,-20)]):
            dx += rng.randrange(-3,4); dy += rng.randrange(-2,3)
            length=math.hypot(dx,dy); cross=(-dy/length,dx/length)
            width=length*rng.uniform(.19,.29)
            def point(t,w):
                return (round(x+t*dx+w*cross[0]*width),round(y+t*dy+w*cross[1]*width))
            outline=[point(0,0),point(.21,.65),point(.48,1),point(.6,.64),point(.67,.72),
                     point(1,0),point(.58,-.9),point(.36,-.95),point(.15,-.55)]
            pen.polygon(outline,fill=rng.choice(["#d4d4c4","#e2e2cd","#bec4b6"]))
            pen.polygon([point(0,0),point(.4,.04),point(.84,0),point(.58,-.9),point(.36,-.95),
                         point(.15,-.55)],fill=rng.choice(["#929e96","#a5ada2","#aab3a5"]))
        atlas.paste(spray.resize((256,256),NEAREST),((index%2)*256,(index//2)*256))
    atlas.save(OUT / "sprays.png")


if __name__ == "__main__":
    materials = swatches()
    house = ["plaster","wood","wood","roof","stone","bark","straw","soil",
             "leaf","grass","stone","plaster","grass","soil","stone","leaf"]
    ground = house.copy()
    ground[6] = "soil"
    surface_atlas(materials, house, "house")
    surface_atlas(materials, ground, "terrain")
    bark = tile(materials["bark"],512)
    bark.save(OUT/"bark.png")
    relief(bark).save(OUT/"bark_normal.png")
    meadow()
    grass_atlas()
    sprays()
