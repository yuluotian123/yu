"""Blender source for the village's split timber fences and creek footbridge.
Run with blender --background --python this_file.py. Z is up; export is Y-up.
Uses the existing painted house atlas and its baked tangent normals/roughness.
"""
import bpy
import bmesh
import json
import math
import random
import tempfile
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'assets/models/environment/forest_village/structures'
TEX = ROOT / 'assets/textures/environment/forest_village/houses'
META = ROOT / 'assets/resources/art/forest_village/structures_models.json'
for directory in (OUT / 'source', META.parent, ROOT / '.godot/forest-village-structures/gltf-temp'):
    directory.mkdir(parents=True, exist_ok=True)
(OUT / 'source/.gdignore').write_text('', encoding='utf-8')
tempfile.tempdir = str(ROOT / '.godot/forest-village-structures/gltf-temp')
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
SCENE = bpy.context.scene
SCENE.name = 'ForestVillageStructures'
SCENE.unit_settings.system = 'METRIC'
RNG = random.Random(2026102801)
MATERIALS = {}
SPECS = []
PARTS = []
COLORS = {
    'Wood': ['806044', '89674a', '765940', '916e50'],
    'WoodLight': ['a48258', 'ae8b60', '9d7a53', 'b18e65'],
    'Iron': ['434840', '51544a', '5a5c50'],
    'Stone': ['939982', 'a5a88e', 'a4a48c', '838e7a'],
}
CELLS = {'Wood': (1, 0), 'WoodLight': (2, 0), 'Stone': (0, 1), 'Iron': (2, 2)}


def color(value):
    return tuple(int(value[i:i+2], 16)/255 for i in (0, 2, 4)) + (1,)


def materials():
    for kind in COLORS:
        mat = bpy.data.materials.new('FVS_' + kind)
        mat.use_nodes = True
        nodes, links = mat.node_tree.nodes, mat.node_tree.links
        bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
        vc = nodes.new('ShaderNodeVertexColor')
        vc.layer_name = 'Palette'
        albedo = nodes.new('ShaderNodeTexImage')
        albedo.image = bpy.data.images.load(str(TEX / 'painterly_house_atlas_v3.png'), check_existing=True)
        mix = nodes.new('ShaderNodeMixRGB')
        mix.blend_type = 'MULTIPLY'
        mix.inputs[0].default_value = .8
        links.new(vc.outputs['Color'], mix.inputs[1])
        links.new(albedo.outputs['Color'], mix.inputs[2])
        links.new(mix.outputs[0], bsdf.inputs['Base Color'])
        normal_tex = nodes.new('ShaderNodeTexImage')
        normal_tex.image = bpy.data.images.load(str(TEX / 'painterly_house_atlas_v3_normal.png'), check_existing=True)
        normal_tex.image.colorspace_settings.name = 'Non-Color'
        normal = nodes.new('ShaderNodeNormalMap')
        normal.inputs['Strength'].default_value = .55 if kind != 'Iron' else .28
        links.new(normal_tex.outputs['Color'], normal.inputs['Color'])
        links.new(normal.outputs['Normal'], bsdf.inputs['Normal'])
        orm = nodes.new('ShaderNodeTexImage')
        orm.image = bpy.data.images.load(str(TEX / 'painterly_house_atlas_v3_orm.png'), check_existing=True)
        orm.image.colorspace_settings.name = 'Non-Color'
        split = nodes.new('ShaderNodeSeparateColor')
        links.new(orm.outputs['Color'], split.inputs['Color'])
        links.new(split.outputs['Green'], bsdf.inputs['Roughness'])
        links.new(split.outputs['Blue'], bsdf.inputs['Metallic'])
        MATERIALS[kind] = mat


