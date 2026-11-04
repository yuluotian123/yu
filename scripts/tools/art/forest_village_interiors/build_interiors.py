"""Author ten furnished cutaway interiors using the forest house palette/UV tools.

Blender --background --python this_file.py. Geometry is authored offline, not at runtime.
The GLB is a spaced library; each room's local mesh is saved separately by the importer.
"""
import bpy
import json
import math
import random
import tempfile
from pathlib import Path
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT/'assets/models/environment/forest_village/interiors'
META = ROOT/'assets/resources/art/forest_village/interiors_models.json'
for folder in (OUT/'source',META.parent,ROOT/'.godot/forest-village-interiors/gltf-temp'):
    folder.mkdir(parents=True,exist_ok=True)
(OUT/'source/.gdignore').write_text('',encoding='utf-8')
tempfile.tempdir=str(ROOT/'.godot/forest-village-interiors/gltf-temp')
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
# Reuse the maintained mesh/atlas definitions, before the exterior-specific detail pass.
ns={'__file__':str(ROOT/'scripts/tools/art/forest_village_houses/build_houses.py')}
source=(ROOT/'scripts/tools/art/forest_village_houses/build_houses.py').read_text(encoding='utf-8')
exec(compile(source.split('\nsetup_preview()\n')[0],'house_palette_and_mesh.py','exec'),ns)
Mesh=ns['Mesh']
SCENE=ns['SCENE']
SCENE.name='ForestVillageInteriors'
RNG=random.Random(2026102802)
SPECS=[
    ('01_chief','Chief','村长家',6.2,5.0,'议事长桌、账册和家庭起居','b59765'),
    ('02_host','Host','杂货与待客户',4.4,4.8,'交易柜台、货架与客居床位','9aab91'),
    ('03_smith','Smith','工具修补户',5.4,4.8,'石砌炉台、铁砧与工具工作区','8d9b97'),
    ('04_carpenter','Carpenter','木工户',6.0,4.8,'木工台、锯架与木料收纳','bd9960'),
    ('05_herbalist','Herbalist','草药照护户',5.2,4.8,'草药瓶柜、调配台与照护床','879a75'),
    ('06_forester','Forester','伐木户',5.0,4.8,'原木围护、斧锯架与柴堆','977452'),
    ('07_gatherer','Gatherer','猎采户',5.2,4.8,'猎采架、晾晒物与采集篮','889773'),
    ('08_farmer_east','FarmerEast','东田农户',6.2,5.2,'备餐桌、粮袋和蔬菜篮','bfaa77'),
    ('09_farmer_west','FarmerWest','西田农户',6.4,4.8,'家庭长桌、织架和谷物储备','b79866'),
    ('10_smallholder','Smallholder','小畜养户',5.6,4.8,'蜂蜜罐、饲料箱与蜂框台','ae975c')]


def p(v): return (v[0],-v[2],v[1])
def s(v): return (v[0],v[2],v[1])
def box(m,c,size,kind='Wood',shade=None,solid=False):
    m.box(p(c),s(size),kind,shade)
    if solid:m.collider(p(c),s(size))
def beam(m,a,b,w=.07,d=None,kind='Wood',shade=None):
    m.beam(p(a),p(b),w,d,kind,shade)
def cylinder(m,a,b,r=.10,kind='Wood',shade=None,sides=10):
    m.tube(p(a),p(b),r,kind,shade,sides)
def vessel(m,x,z,y=.0,r=.13,h=.27,shade='b88c62',kind='Plaster'):
    profile=[(0,.74),(.08,1.0),(.55,1.08),(.85,.73),(1,.70)]
    for k in range(len(profile)-1):
        a,ra=profile[k];b,rb=profile[k+1]
        for j in range(10):
            t0,t1=j*math.tau/10,(j+1)*math.tau/10
            points=[(x+math.cos(t0)*r*ra,y+h*a,z+math.sin(t0)*r*ra),
                    (x+math.cos(t1)*r*ra,y+h*a,z+math.sin(t1)*r*ra),
                    (x+math.cos(t1)*r*rb,y+h*b,z+math.sin(t1)*r*rb),
                    (x+math.cos(t0)*r*rb,y+h*b,z+math.sin(t0)*r*rb)]
            m.poly([p(q) for q in points],kind,shade)
    cylinder(m,(x,y+h-.015,z),(x,y+h,z),r*.67,'Wood','584b36')
