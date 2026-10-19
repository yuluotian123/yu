"""Rebuild the greatsword sprite suite from its preserved ComfyUI sources and frame map.

Requires av, numpy, scipy and Pillow. Never paints faces or quantizes visible RGB.
"""
import hashlib
import json
import math
from pathlib import Path

import av
import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
TEX = ROOT / 'assets/textures/sprites/amber_fox_style_v2/greatsword_v4'
META = ROOT / 'assets/resources/sprites/amber_fox_style_v2/greatsword_v4'
VIDEO = ROOT / 'assets/videos/characters/amber_fox_style_v2/greatsword_v4'


def decode(path):
    with av.open(str(path)) as container:
        stream = container.streams.video[0]
        info = {'size': [stream.width, stream.height], 'fps': float(stream.average_rate)}
        frames = [np.array(f.to_image().convert('RGB').resize((256, 256), Image.Resampling.NEAREST))
                  for f in container.decode(video=0)]
    return frames, info


def cutout(rgb):
    r, g, b = np.moveaxis(rgb.astype(int), 2, 0)
    mask = ~((g-r > 38) & (g-b > 38))
    # Remove only isolated codec specks, never disconnected nearby hair or blade tips.
    labels, _ = ndimage.label(mask, structure=np.ones((3, 3)))
    sizes = np.bincount(labels.ravel()); sizes[0] = 0
    main = labels == sizes.argmax()
    distance = ndimage.distance_transform_edt(~main)
    for label, size in enumerate(sizes[1:], 1):
        part = labels == label
        if size <= 3 and distance[part].min() > 3: mask[part] = False
    core = ndimage.binary_erosion(mask, iterations=3)
    green = (g > r+3) & (g > b+3)
    clean = mask & ~green
    nearest = ndimage.distance_transform_edt(~clean, return_distances=False, return_indices=True)
    edits = mask & ~core & green
    rgba = np.zeros((256, 256, 4), dtype=np.uint8)
    rgba[:, :, :3] = rgb
    rgba[edits, :3] = rgb[nearest[0][edits], nearest[1][edits]]
    rgba[:, :, 3] = mask.astype(np.uint8)*255
    rgba[~mask, :3] = 0
    assert np.array_equal(rgba[:, :, :3][core], rgb[core])
    return Image.fromarray(rgba), int(edits.sum())


def track_face(rgb, previous):
    r, g, b = np.moveaxis(rgb.astype(int), 2, 0)
    skin = (r>165)&(g>95)&(b>70)&(r-g>15)&(g-b>6)&(b>r*.5)&(g<r*.91)
    labels, count = ndimage.label(skin)
    best = None
    for label in range(1, count+1):
        y, x = np.where(labels == label)
        if len(x)<50 or x.max()-x.min()>40 or y.max()-y.min()>40: continue
        center=np.array([x.mean(),y.mean()]); distance=np.linalg.norm(center-previous)
        if distance>55: continue
        score=distance-.02*min(len(x),200)
        if best is None or score<best[0]:best=(score,center)
    if best is not None:previous=best[1]
    x,y=map(lambda n:int(round(n)),previous)
    return [max(0,x-18),max(0,y-20),min(256,x+19),min(256,y+21)],previous


def apng(path, frames, fps):
    durations = [round((i+1)*1000/fps)-round(i*1000/fps) for i in range(len(frames))]
    frames[0].save(path, format='PNG', save_all=True, append_images=frames[1:],
                   duration=durations, loop=0, disposal=0, blend=0)
    with Image.open(path) as check:
        assert check.n_frames == len(frames)
        for i, original in enumerate(frames):
            check.seek(i)
            assert np.array_equal(np.array(check.convert('RGBA')),np.array(original))