def solid(name, a, b, width, depth=None, kind='Wood', bevel=.012, shade=None, bend=0):
    """A dressed timber with real chamfered edges, continuous lengthwise grain."""
    a, b = Vector(a), Vector(b)
    length = (b-a).length
    bpy.ops.mesh.primitive_cube_add(size=1)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = (width, depth or width, length)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bend:
        # The face remains broad; uneven end cuts remove the machine-perfect silhouette.
        for v in obj.data.vertices:
            if v.co.z > 0:
                v.co.z += bend * v.co.x / width
    if bevel:
        mod = obj.modifiers.new('Worn hand cut arris', 'BEVEL')
        mod.width = min(bevel, width*.21, (depth or width)*.21)
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    obj.data.materials.append(MATERIALS[kind])
    uv = obj.data.uv_layers.new(name='PaintedUV')
    palette = obj.data.color_attributes.new(name='Palette', type='BYTE_COLOR', domain='CORNER')
    base = color(shade or RNG.choice(COLORS[kind]))
    cx, cy = CELLS[kind]
    # Offset/scale within the atlas cell per solid; keep all seams away from its gutters.
    u0, v0 = RNG.uniform(.012, .035), RNG.uniform(.012, .035)
    span_u, span_v = .20, .20
    for face in obj.data.polygons:
        normal_axis = max(range(3), key=lambda axis: abs(face.normal[axis]))
        side = 1 if normal_axis == 0 else 0
        face_tone = RNG.uniform(.97, 1.025)
        for li in face.loop_indices:
            p = obj.data.vertices[obj.data.loops[li].vertex_index].co
            # The atlas wood grain runs in V; end grain gets a cross section crop.
            u = p[side] / ((depth or width) if side == 1 else width) + .5
            v = p.z / max(length + abs(bend), .01) + .5
            if normal_axis == 2:
                u, v = p.x / width + .5, p.y / (depth or width) + .5
            uv.data[li].uv = (cx*.25 + u0 + max(0, min(1, u))*span_u,
                              1-(cy+1)*.25 + v0 + max(0, min(1, v))*span_v)
            tint = tuple(min(1, c*face_tone) for c in base[:3])
            palette.data[li].color_srgb = tint + (1,)
    obj.data.color_attributes.active_color = palette
    obj.rotation_mode = 'QUATERNION'
    obj.rotation_quaternion = (b-a).to_track_quat('Z', 'Y')
    obj.location = (a+b)*.5
    PARTS.append(obj)
    return obj


def pin(x, y, z, outward=1):
    solid('Forged square peg', (x,y,z), (x,y+outward*.018,z), .033, .033,
          'Iron', .003)


def post(x, y, height=.98, bridge=False):
    solid('Splayed split oak post', (x,y,-.055), (x+.009,y,height-.075),
          .18 if bridge else .155, .17 if bridge else .15, bend=.026)
    solid('Pitched weather cap', (x+.009,y,height-.075), (x+.009,y,height),
          .19 if bridge else .17, .18 if bridge else .165, 'WoodLight', .015, bend=.045)
    # Moss is part of the post palette, not a floating decal or ambient shadow bake.
    for obj in PARTS[-2:]:
        for li, entry in enumerate(obj.data.color_attributes['Palette'].data):
            # matrix_world isn't evaluated yet; use the explicit post transform.
            world_z = (obj.rotation_quaternion @ obj.data.vertices[obj.data.loops[li].vertex_index].co).z + obj.location.z
            weight = max(0, 1 - world_z/.23)*.32
            rgba = entry.color_srgb
            moss = color('727c4d')
            entry.color_srgb = tuple(rgba[i]*(1-weight)+moss[i]*weight for i in range(3))+(1,)


def finish(name, features, budget):
    bpy.ops.object.select_all(action='DESELECT')
    for part in PARTS:
        part.select_set(True)
    bpy.context.view_layer.objects.active = PARTS[0]
    bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name
    bpy.context.scene.cursor.location = (0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.calc_loop_triangles()
    tris = len(obj.data.loop_triangles)
    assert tris <= budget, (name, tris, budget)
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')), export_format='GLB',
        use_selection=True, export_apply=True, export_materials='EXPORT',
        export_normals=True, export_texcoords=True, export_tangents=True,
        export_vertex_color='ACTIVE', export_all_vertex_colors=False,
        export_cameras=False, export_lights=False, export_yup=True)
    SPECS.append({'name':name, 'file':name+'.glb', 'triangles':tris,
                  'surfaces':[m.name.removeprefix('FVS_') for m in obj.data.materials],
                  'features':features, 'normal_space':'OpenGL tangent +Y'})
    print('STRUCTURE_EXPORTED', name, tris, flush=True)
    PARTS.clear()
    return obj


