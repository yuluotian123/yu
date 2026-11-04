"""Bind the finished paint sheet to external and scene-local forest materials.

Also run after rebuilding the village from legacy authoring tools. Only named
texture dependencies and material parameters are edited. Mesh/scene IDs stay intact.
"""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
MAT = ROOT / "assets/materials/environment"
STYLE = "res://assets/textures/environment/forest_village/character_style/"
SCENE = ROOT / "assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
REPLACEMENTS = {
    "forest_village/houses/painterly_house_atlas_v3": "house",
    "forest_village/houses/painterly_house_atlas_v3_normal": "house_normal",
    "forest_village/houses/painterly_house_atlas_v3_roughness": "house_roughness",
    "forest_village/groundcover_v4": "floor",
    "forest_town/painterly_atlas_v4": "terrain",
    "forest_town/bark_painted_v4": "bark",
    "forest_town/bark_painted_v4_normal": "bark_normal",
    "forest_village/ground_flora/grass_clumps": "grass",
    "forest_vegetation/broad_sprays": "sprays",
}


def bind_textures(text):
    for old, new in REPLACEMENTS.items():
        path = f"res://assets/textures/environment/{old}.res"
        text = re.sub(r'(?: uid="[^"]+")? path="' + re.escape(path) + '"',
                      f' path="{STYLE}{new}.res"', text)
    return text


def set_params(block, settings):
    for key, value in settings.items():
        line = f"shader_parameter/{key} = {value}"
        pattern = rf"(?m)^shader_parameter/{key} = .*$"
        if re.search(pattern, block):
            block = re.sub(pattern, line, block)
        else:
            block = block.rstrip()+"\n"+line+"\n\n"
    return block


def paint_surface(kind):
    settings = dict(painterly_strength=.94, painterly_light_wrap=.56,
                    painterly_shadow_lift=.105, toon_strength=.92,
                    color_saturation=.95, texture_detail=.58, pixel_brush="true")
    if kind == "bark":
        settings.update(texture_detail=.78, normal_scale=.65, color_saturation=1.04)
    elif kind in ("wood", "wood_light", "thatch"):
        settings.update(normal_scale=.17, texture_detail=.68, specular_strength=.035)
    elif kind == "roof":
        settings.update(normal_scale=.13, texture_detail=.64, specular_strength=.035)
    elif kind == "plaster":
        settings.update(normal_scale=.07, texture_detail=.50, specular_strength=.02)
    elif kind in ("stone", "path"):
        settings.update(normal_scale=.14, texture_detail=.54, color_saturation=.87,
                        painterly_shadow_lift=.075, base_color="Color(0.99,1.01,1.04,1)")
    elif kind in ("glass","iron"):
        settings.update(normal_scale=.10, texture_detail=.28)
    elif kind == "grass":
        settings.update(uv_scale="Vector2(0.14705882,0.14705882)", texture_detail=.85,
                        painterly_shadow_lift=.26, base_color="Color(1.0,1.04,1.02,1)")
    return settings


def write_changed(path, old, new):
    if new != old:
        path.write_text(new, encoding="utf8", newline="\n")