def export():
    mapping=json.loads((META/'selection.json').read_text(encoding='utf8'))
    animations=mapping['animations']; sources=mapping['sources']
    caches={}; records={}; output={}; audit=[]
    for clip, source in sources.items():
        video=ROOT/source['video'];raw,info=decode(video); caches[clip]=raw
        indices=sorted({i for spec in animations.values() for c,i in spec['frames'] if c==clip})
        if not indices:continue
        (TEX/'frames'/clip).mkdir(parents=True,exist_ok=True)
        (TEX/'sources'/clip/'rgb').mkdir(parents=True,exist_ok=True)
        (TEX/'reviews'/clip).mkdir(parents=True,exist_ok=True)
        (META/'audits'/clip).mkdir(parents=True,exist_ok=True)
        face_boxes=[]; previous=np.array([128,82],float);changes=[]
        # Track through every source frame so fast movement does not break region correspondence.
        tracked=[]
        for rgb in raw:
            face,previous=track_face(rgb,previous);tracked.append(face)
        # Skin components can merge with a sleeve or follow a hand during occlusion.
        # These head windows were checked against the complete corresponding RGB frames.
        if clip == 'apex-a':
            for index, box in {0:[107,64,149,106],2:[108,72,149,114],4:[108,82,149,124],
                               6:[109,79,151,121],8:[107,52,149,94],12:[107,52,149,94],
                               20:[106,68,148,110],38:[107,68,149,110]}.items(): tracked[index]=box
        if clip == 'follow-a':
            for index in (9,10,12,14,15): tracked[index]=[105,64,148,110]
        if clip == 'death':
            for index in range(30,len(tracked)): tracked[index]=[40,195,82,235]
        if clip == 'dash': tracked[60]=[128,76,171,123]
        # Verified return-to-guard head position; skin tracking otherwise follows the forward hand.
        if clip == 'dash':
            for i in range(64,len(tracked)): tracked[i]=[109,62,148,103]
        pairs=Image.new('RGB',(1024,math.ceil(len(indices)/2)*280),'#e4e7ea')
        pd=ImageDraw.Draw(pairs)
        dark=Image.new('RGB',(1536,math.ceil(len(indices)/6)*280),'#1c2330')
        light=Image.new('RGB',dark.size,'#f5f2e8')
        face_board=Image.new('RGB',(2*320,math.ceil(len(indices)/2)*188),'#b7c1cc')
        for n,index in enumerate(indices):
            rgb=raw[index]; frame,edited=cutout(rgb); output[(clip,index)]=frame
            p=TEX/'frames'/clip/f'{clip}_{index:03}.png';frame.save(p)
            Image.fromarray(rgb).save(TEX/'sources'/clip/'rgb'/f'{clip}_{index:03}.png')
            x=n%2*512;y=n//2*280
            pairs.paste(Image.fromarray(rgb),(x,y+24));pairs.paste(frame,(x+256,y+24),frame)
            pd.text((x+4,y+4),f'{clip} source {index}: RGB / alpha',fill='black')
            for board in (light,dark):
                board.paste(frame,(n%6*256,n//6*280+24),frame)
                ImageDraw.Draw(board).text((n%6*256+5,n//6*280+5),str(index),fill='#858585')
            face=tracked[index];face_boxes.append(face)
            raw_face=Image.fromarray(rgb).crop(face).resize((148,164),Image.Resampling.NEAREST)
            final_face=Image.new('RGB',(37,41),'#e2e4e6');fc=frame.crop(face);final_face.paste(fc,(0,0),fc)
            face_board.paste(raw_face,(n%2*320,n//2*188+20))
            face_board.paste(final_face.resize((148,164),Image.Resampling.NEAREST),(n%2*320+160,n//2*188+20))
            ImageDraw.Draw(face_board).text((n%2*320+4,n//2*188+4),f'{index}: source / output',fill='black')
            alpha=np.array(frame)[:,:,3]>0
            records[(clip,index)]={'file':str(p.relative_to(ROOT)).replace('\\','/'),'source':clip,
                'source_index':index,'bounds':frame.getbbox(),'opaque_pixels':int(alpha.sum()),
                'contour_pixels_recolored':edited,'sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
            changes.append(edited)
        pairs.save(TEX/'reviews'/clip/'paired.png')
        light.save(TEX/'reviews'/clip/'light.png');dark.save(TEX/'reviews'/clip/'dark.png')
        face_board.save(TEX/'reviews'/clip/'faces-paired.png')
        uses=[spec['fps'] for spec in animations.values() if all(c==clip for c,_ in spec['frames'])]
        manifest={'video':str(video),'frames':str(TEX/'frames'/clip/f'{clip}_*.png'),
            'source_indices':indices,'frame_size':[256,256],'output_fps':uses[0] if uses else 30,'binary_alpha':True,
            'preserve_inset':3,'rgb_tolerance':0,'foot_anchor':[128,219],
            'regions':[{'name':'face','bounds_by_frame':face_boxes},
                {'name':'hair','bounds':[70,25,165,115]},
                {'name':'upper_body','bounds':[80,90,180,170]},
                {'name':'hands_and_weapon','bounds':[115,15,250,225]},
                {'name':'legs_and_feet','bounds':[70,135,205,235]},
                {'name':'tail','bounds':[10,85,125,230]}]}
        if clip == 'death':
            prone={'hair':[24,180,87,235], 'upper_body':[75,190,150,237],
                   'hands_and_weapon':[85,190,239,240], 'legs_and_feet':[130,192,210,237],
                   'tail':[64,130,145,225]}
            for region in manifest['regions'][1:]:
                standing=region.pop('bounds')
                region['bounds_by_frame']=[prone[region['name']] if i>=30 else standing for i in indices]
        (META/'audits'/clip/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
        audit.append({'clip':clip,'source':info,'source_frames':len(raw),'selected_frames':len(indices),
            'source_sha256':hashlib.sha256(video.read_bytes()).hexdigest(),'contour_pixels_recolored':sum(changes),
            'interior_rgb_exact':True,'independent_silhouette_reference':False,'status':'needs_visual_review'})
    unique=list(output); positions={key:n for n,key in enumerate(unique)}
    atlas=Image.new('RGBA',(2048,math.ceil(len(unique)/8)*256))
    for key,n in positions.items():atlas.paste(output[key],(n%8*256,n//8*256))
    atlas.save(TEX/'all-actions-atlas.png')
    lines=['[gd_resource type="SpriteFrames" format=3]','',
           '[ext_resource type="Texture2D" path="res://assets/textures/sprites/amber_fox_style_v2/greatsword_v4/all-actions-atlas.png" id="atlas"]']
    for key,n in positions.items():
        lines+=['',f'[sub_resource type="AtlasTexture" id="frame_{n}"]','atlas = ExtResource("atlas")',
                f'region = Rect2({n%8*256}, {n//8*256}, 256, 256)','filter_clip = true']
    animation_entries=[];animation_map={};preview_states={}
    (TEX/'previews').mkdir(exist_ok=True)
    for name,spec in animations.items():
        keys=[(c,i) for c,i in spec['frames']];frames=[output[k] for k in keys]
        entries=',\n'.join('{"duration": 1.0, "texture": SubResource("frame_%d")}'%positions[k] for k in keys)
        animation_entries.append('{"frames": [\n'+entries+'], "loop": '+str(spec['loop']).lower()+
            ', "name": &"'+name+'", "speed": '+str(spec['fps'])+'}')
        apng(TEX/'previews'/f'{name}-transparent-v4.png',frames,spec['fps'])
        yy,xx=np.mgrid[:256,:256]
        background=Image.fromarray(np.where(((xx//16+yy//16)%2)[:,:,None],
            np.array([218,224,230]),np.array([242,244,247])).astype(np.uint8))
        composite=[]
        for frame in frames:
            im=background.convert('RGBA');im.alpha_composite(frame);composite.append(im)
        apng(TEX/'previews'/f'{name}-checker-v4.png',composite,spec['fps'])
        preview_states[name]=frames
        animation_map[name]={'fps':spec['fps'],'loop':spec['loop'],'duration_seconds':len(keys)/spec['fps'],
            'frames':[records[k] for k in keys]}
    lines+=['','[resource]','animations = ['+',\n'.join(animation_entries)+']','']
    (META/'sprite_frames.tres').write_text('\n'.join(lines),encoding='utf8')
    (META/'animation-map.json').write_text(json.dumps({'frame_size':[256,256],'foot_anchor':[128,219],
        'unique_frames':len(unique),'animations':animation_map},indent=2),encoding='utf8')
    (META/'processing-report.json').write_text(json.dumps({'clips':audit,'global_quantization':False,
        'source_transform':'whole 512x512 canvas to 256x256, nearest; no per-frame fitting or translation',
        'alpha':'green chroma key; only isolated <=3px codec specks removed; spill recoloring restricted to outer 3px',
        'mask_coverage':'No independent full silhouette reference. Numerical checks prove retained interior RGB only; manual region review required.'},indent=2),encoding='utf8')
    names=list(animations); count=36;boards=[]
    for n in range(count):
        board=Image.new('RGBA',(3*256,math.ceil(len(names)/3)*280),'#e5e8ec')
        for j,name in enumerate(names):
            spec=animations[name]
            index=int(n*spec['fps']/12)
            index=index%len(preview_states[name]) if spec['loop'] else min(index,len(preview_states[name])-1)
            frame=preview_states[name][index]
            board.alpha_composite(frame,(j%3*256,j//3*280+24))
            ImageDraw.Draw(board).text((j%3*256+6,j//3*280+5),name,fill='black')
        boards.append(board)
    apng(TEX/'all-actions-preview-v4-timed.png',boards,12)
    print(json.dumps({'unique_frames':len(unique),'animations':{k:len(v['frames']) for k,v in animations.items()}}))


if __name__=='__main__':
    export()
