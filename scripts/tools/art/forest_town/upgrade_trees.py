"""Three detailed, independently authored forest trees and a denser woodland.

In Blender MCP: execute definitions, build_tree(name) once per TREE_SPECS key,
then finish_tree_upgrade(). Existing buildings, river and non-tree placements stay intact.
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
BASE = {}
exec(compile((ROOT / 'scripts/tools/art/forest_town/build_assets.py').read_text(encoding='utf8'), 'build_assets.py', 'exec'), BASE)
Mesh = BASE['Mesh']
RNG = BASE['RNG']
color = BASE['color']
LIB = BASE['LIB']
ORIGINAL = json.loads((META / 'models.json').read_text(encoding='utf8'))
TREE_SPECS = {
    'ancient_oak': dict(height=14.8, radius=1.02, seed=1041, branches=7, lean=.75, spread=.195, label='buttressed broad oak'),
    'branching_oak': dict(height=17.2, radius=.64, seed=1042, branches=7, lean=.32, spread=.145, label='forked upright hornbeam'),
    'canopy_oak': dict(height=11.4, radius=.59, seed=1043, branches=6, lean=1.15, spread=.255, label='sweeping crooked oak'),
}


def limb(mesh, points, radii, sides, phase=0.0, flute=.055, sun=False):
    """Stable transported rings, longitudinal bark colours and authored smooth normals."""
    points = [Vector(p) for p in points]
    rings, normals = [], []
    old_u = None
    for i, (p, radius) in enumerate(zip(points, radii)):
        tangent = (points[min(i+1, len(points)-1)]-points[max(0, i-1)]).normalized()
        if old_u is None:
            helper = Vector((1, 0, 0)) if abs(tangent.x) < .9 else Vector((0, 1, 0))
            u = tangent.cross(helper).normalized()
        else:
            u = (old_u - tangent*old_u.dot(tangent)).normalized()
        v = tangent.cross(u).normalized()
        old_u = u
        ring, ns = [], []
        for j in range(sides):
            angle = j*math.tau/sides
            normal = u*math.cos(angle)+v*math.sin(angle)
            profile = 1 + flute*math.sin(angle*5+phase+i*.11) + flute*.32*math.sin(angle*9-phase)
            ring.append(p+normal*radius*profile)
            ns.append(normal)
        rings.append(ring)
        normals.append(ns)
    for i in range(len(points)-1):
        for j in range(sides):
            k = (j+1) % sides
            sector = int((j/sides*5+phase*.17) % 5)
            tone = color('bark', sector)
            if sun and sides//5 <= j <= sides//3:
                tone = color('bark', 4)
            uv = [(j/4, i/5), ((j+1)/4, i/5), ((j+1)/4, (i+1)/5), (j/4, (i+1)/5)]
            # Repeat within the bark atlas cell, keeping each ring's seam continuous.
            uv = [(x % 1 if x % 1 else (1 if x else 0), y % 1 if y % 1 else (1 if y else 0)) for x, y in uv]
            mesh.poly([rings[i][j], rings[i][k], rings[i+1][k], rings[i+1][j]], tone, True,
                      uvs=uv, normals=[normals[i][j], normals[i][k], normals[i+1][k], normals[i+1][j]])
    mesh.poly(list(reversed(rings[0])), color('bark', 2))
    mesh.poly(rings[-1], color('bark', 1))


def arc(a, b, bend, count):
    a, b, bend = Vector(a), Vector(b), Vector(bend)
    return [a.lerp(bend, t).lerp(bend.lerp(b, t), t) for t in [i/(count-1) for i in range(count)]]


def build_tree(name):
    spec = TREE_SPECS[name]
    height, radius, count = spec['height'], spec['radius'], spec['branches']
    RNG.seed(spec['seed'])
    mesh = Mesh()
    phase = RNG.uniform(0, math.tau)

    def stem(t):
        bend = spec['lean']*(math.sin(t*3.8)*.75 + t*t*.75)
        return Vector((bend, radius*.45*math.sin(t*4.5+phase)*t, height*t))

    trunk_ts = [i*.84/21 for i in range(22)]
    radii = [radius*(.88*(1-t)**1.1+.055+.50*math.exp(-t*25)) for t in trunk_ts]
    limb(mesh, [stem(t) for t in trunk_ts], radii, 24, phase, .085, name == 'ancient_oak')

    # Low, rounded buttresses flow into the flared foot; every root has a full underside.
    for j in range(7):
        angle = phase+j*2.399
        reach = radius*RNG.uniform(2.4, 3.4)
        points, widths = [], []
        for k in range(7):
            t = k/6
            a = angle+.12*math.sin(t*math.pi)
            points.append(Vector((math.cos(a)*reach*t, math.sin(a)*reach*t, radius*.95*(1-t)**2+.025)))
            widths.append(radius*(.32*(1-t)**1.35+.012))
        limb(mesh, points, widths, 8, phase+j, .045)

    crowns = []
    for j in range(count):
        angle = phase+j*2.399+RNG.uniform(-.18, .18)
        t0 = .235+j*.060
        reach = height*spec['spread']*RNG.uniform(.83, 1.12)
        end_height = height*(.40+j*.077)
        if name == 'branching_oak':
            end_height = height*(.50+j*.062)
            if j >= count-2:
                angle = phase+(0 if j == count-1 else 2.7)
                end_height = height*(.97 if j == count-1 else .90)
                reach *= .66
        if name == 'canopy_oak':
            end_height = height*(.40+j*.062)
            angle = phase+j*2.12
        end = stem(t0)+Vector((math.cos(angle)*reach, math.sin(angle)*reach, end_height-height*t0))
        path = arc(stem(t0), end, stem(t0)+Vector((math.cos(angle)*reach*.28, math.sin(angle)*reach*.28, height*.13)), 8)
        base_radius = radius*(.56-j*.047)
        limb(mesh, path, [base_radius*(1-k/7)**1.08+.027 for k in range(8)], 9, phase+j, .055)
        for k in range(2):
            a = angle+(-.56 if k == 0 else .65)+RNG.uniform(-.12, .12)
            extension = height*(.060 if name == 'branching_oak' else .070)
            tip = end+Vector((math.cos(a)*extension, math.sin(a)*extension, height*RNG.uniform(.025, .060)))
            off = arc(path[4+k], tip, end+Vector((0, 0, height*.015)), 5)
            limb(mesh, off, [radius*.14*(1-n/4)+.010 for n in range(5)], 6, a, .035)
            # Fine end forks remain visible through intentional canopy gaps.
            twig_tip = tip+Vector((math.cos(a+.65)*height*.035, math.sin(a+.65)*height*.035, height*.018))
            limb(mesh, [off[2], tip, twig_tip], [radius*.048, radius*.022, .004], 4, a, .02)
            size = Vector((height*.145, height*.115, height*.070))
            if name == 'branching_oak': size = Vector((height*.098, height*.088, height*.105))
            if name == 'canopy_oak': size = Vector((height*.155, height*.110, height*.065))
            crowns.append((tip.lerp(off[-2], .16), size, j))
        if j % 2 == 0:
            crowns.append((path[-3]+Vector((0,0,height*.035)), size*.94, j))
    crowns.append((stem(.88), Vector((height*.12, height*.105, height*.085)), count))

    # Raised knot rims and a few curved bark ribs provide actual close-range relief.
    for t, angle in [(.16, phase+.6), (.32, phase+3.4)]:
        out = Vector((math.cos(angle), math.sin(angle), 0))
        right = Vector((-math.sin(angle), math.cos(angle), 0))
        center = stem(t)+out*radius*(.95-t*.8)
        points = [center+right*math.cos(n*math.tau/12)*radius*.17+Vector((0,0,math.sin(n*math.tau/12)*radius*.29)) for n in range(13)]
        limb(mesh, points, [radius*.038]*13, 4, angle, .02)
    for j in range(3):
        angle = phase+j*1.7
        out = Vector((math.cos(angle), math.sin(angle), 0))
        points = [stem(t)+out*radius*(.93-t*.77) for t in [.04+k*.035 for k in range(7)]]
        limb(mesh, points, [radius*.045*math.sin(math.pi*(k+.5)/7) for k in range(7)], 4, angle, .02)

    wood_triangles = sum(len(face)-2 for face in mesh.faces)
    sprays = (7950-wood_triangles)//4
    assert sprays >= 750, (name, wood_triangles, sprays)
    # Balanced spiral shells give layered, full crowns instead of isolated leaf paddles.
    for index in range(sprays):
        cluster_index = index % len(crowns)
        c, size, branch = crowns[cluster_index]
        n = index//len(crowns)
        total = math.ceil((sprays-cluster_index)/len(crowns))
        z = 1-2*(n+.5)/total
        azimuth = n*2.399+cluster_index*.83
        radial = math.sqrt(max(0,1-z*z))
        direction = Vector((math.cos(azimuth)*radial, math.sin(azimuth)*radial, z))
        shell = .64 if n % 5 == 0 else RNG.uniform(.88, 1.04)
        p = c+Vector((direction.x*size.x, direction.y*size.y, direction.z*size.z))*shell
        length = height*RNG.uniform(.050, .071)
        tone_index = min(5, max(0, 2+int(direction.z*1.6)+(branch % 4 == 0)))
        mesh.sprig(p, length, length*RNG.uniform(.85, 1.13), color('leaf', tone_index),
                   direction*.78+Vector((0,0,.30)), tile=index % 4,
                   rotation=RNG.uniform(0,math.tau), cluster_center=c)
    obj = mesh.emit(name, 'tree', [{'position':[0,height*.34,0], 'size':[radius*1.8,height*.68,radius*1.8]}])
    info = BASE['MANIFEST']['assets'][-1]
    info.update({'variant':spec['label'], 'revision':2, 'wood_triangles':wood_triangles,
                 'leaf_sprays':sprays, 'crown_clusters':len(crowns), 'authored_height':height})
    obj['forest_tree_revision'] = 2
    obj['tree_variant'] = spec['label']
    print('TREE_DETAIL', json.dumps(info))
    return obj


def upgrade_layout(manifest):
    manifest['instances'] = [item for item in manifest['instances'] if not item['name'].startswith('DenseWoodland_')]
    old_trees = {a['name'] for a in manifest['assets'] if a['category'] == 'tree'}
    mapped = {'forest_column':('branching_oak', 1.10), 'understory_oak':('canopy_oak', .66)}
    trees = []
    for item in manifest['instances']:
        if item['asset'] not in old_trees: continue
        if item['asset'] in mapped:
            item['asset'], factor = mapped[item['asset']]
            item['scale'] *= factor
        trees.append(item)
    # Preserve all non-tree placements. New trunks avoid buildings and the lane.
    buildings = [item for item in manifest['instances'] if item['asset'] in {a['name'] for a in manifest['assets'] if a['category'] == 'building'}]
    assets = {a['name']:a for a in manifest['assets']}

    def safe(x, z):
        # Keep the new trunks and buttresses on the existing land mesh.
        if not (-25 < x < 27 and -43 < z < -3): return False
        lane = .8+max(0,-z)*.15
        if -35 < z and abs(x-lane) < (4.1 if z > -10 else 3.7): return False
        for building in buildings:
            dx, dz = x-building['position'][0], z-building['position'][2]
            a = math.radians(building['rotation_y'])
            local_x = math.cos(a)*dx-math.sin(a)*dz
            local_z = math.sin(a)*dx+math.cos(a)*dz
            boxes = assets[building['asset']]['collision']
            if any(abs(local_x-box['position'][0]) < box['size'][0]*building['scale']*.5+2.15 and
                   abs(local_z-box['position'][2]) < box['size'][2]*building['scale']*.5+2.15 for box in boxes): return False
        if any((x-t['position'][0])**2+(z-t['position'][2])**2 < 3.85**2 for t in trees): return False
        return True

    rng = random.Random(311042)
    candidates = []
    for row in range(9):
        for col in range(13):
            x = -28+col*4.6+(row % 2)*2.25+rng.uniform(-.75,.75)
            z = -8-row*5.4+rng.uniform(-.7,.7)
            if x < -10 or x > 12 or z < -30: candidates.append((x,z))
    # Fill the two woodland flanks first, then interleave the distant rows.
    candidates.sort(key=lambda p: (p[1] < -27, rng.random()))
    for _ in range(1500):
        x,z = rng.uniform(-24.8,26.8),rng.uniform(-42.8,-3.2)
        if x < -9 or x > 12 or z < -29: candidates.append((x,z))
    added = []
    for x,z in candidates:
        if not safe(x,z): continue
        asset = ['ancient_oak','branching_oak','canopy_oak'][len(added) % 3]
        scale = rng.uniform(.70,1.02) if z > -28 else rng.uniform(.62,.88)
        item = {'name':'DenseWoodland_%02d' % len(added), 'asset':asset,
                'position':[x,0,z], 'rotation_y':rng.uniform(0,360), 'scale':scale}
        trees.append(item)
        added.append(item)
        if len(added) == 30: break
    assert len(added) == 30, len(added)
    manifest['instances'].extend(added)
    return len(trees)


def finish_tree_upgrade():
    built = {a['name']:a for a in BASE['MANIFEST']['assets']}
    assert set(built) == set(TREE_SPECS), list(built)
    manifest = json.loads(json.dumps(ORIGINAL))
    tree_count = upgrade_layout(manifest)
    manifest['assets'] = [built.get(a['name'],a) for a in manifest['assets'] if a['category'] != 'tree' or a['name'] in TREE_SPECS]
    stage = bpy.data.collections['FT_TownLayout']
    active_tree_names = {item['name'] for item in manifest['instances'] if item['asset'] in TREE_SPECS}
    for obj in list(stage.objects):
        if obj.name in active_tree_names or obj.name.startswith('DenseWoodland_'):
            bpy.data.objects.remove(obj, do_unlink=True)
    for item in manifest['instances']:
        if item['asset'] not in TREE_SPECS: continue
        source = bpy.data.objects[item['asset']]
        obj = bpy.data.objects.new(item['name'],source.data)
        stage.objects.link(obj)
        x,y,z = item['position']
        obj.location = (x,-z,y)
        obj.rotation_euler.z = math.radians(item['rotation_y'])
        obj.scale = (item['scale'],)*3
        obj['forest_tree_revision'] = 2
    LIB.hide_viewport = False
    LIB.hide_render = False
    stage.hide_viewport = False
    stage.hide_render = False
    for obj in list(bpy.context.selected_objects): obj.select_set(False)
    for name in TREE_SPECS:
        obj = bpy.data.objects[name]
        obj.hide_set(False)
        obj.hide_render = False
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,
            export_apply=True,export_materials='EXPORT',export_yup=True,export_normals=True,
            export_vertex_color='ACTIVE',export_all_vertex_colors=False,export_cameras=False,export_lights=False)
        obj.select_set(False)
        obj.hide_set(True)
        obj.hide_render = True
    manifest['summary'] = {'unique_assets':len(manifest['assets']), 'placed_instances':len(manifest['instances']),
                           'max_asset_triangles':max(a['triangles'] for a in manifest['assets'])}
    manifest['tree_upgrade'] = {'revision':2,'variants':list(TREE_SPECS),'trees_before':48,'trees_after':tree_count,
                              'added_instances':30,'non_tree_placements_preserved':True}
    (META/'models.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    (META/'trees_v2.json').write_text(json.dumps({'summary':manifest['tree_upgrade'],'assets':list(built.values())},ensure_ascii=False,indent=2),encoding='utf8')
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'source/forest_town.blend'))
    print('TREE_UPGRADE_FINISHED',json.dumps(manifest['summary']), 'tree_instances',tree_count)
