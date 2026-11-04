"""Blender-authored forest kit with branch-volume normals and restrained leaf colour.

Run with Blender --background --factory-startup --python <this file>.
Original geometry; reference principles are documented in docs/art/forest_vegetation.md.
"""
import bpy
import math
import json
import random
import sys
import tempfile
from pathlib import Path
from mathutils import Vector, Matrix

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'assets/models/environment/forest_vegetation'
TEX = ROOT / 'assets/textures/environment/forest_vegetation'
META = ROOT / 'assets/resources/art/forest_vegetation'
PREVIEW = ROOT / '.godot/vegetation_v5'
for p in (OUT / 'source', TEX, META, PREVIEW):
    p.mkdir(parents=True, exist_ok=True)
(OUT/'source/.gdignore').write_text('',encoding='utf8')
(PREVIEW/'tmp').mkdir(exist_ok=True)
tempfile.tempdir=str(PREVIEW/'tmp')

def rgba(hexval):
    rgb = [int(hexval[i:i+2], 16)/255 for i in (0, 2, 4)]
    return tuple(x/12.92 if x <= .04045 else ((x+.055)/1.055)**2.4 for x in rgb)+(1,)

STYLE = ROOT / 'assets/textures/environment/forest_village/character_style'

def atlas():
    return bpy.data.images.load(str(STYLE/'sprays.png'), check_existing=True)

def toon_material(name, shadow, middle, light, texture=None, bark=False):
    mat=bpy.data.materials.new(name); mat.use_nodes=True
    n=mat.node_tree.nodes; n.clear(); l=mat.node_tree.links
    out=n.new('ShaderNodeOutputMaterial')
    diff=n.new('ShaderNodeBsdfDiffuse'); diff.inputs['Color'].default_value=(1,1,1,1)
    to_rgb=n.new('ShaderNodeShaderToRGB'); l.new(diff.outputs[0],to_rgb.inputs[0])
    ramp=n.new('ShaderNodeValToRGB'); ramp.color_ramp.interpolation='CONSTANT'
    ramp.color_ramp.elements.remove(ramp.color_ramp.elements[1])
    for i,(pos,col) in enumerate([(0,shadow),(.36,middle),(.72,light)]):
        elem=ramp.color_ramp.elements[0] if i==0 else ramp.color_ramp.elements.new(pos)
        elem.position=pos; elem.color=rgba(col)
    l.new(to_rgb.outputs[0],ramp.inputs[0])
    em=n.new('ShaderNodeEmission'); l.new(ramp.outputs[0],em.inputs[0])
    if texture:
        tex=n.new('ShaderNodeTexImage'); tex.image=texture; tex.interpolation='Closest'
        paint=n.new('ShaderNodeMixRGB'); paint.blend_type='MULTIPLY'
        paint.inputs[0].default_value=.68
        l.new(ramp.outputs[0],paint.inputs[1]); l.new(tex.outputs['Color'],paint.inputs[2])
        l.new(paint.outputs[0],em.inputs[0])
        if bark:
            coords=n.new('ShaderNodeTexCoord')
            decode=n.new('ShaderNodeVectorMath'); decode.operation='MULTIPLY_ADD'
            decode.inputs[1].default_value=(4.0983605,4.0983605,1)
            decode.inputs[2].default_value=(-1.0368851,-1.0368851,0)
            l.new(coords.outputs['UV'],decode.inputs[0]); l.new(decode.outputs[0],tex.inputs[0])
            l.new(em.outputs[0],out.inputs['Surface'])
            return mat
        trans=n.new('ShaderNodeBsdfTransparent'); mix=n.new('ShaderNodeMixShader')
        l.new(tex.outputs['Alpha'],mix.inputs[0]); l.new(trans.outputs[0],mix.inputs[1])
        l.new(em.outputs[0],mix.inputs[2]); l.new(mix.outputs[0],out.inputs['Surface'])
        mat.surface_render_method='DITHERED'
    else: l.new(em.outputs[0],out.inputs['Surface'])
    return mat

