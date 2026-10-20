"""Author independent forest-town meshes in the live Blender through Blender MCP.

Run definitions, then buildings(), vegetation(), props_and_ground(), finish().
All exterior buildings are complete on four sides. Budget includes triangulation.
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
OUT.mkdir(parents=True, exist_ok=True)
META.mkdir(parents=True, exist_ok=True)
RNG = random.Random(7731)
SCENE = bpy.data.scenes.get('ForestTown') or bpy.data.scenes.new('ForestTown')
bpy.context.window.scene = SCENE
SCENE.unit_settings.system = 'METRIC'
LIB = bpy.data.collections.get('FT_AssetLibrary')
if LIB is None:
    LIB = bpy.data.collections.new('FT_AssetLibrary')
    SCENE.collection.children.link(LIB)
ASSETS = {}
MANIFEST = {'budget': {'polygons': 8000, 'triangles': 8000}, 'assets': [], 'instances': []}

PALETTE = {
    'plaster': ['f6deb3', 'efd3a5', 'e6c89c', 'f7e1b9'],
    'wood': ['4b3234', '573935', '654337', '704d3b'],
    'wood_light': ['8d5c45', 'a06d4c', '79503e'],
    'roof': ['b66751', 'c77958', 'ce865d', 'a85d53', 'd48e61', '945458'],
    'glass': ['2c4148', '38535b', '29414d'],
    'stone': ['889d91', '9eaa98', 'b7b9a0', '7e958e', 'b3b39a'],
    'bark': ['455f6f', '59757c', '3b5869', '84917c', 'b1aa82'],
    'leaf': ['1f515f', '26666a', '387a71', '65956d', '94b17a', 'c4cd87'],
    'fern': ['2c6667', '4d8566', '83a46d', 'bec77f'],
    'soil': ['c4af83', 'cbb68b', 'd3bc8f', 'cbb187'],
    'earth': ['718a6c', '7f936e', '6e886b', '8d9b74'],
}

def color(family, index=None):
    values = PALETTE.get(family, [family])
    value = values[RNG.randrange(len(values)) if index is None else index % len(values)]
    return tuple(int(value[i:i+2], 16) / 255 for i in (0, 2, 4)) + (1,)

MAT = bpy.data.materials.get('FT_VertexPaint') or bpy.data.materials.new('FT_VertexPaint')
nodes = MAT.node_tree.nodes
bsdf = next(n for n in nodes if n.type == 'BSDF_PRINCIPLED')
bsdf.inputs['Roughness'].default_value = 1.0
spec = bsdf.inputs.get('Specular IOR Level')
if spec: spec.default_value = 0.12
vc = next((n for n in nodes if n.type == 'VERTEX_COLOR'), None) or nodes.new('ShaderNodeVertexColor')
vc.layer_name = 'Palette'
MAT.node_tree.links.new(vc.outputs['Color'], bsdf.inputs['Base Color'])
atlas_path=ROOT/'assets/textures/environment/forest_town/painterly_atlas_v3.png'
if atlas_path.exists():
    image=bpy.data.images.load(str(atlas_path),check_existing=True)
    tex=next((n for n in nodes if n.type=='TEX_IMAGE'),None) or nodes.new('ShaderNodeTexImage')
    tex.image=image
    multiply=next((n for n in nodes if n.type=='MIX_RGB'),None) or nodes.new('ShaderNodeMixRGB')
    multiply.blend_type='MULTIPLY'; multiply.inputs[0].default_value=.58
    MAT.node_tree.links.new(vc.outputs['Color'],multiply.inputs[1])
    MAT.node_tree.links.new(tex.outputs['Color'],multiply.inputs[2])
    MAT.node_tree.links.new(multiply.outputs[0],bsdf.inputs['Base Color'])

SURFACE_MATERIALS={}
for label in ['Timber','Roof','Stone','Glass']:
    material=MAT.copy()
    material.name='FT_'+label
    SURFACE_MATERIALS[label]=material

# A separate alpha-cutout surface preserves the painted, lobed oak silhouettes.
LEAF_MAT = bpy.data.materials.get('FT_OakSprigs') or bpy.data.materials.new('FT_OakSprigs')
leaf_nodes=LEAF_MAT.node_tree.nodes
leaf_bsdf=next(n for n in leaf_nodes if n.type=='BSDF_PRINCIPLED')
leaf_bsdf.inputs['Roughness'].default_value=1.0
leaf_spec=leaf_bsdf.inputs.get('Specular IOR Level')
if leaf_spec: leaf_spec.default_value=.02
leaf_vc=next((n for n in leaf_nodes if n.type=='VERTEX_COLOR'),None) or leaf_nodes.new('ShaderNodeVertexColor')
leaf_vc.layer_name='Palette'
leaf_image=bpy.data.images.load(str(ROOT/'assets/textures/environment/forest_town/oak_sprigs_v2.png'),check_existing=True)
leaf_tex=next((n for n in leaf_nodes if n.type=='TEX_IMAGE'),None) or leaf_nodes.new('ShaderNodeTexImage')
leaf_tex.image=leaf_image
leaf_mix=next((n for n in leaf_nodes if n.type=='MIX_RGB'),None) or leaf_nodes.new('ShaderNodeMixRGB')
leaf_mix.blend_type='MULTIPLY'; leaf_mix.inputs[0].default_value=1
LEAF_MAT.node_tree.links.new(leaf_vc.outputs['Color'],leaf_mix.inputs[1])
LEAF_MAT.node_tree.links.new(leaf_tex.outputs['Color'],leaf_mix.inputs[2])
LEAF_MAT.node_tree.links.new(leaf_mix.outputs[0],leaf_bsdf.inputs['Base Color'])
LEAF_MAT.node_tree.links.new(leaf_tex.outputs['Alpha'],leaf_bsdf.inputs['Alpha'])
if hasattr(LEAF_MAT,'surface_render_method'):
    leaf_methods=[i.identifier for i in LEAF_MAT.bl_rna.properties['surface_render_method'].enum_items]
    if 'DITHERED' in leaf_methods: LEAF_MAT.surface_render_method='DITHERED'

ATLAS_X=[0,.25,.5,.75,1]
ATLAS_Y=[0,.25,.5,.75,1]
ATLAS_CELLS={'plaster':(0,0),'wood':(1,0),'wood_light':(2,0),'roof':(3,0),'stone':(0,1),'bark':(1,1),'soil':(2,1),'earth':(0,3),'leaf':(0,2),'fern':(1,2),'glass':(2,3)}

def color_family(tone):
    best=(1e9,'plaster')
    for family in ATLAS_CELLS:
        for value in PALETTE[family]:
            rgb=[int(value[i:i+2],16)/255 for i in (0,2,4)]
            distance=sum((rgb[i]-tone[i])**2 for i in range(3))
            if distance<best[0]: best=(distance,family)
    return best[1]

def atlas_uv(u,v,family):
    column,row=ATLAS_CELLS[family]
    x0,x1=ATLAS_X[column]+.003,ATLAS_X[column+1]-.003
    y0,y1=ATLAS_Y[row]+.003,ATLAS_Y[row+1]-.003
    return (x0+(x1-x0)*u,1-(y1-(y1-y0)*v))

class Mesh:
    def __init__(self):
        self.verts, self.faces, self.colors, self.smooth, self.uvs = [], [], [], [], []
        self.surface_ids, self.normals = [], []

    def poly(self, points, shade, smooth=False, uvs=None, surface=0, normals=None):
        offset = len(self.verts)
        self.verts.extend([tuple(p) for p in points])
        self.faces.append(tuple(range(offset, len(self.verts))))
        self.colors.append(color(shade) if isinstance(shade, str) else shade)
        self.smooth.append(smooth)
        self.uvs.append(uvs)
        self.surface_ids.append(surface)
        self.normals.append(normals)

    def extend(self, other, offset=(0,0,0)):
        offset=Vector(offset)
        for face,tone,smooth,uv,surface,normals in zip(other.faces,other.colors,other.smooth,other.uvs,other.surface_ids,other.normals):
            self.poly([Vector(other.verts[i])+offset for i in face],tone,smooth,uv,surface,normals)

    def curved_limb(self, centers, radii, sides, tone='bark', root=False, sun_streak=False):
        """Continuous ring mesh, with normals following each bend instead of world origin."""
        centers=[Vector(p) for p in centers]
        rings=[]; radial=[]
        for i,(c,r) in enumerate(zip(centers,radii)):
            tangent=(centers[min(i+1,len(centers)-1)]-centers[max(0,i-1)]).normalized()
            helper=Vector((0,1,0)) if abs(tangent.y)<.9 else Vector((1,0,0))
            u=tangent.cross(helper).normalized(); v=tangent.cross(u).normalized()
            ns=[(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides)).normalized() for j in range(sides)]
            ring=[c+n*r*(1+.055*math.sin(j*3+i*.34)) for j,n in enumerate(ns)]
            rings.append(ring); radial.append(ns)
        for i in range(len(rings)-1):
            for j in range(sides):
                k=(j+1)%sides
                shade=color(tone,(j//3)%len(PALETTE[tone]))
                if sun_streak and 3<=j<=5: shade=color('c9b089')
                self.poly([rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]],shade,True,
                          uvs=[((j%5)/5,(i%4)/4),((j%5+1)/5,(i%4)/4),((j%5+1)/5,(i%4+1)/4),((j%5)/5,(i%4+1)/4)],
                          normals=[radial[i][j],radial[i][k],radial[i+1][k],radial[i+1][j]])
        self.poly(list(reversed(rings[0])),tone)
        self.poly(rings[-1],tone)

    def sprig(self, center, length, width, tone, normal, tile=0, rotation=0, cluster_center=None):
        """A gently bowed, four-triangle spray with 12–18 painted lobed leaves."""
        c=Vector(center); n=Vector(normal).normalized()
        helper=Vector((0,0,1)) if abs(n.z)<.95 else Vector((1,0,0))
        u=n.cross(helper).normalized(); v=n.cross(u).normalized()
        u,v=u*math.cos(rotation)+v*math.sin(rotation),-u*math.sin(rotation)+v*math.cos(rotation)
        left=c-u*width*.5; right=c+u*width*.5; mid=c+n*length*.035
        x0=(tile%2)*.5+.008; x1=x0+.484
        y0=(tile//2)*.5+.008; y1=y0+.484
        def uv(x,y): return (x0+(x1-x0)*x,1-(y1-(y1-y0)*y))
        for low,high,a,b in [(left,mid,0,.5),(mid,right,.5,1)]:
            points=[low-v*length*.5,high-v*length*.5,high+v*length*.5,low+v*length*.5]
            # Shared, rounded cluster normals prevent every card becoming a separate glossy shard.
            ns=[]
            for p in points:
                outward=(p-Vector(cluster_center)).normalized() if cluster_center is not None else n
                ns.append((outward*.90+Vector((0,0,.15))).normalized())
            self.poly(points,tone,True,uvs=[uv(a,0),uv(b,0),uv(b,1),uv(a,1)],surface=1,normals=ns)

    def box(self, center, size, shade):
        c = Vector(center); s = Vector(size) * .5
        points = [c + Vector((x*s.x, y*s.y, z*s.z)) for x,y,z in
                  [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
        tone = color(shade) if isinstance(shade, str) else shade
        for face in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:
            self.poly([points[i] for i in face], tone)

    def beam(self, a, b, width, depth, shade):
        a, b = Vector(a), Vector(b)
        axis = (b-a).normalized()
        helper = Vector((0,0,1)) if abs(axis.z) < .95 else Vector((1,0,0))
        u = axis.cross(helper).normalized() * width*.5
        v = axis.cross(u).normalized() * depth*.5
        points = [p+u*x+v*y for p in (a,b) for x,y in [(-1,-1),(1,-1),(1,1),(-1,1)]]
        tone = color(shade) if isinstance(shade, str) else shade
        for face in [(0,3,2,1),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7),(4,5,6,7)]:
            self.poly([points[i] for i in face], tone)

    def tube(self, a, b, r0, r1, shade, sides=9):
        a,b=Vector(a),Vector(b); axis=(b-a).normalized()
        u=axis.cross(Vector((0,1,0)) if abs(axis.y)<.9 else Vector((1,0,0))).normalized()
        v=axis.cross(u).normalized()
        rings=[[p+(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))*r for j in range(sides)] for p,r in [(a,r0),(b,r1)]]
        for j in range(sides):
            k=(j+1)%sides
            self.poly([rings[0][j],rings[0][k],rings[1][k],rings[1][j]],shade)
        self.poly(list(reversed(rings[0])),shade)
        self.poly(rings[1],shade)

    def crown(self, center, scale, family='leaf', sides=10):
        center=Vector(center); scale=Vector(scale)
        rings=[]
        for level,(height,radius) in enumerate([(-.45,.45),(-.25,.92),(.15,1),(.57,.68),(.85,.1)]):
            ring=[]
            for j in range(sides):
                angle=j*math.tau/sides
                jitter=RNG.uniform(.88,1.12)
                ring.append(center+Vector((math.cos(angle)*scale.x*radius*jitter,math.sin(angle)*scale.y*radius*jitter,scale.z*(height+RNG.uniform(-.07,.07)))))
            rings.append(ring)
        base=RNG.randrange(max(1,len(PALETTE[family])-2))
        for i in range(4):
            for j in range(sides):
                tone=color(family,base+(1 if (j//3+i//2)%3==0 else 0))
                self.poly([rings[i][j],rings[i][(j+1)%sides],rings[i+1][(j+1)%sides],rings[i+1][j]],tone,True)
        self.poly(list(reversed(rings[0])),family)
        self.poly(rings[-1],family,True)

    def leaf_cluster(self, center, radius, shade='leaf', normal=None):
        c=Vector(center)
        n=Vector(normal or (RNG.uniform(-1,1),-1,RNG.uniform(.1,1))).normalized()
        u=n.cross(Vector((0,0,1))).normalized()
        v=n.cross(u).normalized()
        shape=[(-1,-.25),(-.7,-.25),(-.7,-.65),(-.25,-.65),(-.25,-.9),(.2,-.9),(.2,-.55),(.75,-.55),(.75,-.15),(1,-.15),(1,.3),(.6,.3),(.6,.7),(.15,.7),(.15,1),(-.3,1),(-.3,.65),(-.8,.65),(-.8,.2),(-1,.2)]
        self.poly([c+(u*x+v*y)*radius for x,y in shape],shade)

    def leaf(self, center, length, width, shade='leaf', normal=None):
        c=Vector(center)
        n=Vector(normal or (RNG.uniform(-1,1),RNG.uniform(-1,1),RNG.uniform(.15,1))).normalized()
        helper=Vector((0,0,1)) if abs(n.z)<.95 else Vector((1,0,0))
        u=n.cross(helper).normalized()
        v=n.cross(u).normalized()
        a=RNG.random()*math.tau
        u,v=u*math.cos(a)+v*math.sin(a),-u*math.sin(a)+v*math.cos(a)
        base=c-v*length*.52; tip=c+v*length*.58
        left=c-u*width*.50; right=c+u*width*.50
        ridge=c+n*length*.10
        tone=color(shade)
        self.poly([base,left,tip,ridge],tone,uvs=[(.5,.03),(.02,.5),(.5,.97),(.5,.5)])
        self.poly([base,ridge,tip,right],tuple(min(1,x*1.05) for x in tone[:3])+(1,),uvs=[(.5,.03),(.5,.5),(.5,.97),(.98,.5)])

    def emit(self, name, category, collision=None):
        old=bpy.data.objects.get(name)
        if old: bpy.data.objects.remove(old,do_unlink=True)
        data=bpy.data.meshes.new(name+'_Mesh')
        data.from_pydata(self.verts,[],self.faces); data.update()
        paint=data.color_attributes.new(name='Palette',type='FLOAT_COLOR',domain='CORNER')
        for face,tone,smooth,surface in zip(data.polygons,self.colors,self.smooth,self.surface_ids):
            face.use_smooth=smooth
            family=color_family(tone)
            face.material_index=(1 if surface==1 else {'wood':2,'wood_light':2,'roof':3,'stone':4,'glass':5}.get(family,0)
                                 if category in ['building','prop'] else surface)
            for index in face.loop_indices: paint.data[index].color_srgb=tone
        if category=='ground':
            # Shared position-based corner colors blend across tile edges. Random flat
            # colors on each 2 m quad made the street look like a checkerboard.
            for face in data.polygons:
                if face.center.z > -.01: continue
                for index in face.loop_indices:
                    p=data.vertices[data.loops[index].vertex_index].co
                    depth=p.y; center=2.5+max(0,depth)*.15
                    width=max(2.5,5-depth*.07)
                    road=max(0,min(1,(width+.8-abs(p.x-center))/1.6)) if depth>4 else 1
                    variation=(math.sin(p.x*.67+depth*.31)+math.sin(depth*.83-p.x*.27))*.022
                    soil=(.79+variation,.70+variation,.52+variation,1)
                    earth=(.43+variation,.53+variation,.36+variation,1)
                    paint.data[index].color_srgb=tuple(earth[k]*(1-road)+soil[k]*road for k in range(4))
        if category=='building':
            # Sparse moss belongs to roof color, retaining the shingle UVs and geometry.
            for face,tone in zip(data.polygons,self.colors):
                if color_family(tone)!='roof' or face.normal.z<.25: continue
                p=face.center
                patch=math.sin(p.x*1.8+p.y*.58)+math.sin(p.y*2.3-p.x*.42)
                blend=max(0,min(.65,(patch-1.18)*1.4))
                moss=(.49,.55,.30,1)
                for index in face.loop_indices:
                    paint.data[index].color_srgb=tuple(tone[k]*(1-blend)+moss[k]*blend for k in range(4))
        data.color_attributes.active_color=paint
        uv_layer=data.uv_layers.new(name='PaintedUV')
        for face,tone,custom_uv,surface in zip(data.polygons,self.colors,self.uvs,self.surface_ids):
            family=color_family(tone)
            positions=[Vector(self.verts[data.loops[k].vertex_index]) for k in face.loop_indices]
            spans=[max(p[i] for p in positions)-min(p[i] for p in positions) for i in range(3)]
            axes=sorted(range(3),key=lambda i:spans[i],reverse=True)[:2]
            if family in ['wood','wood_light','bark']:
                va,ua=axes
            elif 2 in axes:
                va=2; ua=axes[1] if axes[0]==2 else axes[0]
            else: ua,va=axes
            lo_u=min(p[ua] for p in positions); lo_v=min(p[va] for p in positions)
            for j,k in enumerate(face.loop_indices):
                u,v=custom_uv[j] if custom_uv else ((positions[j][ua]-lo_u)/max(spans[ua],1e-6),(positions[j][va]-lo_v)/max(spans[va],1e-6))
                uv_layer.data[k].uv=(u,v) if surface==1 else atlas_uv(u,v,family)
        if any(self.smooth):
            normals=[]
            for face,authored in zip(data.polygons,self.normals):
                for corner,index in enumerate(face.loop_indices):
                    p=data.vertices[data.loops[index].vertex_index].co
                    radial=Vector((p.x,p.y,.015)).normalized()
                    normals.append(authored[corner] if authored else (radial if face.use_smooth else face.normal))
            data.normals_split_custom_set(normals)
        data.materials.append(MAT)
        if category in ['building','prop']:
            data.materials.append(LEAF_MAT)
            for label in ['Timber','Roof','Stone','Glass']: data.materials.append(SURFACE_MATERIALS[label])
        elif 1 in self.surface_ids: data.materials.append(LEAF_MAT)
        obj=bpy.data.objects.new(name,data); LIB.objects.link(obj)
        data.calc_loop_triangles()
        polys,tris=len(data.polygons),len(data.loop_triangles)
        assert polys<=8000 and tris<=8000,(name,polys,tris)
        ASSETS[name]=obj
        MANIFEST['assets'].append({'name':name,'category':category,'file':name+'.glb','polygons':polys,'triangles':tris,'vertices':len(data.vertices),'collision':collision or [],'complete_exterior':category=='building','cutout_sprigs':self.surface_ids.count(1)//2})
        print(name,polys,'polygons',tris,'triangles; painted UVs')
        return obj

def window(m, x, z, y, width=.5, height=.65):
    # All window assemblies include a recess, sill, frame, mullions and shutters.
    m.box((x,y,z),(width+.16,.13,height+.15),'wood')
    m.box((x,y-.077,z),(width,.024,height),'glass')
    for dx in [-width*.5,width*.5]: m.box((x+dx,y-.10,z),(.055,.055,height+.09),'wood_light')
    for dz in [-height*.5,height*.5]: m.box((x,y-.11,z+dz),(width+.1,.06,.055),'wood_light')
    m.box((x,y-.13,z),(.035,.045,height),'wood_light')
    m.box((x,y-.13,z+.04),(width,.045,.035),'wood_light')
    for sign in [-1,1]:
        sx=x+sign*(width*.5+.21)
        m.box((sx,y-.06,z),(.27,.08,height+.04),'wood')
        for plank in [-.07,.07]: m.box((sx+plank,y-.11,z),(.105,.03,height-.04),'wood_light')
        for dz in [-height*.28,height*.28]: m.box((sx,y-.14,z+dz),(.3,.035,.035),'wood')
    m.box((x,y-.16,z-height*.5-.08),(width+.34,.25,.09),'wood_light')

def door(m, x, y, base, width=.66, height=1.12):
    radius=width*.5
    points=[(x-radius,y,base),(x+radius,y,base),(x+radius,y,base+height-radius)]
    points += [(x+radius*math.cos(a),y,base+height-radius+radius*math.sin(a)) for a in [j*math.pi/8 for j in range(1,9)]]
    m.poly(points,'wood')
    for i in range(5):
        dx=(i-2)*width/5
        top=height-radius+math.sqrt(max(0,radius**2-dx**2))
        m.box((x+dx,y-.025,base+top*.5),(width/5-.018,.04,top-.04),'wood_light')
    for z in [base+.28,base+.98]: m.box((x,y-.055,z),(width-.08,.045,.045),'wood')
    m.box((x+width*.25,y-.10,base+.67),(.055,.045,.055),'c4a272')
    for sign in [-1,1]: m.box((x+sign*(radius+.07),y,base+(height-radius)*.5),(.12,.18,height-radius),'stone')
    for j in range(8):
        a=j*math.pi/8; b=(j+1)*math.pi/8
        m.poly([(x+math.cos(t)*r,y-.06,base+height-radius+math.sin(t)*r) for t,r in [(a,radius),(b,radius),(b,radius+.14),(a,radius+.14)]],'stone')

def roof(m, width, depth, eave, rise, rows=10, columns=9):
    w=width*.5+.24; d=depth*.5+.24
    for sign in [-1,1]:
        m.poly([(0,-d,eave+rise),(sign*w,-d,eave-.05),(sign*w,d,eave-.05),(0,d,eave+rise)],'roof')
        for row in range(rows):
            t0=row/rows; t1=min(1,(row+1.12)/rows)
            for column in range(columns):
                y0=-d+2*d*column/columns; y1=-d+2*d*(column+1)/columns-.016
                x0,x1=sign*w*t0,sign*w*t1
                z0,z1=eave+rise*(1-t0)+.012,eave+rise*(1-t1)+.015
                m.poly([(x0,y0,z0),(x1,y0+.02,z1),(x1,y1-.015,z1+.01),(x0,y1,z0)],'roof')
        for y in [-d-.018,d+.018]: m.beam((0,y,eave+rise+.025),(sign*w,y,eave-.04),.13,.13,'wood_light')
        m.beam((sign*w,-d,eave-.055),(sign*w,d,eave-.055),.13,.14,'wood')
    m.beam((0,-d-.04,eave+rise+.04),(0,d+.04,eave+rise+.04),.14,.16,'roof')

def house(name,w,d,h,rise,porch=False,seed=1):
    RNG.seed(seed); m=Mesh()
    m.box((0,0,.10),(w+.18,d+.18,.2),'stone')
    m.box((0,0,h*.5+.18),(w,d,h),'plaster')
    eave=h+.18
    for sign in [-1,1]:
        y=sign*d*.5
        m.poly([(-w*.5,y,eave),(w*.5,y,eave),(0,y,eave+rise)],'plaster')
    # Timber structure and windows cover FRONT, BACK, LEFT and RIGHT.
    for side in [-1,1]:
        y=side*(d*.5+.035)
        for x in [-w*.5,0,w*.5]: m.box((x,y,h*.5+.18),(.115,.13,h+.06),'wood')
        for z in [.25,h*.51+.18,eave]: m.box((0,y,z),(w+.16,.14,.13),'wood')
        m.beam((-w*.5,y,eave),(0,y,eave+rise),.12,.12,'wood')
        m.beam((w*.5,y,eave),(0,y,eave+rise),.12,.12,'wood')
        m.box((0,y,eave+rise*.32),(.11,.14,rise*.64),'wood')
        if side==-1:
            for x in [-w*.28,w*.28]: window(m,x,h*.74+.2,y-.08,min(.56,w*.17),.52)
            window(m,w*.28,.79,y-.08,min(.52,w*.17),.48)
            door(m,-w*.23,y-.10,.21)
            window(m,0,eave+rise*.40,y-.08,.40,.43)
        else:
            # Work in a mirrored local mesh so shutter/window depth faces outwards.
            back=Mesh()
            for x in [-w*.27,w*.27]:
                for z in [.79,h*.74+.2]: window(back,x,z,-d*.5-.10,min(.52,w*.17),.50)
            for face,tone in zip(back.faces,back.colors): m.poly([(-back.verts[i][0],-back.verts[i][1],back.verts[i][2]) for i in face],tone)
    for side in [-1,1]:
        x=side*(w*.5+.025)
        for y in [-d*.5,0,d*.5]: m.box((x,y,h*.5+.18),(.14,.11,h+.03),'wood')
        for z in [.25,h*.51+.18,eave]: m.box((x,0,z),(.14,d+.12,.12),'wood')
        wall=Mesh()
        for y in [-d*.26,d*.26]:
            for z in [.79,h*.74+.2]: window(wall,y,z,-w*.5-.10,.40,.48)
        for face,tone in zip(wall.faces,wall.colors):
            m.poly([(side*(-wall.verts[i][1]),side*wall.verts[i][0],wall.verts[i][2]) for i in face],tone)
        m.beam((x,-d*.45,h*.52+.2),(x,-d*.22,h*.75),.09,.09,'wood')
        m.beam((x,d*.45,h*.52+.2),(x,d*.22,h*.75),.09,.09,'wood')
    roof(m,w,d,eave,rise)
    # Chimney is solid, with distinct cap and a dark flue opening.
    m.box((w*.23,d*.18,eave+rise*.75),(.43,.48,rise*.85),'stone')
    m.box((w*.23,d*.18,eave+rise*1.18),(.56,.61,.14),'stone')
    m.box((w*.23,d*.18,eave+rise*1.26),(.30,.35,.025),'glass')
    if porch:
        y=-d*.5-.48; canopy=1.46+(h-2.3)*.30
        m.box((0,y,.19),(w*.99,1.0,.16),'stone')
        for sign in [-1,1]:
            m.box((sign*w*.43,y-.32,(canopy+.18)*.5),(.10,.12,canopy-.18),'wood')
            m.beam((sign*w*.43,y-.32,canopy-.32),(sign*w*.31,y-.32,canopy),.08,.08,'wood_light')
        for i in range(6):
            x0=-w*.49+i*w*.98/6; x1=x0+w*.98/6+.015
            m.poly([(x0,-d*.5-.06,canopy+.18),(x1,-d*.5-.06,canopy+.18),(x1,y-.48,canopy),(x0,y-.48,canopy)],'roof')
        m.box((0,y-.47,canopy-.03),(w*1.02,.13,.11),'wood_light')
        m.box((-w*.33,-d*.5-.20,1.19),(.12,.18,.24),'wood')
        m.box((-w*.33,-d*.5-.29,1.19),(.085,.035,.15),'ffd58b')
    # Narrow planted window boxes break the regular timber silhouette.
    for x in [-w*.28,w*.28]:
        z=h*.74-.14; y=-d*.5-.24
        m.box((x,y,z),(.58,.22,.13),'wood')
        m.box((x,y-.015,z+.075),(.54,.18,.025),'earth')
        for j in range(13):
            p=Vector((x+RNG.uniform(-.27,.27),y+RNG.uniform(-.06,.07),z+RNG.uniform(.1,.22)))
            m.sprig(p,.28,.27,color('fern',2+j%2),(0,-1,.7),tile=j%4,rotation=RNG.uniform(-.9,.9),cluster_center=(x,y,z+.03))
    # The inn has a complete lower side wing, creating the reference's staggered roofline.
    if name=='inn':
        wing=Mesh(); ww,dd,hh=1.35,d*.91,1.95
        wing.box((0,0,.1),(ww+.12,dd+.12,.2),'stone')
        wing.box((0,0,hh*.5+.18),(ww,dd,hh),'plaster')
        for sy in [-1,1]:
            yy=sy*dd*.5
            wing.poly([(-ww*.5,yy,hh+.18),(ww*.5,yy,hh+.18),(0,yy,hh+1.18)],'plaster')
            for xx in [-ww*.5,ww*.5]: wing.box((xx,yy,hh*.5+.18),(.10,.12,hh),'wood')
            for zz in [.23,1.2,hh+.18]: wing.box((0,yy,zz),(ww+.1,.13,.1),'wood')
        for sx in [-1,1]:
            for zz in [.23,1.2,hh+.18]: wing.box((sx*ww*.5,0,zz),(.12,dd+.1,.1),'wood')
        window(wing,0,1.59,-dd*.5-.08,.40,.48)
        back=Mesh(); window(back,0,1.59,-dd*.5-.08,.40,.48)
        for face,tone in zip(back.faces,back.colors): wing.poly([(-back.verts[i][0],-back.verts[i][1],back.verts[i][2]) for i in face],tone)
        side=Mesh(); window(side,0,1.0,-ww*.5-.08,.38,.48)
        for face,tone in zip(side.faces,side.colors): wing.poly([(side.verts[i][1],side.verts[i][0],side.verts[i][2]) for i in face],tone)
        roof(wing,ww,dd,hh+.18,1.0,rows=7,columns=6)
        offset=(-w*.5-ww*.5+.12,.18,0)
        m.extend(wing,offset)
        # A hanging sign is modeled on both sides.
        xx=-w*.45; yy=-d*.5-.45
        m.beam((xx,yy+.2,1.85),(xx-.52,yy+.2,1.85),.075,.075,'wood')
        m.box((xx-.42,yy+.2,1.56),(.36,.085,.43),'wood_light')
    collision=[{'position':[0,(h+.2)*.5,0],'size':[w,h+.2,d]}]
    if name=='inn': collision.append({'position':[-w*.5-.555,1.07,-.18],'size':[1.35,2.14,d*.91]})
    # Mossy leaf sprays sit on the roof plane and break the perfectly straight eaves.
    for j in range(24):
        sign=-1 if j<16 else 1
        x=sign*(w*.5+.24)*RNG.uniform(.45,.95)
        y=-d*.5-.16+RNG.uniform(-.07,.16)
        z=eave+rise*(1-abs(x)/(w*.5+.24))+.055
        p=Vector((x,y,z)); c=Vector((x,y,z-.15))
        m.sprig(p,RNG.uniform(.22,.34),.28,color('fern',2+j%2),(sign*.5,-.15,.85),j%4,RNG.uniform(-.8,.8),c)
    m.emit(name,'building',collision)

def buildings():
    house('inn',2.65,2.75,2.3,1.15,True,11)
    house('cottage_wide',3.8,3.0,2.65,1.45,False,12)
    house('cottage_tall',2.3,2.8,2.9,1.35,True,13)
    house('cottage_small',2.15,2.45,1.95,1.05,False,14)
    house('house_gabled',2.5,2.6,2.4,1.35,False,15)
    house('cottage_right',3.4,3.2,2.6,1.4,True,16)
    house('far_cottage',2.0,2.4,2.2,1.2,False,17)
    house('back_house',2.8,3.0,2.5,1.35,False,18)

def tree(name,height,radius,seed,lean=.25):
    RNG.seed(seed); m=Mesh()
    phase=RNG.uniform(0,math.tau)
    def stem(t):
        return Vector((lean*math.sin(t*2.7)+radius*.32*math.sin(t*5+phase)*t,
                       radius*.46*math.sin(t*4+phase)*t,height*t))
    trunk=[stem(i/20) for i in range(21)]
    radii=[radius*((1-t)*.82+.09+.56*math.exp(-t*32)) for t in [i/20 for i in range(21)]]
    m.curved_limb(trunk,radii,20,sun_streak=name=='ancient_oak')
    # Buttress roots are curved, tapered continuations, rather than isolated triangular spikes.
    for j in range(7):
        a=j*2.399+phase; reach=RNG.uniform(2.2,3.1)*radius
        points=[]
        for k in range(6):
            t=k/5; angle=a+.17*math.sin(t*2)
            points.append(Vector((math.cos(angle)*reach*t,math.sin(angle)*reach*t,radius*1.4*(1-t)**2+.055)))
        m.curved_limb(points,[radius*(.40*(1-k/5)**1.2+.035) for k in range(6)],9)
    def branch_path(a,b,bend,samples):
        a,b,bend=Vector(a),Vector(b),Vector(bend)
        return [a.lerp(b,t)+bend*math.sin(math.pi*t) for t in [i/(samples-1) for i in range(samples)]]
    crowns=[]
    count=6+seed%2
    for j in range(count):
        a=j*2.399+phase+RNG.uniform(-.25,.25)
        t0=.30+j*.055+RNG.uniform(-.025,.025)
        reach=height*RNG.uniform(.17,.27)
        end=Vector((math.cos(a)*reach,math.sin(a)*reach,height*RNG.uniform(.72,.94)))
        bend=Vector((math.sin(a)*height*.03,-math.cos(a)*height*.035,-height*RNG.uniform(.03,.08)))
        path=branch_path(stem(t0),end,bend,9)
        base=radius*(.60-j*.045)
        m.curved_limb(path,[base*(1-i/8)**1.1+.045 for i in range(9)],10)
        for k in range(2):
            angle=a+(-.65 if k==0 else .50)+RNG.uniform(-.15,.15)
            tip=end+Vector((math.cos(angle)*height*.09,math.sin(angle)*height*.09,height*RNG.uniform(.015,.08)))
            off=branch_path(path[5+k],tip,Vector((math.sin(angle)*height*.018,0,-height*.018)),6)
            m.curved_limb(off,[radius*.15*(1-i/5)+.018 for i in range(6)],7)
            for l in range(2):
                direction=angle+(-.52 if l==0 else .47)
                twig_tip=tip+Vector((math.cos(direction)*height*.045,math.sin(direction)*height*.045,height*.018))
                twig=branch_path(off[3],twig_tip,Vector((0,0,-height*.01)),4)
                m.curved_limb(twig,[radius*.052*(1-i/3)+.005 for i in range(4)],5,'wood')
            crowns.append((tip.lerp(off[3],.18),(height*RNG.uniform(.11,.16),height*RNG.uniform(.08,.13),height*RNG.uniform(.035,.065))))
    crowns.append((stem(.99),(height*.14,height*.12,height*.06)))
    # Coherent leaf sprays wrap smaller, uneven volumes attached to real twig tips.
    for index,(c,s) in enumerate(crowns):
        for j in range(44):
            direction=Vector((RNG.gauss(0,1),RNG.gauss(0,1),RNG.gauss(.15,1))).normalized()
            reach=RNG.uniform(.42,1.05)
            p=c+Vector((direction.x*s[0],direction.y*s[1],direction.z*s[2]))*reach
            size=height*RNG.uniform(.070,.105)
            # A palette per branch volume and height, not a random new color per leaf.
            tone=color('leaf',min(5,max(0,2+int(direction.z*1.7)+(index%3==0))))
            m.sprig(p,size,size*RNG.uniform(.82,1.08),tone,direction*.7+Vector((0,0,.20)),
                    tile=(j+index)%4,rotation=RNG.uniform(0,math.tau),cluster_center=c)
    # Raised knot rims are complete modeled details on the visible lower trunk.
    for t,a in [(.16,phase+.9),(.33,phase+3.5)]:
        c=stem(t); r=radius*(.92-t*.8)
        tangent=Vector((-math.sin(a),math.cos(a),0)); out=Vector((math.cos(a),math.sin(a),0))
        points=[c+out*r+tangent*math.cos(j*math.tau/12)*radius*.19+Vector((0,0,math.sin(j*math.tau/12)*radius*.28)) for j in range(13)]
        m.curved_limb(points,[radius*.038]*13,5)
    m.emit(name,'tree',[{'position':[0,height*.38,0],'size':[radius*1.5,height*.76,radius*1.5]}])

def shrub(name,size,seed,tint=None):
    RNG.seed(seed); m=Mesh()
    for j in range(15):
        a=j*2.399; reach=math.sqrt((j+.5)/15)
        c=Vector((math.cos(a)*size*.48*reach,math.sin(a)*size*.46*reach,size*(.38+.36*(1-reach))))
        base=Vector((c.x*.25,c.y*.25,.025))
        points=[base.lerp(c,t)+Vector((0,0,math.sin(t*math.pi)*size*.12)) for t in [0,.25,.5,.75,1]]
        m.curved_limb(points,[size*x for x in [.018,.015,.011,.007,.003]],6,'wood')
        for k in range(28):
            direction=Vector((RNG.gauss(0,1),RNG.gauss(0,1),RNG.gauss(.25,1))).normalized()
            q=c+Vector((direction.x*.22,direction.y*.21,direction.z*.20))*size*RNG.uniform(.4,1)
            length=size*RNG.uniform(.35,.48)
            tone=color('fern',min(3,max(0,1+int(direction.z*1.7)+(j%4==0))))
            if tint: tone=tuple(tone[i]*tint[i] for i in range(3))+(1,)
            m.sprig(q,length,length*RNG.uniform(.85,1.1),tone,direction+Vector((0,0,.2)),
                    tile=(j+k)%4,rotation=RNG.uniform(0,math.tau),cluster_center=c)
    m.emit(name,'plant')

def fern(name,seed,flowers=False):
    RNG.seed(seed); m=Mesh()
    for j in range(9):
        a=j*math.tau/9; direction=Vector((math.cos(a),math.sin(a),0)); right=Vector((-math.sin(a),math.cos(a),0))
        length=RNG.uniform(.34,.58)
        for k in range(1,8):
            t=k/8; p=direction*length*t+Vector((0,0,length*math.sin(t*math.pi*.9)))
            half=length*.2*(1-t*.72)
            for sign in [-1,1]:
                m.poly([p-direction*.04,p+right*half*sign+direction*.025,p+direction*.065], 'fern')
    if flowers:
        for j in range(7):
            p=Vector((RNG.uniform(-.16,.16),RNG.uniform(-.16,.16),RNG.uniform(.32,.57)))
            m.tube((p.x,p.y,0),p,.008,.005,'fern',4)
            for k in range(5):
                a=k*math.tau/5
                q=p+Vector((math.cos(a)*.035,math.sin(a)*.035,.006))
                m.leaf_cluster(q,.035,'e9dca3',(.15,-1,.6))
    m.emit(name,'plant')

def vegetation():
    tree('ancient_oak',15.8,1.22,41,.65)
    tree('branching_oak',13.3,.66,42,-.45)
    tree('forest_column',19.0,.67,43,.18)
    tree('canopy_oak',11.2,.63,44,.35)
    tree('understory_oak',7.4,.40,45,.48)
    shrub('shrub_round',.85,51)
    shrub('shrub_low',.50,52)
    shrub('shrub_tall',1.2,53)
    shrub('shrub_shadow',.85,59,(.10,.22,.35))
    fern('fern',54)
    fern('woodland_flowers',55,True)
    RNG.seed(58); m=Mesh()
    points=[(0,0,0),(.65,.05,.22),(1.3,.08,.30),(2.0,.14,.48),(2.75,.12,.62),(3.4,.20,.72)]
    m.curved_limb(points,[.16,.14,.10,.075,.045,.008],9)
    for j in range(9):
        t=(j+1)/10; c=Vector((3.3*t,(-1 if j%2 else 1)*(.4+t*.5),.35+t*.4))
        base=Vector((3.3*t,.1,.3+t*.4))
        m.curved_limb([base,base.lerp(c,.5)+Vector((0,0,.13)),c],[.045,.026,.007],6)
        for k in range(32):
            d=Vector((RNG.gauss(0,1),RNG.gauss(0,1),RNG.gauss(0,1))).normalized()
            p=c+Vector((d.x*.57,d.y*.48,d.z*.25))*RNG.uniform(.3,1)
            m.sprig(p,RNG.uniform(.48,.68),.58,color('leaf',2+(k%3)),d+Vector((0,0,.1)),k%4,RNG.random()*math.tau,c)
    m.emit('leaf_bough','plant')

def props_and_ground():
    RNG.seed(67)
    m=Mesh()
    for j in range(5):
        for k in range(2): m.box((- .8+j*.4+RNG.uniform(-.015,.015),0,.17+k*.31),(.39,.38,.29),'stone')
    m.box((0,0,.80),(2.06,.44,.15),'stone'); m.emit('stone_wall','prop',[{'position':[0,.44,0],'size':[2.05,.9,.44]}])
    m=Mesh()
    for x in [-.84,.84]:
        m.box((x,0,.47),(.12,.14,.94),'wood'); m.box((x,0,.96),(.16,.18,.09),'wood_light')
    for z in [.33,.71]: m.box((0,0,z),(1.8,.085,.10),'wood')
    m.emit('fence','prop',[{'position':[0,.45,0],'size':[1.8,.9,.18]}])
    m=Mesh()
    for j in range(4):
        m.box((0,-j*.22,.07*(j+1)),(1.25,.25,.14*(j+1)),'stone')
    m.emit('steps','prop',[{'position':[0,.14,-.34],'size':[1.25,.28,.94]}])
    m=Mesh(); m.tube((0,0,.03),(0,0,.55),.22,.22,'wood_light',12)
    for z in [.1,.43]: m.tube((0,0,z),(0,0,z+.045),.231,.231,'wood',12)
    m.emit('barrel','prop',[{'position':[0,.29,0],'size':[.46,.58,.46]}])
    m=Mesh(); m.box((0,0,.24),(.48,.48,.48),'wood_light')
    for y in [-.25,.25]:
        for x in [-.19,.19]: m.box((x,y,.24),(.055,.035,.49),'wood')
        for z in [.04,.43]: m.box((0,y,z),(.48,.035,.055),'wood')
    m.emit('crate','prop',[{'position':[0,.24,0],'size':[.5,.5,.5]}])
    m=Mesh()
    # Meter-scale vertex-painted ground. The open road widens towards the camera.
    for x in range(-28,29,2):
        for y in range(-10,45,2):
            gx=x+1; depth=y+1
            center=2.5+max(0,depth)*.15
            road=depth<4 or abs(gx-center)<max(2.5,5-depth*.07)
            m.poly([(x,y,-.015),(x+2,y,-.015),(x+2,y+2,-.015),(x,y+2,-.015)],'soil' if road else 'earth')
    for j in range(650):
        y=RNG.uniform(-7,31); x=RNG.uniform(-11,15) if y<4 else 2.5+y*.15+RNG.uniform(-3.4,3.4)
        rx,ry=RNG.uniform(.20,.52),RNG.uniform(.14,.40)
        points=[(x+math.cos(k*math.tau/6)*rx*RNG.uniform(.8,1.1),y+math.sin(k*math.tau/6)*ry*RNG.uniform(.8,1.1),.004+RNG.random()*.002) for k in range(6)]
        m.poly(points,'stone' if RNG.random()<.6 else 'd8c59c')
    m.emit('village_ground','ground')
    m=Mesh()
    # A complete raised landing: top paving, foundation, and all four retaining faces.
    m.box((-.5,0,.15),(4.5,3.25,.30),'stone')
    for x in range(6):
        for y in range(4):
            xx=-2.7+x*.72; yy=-1.55+y*.78
            m.box((xx,yy,.322),(.69,.75,.045),'stone')
    for sign in [-1,1]:
        for i in range(9): m.box((-2.5+i*.5,sign*1.64,.18),(.48,.19,.31),'stone')
        for i in range(6): m.box((-.5+sign*2.27,-1.4+i*.52,.18),(.19,.5,.31),'stone')
    m.emit('terrace','prop',[{'position':[-.5,.16,0],'size':[4.7,.34,3.5]}])
    m=Mesh()
    for j in range(12):
        height=(j+1)*.12
        m.box((0,j*.28,height*.5),(1.7,.30,height),'stone')
        for i in range(3):
            m.box((-.57+i*.56,j*.28,height+.012),(.54,.285,.025),'stone')
    m.emit('street_steps','prop',[{'shape':'ramp','width':1.7,'run':3.38,'height':1.44}])
    m=Mesh(); collisions=[]
    for j in range(11):
        y0=j*2; y1=y0+2; x0=y0*.09; x1=y1*.09; width=3.4
        top=[(x0-width*.5,y0,1.44),(x0+width*.5,y0,1.44),(x1+width*.5,y1,1.44),(x1-width*.5,y1,1.44)]
        m.poly(top,'soil')
        m.poly([(p[0],p[1],0) for p in reversed(top)],'earth')
        for k in range(4):
            a,b=top[k],top[(k+1)%4]
            m.poly([a,b,(b[0],b[1],0),(a[0],a[1],0)],'stone')
        for k in range(14):
            x=RNG.uniform(-1.5,1.5)+(y0+1)*.09; y=RNG.uniform(y0,y1)
            m.box((x,y,1.45),(RNG.uniform(.18,.42),RNG.uniform(.20,.40),.02),'stone')
        collisions.append({'position':[(x0+x1)*.5,.72,-(y0+1)],'size':[3.5,1.44,2.1]})
    m.emit('raised_lane','prop',collisions)

def place(name,asset,x,y,z=0,rotation=0,scale=1):
    MANIFEST['instances'].append({'name':name,'asset':asset,'position':[x,z,y],'rotation_y':rotation,'scale':scale})

def layout():
    place('MainInn','inn',.6,-3.9,.34,18)
    place('LeftCottage','cottage_wide',-4.7,.5,0,14)
    place('RightCottage','cottage_right',5.9,.7,0,-26)
    place('TallHouse','cottage_tall',4.7,-15.9,1.44,-10,.92)
    place('SmallHouse','cottage_small',5.8,-12.7,1.44,8)
    place('LaneHouse','house_gabled',7.7,-23.8,1.44,-15,.95)
    place('FarCottage','far_cottage',6.9,-30.5,1.44,8)
    place('LeftBackHouse','back_house',-7.8,-13.8,.0,18)
    trees=[(-3.2,-6.8,1.0,'ancient_oak'),(-7.6,-10,1.03,'branching_oak'),(1.2,-18,1.05,'forest_column'),(6.5,-21,1.03,'forest_column'),(8.2,-6.7,1.02,'ancient_oak'),(12.5,-17,.95,'branching_oak'),(-13,-21,1.15,'forest_column'),(-7,-27,1.1,'forest_column'),(2,-32,1.15,'forest_column'),(12,-31,1.08,'forest_column'),(20,-29,1.2,'forest_column'),(-12,-12,1.1,'canopy_oak'),(14,-3,1.0,'canopy_oak'),(-15,-34,1.2,'forest_column'),(18,-38,1.15,'branching_oak')]
    for i,(x,y,s,a) in enumerate(trees): place('Tree_%02d'%i,a,x,y,0,i*47,s)
    for row in range(3):
        for column in range(11):
            x=-29+column*5.8+(row%2)*2.8
            y=-27-row*10+RNG.uniform(-1.8,1.8)
            place('Woodland_%d_%02d'%(row,column),'understory_oak',x,y,0,column*71+row*29,RNG.uniform(.95,1.65))
    place('InnTerrace','terrace',.35,-3.65,.0,18)
    for i,x in enumerate([-3.7,-1.8]): place('InnWall_%d'%i,'stone_wall',x,-1.0,.0,-3,.85)
    for i,x in enumerate([-4.0,-2.6,-1.2]): place('LeftFence_%d'%i,'fence',x,5.3,0,-4,.85)
    for i,x in enumerate([2.8,4.2,5.6]): place('RightFence_%d'%i,'fence',x,5.7,0,8,.85)
    for i,x in enumerate([-4.0,-2.6]): place('BackFence_%d'%i,'fence',x,-3.4,.65,-2,.8)
    for name,x,y,z in [('InnSteps',.25,-.85,.02),('LeftSteps',-3.2,1.4,.02)]: place(name,'steps',x,y,z)
    place('LaneSteps','street_steps',4.6,-8.2,0,-8)
    place('LaneLanding','terrace',5.3,-13.1,1.14,-8,.90)
    place('UpperLanding','terrace',5.8,-18.0,1.14,-8,.90)
    place('RaisedLane','raised_lane',5.1,-11.5)
    for i in range(5): place('LaneRetaining_%d'%i,'stone_wall',3.45+i*.25,-10.5-i*2.2,.6,-8,.80)
    for name,x,y in [('InnBarrel',1.9,-1.8),('LeftBarrel',-3.5,1.0),('RightBarrel',5.0,.5)]: place(name,'barrel',x,y,.34 if name=='InnBarrel' else 0)
    place('LeftCrate','crate',-3.75,1.12); place('RightCrate','crate',4.7,.6)
    RNG.seed(202)
    for i in range(72):
        y=RNG.uniform(-26,1.5)
        if i<24: x=RNG.uniform(-10,-3.5)
        else: x=2.5+(-y)*.15+RNG.choice([-1,1])*RNG.uniform(3.3,6.8)
        asset=RNG.choice(['shrub_round','shrub_low','shrub_tall','fern','woodland_flowers'])
        place('Plant_%03d'%i,asset,x,y,.015,RNG.uniform(0,360),RNG.uniform(.7,1.2))
    for i,(x,y) in enumerate([(-2.8,6.2),(-1.9,6.8),(3.15,6.4),(4.1,6.8)]): place('ForegroundShrub_%d'%i,'shrub_shadow',x,y,0,i*70,1.6)
    for i,(x,y,rotation) in enumerate([(-3.2,-6.8,0),(8.2,-6.7,170),(-6,-4.0,-25),(9,-13,145)]):
        place('CanopyBough_%d'%i,'leaf_bough',x,y,4.4 if i<2 else 4.8,rotation,1.15)
    for i in range(20):
        x=RNG.uniform(-4,-1.3) if i<10 else RNG.uniform(3.8,5.6)
        y=RNG.uniform(-1.5,2.0)
        place('StreetGarden_%02d'%i,RNG.choice(['fern','woodland_flowers','shrub_low']),x,y,0,RNG.uniform(0,360),RNG.uniform(.65,1.0))
    place('Ground','village_ground',0,0)

def finish():
    # Export every asset independently at its own origin. Never merge the town.
    LIB.hide_viewport=False; LIB.hide_render=False
    for obj in list(bpy.context.selected_objects): obj.select_set(False)
    for name,obj in ASSETS.items():
        obj.hide_set(False); obj.hide_render=False; obj.select_set(True)
        bpy.context.view_layer.objects.active=obj
        bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,export_apply=True,export_materials='EXPORT',export_yup=True,export_normals=True,export_vertex_color='ACTIVE',export_all_vertex_colors=False,export_cameras=False,export_lights=False)
        obj.select_set(False); obj.hide_set(True); obj.hide_render=True
    if not MANIFEST['instances']: layout()
    stage=bpy.data.collections.get('FT_TownLayout')
    if stage:
        for obj in list(stage.objects): bpy.data.objects.remove(obj,do_unlink=True)
    else:
        stage=bpy.data.collections.new('FT_TownLayout'); SCENE.collection.children.link(stage)
    for instance in MANIFEST['instances']:
        source=ASSETS[instance['asset']]
        obj=bpy.data.objects.new(instance['name'],source.data); stage.objects.link(obj)
        x,y,z=instance['position']; obj.location=(x,-z,y)
        obj.rotation_euler.z=math.radians(instance['rotation_y']); obj.scale=(instance['scale'],)*3
    camera_data=bpy.data.cameras.get('FT_Composition') or bpy.data.cameras.new('FT_Composition')
    camera_data.sensor_width=36; camera_data.sensor_height=20.25
    fits=[i.identifier for i in camera_data.bl_rna.properties['sensor_fit'].enum_items]
    if 'VERTICAL' in fits: camera_data.sensor_fit='VERTICAL'
    camera_data.lens=20.25/(2*math.tan(math.radians(15))); camera_data.clip_end=160
    cam=bpy.data.objects.get('FT_Composition')
    if cam is None: cam=bpy.data.objects.new('FT_Composition',camera_data); SCENE.collection.objects.link(cam)
    # Preview the fixed Godot camera in Blender; the Godot scene camera is never edited.
    cam.location=(.299139,-11.80333,2.003256)
    direction=Vector((0,.997441,-.071497))
    cam.rotation_euler=direction.to_track_quat('-Z','Y').to_euler()
    SCENE.camera=cam
    sun_data=bpy.data.lights.get('FT_Sun') or bpy.data.lights.new('FT_Sun','SUN')
    sun_data.energy=2.1; sun_data.angle=.015
    sun=bpy.data.objects.get('FT_Sun')
    if sun is None: sun=bpy.data.objects.new('FT_Sun',sun_data); SCENE.collection.objects.link(sun)
    sun.rotation_euler=(math.radians(28),math.radians(-35),math.radians(-35))
    world=bpy.data.worlds.get('FT_World') or bpy.data.worlds.new('FT_World')
    world.use_nodes=True
    background=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND')
    background.inputs['Color'].default_value=(.25,.42,.50,1)
    background.inputs['Strength'].default_value=.5
    SCENE.world=world; SCENE.render.resolution_x=1600; SCENE.render.resolution_y=900
    SCENE.render.resolution_percentage=100
    try: SCENE.view_settings.view_transform='Standard'
    except TypeError: pass
    SCENE.view_settings.exposure=0
    for area in bpy.context.screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_perspective='CAMERA'
            area.spaces.active.shading.type='MATERIAL'
            area.spaces.active.overlay.show_overlays=False
    MANIFEST['summary']={'unique_assets':len(MANIFEST['assets']),'placed_instances':len(MANIFEST['instances']),'max_asset_triangles':max(a['triangles'] for a in MANIFEST['assets'])}
    (META/'models.json').write_text(json.dumps(MANIFEST,ensure_ascii=False,indent=2),encoding='utf8')
    if atlas_path.exists(): image.pack()
    leaf_image.pack()
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'source/forest_town.blend'))
    print(json.dumps(MANIFEST['summary']))