def bind_external(path):
    old = path.read_text(encoding="utf8")
    header, block = bind_textures(old).split("[resource]",1)
    if "leaf_article.gdshader" in header:
        settings = dict(painterly_strength=.90, directional_shadow_lift=.32,
                        gradient_strength=.78, lower_color="Vector3(0.76,0.92,0.91)",
                        upper_color="Vector3(0.85,0.98,0.86)", texture_detail=0.0)
        settings["use_painted_sprays"] = str(path.stem != "fern").lower()
        settings["canopy_ambient_up"] = .90 if path.stem != "fern" else 0.0
    elif "grass_tuft.gdshader" in header:
        settings = dict(directional_shadow_lift=.23)
    elif "plant_painterly.gdshader" in header:
        settings = dict(directional_shadow_lift=.19, painterly_blend=.30,
                        band_softness=.07, light_wrap=.30)
        floor_plants = {"verge": (.60, .78, .80), "rosette": (.36, .32, .92), "fern": (.35, .35, 1.0)}
        if path.stem in floor_plants:
            floor_mix, normals, value = floor_plants[path.stem]
            settings.update(floor_color_mix=floor_mix, grass_normal_blend=normals, albedo_value=value,
                            directional_shadow_lift=.26, light_wrap=.48)
        elif path.stem == "flowers":
            settings.update(grass_normal_blend=.45, directional_shadow_lift=.26, light_wrap=.48)
    else:
        settings = paint_surface(path.stem)
    paints_floor = path == MAT/"forest_village/grass.tres"
    if paints_floor or "floor_color_mix" in settings:
        contact = 'res://assets/textures/environment/forest_village/ground_contact.res'
        match = re.search(r'path="'+re.escape(contact)+r'" id="([^"]+)"',header)
        resource_id = match[1] if match else "painted_floor"
        if not match:
            header += f'[ext_resource type="Texture2D" path="{contact}" id="{resource_id}"]\n\n'
        settings.update(ground_contact_map=f'ExtResource("{resource_id}")',
                        ground_contact_rect="Vector4(-40.56,-37.44,81.12,74.88)")
        if paints_floor:
            settings["paint_floor_from_contact"] = "true"
    write_changed(path,old,header+"[resource]"+set_params(block,settings))


def bind_scene():
    old = SCENE.read_text(encoding="utf8")
    text = bind_textures(old)
    # Older bakes embedded a generic surface shader in all roadside grass meshes.
    old_verge = re.search(r'(?m)^\[sub_resource type="ShaderMaterial" id="([^"]+)"\]\n'
                          r'resource_name = "Forest floor · contact grass"\n[^\[]*', text)
    if old_verge:
        verge = "res://assets/materials/environment/forest_village/ground_flora/verge.tres"
        text = text[:old_verge.start()] + text[old_verge.end():]
        marker = text.index("[sub_resource")
        text = text[:marker] + f'[ext_resource type="Material" path="{verge}" id="painted_verge"]\n\n' + text[marker:]
        text = text.replace(f'SubResource("{old_verge[1]}")', 'ExtResource("painted_verge")')
    def update(match):
        block = match.group()
        name = re.search(r'^resource_name = "(.*)"',block,re.M)
        if not name:
            return block
        name = name[1].lower()
        if "stream · varied" in name:
            return set_params(block,dict(deep_color="Color(0.16,0.32,0.34,1)",
                middle_color="Color(0.30,0.45,0.40,1)",shallow_color="Color(0.56,0.63,0.48,1)",
                foam_color="Color(0.83,0.81,0.64,1)", ripple_strength=.29))
        if "shader_parameter/painterly_shadow_lift" in block:
            kind = "bark" if "terrain contact" in name else "path"
            return set_params(block,paint_surface(kind))
        return block
    text = re.sub(r'(?ms)^\[sub_resource type="ShaderMaterial"[^\n]+\n.*?(?=^\[|\Z)',update,text)
    # One bank uses an old inline copy. Share the map baked from the painted floor.
    map_id = re.search(r'path="[^"]+/ground_contact.res" id="([^"]+)"',text)[1]
    text = re.sub(r'(?m)^shader_parameter/ground_contact_map = SubResource\("ImageTexture_[^"]+"\)',
                  f'shader_parameter/ground_contact_map = ExtResource("{map_id}")',text)
    write_changed(SCENE,old,text)


if __name__ == "__main__":
    paths = list((MAT/"forest_village").rglob("*.tres"))
    paths.extend((MAT/"forest_vegetation").glob("*.tres"))
    for path in paths:
        bind_external(path)
    bind_scene()
    print(f"Bound {len(paths)} forest materials and the village's local overrides")
