"""Bake tangent normals and material-specific roughness from the painted atlas.

Run with Blender --background --factory-startup --python this_file.py.
No lighting, AO or directional shadows are painted into these maps.
"""
import bpy
import json
from pathlib import Path

ROOT=Path('D:/Project/GitHub/yu')
OUT=ROOT/'assets/textures/environment/forest_village/houses'
scene=bpy.context.scene
for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
try:scene.render.engine='CYCLES'
except TypeError as error:raise RuntimeError('Cycles is required for data-map baking') from error
scene.cycles.samples=4
scene.cycles.device='CPU'
scene.render.bake.margin=2
source=bpy.data.images.load(str(OUT/'painterly_house_atlas_v3.png'),check_existing=True)

# low roughness, high roughness, bump depth (one chart spans one metre).
profiles=[('Plaster',.86,.98,.012),('Wood',.70,.91,.022),('WoodLight',.67,.88,.020),('Roof',.64,.88,.023),
          ('Stone',.80,.98,.035),('Slate',.74,.94,.026),('Thatch',.89,.99,.030),('EndGrain',.76,.94,.025),
          ('Plant',.68,.90,.018),('Lichen',.85,.99,.022),('Iron',.39,.66,.007),('RippleGlass',.12,.29,.003),
          ('Turf',.89,.99,.040),('Reed',.82,.96,.025),('Glass',.10,.23,.002),('Soil',.89,.99,.028)]
vertices=[];faces=[]
for row in range(4):
    for col in range(4):
        i=len(vertices);y=3-row
        vertices.extend([(col,y,0),(col+1,y,0),(col+1,y+1,0),(col,y+1,0)])
        faces.append((i,i+1,i+2,i+3))
mesh=bpy.data.meshes.new('MaterialBakeCharts')
mesh.from_pydata(vertices,[],faces);mesh.update()
uv=mesh.uv_layers.new(name='UVMap')
obj=bpy.data.objects.new('MaterialBakeCharts',mesh);scene.collection.objects.link(obj)
bpy.context.view_layer.objects.active=obj;obj.select_set(True)
graphs=[]
for index,face in enumerate(mesh.polygons):
    row,col=divmod(index,4)
    for loop,coordinate in zip(face.loop_indices,[(col/4,1-(row+1)/4),((col+1)/4,1-(row+1)/4),((col+1)/4,1-row/4),(col/4,1-row/4)]):uv.data[loop].uv=coordinate
    kind,low,high,depth=profiles[index]
    mat=bpy.data.materials.new('Bake_'+kind);nodes=mat.node_tree.nodes;links=mat.node_tree.links
    bsdf=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    output=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    tex=nodes.new('ShaderNodeTexImage');tex.image=source
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.7;bump.inputs['Distance'].default_value=depth
    links.new(tex.outputs['Color'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bsdf.inputs['Normal'])
    mapping=nodes.new('ShaderNodeMapRange')
    mapping.inputs['From Min'].default_value=0;mapping.inputs['From Max'].default_value=1
    mapping.inputs['To Min'].default_value=high;mapping.inputs['To Max'].default_value=low
    links.new(tex.outputs['Color'],mapping.inputs['Value']);links.new(mapping.outputs['Result'],bsdf.inputs['Roughness'])
    target=nodes.new('ShaderNodeTexImage');nodes.active=target
    face.material_index=index;mesh.materials.append(mat)
    graphs.append((nodes,links,bsdf,output,mapping,target,kind))

valid=[e.identifier for e in bpy.ops.object.bake.get_rna_type().properties['type'].enum_items]
for bake_type,suffix in [('NORMAL','normal'),('ROUGHNESS','roughness'),('EMIT','orm')]:
    assert bake_type in valid
    image=bpy.data.images.new('House_'+suffix,2048,2048,alpha=False,float_buffer=False,is_data=True)
    image.colorspace_settings.name='Non-Color'
    for nodes,links,bsdf,output,mapping,target,kind in graphs:
        target.image=image;nodes.active=target
        if bake_type=='EMIT':
            combine=nodes.new('ShaderNodeCombineColor')
            combine.inputs[0].default_value=1.0
            links.new(mapping.outputs['Result'],combine.inputs[1])
            combine.inputs[2].default_value=1.0 if kind=='Iron' else 0.0
            emission=nodes.new('ShaderNodeEmission');emission.inputs['Strength'].default_value=1
            links.new(combine.outputs[0],emission.inputs['Color']);links.new(emission.outputs[0],output.inputs['Surface'])
    print('BAKING',bake_type,flush=True)
    bpy.ops.object.bake(type=bake_type)
    image.filepath_raw=str(OUT/('painterly_house_atlas_v3_'+suffix+'.png'))
    image.file_format='PNG';image.save()
    print('SAVED',image.filepath_raw,flush=True)

manifest={'workflow':'Blender Cycles tangent normal / roughness / emission ORM bake from painted albedo luminance',
          'size':[2048,2048],'normal_convention':'OpenGL +Y tangent space','orm_channels':{'R':'1 (no baked ambient occlusion)','G':'material roughness','B':'1 for iron, 0 otherwise'},
          'charts':[{'kind':p[0],'roughness_range':[p[1],p[2]],'bump_depth_m':p[3]} for p in profiles],
          'files':['assets/textures/environment/forest_village/houses/painterly_house_atlas_v3_'+s+'.png' for s in ['normal','roughness','orm']]}
(ROOT/'assets/resources/art/forest_village/houses_surface_maps_v3.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
print('HOUSE_SURFACE_MAPS_COMPLETE',flush=True)
