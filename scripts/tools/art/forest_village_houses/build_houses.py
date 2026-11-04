"""Author the ten V2 concept houses in Blender; +Y rear, +Z up, door faces -Y.

Run definitions, then build(name) per asset, export_all(), save_source().
Every elevation is authored; no render-time geometry or imported AI mesh required.
"""
import bpy
import bmesh
import json
import math
import random
import tempfile
from pathlib import Path
from mathutils import Vector

ROOT = Path('D:/Project/GitHub/yu')
OUT = ROOT / 'assets/models/environment/forest_village/houses'
META = ROOT / 'assets/resources/art/forest_village/houses_models_v2.json'
CAPTURES = ROOT / '.godot/forest-village-houses'
for directory in (OUT, OUT / 'source', META.parent, CAPTURES):
    directory.mkdir(parents=True, exist_ok=True)
SCENE = bpy.data.scenes.get('ForestVillageHousesV2') or bpy.data.scenes.new('ForestVillageHousesV2')
bpy.context.window.scene = SCENE
SCENE.unit_settings.system = 'METRIC'
LIB = bpy.data.collections.get('FV_HousesV2')
if LIB is None:
    LIB = bpy.data.collections.new('FV_HousesV2')
    SCENE.collection.children.link(LIB)
RNG = random.Random(2026102608)
ASSETS = {}
SPECS = {}
TRIANGLE_BUDGET = 20000
SOURCE_TRIANGLE_BUDGET = 70000

# The project's grayscale atlas keeps slate, straw and turf colours independent.
CELLS = {'Plaster': (0, 0), 'Wood': (1, 0), 'WoodLight': (2, 0),
         'Roof': (3, 0), 'Stone': (0, 1), 'Glass': (2, 3),
         'Plant': (0, 2), 'Thatch': (2, 1), 'Turf': (0, 3), 'Iron': (2, 2)}
PALETTE = {
    'Plaster': ['e5d2aa', 'efdfbb', 'd7c59f', 'ebd9b4'],
    'Wood': ['674732', '75513a', '805b3f', '5d4333'],
    'WoodLight': ['a7794b', 'b48755', '956c45', 'bf945f'],
    'Roof': ['b76d48', 'c78350', 'a96145', 'ca8656'],
    'Stone': ['a7aa98', 'bcb8a3', '969e93', 'c2bba5'],
    'Glass': ['29434a', '35535a', '3b5c60'],
    'Plant': ['4e754b', '6b8d4f', '86a557', 'a9b568'],
    'Thatch': ['b69a55', 'c5a963', 'd3b774', 'b99c5a'],
    'Turf': ['607841', '7d9147', '8d9f51', '688643'],
    'Iron': ['3b4041', '4f514b', '5b594e']}
MATS = {}
atlas = bpy.data.images.load(str(ROOT/'assets/textures/environment/forest_village/houses/painterly_house_atlas_v3.png'), check_existing=True)
for label in CELLS:
    mat = bpy.data.materials.get('FV2_'+label) or bpy.data.materials.new('FV2_'+label)
    nodes = mat.node_tree.nodes
    bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Roughness'].default_value = .94
    spec = bsdf.inputs.get('Specular IOR Level')
    if spec:
        spec.default_value = .06
    vc = next((n for n in nodes if n.type == 'VERTEX_COLOR'), None) or nodes.new('ShaderNodeVertexColor')
    vc.layer_name = 'Palette'
    tex = next((n for n in nodes if n.type == 'TEX_IMAGE'), None) or nodes.new('ShaderNodeTexImage')
    tex.image = atlas
    mix = next((n for n in nodes if n.type == 'MIX_RGB'), None) or nodes.new('ShaderNodeMixRGB')
    mix.blend_type = 'MULTIPLY'
    mix.inputs[0].default_value = .5
    mat.node_tree.links.new(vc.outputs['Color'], mix.inputs[1])
    mat.node_tree.links.new(tex.outputs['Color'], mix.inputs[2])
    mat.node_tree.links.new(mix.outputs[0], bsdf.inputs['Base Color'])
    surface_root=ROOT/'assets/textures/environment/forest_village/houses'
    normal_path=surface_root/'painterly_house_atlas_v3_normal.png'
    rough_path=surface_root/'painterly_house_atlas_v3_roughness.png'
    if normal_path.exists() and rough_path.exists():
        normal_tex=next((n for n in nodes if n.label=='HouseNormal'),None) or nodes.new('ShaderNodeTexImage')
        normal_tex.label='HouseNormal';normal_tex.image=bpy.data.images.load(str(normal_path),check_existing=True)
        normal_tex.image.colorspace_settings.name='Non-Color'
        normal=next((n for n in nodes if n.type=='NORMAL_MAP'),None) or nodes.new('ShaderNodeNormalMap')
        normal.inputs['Strength'].default_value={'Plaster':.20,'Stone':.65,'Glass':.10,'Iron':.28}.get(label,.48)
        rough_tex=next((n for n in nodes if n.label=='HouseRoughness'),None) or nodes.new('ShaderNodeTexImage')
        rough_tex.label='HouseRoughness';rough_tex.image=bpy.data.images.load(str(rough_path),check_existing=True)
        rough_tex.image.colorspace_settings.name='Non-Color'
        mat.node_tree.links.new(normal_tex.outputs['Color'],normal.inputs['Color'])
        mat.node_tree.links.new(normal.outputs['Normal'],bsdf.inputs['Normal'])
        # glTF already expects roughness in G and metalness in B; use the baked
        # packed map so exporting needs no channel-repacking temporary images.
        orm_tex=next((n for n in nodes if n.label=='HouseORM'),None) or nodes.new('ShaderNodeTexImage')
        orm_tex.label='HouseORM';orm_tex.image=bpy.data.images.load(str(surface_root/'painterly_house_atlas_v3_orm.png'),check_existing=True)
        orm_tex.image.colorspace_settings.name='Non-Color'
        separate=next((n for n in nodes if n.type=='SEPARATE_COLOR'),None) or nodes.new('ShaderNodeSeparateColor')
        mat.node_tree.links.new(orm_tex.outputs['Color'],separate.inputs['Color'])
        mat.node_tree.links.new(separate.outputs['Green'],bsdf.inputs['Roughness'])
        mat.node_tree.links.new(separate.outputs['Blue'],bsdf.inputs['Metallic'])
    MATS[label] = mat


def rgb(value):
    if isinstance(value, (tuple, list)):
        return tuple(value)
    return tuple(int(value[i:i+2], 16)/255 for i in (0, 2, 4))+(1,)


def tone(label, value=None, variation=.035):
    result = rgb(value or RNG.choice(PALETTE[label]))
    f = 1+RNG.uniform(-variation, variation)
    return tuple(min(.98, max(.01, c*f)) for c in result[:3])+(1,)


