"""Run in the live Blender after build_assets.py/finish(): add a foreground river.

Only this task's ground mesh, river assets and river placements are maintained.
Camera, lights, buildings and existing vegetation are preserved.
"""
import bpy
import json
import math
import random
from pathlib import Path
from mathutils import Vector

ROOT = Path('D:/Project/GitHub/yu')
OUT = ROOT / 'assets/models/environment/forest_town'
META = ROOT / 'assets/resources/art/forest_town_v1'
namespace = {'__name__': 'forest_town_asset_definitions'}
exec((ROOT / 'scripts/tools/art/forest_town/build_assets.py').read_text(encoding='utf8'), namespace)
Mesh = namespace['Mesh']
LIB = namespace['LIB']
stage = bpy.data.collections['FT_TownLayout']
manifest = json.loads((META / 'models.json').read_text(encoding='utf8'))
namespace['MANIFEST'] = manifest
river_names = {'river_surface', 'river_bank', 'river_reeds', 'river_stone'}
manifest['assets'] = [a for a in manifest['assets'] if a['name'] not in river_names]
manifest['instances'] = [i for i in manifest['instances'] if i['asset'] not in river_names]


def bank(x):
    """Godot's positive Z is foreground. Leave land beneath the side shrubs."""
    t = min(1.0, abs(x - .25) / 3.4)
    smooth = t*t*(3-2*t)
    return 3.92 + 2.95*smooth + .10*math.sin(x*1.8) + .045*math.sin(x*4.1)


def clip_polygon(polygon, distance):
    output = []
    for a, b in zip(polygon, polygon[1:]+polygon[:1]):
        fa,fb = distance(a[0]),distance(b[0])
        if fa >= -1e-7: output.append(a)
        if (fa > 1e-7 and fb < -1e-7) or (fa < -1e-7 and fb > 1e-7):
            low,high = 0.,1.
            for _ in range(28):
                t=(low+high)*.5
                if (distance(a[0].lerp(b[0],t))>=0)==(fa>=0): low=t
                else: high=t
            t=(low+high)*.5
            output.append(tuple(a[k].lerp(b[k],t) for k in range(3)))
    cleaned=[]
    for item in output:
        if not cleaned or (item[0]-cleaned[-1][0]).length>1e-6: cleaned.append(item)
    if len(cleaned)>1 and (cleaned[0][0]-cleaned[-1][0]).length<1e-6: cleaned.pop()
    return cleaned


def clipped_ground():
    source = bpy.data.objects['village_ground']
    previous = source.data
    old = bpy.data.meshes.get('FT_GroundBeforeRiver')
    if old is None or not previous.name.startswith('village_ground_RiverCut'):
        # Retain the latest uncut authored mesh for deterministic subsequent runs.
        original = previous
        if original.name.startswith('village_ground_RiverCut'):
            original = sorted([m for m in bpy.data.meshes if m.name.startswith('village_ground_Mesh') and len(m.polygons)==1462],key=lambda m:m.name)[-1]
        if old is not None:
            old.name = 'FT_GroundBeforeRiver_Previous'
            old.use_fake_user = False
        old = original.copy()
        old.name = 'FT_GroundBeforeRiver'
        old.use_fake_user = True
    colors = old.color_attributes['Palette']
    uv = old.uv_layers.active
    verts, faces, paint, uvs = [], [], [], []
    for face in old.polygons:
        polygon = [(old.vertices[old.loops[k].vertex_index].co.copy(),
                    Vector(colors.data[k].color_srgb), uv.data[k].uv.copy()) for k in face.loop_indices]
        distances = [bank(p[0].x)+p[0].y for p in polygon]
        pieces = [polygon]
        if min(distances)<.25 and max(distances)>-.25:
            pieces=[]
            for strip in range(math.floor(min(p[0].x for p in polygon)*4),math.ceil(max(p[0].x for p in polygon)*4)):
                part=clip_polygon(polygon,lambda p:p.x-strip*.25)
                if len(part)>=3: part=clip_polygon(part,lambda p:(strip+1)*.25-p.x)
                if len(part)>=3: pieces.append(part)
        for piece in pieces:
            output=clip_polygon(piece,lambda p:bank(p.x)+p.y)
            if len(output)<3: continue
            offset=len(verts)
            verts.extend(tuple(p[0]) for p in output)
            faces.append(tuple(range(offset,len(verts))))
            paint.append([p[1] for p in output])
            uvs.append([p[2] for p in output])
    data = bpy.data.meshes.new('village_ground_RiverCut')
    data.from_pydata(verts, [], faces)
    data.update()
    color = data.color_attributes.new(name='Palette', type='FLOAT_COLOR', domain='CORNER')
    tex = data.uv_layers.new(name='PaintedUV')
    for face, tones, mapped in zip(data.polygons, paint, uvs):
        for k, tone, coord in zip(face.loop_indices, tones, mapped):
            color.data[k].color_srgb = tone
            tex.data[k].uv = coord
    data.color_attributes.active_color = color
    for material in old.materials:
        data.materials.append(material)
    for obj in bpy.data.objects:
        if obj.type == 'MESH' and obj.data == previous:
            obj.data = data
    data.calc_loop_triangles()
    for asset in manifest['assets']:
        if asset['name'] == 'village_ground':
            asset.update(polygons=len(data.polygons), triangles=len(data.loop_triangles), vertices=len(data.vertices))
            # Strips stop at the real bank; water is not a walkable invisible floor.
            asset['collision'] = []
            for x in range(-28,30):
                end = min(bank(x),bank(x+1),bank(x+.5))-.02
                asset['collision'].append({'position':[x+.5,-.51,(end-46)*.5], 'size':[1,1,end+46]})
            asset['river_cut'] = True
    return source