def export_material(name, texture=None, bark=False):
    mat=bpy.data.materials.new(name); mat.use_nodes=True
    n=mat.node_tree.nodes; l=mat.node_tree.links
    bsdf=next(x for x in n if x.type=='BSDF_PRINCIPLED')
    vc=n.new('ShaderNodeVertexColor'); vc.layer_name='Palette'
    l.new(vc.outputs['Color'],bsdf.inputs['Base Color'])
    bsdf.inputs['Roughness'].default_value=1
    bsdf.inputs['Specular IOR Level'].default_value=0
    if texture:
        tx=n.new('ShaderNodeTexImage'); tx.image=texture
        tx.interpolation='Closest'
        l.new(tx.outputs['Color'],bsdf.inputs['Base Color'])
        if bark:
            uv=n.new('ShaderNodeTexCoord')
            mapping=n.new('ShaderNodeMapping')
            mapping.inputs['Location'].default_value=(-1.0368851,-1.0368851,0)
            mapping.inputs['Scale'].default_value=(4.0983605,4.0983605,1)
            l.new(uv.outputs['UV'],mapping.inputs['Vector'])
            l.new(mapping.outputs['Vector'],tx.inputs['Vector'])
        else:
            l.new(tx.outputs['Alpha'],bsdf.inputs['Alpha'])
            mat.surface_render_method='DITHERED'; mat.use_backface_culling=False
    return mat