class Mesh:
    def __init__(self, name):
        self.name = name
        self.vertices, self.faces, self.labels, self.colors = [], [], [], []
        self.collisions = []
        self.features = {side: [] for side in ('front', 'back', 'left', 'right')}
        self.door = (0, -2, .18)
        self.approach = (0, -2.8, .5)

    def poly(self, points, label='Wood', shade=None):
        points = [Vector(p) for p in points]
        if len(points) < 3 or (points[1]-points[0]).cross(points[-1]-points[0]).length < 1e-7:
            return
        start = len(self.vertices)
        self.vertices.extend(tuple(p) for p in points)
        self.faces.append(tuple(range(start, start+len(points))))
        self.labels.append(label)
        self.colors.append(tone(label, shade))

    def box(self, center, size, label='Wood', shade=None):
        c, s = Vector(center), Vector(size)*.5
        p = [c+Vector((x*s.x, y*s.y, z*s.z)) for x, y, z in
             [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        col = tone(label, shade)
        for f in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:
            self.poly([p[i] for i in f], label, col)

    def beam(self, a, b, width=.10, depth=None, label='Wood', shade=None):
        a, b = Vector(a), Vector(b)
        axis = (b-a).normalized()
        helper = Vector((0,0,1)) if abs(axis.z)<.95 else Vector((0,1,0))
        u = axis.cross(helper).normalized()*width*.5
        v = axis.cross(u).normalized()*(depth or width)*.5
        p = [p+u*x+v*y for p in (a,b) for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
        col = tone(label, shade)
        for f in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:
            self.poly([p[i] for i in f], label, col)

    def tube(self, a, b, r=.10, label='Wood', shade=None, sides=8):
        a, b = Vector(a), Vector(b)
        axis = (b-a).normalized()
        helper = Vector((0,0,1)) if abs(axis.z)<.95 else Vector((1,0,0))
        u = axis.cross(helper).normalized()
        v = axis.cross(u).normalized()
        rings = [[p+(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))*r for j in range(sides)] for p in (a,b)]
        for j in range(sides):
            k = (j+1)%sides
            self.poly([rings[0][j],rings[0][k],rings[1][k],rings[1][j]],label,shade)
        self.poly(list(reversed(rings[0])),label,shade)
        self.poly(rings[1],label,shade)

    def slab(self, points, thickness, label='Roof', shade=None):
        top = [Vector(p) for p in points]
        if (top[1]-top[0]).cross(top[-1]-top[0]).z < 0:
            top.reverse()
        low = [p-Vector((0,0,thickness)) for p in top]
        col = tone(label, shade)
        self.poly(top,label,col)
        self.poly(list(reversed(low)),label,col)
        for i in range(len(top)):
            j = (i+1)%len(top)
            self.poly([low[i],low[j],top[j],top[i]],label,col)

    def roof_panel(self, a, b, c, d, label='Roof', shade=None, course=.32, tile=.50):
        # a,b are eave ends; c,d are ridge ends. Each panel has a solid soffit.
        a,b,c,d = map(Vector,(a,b,c,d))
        self.slab([a,b,d,c],.10,label,shade)
        rows = max(2,round(((c-a).length+(d-b).length)*.5/course))
        cols = max(2,round((b-a).length/tile))
        for row in range(rows):
            t0, t1 = row/rows, min(1,(row+1.12)/rows)
            lo0, lo1 = a.lerp(c,t0), b.lerp(d,t0)
            hi0, hi1 = a.lerp(c,t1), b.lerp(d,t1)
            for j in range(cols):
                s0,s1 = j/cols, (j+1)/cols
                points = [lo0.lerp(lo1,s0),lo0.lerp(lo1,s1),hi0.lerp(hi1,s1),hi0.lerp(hi1,s0)]
                # Merge the converging last row of hip/cone faces into triangles.
                if (points[2]-points[3]).length < .001:
                    points = points[:3]
                self.slab([p+Vector((0,0,.018+row*.0005)) for p in points],.026,label,shade)

    def collider(self, center, size):
        x,y,z = center
        sx,sy,sz = size
        self.collisions.append({'position':[x,z,-y],'size':[sx,sz,sy]})

    def ramp(self, x, y, width, run, height):
        self.collisions.append({'shape':'convex','points':[[x+dx,z,-yy] for dx,yy,z in
            [(-width/2,y,0),(width/2,y,0),(-width/2,y+run,0),(width/2,y+run,0),(-width/2,y+run,height),(width/2,y+run,height)]]})

    def create(self):
        old = bpy.data.objects.get(self.name)
        if old:
            bpy.data.objects.remove(old,do_unlink=True)
        data = bpy.data.meshes.new(self.name+'_Mesh')
        data.from_pydata(self.vertices,[],self.faces)
        data.update()
        palette = data.color_attributes.new(name='Palette',type='BYTE_COLOR',domain='CORNER')
        uv = data.uv_layers.new(name='PaintedUV')
        used = list(dict.fromkeys(self.labels))
        mesh_min = [min(v.co[a] for v in data.vertices) for a in range(3)]
        mesh_span = [max(v.co[a] for v in data.vertices)-mesh_min[a] for a in range(3)]
        for label in used:
            data.materials.append(MATS[label])
        for face,label,col in zip(data.polygons,self.labels,self.colors):
            face.material_index = used.index(label)
            points = [data.vertices[data.loops[i].vertex_index].co for i in face.loop_indices]
            spans = [max(p[a] for p in points)-min(p[a] for p in points) for a in range(3)]
            axes = sorted(range(3),key=lambda a:spans[a],reverse=True)[:2]
            ua,va = axes
            if 2 in axes:
                ua,va = (axes[1] if axes[0]==2 else axes[0]),2
            if label in ('Wood', 'WoodLight', 'Thatch'):
                va,ua = axes[0],axes[1] # Brush grain follows the long beam/board axis.
            mins = [min(p[a] for p in points) for a in range(3)]
            if label == 'Plaster':
                # Keep plaster continuous across the separate cells around real openings.
                normal_axis = max(range(3), key=lambda a:abs(face.normal[a]))
                ua,va = [a for a in range(3) if a != normal_axis]
                mins,spans = mesh_min,mesh_span
            cx,cy = CELLS[label]
            for index,p in zip(face.loop_indices,points):
                palette.data[index].color_srgb = col
                u = (p[ua]-mins[ua])/max(spans[ua],1e-6)
                v = (p[va]-mins[va])/max(spans[va],1e-6)
                uv.data[index].uv = (cx*.25+.004+u*.242,1-((cy+1)*.25-.004)+v*.242)
        data.color_attributes.active_color = palette
        for index in getattr(self, 'smooth_faces', ()):
            data.polygons[index].use_smooth = True
        # Weld only exact coincident corners so each solid has outward, coherent normals.
        bm = bmesh.new()
        bm.from_mesh(data)
        bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
        bm.to_mesh(data)
        bm.free()
        data.update()
        data.calc_loop_triangles()
        tris = len(data.loop_triangles)
        assert tris <= SOURCE_TRIANGLE_BUDGET,(self.name,tris)
        obj = bpy.data.objects.new(self.name,data)
        LIB.objects.link(obj)
        obj['concept'] = 'res://assets/textures/concepts/forest_village/houses_v2/'+self.name+'.png'
        obj['complete_exterior'] = True
        obj['front_direction'] = '-Y Blender / +Z Godot'
        ASSETS[self.name] = obj
        bounds = [list(min(v.co[i] for v in data.vertices) for i in range(3)),list(max(v.co[i] for v in data.vertices) for i in range(3))]
        SPECS[self.name] = {'name':self.name,'file':self.name+'.glb','category':'building','triangles':tris,'polygons':len(data.polygons),'vertices':len(data.vertices),'bounds_blender':bounds,'collision':self.collisions,'door':[self.door[0],self.door[2],-self.door[1]],'approach':[self.approach[0],self.approach[2],-self.approach[1]],'elevation_features':self.features,'complete_exterior':True,'surfaces':used}
        show(self.name)
        print(self.name,tris,'triangles',len(self.collisions),'simple collision volumes',self.features)
        return obj


def show(name):
    for obj in LIB.objects:
        obj.hide_set(obj.name != name)
        obj.hide_render = obj.name != name
    bpy.context.view_layer.objects.active = bpy.data.objects.get(name)


def plane_point(origin, tangent, normal, u, outward, z):
    return Vector(origin)+Vector(tangent)*u+Vector(normal)*outward+Vector((0,0,z))


def facade_box(m, origin, tangent, normal, u, outward, z, size, label='Wood', shade=None):
    # Box in the local elevation frame, including sides and rear faces.
    origin = plane_point(origin,tangent,normal,u,outward,z)
    a = origin-Vector(tangent)*size[0]*.5
    b = origin+Vector(tangent)*size[0]*.5
    m.beam(a,b,size[1],size[2],label,shade)


def opening(m, origin, tangent, normal, u, z, width, height, door=False, shutters=True, shade=None, label=None, side='front'):
    P = lambda a,b,c: plane_point(origin,tangent,normal,a,b,c)
    label = label or 'Wood'
    frame = 'WoodLight'
    base = z-height*.5
    # Pane/door lies behind the actual wall opening, with visible jamb depth.
    facade_box(m,origin,tangent,normal,u,-.075,z,(width,.055,height),'Wood' if door else 'Glass',shade if door else None)
    for sign in (-1,1):
        facade_box(m,origin,tangent,normal,u+sign*(width*.5+.045),.015,z,(.09,.22,height+.15),frame)
    for dz in (-height*.5-.035,height*.5+.035):
        facade_box(m,origin,tangent,normal,u,.02,z+dz,(width+.18,.22,.08),frame)
    if door:
        for j in range(max(4,round(width/.14))):
            x = u-width*.5+(j+.5)*width/max(4,round(width/.14))
            facade_box(m,origin,tangent,normal,x,-.034,z,(width/max(4,round(width/.14))-.012,.025,height-.035),label,shade)
        for dz in (-height*.28,height*.28):
            facade_box(m,origin,tangent,normal,u,.003,z+dz,(width-.07,.045,.055),'Iron')
        m.tube(P(u+width*.28,.085,z-.02),P(u+width*.28,.105,z-.02),.045,'Iron',sides=8)
        m.features[side].append('storage_door' if width>1.05 else 'door')
    else:
        facade_box(m,origin,tangent,normal,u,-.012,z,(.035,.04,height),'WoodLight')
        facade_box(m,origin,tangent,normal,u,-.012,z+.02,(width,.04,.035),'WoodLight')
        if shutters:
            for sign in (-1,1):
                x = u+sign*(width*.5+.15)
                facade_box(m,origin,tangent,normal,x,.045,z,(.22,.06,height+.02),'Wood',shade)
                facade_box(m,origin,tangent,normal,x,.08,z-height*.27,(.23,.035,.045),'WoodLight')
                facade_box(m,origin,tangent,normal,x,.08,z+height*.27,(.23,.035,.045),'WoodLight')
        facade_box(m,origin,tangent,normal,u,.11,base-.095,(width+.3,.32,.095),'WoodLight')
        m.features[side].append('recessed_window')


def wall(m, origin, tangent, normal, length, height, holes=(), label='Plaster', shade=None, boards=False, side='front', base=.16, timber=True):
    xs = sorted(set([-length*.5,length*.5]+[v for h in holes for v in (h[0]-h[2]/2,h[0]+h[2]/2)]))
    zs = sorted(set([base,base+height]+[v for h in holes for v in (h[1]-h[3]/2,h[1]+h[3]/2)]))
    for a,b in zip(xs,xs[1:]):
        for low,high in zip(zs,zs[1:]):
            if a < -length*.5-.01 or b>length*.5+.01 or low<base-.001 or high>base+height+.001:
                continue
            u,z=(a+b)*.5,(low+high)*.5
            if any(abs(u-h[0])<h[2]/2-.001 and abs(z-h[1])<h[3]/2-.001 for h in holes):
                continue
            facade_box(m,origin,tangent,normal,u,-.08,z,(b-a,.16,high-low),label,shade)
    facade_box(m,origin,tangent,normal,0,-.045,base-.085,(length+.06,.23,.17),'Stone')
    facade_box(m,origin,tangent,normal,0,.015,base+height-.02,(length+.12,.20,.13),'Wood')
    if timber:
        for u in (-length*.5+.025,length*.5-.025):
            facade_box(m,origin,tangent,normal,u,.012,base+height/2,(.13,.18,height+.03),'Wood')
    if boards:
        for j in range(max(2,round(length/.28))):
            u = -length*.5+(j+.5)*length/max(2,round(length/.28))
            segments = [(base,base+height)]
            for h in holes:
                if abs(u-h[0])<h[2]*.5+.02:
                    segments = [(a,min(b,h[1]-h[3]/2)) for a,b in segments if a<h[1]-h[3]/2]+[(max(a,h[1]+h[3]/2),b) for a,b in segments if b>h[1]+h[3]/2]
            for a,b in segments:
                if b-a>.04:
                    facade_box(m,origin,tangent,normal,u,.006,(a+b)/2,(.035,.025,b-a),'WoodLight',shade)
    for h in holes:
        opening(m,origin,tangent,normal,*h[:4],door=h[4]=='door',shutters=h[4]!='vent',shade=h[5] if len(h)>5 else None,side=side)


def room(m, center, width, depth, height, label='Plaster', shade=None, holes=None, base=.16, boards=False, timber=True, collision=True):
    x,y=center
    holes=holes or {}
    for side,origin,tangent,normal,length in [
        ('front',(x,y-depth/2,0),(1,0,0),(0,-1,0),width),
        ('back',(x,y+depth/2,0),(-1,0,0),(0,1,0),width),
        ('right',(x+width/2,y,0),(0,1,0),(1,0,0),depth),
        ('left',(x-width/2,y,0),(0,-1,0),(-1,0,0),depth)]:
        wall(m,origin,tangent,normal,length,height,holes.get(side,()),label,shade,boards,side,base,timber)
    m.box((x,y,base-.06),(width,depth,.12),'Stone')
    if collision:
        m.collider((x,y,base+height/2),(width,depth,height))


def standard_holes(width,depth,door_x=0,base=.16):
    return {'front':[(door_x,base+.69,.78,1.38,'door'),(-width*.31,base+1.0,.55,.65,'window')],
            'back':[(0,base+1.0,.60,.65,'window')],
            'left':[(0,base+1.0,.60,.65,'window')],
            'right':[(0,base+1.0,.60,.65,'window')]}


def hip_roof(m,center,width,depth,eave,rise,label='Roof',shade=None):
    x,y=center
    cut=min(depth*.43,width*.32)
    p=[(x-width/2,y-depth/2,eave),(x+width/2,y-depth/2,eave),(x+width/2,y+depth/2,eave),(x-width/2,y+depth/2,eave)]
    a=(x-width/2+cut,y,eave+rise)
    b=(x+width/2-cut,y,eave+rise)
    for low0,low1,high0,high1 in [(p[0],p[1],a,b),(p[2],p[3],b,a),(p[3],p[0],a,a),(p[1],p[2],b,b)]:
        m.roof_panel(low0,low1,high0,high1,label,shade,course=.36,tile=.60)
    for i in range(4):
        m.beam(p[i],p[(i+1)%4],.12,.13,'Wood')
    for end,peak in [(p[0],a),(p[3],a),(p[1],b),(p[2],b)]:
        m.beam(end,peak,.11,.12,label,shade)
    m.beam(a,b,.12,.12,label,shade)


def gable_roof(m,center,width,depth,eave,rise,label='Roof',shade=None,clip=0,cap=True):
    x,y=center
    for sign in (-1,1):
        m.roof_panel((x+sign*width/2,y-depth/2,eave),(x+sign*width/2,y+depth/2,eave),(x,y-depth/2+clip,eave+rise),(x,y+depth/2-clip,eave+rise),label,shade)
        m.beam((x+sign*width/2,y-depth/2,eave-.06),(x+sign*width/2,y+depth/2,eave-.06),.13,.14,'Wood')
    for sign in (-1,1):
        yy=y+sign*depth/2
        peak=(x,yy-sign*clip,eave+rise)
        if clip:
            m.roof_panel((x-width/2,yy,eave),(x+width/2,yy,eave),peak,peak,label,shade,course=.36,tile=.6)
        elif cap:
            m.poly([(x-width/2,yy,eave),(x+width/2,yy,eave),(x,yy,eave+rise)],'Wood')
        for xx in (x-width/2,x+width/2):
            m.beam((xx,yy,eave),peak,.14,.16,'Wood')
    m.beam((x,y-depth/2+clip,eave+rise+.02),(x,y+depth/2-clip,eave+rise+.02),.13,.15,label,shade)


def mono_roof(m,center,width,depth,front,back,label='Roof',shade=None):
    x,y=center
    m.roof_panel((x-width/2,y-depth/2,front),(x+width/2,y-depth/2,front),(x-width/2,y+depth/2,back),(x+width/2,y+depth/2,back),label,shade)
    for yy,z in [(y-depth/2,front),(y+depth/2,back)]:
        m.beam((x-width/2,yy,z-.07),(x+width/2,yy,z-.07),.12,.16,'Wood')
    for xx in (x-width/2,x+width/2):
        m.beam((xx,y-depth/2,front-.07),(xx,y+depth/2,back-.07),.12,.16,'Wood')


def close_mono(m,center,width,depth,body_top,roof_center,roof_depth,front,back,label='Wood',shade=None):
    x,y=center
    ry=roof_center[1]
    roof_z=lambda yy:front+(yy-(ry-roof_depth/2))/roof_depth*(back-front)-.10
    corners=[(x-width/2,y-depth/2),(x+width/2,y-depth/2),(x+width/2,y+depth/2),(x-width/2,y+depth/2)]
    for i in range(4):
        a,b=corners[i],corners[(i+1)%4]
        m.poly([(*a,body_top),(*b,body_top),(*b,roof_z(b[1])),(*a,roof_z(a[1]))],label,shade)


def chimney(m,x,y,z,height=.85,width=.45):
    m.box((x,y,z+height/2),(width,width,height),'Stone')
    for j in range(max(2,round(height/.22))):
        zz=z+(j+.5)*height/max(2,round(height/.22))
        for side in (-1,1):
            m.box((x+side*(width/2+.009),y+(j%2-.5)*.06,zz),(.025,width*.85,.035),'Stone','8a8e80')
    m.box((x,y,z+height+.04),(width+.16,width+.16,.10),'Stone')
    m.box((x,y,z+height+.103),(width-.11,width-.11,.025),'Iron')


def steps(m,x,y,width=1.05,height=.24,count=2):
    if not hasattr(m,'stairs'):m.stairs=[]
    m.stairs.append({'x':x,'y':y,'width':width,'height':height,'inner_edge':y+.15+(count-1)*.25+.16})
    for i in range(count):
        h=height*(i+1)/count
        m.box((x,y+.15+i*.25,h/2),(width,.32,h),'Stone')
    m.ramp(x,y-.04,width+.08,count*.25+.10,height)


def posts(m,xs,y,top,base=.16,braces=True):
    if not hasattr(m,'posts'):m.posts=[]
    for x in xs:
        m.posts.append({'x':x,'y':y,'bottom':base,'top':top,'half_width':.08})
        m.box((x,y,base/2),(.29,.29,base),'Stone')
        m.beam((x,y,base),(x,y,top),.13,.16,'Wood')
        if braces:
            for sign in (-1,1):
                m.beam((x,y,top-.38),(x+sign*.30,y,top-.07),.08,.09,'WoodLight')
    m.beam((min(xs)-.10,y,top),(max(xs)+.10,y,top),.15,.18,'Wood')


def bench(m,x,y,length=1.25,z=.38):
    m.box((x,y,z),(length,.35,.10),'WoodLight')
    for dx in (-length*.36,length*.36):
        m.box((x+dx,y,z/2),(.11,.28,z),'Wood')
    m.box((x,y+.15,z+.22),(length,.07,.14),'Wood')


def crate(m,x,y,z=0,size=.42):
    m.box((x,y,z+size/2),(size,size,size),'Wood')
    for side in (-1,1):
        for zz in (.10,.30):
            m.box((x,y+side*size*.51,z+zz),(size+.025,.035,.055),'WoodLight')
        m.box((x+side*size*.41,y-size*.53,z+size*.5),(.055,.03,size),'WoodLight')


def logs(m,x,y,count=4,length=1.3):
    for j in range(count):
        xx=x+(j%3)*.22
        zz=.12+(j//3)*.19
        m.tube((xx,y-length/2,zz),(xx,y+length/2,zz),.105,'Wood',sides=7)
        m.tube((xx,y-length/2-.005,zz),(xx,y-length/2-.016,zz),.085,'WoodLight',sides=7)


def planter(m,x,y,z=.4,length=.70):
    m.box((x,y,z),(length,.25,.18),'WoodLight')
    for j in range(5):
        xx=x+length*(j/5-.4)
        m.box((xx,y,z+.15),(.12,.15,RNG.uniform(.12,.22)),'Plant')


def stone_patches(m,center,width,depth,height=1.8,holes=None):
    # Small, irregular dressed stones on every elevation rather than an entire noisy brick grid.
    holes=holes or {}
    x,y=center
    for side,origin,tangent,normal,length in [('front',(x,y-depth/2,0),(1,0,0),(0,-1,0),width),('back',(x,y+depth/2,0),(-1,0,0),(0,1,0),width),('right',(x+width/2,y,0),(0,1,0),(1,0,0),depth),('left',(x-width/2,y,0),(0,-1,0),(-1,0,0),depth)]:
        for row in range(4):
            z=.32+row*height/4
            for j in range(max(2,round(length/.65))):
                u=-length/2+(j+.45+(row%2)*.18)*length/max(2,round(length/.65))
                if abs(u)>length/2-.15 or any(abs(u-h[0])<h[2]/2+.12 and abs(z-h[1])<h[3]/2+.13 for h in holes.get(side,())):
                    continue
                facade_box(m,origin,tangent,normal,u,.02,z,(RNG.uniform(.23,.44),.065,RNG.uniform(.13,.21)),'Stone')


def build_chief(m):
    holes=standard_holes(5.6,2.6,door_x=.35)
    holes['front']=[(.35,.85,.82,1.38,'door'),(-1.65,1.33,.60,.42,'window')]
    holes['back']=[(-1.5,1.2,.58,.52,'window'),(.35,.86,.76,1.4,'door'),(1.8,1.2,.55,.52,'window')]
    room(m,(0,.45),5.6,2.6,1.85,holes=holes)
    hip_roof(m,(0,.12),6.25,3.75,2.13,.88)
    # Low return behind/right of the porch; both outer elevations receive windows.
    wh={'front':[(0,1.25,.55,.45,'window')],'back':[(0,1.23,.56,.48,'window')], 'right':[(.5,1.2,.55,.55,'window')],'left':[]}
    room(m,(2.25,-.35),1.55,3.7,1.8,holes=wh)
    hip_roof(m,(2.25,-.45),2.08,4.28,2.07,.81)
    m.box((-.45,-1.45,.08),(5.0,1.1,.16),'Stone')
    m.collider((-.45,-1.45,.08),(5,1.1,.16))
    posts(m,[-2.8,-1.7,-.65,1.15],-1.95,2.03)
    bench(m,-1.9,-1.28,1.25)
    bench(m,1.2,-1.25,.5)
    m.box((-.55,-1.22,.65),(.75,.45,.08),'WoodLight')
    for dx in (-.27,.27): m.box((-.55+dx,-1.22,.35),(.07,.35,.6),'Wood')
    crate(m,1.3,2.07,size=.5)
    steps(m,.35,-2.55,1.12,.16,2)
    chimney(m,-1.5,1.1,2.5,.65,.38)
    m.door=(.35,-.85,.16)
    m.approach=(.35,-2.85,.5)


def build_host(m):
    ground={'front':[(.74,.87,.68,1.42,'door'),(-.55,1.1,1.05,.72,'window')],
            'back':[(0,.9,1.2,1.45,'door')], 'left':[(.12,1.12,.55,.63,'window')],'right':[(.1,1.12,.50,.6,'window')]}
    room(m,(0,0),2.6,2.7,1.65,'Stone',holes=ground,timber=False)
    stone_patches(m,(0,0),2.6,2.7,1.65,ground)
    for level,(w,d,base,h) in enumerate([(2.9,2.95,1.81,1.38),(3.05,3.05,3.19,1.32)]):
        hs={side:[(0,base+.7,.6,.73,'window')] for side in ('front','back','left','right')}
        if level==1: hs['right']=[(0,base+.65,.62,1.25,'door')]
        room(m,(0,0),w,d,h,'Wood',shade='986942',holes=hs,base=base,boards=True,collision=True)
        for x in (-w*.38,w*.38):
            for yy in (-d*.5,d*.5):
                m.beam((x,yy,base-.30),(x,yy*1.08,base+.03),.09,.11,'Wood')
    gable_roof(m,(0,0),3.5,3.6,4.69,1.15,clip=.42)
    chimney(m,-.75,.7,5.1,.8,.34)
    # Side guest balcony with floor, brackets and rails, all backed by a real door.
    m.box((1.95,0,3.24),(.9,1.55,.13),'Wood')
    for yy in (-.68,.68):
        m.beam((1.5,yy,2.75),(2.35,yy,3.19),.09,.11,'Wood')
        m.beam((2.32,yy,3.27),(2.32,yy,3.95),.07,.08,'Wood')
    m.beam((2.32,-.70,3.95),(2.32,.70,3.95),.07,.09,'WoodLight')
    for yy in (-.4,0,.4): m.beam((2.32,yy,3.3),(2.32,yy,3.93),.045,.05,'Wood')
    # Canvas front awning, not a second house wing.
    for j in range(5):
        x=-1.15+j*.24
        for k in range(4):
            t0,t1=k/4,(k+1)/4
            y0,y1=-1.37-.66*t0,-1.37-.66*t1
            z0,z1=1.68-.33*t0-.055*math.sin(math.pi*t0),1.68-.33*t1-.055*math.sin(math.pi*t1)
            m.slab([(x,y0,z0),(x+.24,y0,z0),(x+.24,y1,z1),(x,y1,z1)],.018,'Plaster','d9bc7c' if j%2 else 'eee0b5')
        m.poly([(x,-2.03,1.35),(x+.24,-2.03,1.35),(x+.22,-2.036,1.28),(x+.12,-2.04,1.25),(x+.02,-2.036,1.28)],'Plaster','d9bc7c' if j%2 else 'eee0b5')
    m.box((-.55,-1.58,.62),(1.1,.48,.12),'WoodLight')
    for x in (-1.06,-.04): m.beam((x,-1.78,.15),(x,-1.78,1.35),.065,.08,'Wood')
    crate(m,-1.62,-1.60,size=.4)
    steps(m,.74,-2.05,.95,.16,2)
    m.door=(.74,-1.35,.16)
    m.approach=(.74,-2.6,.5)


def build_smith(m):
    h=standard_holes(3.8,3.1,door_x=-.3)
    h['front']=[(-.3,.85,.78,1.38,'door'),(-1.25,1.14,.45,.57,'window'),(1.15,1.14,.45,.57,'window')]
    room(m,(-.65,.1),3.8,3.1,1.85,'Stone',holes=h,timber=False)
    stone_patches(m,(-.65,.1),3.8,3.1,1.85,h)
    mono_roof(m,(-.65,.1),4.35,3.65,2.12,2.75,'Roof','616d70')
    close_mono(m,(-.65,.1),3.8,3.1,2.01,(-.65,.1),3.65,2.12,2.75,'Stone')
    # Solid wedge closes the high side and both slope edges.
    x0,x1,y0,y1=-2.55,1.25,-1.45,1.65
    for xx in (x0,x1):
        m.poly([(xx,y0,2.01),(xx,y1,2.01),(xx,y1,2.65)],'Stone')
    m.box((-.65,1.58,2.32),(3.8,.14,.63),'Stone')
    chimney(m,-2.0,.95,2.38,1.15,.58)
    # Fireproof separate small repair canopy, with a visible gap from the home.
    for xx in (2.0,3.35):
        for yy in (.50,1.5):
            top=1.65+(yy-.175)/1.65*.33-.09
            m.box((xx,yy,top/2),(.22,.22,top),'Stone')
    for yy in (.50,1.5):
        top=1.65+(yy-.175)/1.65*.33-.09
        m.beam((1.92,yy,top),(3.43,yy,top),.12,.16,'Wood')
    mono_roof(m,(2.67,1.0),1.95,1.65,1.65,1.98,'Roof','657274')
    m.box((2.67,1.36,.56),(1.0,.50,1.12),'Stone')
    m.box((2.67,1.08,.60),(.58,.07,.48),'Iron')
    m.box((2.67,.95,.38),(.86,.7,.12),'Stone')
    m.box((2.55,-.15,.3),(.35,.35,.6),'Wood')
    m.box((2.55,-.15,.65),(.60,.25,.14),'Iron')
    m.collider((2.67,1.30,.55),(1.1,.65,1.1))
    m.collider((2.55,-.15,.4),(.65,.35,.8))
    for j in range(3):
        m.beam((1.3+j*.18,-.2,.1),(1.3+j*.18,-.2,1.1),.035,.045,'WoodLight')
        m.box((1.3+j*.18,-.2,1.12),(.17,.07,.11),'Iron')
    steps(m,-.95,-2.2,.98,.16,2)
    m.door=(-.95,-1.45,.16)
    m.approach=(-.95,-2.75,.5)


def build_carpenter(m):
    hs=standard_holes(1.9,2.8,door_x=.18)
    hs['front']=[(.18,.85,.76,1.38,'door')]
    room(m,(-1.95,.2),1.9,2.8,1.85,'Wood',holes=hs,boards=True)
    # Rear enclosed store wall completes the open working bays.
    wall(m,(.8,1.6,0),(-1,0,0),(0,1,0),3.65,1.85,[(0,1.15,.65,.55,'window')],'Wood',boards=True,side='back')
    m.box((.8,.1,.09),(3.7,2.9,.18),'Wood')
    m.collider((.8,.1,.09),(3.7,2.9,.18))
    posts(m,[-.96,.27,1.5,2.73],-1.55,2.08)
    # Saltbox long front slope and shorter steep rear slope.
    m.roof_panel((-3.2,-1.92,2.03),(3.05,-1.92,2.03),(-3.2,.85,3.16),(3.05,.85,3.16),'WoodLight','a47e50',course=.40,tile=.62)
    m.roof_panel((-3.2,1.95,2.11),(3.05,1.95,2.11),(-3.2,.85,3.16),(3.05,.85,3.16),'WoodLight','a47e50',course=.4,tile=.62)
    for xx in (-3.13,2.98):
        m.beam((xx,-1.92,2.01),(xx,.85,3.16),.13,.14,'Wood')
        m.beam((xx,1.95,2.09),(xx,.85,3.16),.13,.14,'Wood')
        m.poly([(xx,-1.3,2.0),(xx,1.6,2.0),(xx,.85,3.13)],'Wood')
    m.beam((-3.2,.85,3.17),(3.05,.85,3.17),.15,.16,'Wood')
    facade_box(m,(0,1.6,0),(-1,0,0),(0,1,0),-.0,-.03,2.177,(5.65,.15,.334),'Wood')
    facade_box(m,(-1.95,-1.2,0),(1,0,0),(0,-1,0),0,-.03,2.12,(1.9,.15,.22),'Wood')
    for xx in (-2.9,-1.0):
        m.poly([(xx,-1.2,2.01),(xx,.85,2.01),(xx,.85,3.06),(xx,-1.2,2.226)],'Wood')
        m.poly([(xx,.85,2.01),(xx,1.6,2.01),(xx,1.6,2.344),(xx,.85,3.06)],'Wood')
    for j in range(5):
        m.box((1.97,1.18,.18+j*.12),(1.1,.55,.09),'WoodLight')
    logs(m,-.48,.60,6,1.35)
    m.box((.85,-.45,.70),(1.4,.55,.10),'WoodLight')
    for dx in (-.5,.5):
        for yy in (-.61,-.29): m.beam((.85+dx,yy,.17),(.85+dx,yy,.67),.08,.09,'Wood')
    m.collider((.85,-.45,.45),(1.4,.55,.9))
    chimney(m,-2.35,1.0,2.95,.55,.34)
    steps(m,-1.77,-2.15,.98,.16,2)
    m.door=(-1.77,-1.2,.16)
    m.approach=(-1.77,-2.7,.5)


def build_herbalist(m):
    radius=1.7
    points=[Vector((math.cos(math.radians(-120+j*60))*radius,math.sin(math.radians(-120+j*60))*radius,0)) for j in range(6)]
    for j in range(6):
        a,b=points[j],points[(j+1)%6]
        tangent=(b-a).normalized()
        normal=tangent.cross(Vector((0,0,1)))
        origin=(a+b)/2
        side='front' if normal.y<-.5 else ('back' if normal.y>.5 else ('right' if normal.x>0 else 'left'))
        hs=[(0,.85,.76,1.38,'door')] if j==0 else [(0,1.1,.55,.64,'window')]
        wall(m,origin,tangent,normal,(b-a).length,1.8,hs,'Plaster','e4ddbc',side=side,timber=False)
    # Hexagonal solid collision (no generic box that would block the side route).
    m.collisions.append({'shape':'convex','points':[[p.x,z,-p.y] for z in (.02,1.96) for p in points]})
    eaves=[Vector((p.x*1.24,p.y*1.24,2.04)) for p in points]
    for j in range(6):
        apex=(0,0,3.88)
        m.roof_panel(eaves[j],eaves[(j+1)%6],apex,apex,'Roof','6c8050',course=.38,tile=.55)
        m.beam(eaves[j],eaves[(j+1)%6],.10,.12,'Wood')
    chimney(m,0,.0,3.77,.35,.25)
    # Low household preparation lean-to, closed at the rear and outer side.
    room(m,(2.1,.35),1.4,2.1,1.35,'Plaster','dcd4b3',holes={'front':[(0,.86,.72,.95,'window')],'right':[(0,.85,.55,.60,'window')],'back':[(0,.85,.6,.5,'window')]},timber=True)
    mono_roof(m,(2.1,.35),1.9,2.6,1.64,1.95,'Roof','778650')
    close_mono(m,(2.1,.35),1.4,2.1,1.51,(2.1,.35),2.6,1.64,1.95,'Plaster','dcd4b3')
    planter(m,2.1,-1.04,.28,.9)
    for j in range(3):
        m.beam((1.65+j*.30,-.77,1.15),(1.65+j*.30,-.77,1.53),.025,.03,'Wood')
        m.box((1.65+j*.30,-.77,1.23),(.11,.12,.19),'Plant')
    steps(m,0,-2.22,.98,.16,2)
    m.door=(0,-1.472,.16)
    m.approach=(0,-2.8,.5)


def build_forester(m):
    width,depth,eave,rise=3.9,3.6,.28,3.48
    # Entire triangular front and rear built from structural logs; doorway is cut out.
    for side,yy in [('front',-depth/2),('back',depth/2)]:
        # A continuous infill behind the logs seals the taper at both roof edges.
        # Cut the doorway and windows through this infill as well as the log courses.
        holes=[(-.39,.39,.23,1.61),(-.175,.175,2.07,2.41)] if side=='front' else [(-.36,.36,.795,1.445),(-.175,.175,2.07,2.41)]
        cuts=sorted(set([.16,eave+rise]+[v for h in holes for v in h[2:]]))
        backing_y=yy+(.045 if side=='front' else -.045)
        half_at=lambda z:(width+.32)/2*max(0,1-(z-eave)/rise)
        for low,high in zip(cuts,cuts[1:]):
            mid=(low+high)/2
            active=next((h for h in holes if h[2]<mid<h[3]),None)
            if active:
                spans=[(-half_at(low),active[0],-half_at(high),active[0]),(active[1],half_at(low),active[1],half_at(high))]
            else:
                spans=[(-half_at(low),half_at(low),-half_at(high),half_at(high))]
            for lo0,lo1,hi0,hi1 in spans:
                poly=[(lo0,backing_y,low),(lo1,backing_y,low),(hi1,backing_y,high),(hi0,backing_y,high)]
                m.poly(poly if side=='front' else list(reversed(poly)),'Wood','785937')
        for j in range(20):
            z=.22+j*.19
            half=width/2*max(.0,1-(z-eave)/rise)-.10
            if half<.08: continue
            ranges=[(-half,half)]
            if side=='front' and z<1.66:
                ranges=[(-half,-.43),(.43,half)]
            if side=='back' and .8<z<1.45:
                ranges=[(-half,-.40),(.40,half)]
            if 2.03<z<2.45:
                ranges=[(-half,-.23),(.23,half)]
            for a,b in ranges:
                if b-a>.08: m.tube((a,yy,z),(b,yy,z),.105,'Wood','98734a',sides=7)
        normal=(0,-1,0) if side=='front' else (0,1,0)
        tangent=(1,0,0) if side=='front' else (-1,0,0)
        if side=='front': opening(m,(0,yy,0),tangent,normal,0,.92,.78,1.38,door=True,side=side)
        else: opening(m,(0,yy,0),tangent,normal,0,1.12,.72,.65,side=side)
        opening(m,(0,yy,0),tangent,normal,0,2.24,.35,.34,shutters=False,side=side)
    gable_roof(m,(0,0),width+.32,depth+.7,eave,rise,'Roof','786044',cap=False)
    m.box((0,0,.10),(3.75,3.6,.20),'Stone')
    # A triangular prism follows the low roof edge instead of a full-width tall box.
    m.collisions.append({'shape':'convex','points':[[x,z,-y] for y in (-1.8,1.8) for x,z in [(-2.05,.05),(2.05,.05),(0,3.8)]]})
    # Both long roof elevations have complete eaves, log-end details and a low store hatch.
    for sign,side in [(-1,'left'),(1,'right')]:
        m.box((sign*1.58,.55,.40),(.10,.62,.38),'Wood')
        m.features[side].append('eave_storage_hatch')
        for yy in (-1.5,1.5): m.beam((sign*1.93,yy,.22),(sign*1.73,yy,.61),.1,.11,'WoodLight')
        pane=[]
        for xx,yy in [(.86,-.36),(1.30,-.36),(1.30,.32),(.86,.32)]:
            zz=eave+rise*(1-xx/((width+.32)/2))+.065
            pane.append((sign*xx,yy,zz))
        m.poly(pane,'Glass')
        for j in range(4): m.beam(pane[j],pane[(j+1)%4],.065,.07,'WoodLight')
        m.features[side].append('framed_roof_window')
    steps(m,0,-2.6,1.15,.23,2)
    chimney(m,.65,.72,3.05,.90,.34)
    m.door=(0,-1.8,.23)
    m.approach=(0,-3.0,.5)


def build_gatherer(m):
    hs=standard_holes(3.6,2.7,door_x=-.7,base=.75)
    hs['front']=[(-.7,1.44,.75,1.38,'door'),(.65,1.8,.62,.64,'window')]
    hs['back']=[(0,1.42,.75,1.3,'door'),(-1.1,1.8,.5,.58,'window')]
    room(m,(0,.25),3.6,2.7,1.70,'Wood','8a8067',holes=hs,base=.75,boards=True)
    # Close wedge under mono-pitch top along side/rear walls.
    for xx in (-1.8,1.8): m.poly([(xx,-1.1,2.45),(xx,1.6,2.45),(xx,1.6,2.92)],'Wood','8a8067')
    m.box((0,1.58,2.68),(3.6,.14,.48),'Wood','8a8067')
    mono_roof(m,(.3,.1),4.95,4.15,2.40,3.12,'Wood','665c49')
    m.box((.30,-1.65,.68),(4.6,1.15,.14),'WoodLight')
    m.collider((.3,-1.65,.68),(4.6,1.15,.14))
    m.box((2.15,.25,.68),(.70,3.8,.14),'WoodLight')
    m.collider((2.15,.25,.68),(.70,3.8,.14))
    m.box((0,1.95,.68),(3.85,.70,.14),'WoodLight')
    m.collider((0,1.95,.68),(3.85,.70,.14))
    for xx in (-1.8,1.8):
        m.beam((xx,2.25,.07),(xx,2.25,.68),.12,.14,'Wood')
        m.box((xx,2.25,.07),(.27,.27,.14),'Stone')
    for x in (-1.9,-.4,1.0,2.5):
        for yy in (-2.12,1.6):
            m.box((x,yy,.34),(.16,.16,.68),'Wood')
            m.box((x,yy,.07),(.30,.30,.14),'Stone')
    posts(m,[-1.9,2.5],-2.12,2.38,base=.72,braces=False)
    for a,b in [((-1.9,-2.12),(-1.3,-2.12)),((-.1,-2.12),(2.5,-2.12)),((2.5,-2.12),(2.5,1.6))]:
        m.beam((*a,1.30),(*b,1.30),.065,.08,'WoodLight')
        for t in (.0,.5,1):
            x=a[0]+(b[0]-a[0])*t; y=a[1]+(b[1]-a[1])*t
            m.beam((x,y,.76),(x,y,1.3),.055,.06,'Wood')
    for j in range(4):
        m.box((-.7,-3.12+j*.25,.09+j*.17),(1.0,.29,.14),'WoodLight')
    m.ramp(-.7,-3.3,1.1,1.075,.75)
    crate(m,.5,.4,size=.4)
    for z in (1.0,1.32,1.64):
        m.box((1.2,-1.6,z),(.8,.43,.055),'WoodLight')
        for j in range(4): m.box((.92+j*.18,-1.6,z+.06),(.12,.17,.07),'Plant')
    m.door=(-.7,-1.1,.75)
    m.approach=(-.7,-3.6,.5)


def build_farmer_east(m):
    green='627e62'
    back={'front':[(0,.85,.80,1.38,'door',green),(-1.5,1.15,.60,.62,'window',green),(1.5,1.15,.60,.62,'window',green)],
          'back':[(0,.85,.78,1.38,'door',green),(-1.5,1.15,.6,.6,'window',green),(1.5,1.15,.6,.6,'window',green)]}
    room(m,(0,1.35),5.3,1.4,1.70,'Plaster','e5e1cb',holes=back,timber=False)
    hip_roof(m,(0,1.35),5.8,1.95,1.98,.45)
    for sign in (-1,1):
        x=sign*2.0
        hs={'front':[(0,1.1,.6,.6,'window',green)],'back':[],
            'left':[(0,1.12,.6,.6,'window',green)],'right':[(0,1.12,.6,.6,'window',green)]}
        room(m,(x,-.12),1.3,3.1,1.70,'Plaster','e8e3ce',holes=hs,timber=False)
        hip_roof(m,(x,-.12),1.8,3.65,1.98,.5)
    # Small open gate leaves the central route and court unblocked.
    for x in (-.67,.67): m.beam((x,-1.95,.05),(x,-1.95,.80),.08,.10,'WoodLight')
    for sign in (-1,1):
        m.beam((sign*.7,-1.95,.62),(sign*1.3,-1.95,.62),.065,.07,'WoodLight')
    planter(m,-.90,-.65,.14,.65)
    planter(m,.90,-.65,.14,.65)
    chimney(m,1.4,1.4,2.30,.55,.30)
    m.door=(0,.65,.16)
    m.approach=(0,-2.4,.5)


def build_farmer_west(m):
    hs=standard_holes(5.3,3.1,door_x=1.1)
    hs['front']=[(1.1,.85,.78,1.38,'door'),(-1.35,1.02,1.42,1.72,'door'),(.15,1.17,.55,.62,'window')]
    hs['back']=[(-1.15,1.14,.6,.64,'window'),(.9,.85,.76,1.38,'door')]
    room(m,(0,0),5.3,3.1,1.78,'Stone',holes=hs,timber=False)
    stone_patches(m,(0,0),5.3,3.1,1.78,hs)
    # Broad rounded thatch profile, built as successive closed bands.
    profile=[(-2.05,1.88),(-1.92,2.12),(-1.55,2.58),(-.95,3.12),(0,3.42),(.95,3.12),(1.55,2.58),(1.92,2.12),(2.05,1.88)]
    for j in range(len(profile)-1):
        ya,za=profile[j]; yb,zb=profile[j+1]
        m.roof_panel((-2.98,ya,za),(2.98,ya,za),(-2.98,yb,zb),(2.98,yb,zb),'Thatch',course=.33,tile=.65)
        for strand in range(26):
            xx=-2.9+(strand+.5)*5.8/26
            offset=RNG.uniform(-.035,.035)
            m.poly([(xx-.012,ya,za+.045),(xx+.012,ya,za+.045),(xx+.009,yb,zb+.045),(xx-.009,yb,zb+.045)],'Thatch','dbc58c' if strand%3 else 'a38d55')
    # Thick rounded end caps; the loft shutter belongs to the stone end wall.
    m.poly([(-2.98,y,z-.05) for y,z in profile]+[(-2.98,1.75,1.85),(-2.98,-1.75,1.85)],'Thatch')
    # The loft hatch is a real cut-out in the end cap, not a window buried behind it.
    edge_z=3.42-.33/.95*.30-.05
    for sign in (-1,1):
        outer=[(2.98,y,z-.05) for y,z in profile if y*sign>.33]
        if sign>0: outer.reverse()
        edge=(2.98,sign*.33,edge_z)
        bottom=[(2.98,sign*.33,1.85),(2.98,sign*2.05,1.85)]
        m.poly(outer+[edge]+bottom,'Thatch')
    m.poly([(2.98,-.33,1.85),(2.98,.33,1.85),(2.98,.33,2.21),(2.98,-.33,2.21)],'Thatch')
    m.poly([(2.98,-.33,2.69),(2.98,.33,2.69),(2.98,.33,edge_z),(2.98,0,3.37),(2.98,-.33,edge_z)],'Thatch')
    # A small eyebrow over the grain-loft hatch on the side elevation.
    opening(m,(2.99,0,0),(0,1,0),(1,0,0),0,2.45,.55,.48,door=True,side='right')
    m.slab([(2.94,-.5,2.95),(3.35,-.5,2.78),(3.35,.5,2.78),(2.94,.5,2.95)],.09,'Thatch')
    for j in range(4):
        x=-2.7+j*1.6
        m.beam((x,-2.03,1.84),(x,-1.72,2.03),.09,.11,'Wood')
        m.beam((x,2.03,1.84),(x,1.72,2.03),.09,.11,'Wood')
    chimney(m,-1.6,.5,3.23,.72,.42)
    steps(m,1.1,-2.2,1.0,.16,2)
    m.door=(1.1,-1.55,.16)
    m.approach=(1.1,-2.65,.5)


def turf_roof(m,center,width,depth,eave,rise):
    x,y=center
    gable_roof(m,center,width,depth,eave,rise,'Turf')
    # Exposed earth layer and log rafters under irregular green roof blocks.
    for sign in (-1,1):
        for j in range(7):
            yy=y-depth/2+(j+.5)*depth/7
            m.tube((x+sign*(width/2-.32),yy,eave-.15),(x+sign*(width/2+.08),yy,eave-.15),.075,'WoodLight',sides=7)
        m.box((x+sign*width/2,y,eave-.07),(.14,depth,.14),'Wood','756543')
    for j in range(28):
        xx=x+RNG.uniform(-width*.45,width*.45)
        yy=y+RNG.uniform(-depth*.46,depth*.46)
        zz=eave+rise*(1-abs(xx-x)/(width/2))+.035
        m.box((xx,yy,zz),(RNG.uniform(.14,.3),RNG.uniform(.18,.34),.05),'Turf')
    for j in range(20):
        xx=x+RNG.uniform(-width*.44,width*.44)
        yy=y+RNG.uniform(-depth*.44,depth*.44)
        zz=eave+rise*(1-abs(xx-x)/(width/2))+.07
        for dx,dy in [(.07,0),(-.045,.06),(-.04,-.055)]:
            m.poly([(xx-.025,yy,zz),(xx+.025,yy,zz),(xx+dx,yy+dy,zz+RNG.uniform(.07,.15))],'Turf')


def build_smallholder(m):
    hs=standard_holes(3.1,3.1,door_x=-.45)
    hs['front']=[(-.45,.85,.78,1.38,'door'),(.75,1.15,.55,.6,'window')]
    room(m,(-.65,.4),3.1,3.1,1.8,'Stone',holes=hs,timber=False)
    stone_patches(m,(-.65,.4),3.1,3.1,1.8,hs)
    turf_roof(m,(-.65,.4),3.75,3.75,2.07,.47)
    hs2={'front':[(0,1.02,.58,.6,'window')],'back':[(0,.8,.74,1.28,'door')],'right':[(0,1.0,.55,.58,'window')], 'left':[]}
    room(m,(1.72,.80),1.65,2.3,1.4,'Wood','726b4c',holes=hs2,boards=True)
    turf_roof(m,(1.72,.80),2.15,2.85,1.70,.33)
    chimney(m,-1.5,.85,2.45,.8,.47)
    logs(m,-2.1,-1.55,3,.5)
    steps(m,-1.1,-1.95,1.0,.16,2)
    # Pen and bees remain level production props, not part of the building collision footprint.
    m.door=(-1.1,-1.15,.16)
    m.approach=(-1.1,-2.45,.5)


BUILDERS={'01_chief':build_chief,'02_host':build_host,'03_smith':build_smith,'04_carpenter':build_carpenter,'05_herbalist':build_herbalist,'06_forester':build_forester,'07_gatherer':build_gatherer,'08_farmer_east':build_farmer_east,'09_farmer_west':build_farmer_west,'10_smallholder':build_smallholder}


def build(name):
    RNG.seed(20261026+list(BUILDERS).index(name)*733)
    m=Mesh(name)
    BUILDERS[name](m)
    detail_props(m)
    return m.create()


def _export_all():
    props=bpy.ops.export_scene.gltf.get_rna_type().properties
    def enums(key):
        values=[i.identifier for i in props[key].enum_items]
        if values: return values
        operator=bpy.types.Operator.bl_rna_get_subclass_py('EXPORT_SCENE_OT_gltf')
        for base in operator.__mro__:
            deferred=base.__dict__.get('__annotations__',{}).get(key)
            if deferred is not None:
                items=deferred.keywords['items']
                return [i[0] for i in (items(None,bpy.context) if callable(items) else items)]
        return []
    assert 'GLB' in enums('export_format')
    for name,obj in ASSETS.items():
        for selected in list(bpy.context.selected_objects): selected.select_set(False)
        show(name)
        obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        kwargs=dict(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,export_apply=True,export_materials='EXPORT',export_yup=True,export_normals=True,export_texcoords=True,export_cameras=False,export_lights=False)
        if 'export_tangents' in props: kwargs['export_tangents']=True
        if 'export_vertex_color' in props:
            assert 'ACTIVE' in enums('export_vertex_color')
            kwargs['export_vertex_color']='ACTIVE'
        if 'export_all_vertex_colors' in props: kwargs['export_all_vertex_colors']=False
        bpy.ops.export_scene.gltf(**kwargs)
        obj.select_set(False)
    concepts=json.loads((ROOT/'assets/resources/art/forest_village/houses_v2_generation.json').read_text(encoding='utf-8'))
    for spec,concept in zip([SPECS[k] for k in BUILDERS],concepts['households']):
        spec['household']=concept['scene_node']
        spec['concept']=concept['file']
        spec['design']=concept['design']
    META.write_text(json.dumps({'budget_triangles':TRIANGLE_BUDGET,'detail_revision':3,'coordinate_system':'Blender Z-up, -Y front; GLB exports Godot +Y-up, +Z front','assets':[SPECS[k] for k in BUILDERS]},ensure_ascii=False,indent=2),encoding='utf-8')
    print('EXPORTED',len(ASSETS),'individual GLBs',sum(s['triangles'] for s in SPECS.values()),'total triangles')


def export_all():
    # glTF repacks the scalar roughness channel into an intermediate PNG.
    # Keep its automatic temporary-file lifecycle inside this task's workspace.
    task_tmp=(CAPTURES/'gltf-temp').resolve()
    assert task_tmp.is_relative_to(CAPTURES.resolve())
    task_tmp.mkdir(parents=True,exist_ok=True)
    previous=tempfile.tempdir
    tempfile.tempdir=str(task_tmp)
    try:_export_all()
    finally:tempfile.tempdir=previous


def save_source():
    bpy.data.libraries.write(str(OUT/'source/forest_village_houses_v2.blend'),{SCENE},path_remap='RELATIVE_ALL',fake_user=True,compress=True)
    print('SOURCE_SAVED',OUT/'source/forest_village_houses_v2.blend')


def setup_preview():
    world=bpy.data.worlds.get('FV2_World') or bpy.data.worlds.new('FV2_World')
    world.use_nodes=True
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    bg.inputs['Color'].default_value=(.27,.37,.39,1)
    bg.inputs['Strength'].default_value=.65
    SCENE.world=world
    light=bpy.data.lights.get('FV2_Sun') or bpy.data.lights.new('FV2_Sun','SUN')
    light.energy=2.1
    light.angle=.15
    sun=bpy.data.objects.get('FV2_Sun')
    if not sun:
        sun=bpy.data.objects.new('FV2_Sun',light)
        SCENE.collection.objects.link(sun)
    sun.rotation_euler=(math.radians(24),math.radians(-28),math.radians(-35))
    SCENE.render.resolution_x=1200
    SCENE.render.resolution_y=900
    SCENE.render.resolution_percentage=100
    try: SCENE.view_settings.view_transform='Standard'
    except TypeError: pass
    SCENE.view_settings.exposure=0


setup_preview()
exec(compile((ROOT/'scripts/tools/art/forest_village_houses/detail_pass.py').read_text(encoding='utf-8'), 'detail_pass.py', 'exec'), globals())