def plant(m,x,z,y=.0):
    vessel(m,x,z,y,.14,.22,'b88c62')
    for j in range(7):
        angle=j*2.4
        a=(x,y+.22,z)
        tip=(x+math.cos(angle)*.24,y+.44+RNG.random()*.12,z+math.sin(angle)*.24)
        mid=tuple((a[i]+tip[i])*.5 for i in range(3))
        m.poly([p(a),p((mid[0]-.07,mid[1],mid[2])),p(tip),p((mid[0]+.07,mid[1]+.04,mid[2]))],'Plant')
def furniture_box(m,x,z,w,d,h,kind='WoodLight',shade=None):
    box(m,(x,h/2,z),(w,h,d),kind,shade,True)
def table(m,x,z,w=1.25,d=.85,h=.66):
    for dx in (-w*.37,w*.37):
        for dz in (-d*.34,d*.34):beam(m,(x+dx,.03,z+dz),(x+dx,h-.06,z+dz),.075,.085)
    for j in range(4):box(m,(x+(j-1.5)*w/4,h,z),(w/4-.008,.07,d),'WoodLight')
    m.collider(p((x,h/2,z)),s((w,h,d)))
def stool(m,x,z):
    for dx,dz in [(-.11,-.10),(.11,-.10),(0,.12)]:beam(m,(x+dx,.02,z+dz),(x+dx*.8,.34,z+dz*.8),.045)
    box(m,(x,.36,z),(.36,.07,.34),'WoodLight')
    m.collider(p((x,.19,z)),s((.35,.38,.33)))
def bed(m,x,z,shade):
    box(m,(x,.26,z),(1.10,.16,1.80),'Wood',solid=True)
    for dx in (-.48,.48):
        for dz in (-.80,.80):beam(m,(x+dx,.02,z+dz),(x+dx,.48,z+dz),.09)
    box(m,(x,.55,z-.84),(1.14,.58,.09),'WoodLight')
    box(m,(x,.38,z),(1.01,.17,1.67),'Plaster','d9cdb0')
    box(m,(x,.49,z+.22),(1.03,.09,1.16),'Plaster',shade)
    for xx in (-.33,0,.33):box(m,(x+xx,.537,z+.23),(.018,.005,1.10),'Plaster','d0bb8c')
    box(m,(x,.50,z-.56),(.72,.15,.38),'Plaster','eee1c5')
    m.collider(p((x,.30,z)),s((1.10,.60,1.80)))
def shelf(m,x,z,width=1.4,contents='jars'):
    box(m,(x,.79,z),(width,1.58,.44),'Wood',solid=True)
    # A dark recessed back with boards/shelves gives depth instead of a solid painted cube.
    box(m,(x,.80,z+.235),(width-.12,1.40,.025),'Wood','493a2e')
    for h in (.10,.54,.98,1.48):box(m,(x,h,z+.30),(width,.06,.64),'WoodLight')
    for h in (.59,1.03):
        for j in range(5):
            xx=x+(j-2)*width*.16
            if contents=='books':
                box(m,(xx,h+.16,z+.33),(.12,.31+RNG.uniform(-.03,.05),.23),'Plaster',RNG.choice(['8b563e','bdab74','707d71','b18b55']))
            else:vessel(m,xx,z+.32,h+.035,.065,.18,RNG.choice(['a78355','d7c59f','6f806e','b08a68']))
def hearth(m,x,z):
    furniture_box(m,x,z,1.25,.72,.18,'Stone')
    box(m,(x,.52,z-.24),(1.12,.9,.24),'Stone',solid=True)
    for sign in (-1,1):box(m,(x+sign*.46,.46,z+.05),(.22,.57,.48),'Stone',solid=True)
    box(m,(x,.79,z),(1.28,.20,.70),'Stone')
    box(m,(x,1.13,z-.15),(.69,.65,.40),'Stone')
    cylinder(m,(x-.30,.24,z+.12),(x+.30,.24,z-.02),.075,'Wood','48372a')
    cylinder(m,(x-.25,.25,z-.04),(x+.26,.25,z+.13),.055,'Wood','5a3d28')
    for dx in (-.2,0,.2):
        m.poly([p((x+dx-.07,.25,z+.12)),p((x+dx+.08,.26,z+.13)),p((x+dx,.47+RNG.random()*.1,z+.12))],'Glass','e9a548')
    vessel(m,x,z-.05,.83,.17,.24,'484c45','Iron')