materials()
post(0,0)
finish('fence_post', ['split oak', 'sloped cap', 'moss tinted foot', 'worn bevels'], 1600)
for variant in range(2):
    for z in (.38,.73):
        solid('Mortised horizontal rail', (0,0,z), (2,0,z+RNG.uniform(-.009,.009)),
              .10,.12,'WoodLight',.013)
        for x in (.07,1.93):
            for sign in (-1,1): pin(x,sign*.054,z,sign)
    # Alternating single braces read as repaired rural joinery rather than a picket wall.
    x0, x1 = (.10,1.90) if variant == 0 else (1.90,.10)
    solid('Diagonal anti-racking brace', (x0,.068,.30),(x1,.068,.80),.062,.075,
          'Wood',.009)
    finish('fence_bay_'+('a' if variant == 0 else 'b'),
           ['two mortised rails','diagonal brace','forged square pegs','lengthwise grain'],2200)

# Bridge keeps the existing 5.85 x 3.276 m footprint and +0.015 m walking surface.
LENGTH, WIDTH, SIDE = 5.85, 3.276, 1.716
for y in (-1.21,0,1.21):
    solid('Continuous load bearing stringer',(-LENGTH/2,y,-.235),(LENGTH/2,y,-.235),
          .23,.27,'Wood',.022)
count = 23
pitch = LENGTH/count
for i in range(count):
    x = -LENGTH/2+(i+.5)*pitch
    # Narrow real joints; support uses a continuous independent collider.
    height = -.047 + RNG.uniform(-.002,.002)
    solid('Individually dressed deck plank', (x,-WIDTH/2,height),(x,WIDTH/2,height),
          pitch-.009,.12,'WoodLight',.010,bend=RNG.uniform(-.008,.008))
    for y in (-1.21,1.21):
        solid('Recessed deck nail',(x,y,.011),(x,y,.019),.025,.025,'Iron',.002)
for y in (-SIDE,SIDE):
    solid('Deck edge fascia',(-LENGTH/2,y,-.115),(LENGTH/2,y,-.115),.19,.14,'Wood',.015)
    xs = [-2.78,-.93,.93,2.78]
    for x in xs:
        post(x,y,1.04,True)
        for z in (.40,.84):
            for sign in (-1,1): pin(x,y+sign*.091,z,sign)
    for j in range(3):
        a,b=xs[j],xs[j+1]
        for z,kind in ((.40,'Wood'),(.84,'WoodLight')):
            solid('Joined bridge rail',(a,y,z),(b,y,z),.11,.13,kind,.015)
        solid('Broad rounded hand rail',(a-.06,y,.94),(b+.06,y,.94),.15,.105,'WoodLight',.018)
        solid('Diagonal railing brace',(a+.10,y+.025,.31),(b-.10,y+.025,.84),
              .075,.085,'Wood',.010)
    for x in (-2.54,2.54):
        # Stone pads sit under the banks; the creek channel remains open.
        solid('Mossy bank abutment',(x,y,-.63),(x,y,-.31),.58,.48,'Stone',.065,bend=.035)
        solid('Bank support upright',(x,y,-.39),(x,y,-.08),.23,.22,'Wood',.020)
for x in (-2.48,2.48):
    solid('Transverse bearing sill',(x,-1.89,-.34),(x,1.89,-.34),.22,.24,'Wood',.022)
finish('creek_bridge', ['23 separate deck boards','three load bearing stringers',
    'continuous handrails','mortised braced rails','forged pegs and deck nails',
    'stone bank pads','open creek channel','flat accessible deck'],18000)
META.write_text(json.dumps({'revision':1,'atlas':'res://assets/textures/environment/forest_village/houses/painterly_house_atlas_v3.res',
    'seed':2026102801,'bridge_size':[5.85,.25,3.276],'bridge_deck_center':[28.08,-.11,2.34],
    'assets':SPECS},ensure_ascii=False,indent=2),encoding='utf-8')
SCENE['description'] = 'Forest village final timber structures; shared painted PBR surface maps'
bpy.data.libraries.write(str(OUT/'source/forest_village_structures.blend'),{SCENE},
                         path_remap='RELATIVE_ALL',fake_user=True,compress=True)
print('STRUCTURES_DONE',sum(s['triangles'] for s in SPECS),flush=True)