class Mesh:
    def __init__(self): self.verts=[]; self.faces=[]; self.normals=[]; self.colors=[]; self.uv=[]; self.mats=[]
    def face(self, points, normals, tint, uv=None, material=0):
        base=len(self.verts); self.verts.extend(points)
        self.faces.append(tuple(range(base,base+len(points))))
        self.normals.extend(normals); self.colors.extend([tint]*len(points))
        self.uv.extend(uv or [(0,0)]*len(points)); self.mats.append(material)
    def tube(self, points, radii, sides=10, phase=0, root=False):
        points=[Vector(x) for x in points]; rings=[]; norms=[]; prev=None
        for i,(p,r) in enumerate(zip(points,radii)):
            t=(points[min(i+1,len(points)-1)]-points[max(i-1,0)]).normalized()
            u=t.cross(Vector((0,1,0))).normalized() if prev is None else (prev-t*prev.dot(t)).normalized()
            if u.length < .01: u=t.cross(Vector((1,0,0))).normalized()
            v=t.cross(u).normalized(); prev=u
            ring=[]; ns=[]
            for j in range(sides):
                a=j*math.tau/sides
                normal=u*math.cos(a)+v*math.sin(a)
                flare=(max(0,1-p.z/.75)**2*.30*(.5+.5*math.cos(a*5+phase))) if root else 0
                ring.append(p+normal*(r*(1+.035*math.sin(a*5+phase))+flare)); ns.append(normal)
            rings.append(ring); norms.append(ns)
        for i in range(len(points)-1):
            for j in range(sides):
                k=(j+1)%sides
                tint=rgba(['786048','8b6c4b','967952','735942'][int(j/sides*4)%4])
                self.face([rings[i][j],rings[i][k],rings[i+1][k],rings[i+1][j]],
                          [norms[i][j],norms[i][k],norms[i+1][k],norms[i+1][j]],tint,
                          [((u+1.0368851)/4.0983605,(v+1.0368851)/4.0983605) for u,v in
                           [(j/sides,i*.17),((j+1)/sides,i*.17),((j+1)/sides,(i+1)*.17),(j/sides,(i+1)*.17)]])
        self.face(list(reversed(rings[0])),[-(points[1]-points[0]).normalized()]*sides,rgba('786048'))
        self.face(rings[-1],[(points[-1]-points[-2]).normalized()]*sides,rgba('786048'))
    def crown(self, center, radii, count, size, rng, global_center, leaf_color='759651'):
        center=Vector(center); radii=Vector(radii); global_center=Vector(global_center)
        for i in range(count):
            z=1-2*(i+.5)/count; a=i*2.399963+ rng.uniform(-.3,.3)
            d=Vector((math.sqrt(1-z*z)*math.cos(a),math.sqrt(1-z*z)*math.sin(a),z))
            factor=rng.uniform(.74,1.03)
            p=center+Vector((d.x*radii.x,d.y*radii.y,d.z*radii.z))*factor
            n=Vector((d.x/radii.x,d.y/radii.y,d.z/radii.z)).normalized()
            real_n=(n*.75+Vector((rng.uniform(-.3,.3),rng.uniform(-.3,.3),.45))).normalized()
            u=real_n.cross(Vector((0,0,1))).normalized()
            if u.length < .01: u=Vector((1,0,0))
            v=real_n.cross(u).normalized(); roll=rng.random()*math.tau
            u,v=u*math.cos(roll)+v*math.sin(roll),-u*math.sin(roll)+v*math.cos(roll)
            s=size*rng.uniform(.78,1.28); u*=s; v*=s
            pts=[p-u-v,p+u-v,p+u+v,p-u+v]
            ns=[]
            for q in pts:
                rel=q-center
                local=Vector((rel.x/(radii.x**2),rel.y/(radii.y**2),rel.z/(radii.z**2))).normalized()
                big=(q-global_center).normalized()
                ns.append((local*.82+big*.18).normalized())
            cell=rng.randrange(4); x=(cell%2)*.5; y=(cell//2)*.5
            uv=[(x+.002,y+.002),(x+.498,y+.002),(x+.498,y+.498),(x+.002,y+.498)]
            self.face(pts,ns,rgba(leaf_color),uv,1)
    def object(self,name):
        data=bpy.data.meshes.new(name); data.from_pydata(self.verts,[],self.faces); data.update()
        obj=bpy.data.objects.new(name,data); bpy.context.collection.objects.link(obj)
        data.materials.append(BARK); data.materials.append(LEAF)
        uv=data.uv_layers.new(name='UVMap'); colors=data.color_attributes.new(name='Palette',type='FLOAT_COLOR',domain='CORNER')
        for poly,idx in zip(data.polygons,self.mats): poly.material_index=idx; poly.use_smooth=True
        for loop in data.loops:
            uv.data[loop.index].uv=self.uv[loop.vertex_index]
            colors.data[loop.index].color=self.colors[loop.vertex_index]
        data.color_attributes.active_color=colors
        data.normals_split_custom_set([self.normals[x.vertex_index] for x in data.loops])
        return obj

def curve(a,b,bend,steps=7):
    a=Vector(a); b=Vector(b); bend=Vector(bend)
    return [a.lerp(b,i/steps)+bend*(math.sin(math.pi*i/steps)) for i in range(steps+1)]

SPECS={
    'oak_broad':dict(seed=511,h=14.8,r=.90,spread=4.0,lean=.65,branches=7,tint='7d9854'),
    'hornbeam_tall':dict(seed=512,h=17.2,r=.58,spread=3.1,lean=.32,branches=7,tint='739153'),
    'oak_sweeping':dict(seed=513,h=11.4,r=.57,spread=3.55,lean=1.30,branches=6,tint='7e9b5b'),
    'hazel_round':dict(seed=514,h=1.19,w=1.93,tint='688456'),
    'hazel_tall':dict(seed=515,h=1.78,w=1.52,tint='607f59'),
    'woodland_low':dict(seed=516,h=.54,w=1.70,tint='718a58'),
    'oak_sapling':dict(seed=517,h=3.25,r=.11,spread=.77,lean=.16,branches=4,tint='90a95c'),
}

def build(name,spec):
    m=Mesh(); rng=random.Random(spec['seed']); h=spec['h']
    if 'r' in spec:
        r=spec['r']; spread=spec['spread']; lean=spec['lean']
        trunk=[(lean*(i/16)**1.4,math.sin(i*.27)*.13*h/14,h*.78*i/16) for i in range(17)]
        radii=[r*(1-.90*(i/16)**.7)*(1+.3*math.exp(-i)) for i in range(17)]
        m.tube(trunk,radii,16,root=True)
        global_center=(lean*.8,0,h*.75)
        count=spec['branches']
        for j in range(count):
            t=.43+j/(count-1)*.35
            a=j*2.399963+ rng.uniform(-.25,.25)
            start=Vector((lean*t**1.4,0,h*t))
            length=spread*(1-.25*j/count)*rng.uniform(.9,1.08)
            end=start+Vector((math.cos(a)*length,math.sin(a)*length,h*(.15+.035*j/count)))
            path=curve(start,end,(math.cos(a)*.2,math.sin(a)*.2,-h*.04),7)
            m.tube(path,[r*.43*(1-i/8)**1.1+.012 for i in range(8)],10,phase=j)
            for k in range(2):
                angle=a+(k-.5)*.90
                origin=path[4+k]
                target=end+Vector((math.cos(angle)*spread*.38,math.sin(angle)*spread*.38,h*(.06+.025*k)))
                m.tube(curve(origin,target,(0,0,-h*.025),5),[r*.17*(1-i/6)+.009 for i in range(6)],7)
                rad=Vector((spread*.61,spread*.55,h*.125))
                center=target+Vector((0,0,rad.z*.10))
                m.crown(center,rad,110 if h>5 else 65,spread*.22,rng,global_center,spec['tint'])
        m.crown((lean,0,h*.88),(spread*.62,spread*.59,h*.115),160 if h>5 else 90,spread*.22,rng,global_center,spec['tint'])
        m.crown((lean*.85,0,h*.72),(spread*.96,spread*.90,h*.20),360 if h>5 else 160,spread*.23,rng,global_center,spec['tint'])
    else:
        w=spec['w']; global_center=(0,0,h*.52)
        fans = {
            'hazel_round':[(-.33,.11,.45,.23,.18),(.23,.13,.65,.25,.19),(-.08,.03,.82,.23,.18),(.13,-.22,.30,.20,.13)],
            'hazel_tall':[(-.25,-.05,.40,.20,.14),(.21,.09,.63,.21,.17),(-.04,.10,.83,.18,.17),(.31,-.11,.31,.17,.12)],
            'woodland_low':[(-.34,.06,.44,.22,.21),(.20,.17,.72,.26,.22),(.09,-.18,.42,.20,.19)],
        }
        for x,y,z,rx,rz in fans[name]:
            tip=Vector((w*x,w*y,h*z))
            path=curve((0,0,0),tip,(-x*w*.18,0,-h*.05),5)
            m.tube(path,[h*(.020*(1-i/6)+.002) for i in range(6)],6)
            m.crown(tip,(w*rx,w*rx*.69,h*rz),72,w*.092,rng,global_center,spec['tint'])
            side=tip+Vector((w*rx*.64,-w*rx*.58,-h*rz*.90))
            m.tube(curve(path[3],side,(0,0,-h*.025),3),[h*.010,h*.008,h*.005,h*.002],5)
            m.crown(side,(w*rx*.54,w*rx*.48,h*rz*.60),27,w*.066,rng,global_center,spec['tint'])
    obj=m.object(name)
    top=max(v.co.z for v in obj.data.vertices)
    obj.data.transform(Matrix.Scale(h/top,4))
    return obj

def fern():
    m=Mesh()
    for j in range(9):
        a=j*2.399963; d=Vector((math.cos(a),math.sin(a),0)); side=Vector((-d.y,d.x,0))
        points=[d*(i/10*.66)+Vector((0,0,.06+.48*math.sin(i/10*math.pi*.80))) for i in range(11)]
        points[0]=Vector((0,0,0))
        m.tube(points,[.008*(1-i/12) for i in range(11)],5)
        for i in range(1,10):
            t=i/10; center=d*(t*.66)+Vector((0,0,.06+.48*math.sin(t*math.pi*.80)))
            width=.16*math.sin(t*math.pi)**.7
            for sign in (-1,1):
                p=center+side*sign*width+d*.065
                pts=[center-d*.025,p-d*.04,p+d*.05,center+d*.035]
                m.face(pts,[Vector((d.x*.3,d.y*.3,1)).normalized()]*4,rgba('7b9854'),material=1)
    obj=m.object('fern_fan')
    obj.data.materials[1]=FERN
    return obj

def aim(obj,point): obj.rotation_euler=(Vector(point)-obj.location).to_track_quat('-Z','Y').to_euler()

def main():
    global BARK,LEAF,FERN
    bpy.context.preferences.filepaths.save_version=0
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
    scene=bpy.context.scene; scene.name='Forest_Vegetation_Atelier'
    scene.unit_settings.system='METRIC'
    texture=atlas()
    bark_image=bpy.data.images.load(str(STYLE/'bark.png'),check_existing=True)
    BARK=toon_material('FV_Bark_Preview','514543','906c51','b69a70',bark_image,bark=True)
    LEAF=toon_material('FV_Foliage_Preview','304e48','64864d','a9b968',texture)
    FERN=toon_material('FV_Fern_Preview','304e48','64864d','a9b968')
    export_bark=export_material('FV_Bark',bark_image,bark=True)
    export_leaf=export_material('FV_Foliage',texture)
    export_fern=export_material('FV_Foliage_Fern')
    assets=[build(name,spec) for name,spec in SPECS.items()]+[fern()]
    manifest={'revision':5,'authoring':'Blender '+bpy.app.version_string,'assets':[],
              'references':['https://www.bilibili.com/video/BV1Knsd6eEKu/','https://www.bilibili.com/video/BV1HPnU64EBZ/','https://github.com/nicoo-474/procedural-tree'],
              'normal_method':'ellipsoid gradient per branch crown, 18% whole-crown blend; custom corner normals baked into GLB',
              'character_reference':'res://assets/textures/sprites/amber_fox_style_v2/greatsword_v4/sources/guard-keyframe-256.png'}
    for obj in assets:
        bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
        obj.data.calc_loop_triangles(); tris=len(obj.data.loop_triangles)
        assert tris<=8000,(obj.name,tris)
        obj.data.materials[0]=export_bark; obj.data.materials[1]=export_fern if obj.name=='fern_fan' else export_leaf
        bpy.ops.export_scene.gltf(filepath=str(OUT/(obj.name+'.glb')),export_format='GLB',use_selection=True,
                                  export_normals=True,export_texcoords=True,export_vertex_color='ACTIVE',export_all_vertex_colors=False,
                                  export_materials='EXPORT',export_yup=True,export_extras=True)
        obj.data.materials[0]=BARK; obj.data.materials[1]=FERN if obj.name=='fern_fan' else LEAF
        manifest['assets'].append({'name':obj.name,'triangles':tris,'size':list(obj.dimensions),
                                  'category':'tree' if obj.name in ('oak_broad','hornbeam_tall','oak_sweeping','oak_sapling') else 'plant',
                                  'root_origin':[0,0,0],'source':obj.name+'.glb'})
        print('ASSET',obj.name,tris,flush=True)
    (META/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
    # Individual assets remain editable; the layout is an inspection board, not game placement.
    positions=[(-10,4,0),(0,6,0),(10,4,0),(-7,-5,0),(-2.5,-5,0),(2.3,-5,0),(7,-4,0),(0,-7,0)]
    for obj,pos in zip(assets,positions): obj.location=pos
    bpy.ops.mesh.primitive_plane_add(size=200)
    ground=bpy.context.object; ground.name='Review_Ground'; ground.location.z=-.025
    ground.data.materials.append(toon_material('Review_Soil','aca786','c7c2a1','ded8bc'))
    bpy.ops.object.light_add(type='SUN', location=(-10,-15,20)); sun=bpy.context.object; sun.name='Review_Sun'
    sun.data.energy=2; sun.rotation_euler=(.48,-.55,-.6)
    world=bpy.data.worlds.new('Review_World'); scene.world=world; world.use_nodes=True
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND'); bg.inputs[0].default_value=(.48,.57,.61,1); bg.inputs[1].default_value=.45
    bpy.ops.object.camera_add(location=(27,-44,29)); camera=bpy.context.object; camera.name='Review_Camera'
    camera.data.type='ORTHO'; camera.data.ortho_scale=44; aim(camera,(0,2,8)); scene.camera=camera
    # Retain the factory Eevee engine identifier; it changed in Blender 5.2.
    scene.render.resolution_x=1600; scene.render.resolution_y=1100; scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'; scene.view_settings.view_transform='Standard'
    scene.render.filepath=str(PREVIEW/'kit_blender.png')
    texture.pack()
    bark_image.pack()
    bpy.ops.object.select_all(action='DESELECT')
    for obj in assets: obj.select_set(True)
    bpy.context.view_layer.objects.active=assets[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'source/forest_vegetation.blend'))
    bpy.ops.render.render(write_still=True)
    print('FOREST_KIT_DONE',flush=True)

if __name__=='__main__': main()