def crate(m,x,z,size=.47):
    furniture_box(m,x,z,size,size,size,'Wood','8f704b')
    for y in (.08,size-.08):box(m,(x,y,z+size/2+.008),(size+.02,.045,.03),'WoodLight')
    beam(m,(x-size*.4,.06,z+size/2+.03),(x+size*.4,size-.06,z+size/2+.03),.035,.045,'WoodLight')
def books(m,x,z,y):
    for j in range(3):box(m,(x,y+.04+j*.045,z),(.24,.04,.30),'Plaster',['895a3f','71816e','c2a66b'][j])
def tools(m,x,z,y=1.1):
    box(m,(x,y,z),(1.25,.12,.08),'WoodLight')
    for j in range(4):
        xx=x+(j-1.5)*.26
        beam(m,(xx,y-.46,z+.09),(xx,y+.03,z+.09),.035,.04,'WoodLight')
        box(m,(xx+.06,y-.02,z+.09),(.19,.08,.08),'Iron')


records=[]
for index,(name,household,title,w,d,theme,accent) in enumerate(SPECS):
    m=Mesh(name+'_interior')
    hw,hd=w/2,d/2
    stonefloor=household in ('Smith','Herbalist','Smallholder')
    # Planked floors (or laid stone slabs), broad plaster/log wall panels and real window recess.
    n=math.ceil(w/(.47 if stonefloor else .23))
    for j in range(n):
        x=-hw+(j+.5)*w/n
        if stonefloor:
            for k in range(6):box(m,(x,-.07,-hd+(k+.5)*d/6),(w/n-.012,.14,d/6-.012),'Stone')
        else:box(m,(x,-.07,0),(w/n-.008,.14,d),'WoodLight','9d7d56' if j%3 else 'ad895d')
    m.collider(p((0,-.15,0)),s((w+.2,.30,d+.2)))
    wallkind='Wood' if household in ('Forester','Gatherer','Carpenter') else 'Plaster'
    # Back window is a genuine opening, with four surrounding wall segments.
    for sign in (-1,1):box(m,(sign*(hw+.62)/2,1.13,-hd),((w-1.24)/2,2.26,.14),wallkind,solid=True)
    box(m,(0,.43,-hd),(1.24,.86,.14),wallkind,solid=True)
    box(m,(0,1.98,-hd),(1.24,.56,.14),wallkind,solid=True)
    for sign in (-1,1):
        # Camera-facing walls are deliberately cut away; their full physics stays in place.
        box(m,(sign*hw,.84,0),(.14,1.68,d),wallkind)
        m.collider(p((sign*hw,1.13,0)),s((.14,2.26,d)))
        frontwidth=hw-.66
        box(m,(sign*(.66+frontwidth/2),.17,hd),(frontwidth,.34,.16),'Wood')
        m.collider(p((sign*(.66+frontwidth/2),1.0,hd)),s((frontwidth,2,.16)))
        beam(m,(sign*.64,.02,hd),(sign*.64,1.12,hd),.10,.13,'WoodLight')
    # Front boundary beyond the exit trigger prevents walking off the scene while loading.
    m.collider(p((0,1.0,hd+.18)),s((1.3,2,.12)))
    box(m,(.88,.48,hd-.10),(.42,.94,.10),'WoodLight')
    for j in range(4):box(m,(.88+(j-1.5)*.095,.48,hd-.04),(.082,.84,.025),'Wood')
    for x in (-hw,hw):beam(m,(x,.02,-hd),(x,2.35,-hd),.14,.16)
    beam(m,(-hw,2.25,-hd),(hw,2.25,-hd),.15,.17)
    for sign in (-1,1):beam(m,(sign*hw,1.7,-hd),(sign*hw,1.7,hd),.10,.15)
    # Recessed cool glazing, timber cross bars and a deep sill.
    box(m,(0,1.25,-hd+.015),(1.12,.69,.025),'Glass','7a9690')
    for xx in (-.60,0,.60):box(m,(xx,1.25,-hd+.09),(.055,.87,.08),'WoodLight')
    for yy in (.84,1.25,1.68):box(m,(0,yy,-hd+.09),(1.25,.055,.08),'WoodLight')
    box(m,(0,.84,-hd+.18),(1.35,.08,.35),'WoodLight')
    plant(m,.35,-hd+.17,.9)
    # Domestic base: a quilted bed, hearth, meal table and substantial storage.
    bed(m,-hw+.72,.25,accent)
    hearth(m,-hw+.78,-hd+.48)
    tx,tz=hw-.92,.30
    table(m,tx,tz,1.15,1.1)
    stool(m,tx,tz+.92)
    vessel(m,tx-.2,tz,.71,.085,.14,'d8c49b')
    box(m,(tx+.20,.71,tz+.05),(.30,.025,.25),'Plaster','ddd0b5')
    shelf(m,hw-.85,-hd+.35,1.35,'books' if household=='Chief' else 'jars')
    box(m,(0,.012,.1),(1.0,.018,2.30),'Plaster',accent)
    for z in (-.93,1.13):box(m,(0,.024,z),(.97,.004,.055),'Plaster','d1bd8f')
    # Job-specific furniture and props distinguish every room.
    if household=='Chief':
        table(m,.64,-.76,.82,1.45)
        books(m,.7,-.95,.71)
        for z in (-.4,-1.2):stool(m,.05,z)
        box(m,(1.0,1.25,-hd+.12),(.72,.68,.035),'WoodLight')
        box(m,(1.0,1.25,-hd+.15),(.62,.58,.015),'Plaster','d2c39c')
    elif household=='Host':
        for z in (-1.25,-.7):crate(m,hw-.33,z,.43)
        books(m,tx+.15,tz-.25,.71)
        box(m,(tx,.80,tz-.42),(1.10,.16,.12),'Wood')
        for j in range(4):vessel(m,hw-.34,-1.25+j*.3,.47,.065,.18,'9f884d')
    elif household=='Smith':
        furniture_box(m,hw-.95,-1.23,.48,.46,.41,'Wood')
        box(m,(hw-.95,.51,-1.23),(.67,.14,.31),'Iron')
        beam(m,(hw-1.22,.54,-1.23),(hw-1.42,.57,-1.23),.08,.14,'Iron')
        tools(m,hw-1.0,-hd+.16,1.62)
        for j in range(3):box(m,(tx-.25+j*.22,.74,tz),(.15,.055,.28),'Iron')
    elif household=='Carpenter':
        tools(m,hw-.85,-hd+.16,1.64)
        for j in range(3):box(m,(tx,.74+j*.045,tz-.08),(1.08,.04,.20),'WoodLight')
        for j in range(5):cylinder(m,(hw-.24,.07+j*.11,.85),(hw-.24,.07+j*.11,1.65),.05,'WoodLight')
    elif household=='Herbalist':
        for j in range(5):vessel(m,tx-.4+j*.19,tz,.72,.045,.14,['849872','a4a774','b48d66'][j%3])
        plant(m,tx+.27,tz-.32,.73)
        for j in range(6):
            x=.78+j*.18
            beam(m,(x,1.63,-hd+.22),(x,1.87,-hd+.22),.013,.015,'WoodLight')
            for k in range(3):cylinder(m,(x,1.43+k*.03,-hd+.24),(x+.035,1.64,-hd+.24),.025,'Plant')
    elif household=='Forester':
        tools(m,hw-.86,-hd+.16,1.64)
        for j in range(10):cylinder(m,(hw-.29,.10+(j%3)*.12,.75+(j//3)*.16),(hw-.29,.10+(j%3)*.12,1.55+(j//3)*.16),.06,'WoodLight')
        for j in range(7):cylinder(m,(-hw+.08,.25+j*.27,-hd+.10),(hw-.1,.25+j*.27,-hd+.10),.08,'Wood','78583d')
    elif household=='Gatherer':
        for x in (hw-.38,hw-1.48):beam(m,(x,.05,1.4),(x,1.65,1.4),.055)
        beam(m,(hw-.38,1.62,1.4),(hw-1.48,1.62,1.4),.06)
        for j in range(5):box(m,(hw-.52-j*.17,1.18,1.4),(.12,.64,.025),'Plaster','a88d60')
        vessel(m,hw-.30,1.88,0,.22,.38,'987d50','WoodLight')
    elif household=='FarmerEast':
        for j in range(3):vessel(m,hw-.30,.9+j*.37,0,.19,.4,'b7a579','Plaster')
        crate(m,hw-.65,1.68,.52)
        for j in range(7):vessel(m,hw-.84+(j%3)*.16,1.5+(j//3)*.13,.53,.06,.10,'7f9851','Plant')
    elif household=='FarmerWest':
        for x in (hw-.26,hw-1.0):beam(m,(x,.05,1.20),(x,1.45,1.20),.065)
        for y in (.40,1.40):beam(m,(hw-1.05,y,1.20),(hw-.20,y,1.20),.06)
        for j in range(12):beam(m,(hw-.94+j*.057,.40,1.21),(hw-.94+j*.057,1.40,1.21),.009,.012,'Plaster','dac49c')
        box(m,(hw-.62,.73,1.19),(.64,.53,.025),'Plaster',accent)
    else:
        for j in range(3):vessel(m,tx-.30+j*.28,tz-.1,.73,.09,.19,'c49f53')
        for j in range(4):
            xx=hw-.30-j*.16
            box(m,(xx,.30,1.35),(.12,.58,.46),'WoodLight','bc9a61')
            box(m,(xx,.31,1.35),(.13,.40,.31),'Plaster','b99548')
        crate(m,hw-.48,1.9,.48)
    # Wall lantern and hooks, with a native light added by the scene importer.
    box(m,(-.85,1.66,-hd+.23),(.22,.32,.20),'Iron')
    box(m,(-.85,1.66,-hd+.345),(.14,.22,.018),'Glass','dfb575')
    obj=m.create()
    obj.name=name+'_interior'
    for key in list(obj.keys()):del obj[key]
    obj['household']=household
    obj['theme']=theme
    bevel=obj.modifiers.new('Worn interior joinery','BEVEL')
    bevel.width=.009;bevel.segments=2
    bpy.context.view_layer.objects.active=obj
    bpy.ops.object.modifier_apply(modifier=bevel.name)
    triangulate=obj.modifiers.new('Export tangent triangles','TRIANGULATE')
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    obj.data.calc_tangents(uvmap='PaintedUV')
    obj.data.calc_loop_triangles()
    tris=len(obj.data.loop_triangles)
    assert tris<65000,(name,tris)
    obj.hide_set(False);obj.hide_render=False
    obj.location=((index%5)*8,(index//5)*8,0)
    records.append({'name':name,'household':household,'display_name':title,'theme':theme,
        'width':w,'depth':d,'triangles':tris,'collision':m.collisions,
        'default_spawn':[0,.52,hd-1.15],'exit_position':[0,.85,hd-.10],
        'visit_points':[[0,.05,0],[tx,.05,tz+.6],[-hw+1.55,.05,.25]],
        'normal_space':'OpenGL tangent +Y','cutaway':True})
    print('INTERIOR_AUTHORED',name,tris,flush=True)

for obj in ns['LIB'].objects:obj.hide_set(False);obj.hide_render=False
bpy.ops.object.select_all(action='DESELECT')
for obj in ns['LIB'].objects:obj.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'forest_village_interiors.glb'),export_format='GLB',use_selection=True,
    export_apply=True,export_materials='EXPORT',export_normals=True,export_texcoords=True,export_tangents=True,
    export_vertex_color='ACTIVE',export_all_vertex_colors=False,export_cameras=False,export_lights=False,export_yup=True)
META.write_text(json.dumps({'revision':1,'assets':records},ensure_ascii=False,indent=2),encoding='utf-8')
bpy.data.libraries.write(str(OUT/'source/forest_village_interiors.blend'),{SCENE},path_remap='RELATIVE_ALL',fake_user=True,compress=True)
print('INTERIORS_DONE',sum(r['triangles'] for r in records),flush=True)