ground = clipped_ground()
water = Mesh()
water_uvs = []
width = 4.8
for column in range(240):
    x0, x1 = -30+column*.25, -30+(column+1)*.25
    for row in range(12):
        v0, v1 = row/12, (row+1)/12
        water.poly([(x0,-bank(x0)-v0*width,-.24),(x0,-bank(x0)-v1*width,-.24),
                    (x1,-bank(x1)-v1*width,-.24),(x1,-bank(x1)-v0*width,-.24)], '387a7a')
        # glTF export flips Blender's texture V. Godot needs V=0 at the town bank.
        water_uvs.append([(column/240,1-v0),(column/240,1-v1),((column+1)/240,1-v1),((column+1)/240,1-v0)])
water_obj = water.emit('river_surface','water')
for face, coordinates in zip(water_obj.data.polygons,water_uvs):
    face.use_smooth = True
    for k, coord in zip(face.loop_indices,coordinates):
        water_obj.data.uv_layers.active.data[k].uv = coord
water_mat = bpy.data.materials.get('FT_River') or bpy.data.materials.new('FT_River')
bsdf = next(n for n in water_mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
bsdf.inputs['Base Color'].default_value = (.065,.24,.27,1)
bsdf.inputs['Roughness'].default_value = .58
water_obj.data.materials.clear()
water_obj.data.materials.append(water_mat)

# A complete, capped earth/stone bank, with a pale weathered upper lip.
bank_mesh = Mesh()
for column in range(150):
    x0,x1 = -30+column*.4,-30+(column+1)*.4
    b0,b1 = bank(x0),bank(x1)
    top = [(x0,-b0+.22,.025),(x0,-b0-.025,.025),(x1,-b1-.025,.025),(x1,-b1+.22,.025)]
    bank_mesh.poly(top,'9b9f7f')
    bottom = [(x,y,-.72) for x,y,z in top]
    bank_mesh.poly(list(reversed(bottom)), 'stone')
    for j in range(4):
        if (j == 0 and column != 0) or (j == 2 and column != 149):
            continue
        k=(j+1)%4
        bank_mesh.poly([top[j],bottom[j],bottom[k],top[k]],'stone' if j==1 else '71826c')
bank_obj = bank_mesh.emit('river_bank','prop')

# An individually reusable, faceted river stone, complete underneath.
stone = Mesh()
rings = []
for height, radius in [(-.07,.68),(.02,1.0),(.14,.8),(.19,.34)]:
    rings.append([Vector((math.cos(k*math.tau/12)*.31*radius,
                          math.sin(k*math.tau/12)*.22*radius,height)) for k in range(12)])
for row in range(3):
    for k in range(12):
        stone.poly([rings[row][k],rings[row][(k+1)%12],rings[row+1][(k+1)%12],rings[row+1][k]],'stone')
stone.poly(list(reversed(rings[0])),'stone')
stone.poly(rings[-1],'b3b39a')
stone_obj = stone.emit('river_stone','prop')

# Curved, ridged grass blades plus four cattails; no low-detail crossed cards.
reeds = Mesh()
rng = random.Random(8802)
for blade in range(23):
    angle = blade*2.4
    height = rng.uniform(.32,.68)
    outward = Vector((math.cos(angle),math.sin(angle),0))
    lateral = Vector((-math.sin(angle),math.cos(angle),0))
    base = outward*rng.uniform(.01,.11)
    for segment in range(6):
        a,b=segment/6,(segment+1)/6
        ca=base+outward*(a*a*.26)+Vector((0,0,height*(a-.16*a*a)))
        cb=base+outward*(b*b*.26)+Vector((0,0,height*(b-.16*b*b)))
        wa=max(.001,.037*math.sin(math.pi*(a*.87+.11)))
        wb=max(.001,.037*math.sin(math.pi*(b*.87+.11))) if segment<5 else .001
        ra=ca+outward*.008
        rb=cb+outward*.008
        reeds.poly([ca-lateral*wa,cb-lateral*wb,rb,ra],'65956d')
        reeds.poly([ra,rb,cb+lateral*wb,ca+lateral*wa],'94b17a')
for k in range(4):
    x,y=rng.uniform(-.10,.10),rng.uniform(-.10,.10)
    h=rng.uniform(.62,.85)
    reeds.tube((x,y,0),(x+.04,y,h),.009,.005,'65956d',8)
    reeds.tube((x+.04,y,h-.17),(x+.04,y,h),.026,.022,'795b42',10)
reeds_obj = reeds.emit('river_reeds','plant')

def place(name, asset, x, y, z=0., scale=1., rotation=0.):
    manifest['instances'].append({'name':name,'asset':asset,'position':[x,y,z], 'rotation_y':rotation,'scale':scale})

place('ForegroundRiver','river_surface',0,0)
place('RiverBank','river_bank',0,0)
for i,x in enumerate([-6,-4.8,-3.65,-2.9,-2.2,-1.4,1.75,2.6,3.7,4.9,6.5]):
    place('BankReeds_%02d'%i,'river_reeds',x,-.04,bank(x)-rng.uniform(.04,.18),rng.uniform(.55,.9),rng.uniform(0,360))
for i,x in enumerate([-7,-5,-3.7,-2.7,-1.5,-.65,.8,1.65,2.7,3.5,5.2,7.]):
    place('RiverStone_%02d'%i,'river_stone',x,-.08,bank(x)+rng.uniform(-.03,.14),rng.uniform(.6,1.1),rng.uniform(0,360))

# The original straight fences must follow the new bank with both posts on land.
for item in manifest['instances']:
    if not item['name'].startswith(('LeftFence_','RightFence_')): continue
    x=item['position'][0]
    slope=(bank(x+.03)-bank(x-.03))/.06
    yaw=-math.atan(slope)
    half_post=.84*item['scale']
    z=min(bank(x+sign*half_post*math.cos(yaw))+sign*half_post*math.sin(yaw) for sign in [-1,1])-.24
    item['position'][2]=z
    item['rotation_y']=math.degrees(yaw)
    obj=stage.objects.get(item['name'])
    obj.location=(x,-z,item['position'][1])
    obj.rotation_euler.z=yaw

changed = [ground, water_obj,bank_obj,stone_obj,reeds_obj]
LIB.hide_viewport=False
LIB.hide_render=False
for obj in list(bpy.context.selected_objects): obj.select_set(False)
for obj in changed:
    obj.hide_set(False)
    obj.hide_render=False
    obj.select_set(True)
    bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.gltf(filepath=str(OUT/(obj.name+'.glb')),export_format='GLB',use_selection=True,
        export_apply=True,export_materials='EXPORT',export_yup=True,export_normals=True,
        export_vertex_color='ACTIVE',export_all_vertex_colors=False,export_cameras=False,export_lights=False)
    obj.select_set(False)
    obj.hide_set(True)
    obj.hide_render=True
for obj in list(stage.objects):
    if obj.get('river_asset',False): bpy.data.objects.remove(obj,do_unlink=True)
for item in manifest['instances']:
    if item['asset'] not in river_names: continue
    obj=bpy.data.objects.new(item['name'],bpy.data.objects[item['asset']].data)
    stage.objects.link(obj)
    x,y,z=item['position']
    obj.location=(x,-z,y)
    obj.scale=(item['scale'],)*3
    obj.rotation_euler.z=math.radians(item['rotation_y'])
    obj['river_asset']=True
manifest['river']={'water_height':-.24,'width':width,'center_bank_z':bank(.25),
    'shader':'res://assets/shaders/town/forest_river.gdshader','collision':'land stops at bank; no water collision'}
manifest['summary']={'unique_assets':len(manifest['assets']),'placed_instances':len(manifest['instances']),
    'max_asset_triangles':max(a['triangles'] for a in manifest['assets'])}
(META/'models.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'source/forest_town.blend'))
print('RIVER_BUILT',json.dumps(manifest['summary']))
for asset in manifest['assets']:
    if asset['name'] in river_names or asset['name']=='village_ground':
        assert asset['triangles']<=8000 and asset['polygons']<=8000
        print(asset['name'],asset['polygons'],asset['triangles'])
