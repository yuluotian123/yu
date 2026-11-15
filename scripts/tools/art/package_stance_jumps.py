"""提取跳跃 RGB 并核对 Aseprite 交付，生成无损预览和逐区域审核清单。"""
import argparse
import hashlib
import json
import os
from pathlib import Path

import av
import numpy as np
from PIL import Image, ImageDraw
from package_stance_attacks import pack_apng

ROOT = Path(__file__).resolve().parents[3]
GROUP = 'amber_fox_style_v2/greatsword_jump'
META = ROOT/'assets/resources/sprites'/GROUP
TEX = ROOT/'assets/textures/sprites'/GROUP
REVIEW = ROOT/'.godot/art-validation/greatsword_jump'


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2)+'\n', 'utf-8')


def prepare(config):
    for spec in config['clips']:
        folder=TEX/spec['stance']/'source-rgb'
        folder.mkdir(parents=True, exist_ok=True)
        (folder/'.gdignore').touch()
        wanted={f['source'] for f in spec['frames'] if not (f.get('reference') or f.get('idle'))}
        with av.open(ROOT/spec['video']) as video:
            for n,frame in enumerate(video.decode(video=0)):
                if n in wanted:
                    frame.to_image().convert('RGB').resize(tuple(config.get('source_canvas',config['canvas'])),Image.Resampling.NEAREST).save(folder/f'{n:03d}.png')
                    wanted.remove(n)
        assert not wanted, wanted


def package(config):
    manifest=json.loads((META/'manifest.json').read_text('utf-8'))
    w,h=config['canvas'];ox,oy=config.get('reference_offset',config.get('idle_offset'))
    sx,sy=config.get('source_offset',[0,0])
    results={}
    corrections=json.loads((ROOT/config['pixel_corrections']).read_text('utf-8'))['clips'] if config.get('pixel_corrections') else {}
    for spec,clip in zip(config['clips'],manifest['clips']):
        path=TEX/spec['stance']
        frames=[Image.open(path/'frames'/f'jump_{i:03d}.png').convert('RGBA') for i in range(len(spec['frames']))]
        atlas=np.array(Image.open(path/'jump-atlas.png').convert('RGBA'))
        idle=np.zeros((h,w,4),dtype=np.uint8)
        raw=np.array(Image.open(ROOT/spec.get('reference_frame',spec.get('idle_frame'))).convert('RGBA'))
        idle[oy:oy+raw.shape[0],ox:ox+raw.shape[1]]=raw
        for i,(im,frame,record) in enumerate(zip(frames,spec['frames'],clip['records'])):
            rgba=np.array(im);mask=rgba[:,:,3]>0
            assert np.isin(rgba[:,:,3],[0,255]).all()
            if frame.get('reference') or frame.get('idle'):
                expected=idle
            else:
                rgb=np.array(Image.open(path/'source-rgb'/f"{frame['source']:03d}.png").convert('RGB'))
                a=rgb.astype(np.int16)
                foreground=~((a[:,:,1]-a[:,:,0]>config['chroma_dominance'])&(a[:,:,1]-a[:,:,2]>config['chroma_dominance']))
                source=np.concatenate((rgb,(foreground*255).astype(np.uint8)[:,:,None]),axis=2)
                for x,y,r,g,b,alpha in corrections.get(spec['stance'],{}).get(str(i),{}).get('restore',[]):
                    source[y,x]=[r,g,b,alpha]
                dy=sy+frame['root_y'];expected=np.zeros((h,w,4),dtype=np.uint8)
                yy,xx=np.where(source[:,:,3]>0)
                assert (xx+sx>=0).all() and (xx+sx<w).all() and (yy+dy>=0).all() and (yy+dy<h).all(), (spec['stance'],i,'clipped foreground')
                expected[yy+dy,xx+sx]=source[yy,xx]
            assert np.array_equal(rgba[:,:,3],expected[:,:,3]), (spec['stance'],i,'alpha')
            assert np.array_equal(rgba[mask],expected[mask]), (spec['stance'],i,'RGB')
            independent=np.array(Image.open(path/'sources/aseprite-export'/f'jump_{i:03d}.png').convert('RGBA'))
            assert np.array_equal(rgba,independent), (spec['stance'],i,'Aseprite export')
            assert np.array_equal(rgba,atlas[i//6*h:(i//6+1)*h,i%6*w:(i%6+1)*w])
            assert frame['duration_ms']==clip['aseprite_durations_ms'][i]
            record['sha256']=hashlib.sha256((path/'frames'/f'jump_{i:03d}.png').read_bytes()).hexdigest()
        times=[f['duration_ms'] for f in spec['frames']]
        pack_apng(path/'jump.apng',frames,times)
        audit=META/'audit'/f"{spec['stance']}.json"
        relative=lambda p:Path(os.path.relpath(p,audit.parent)).as_posix()
        # Region boxes follow the root-extracted character; guard/weapon box differs by stance.
        regions=spec['audit_regions']
        write(audit,{
            'authoring_mode':'comfyui_with_aseprite_packaging',
            'reference_frames':relative(path/'sources/aseprite-export')+'/jump_*.png',
            'reference_source':'Independent RGBA export reopened from the saved jump.aseprite.',
            'frames':relative(path/'frames')+'/jump_*.png',
            'source_indices':list(range(len(frames))), 'frame_size':[w,h],
            'frame_durations_ms':times,'binary_alpha':True,'preserve_inset':0,'rgb_tolerance':0,
            'expected_masks':relative(path/'sources/aseprite-export')+'/jump_*.png',
            'expected_mask_source':'Saved Aseprite source alpha; verifies export fidelity only, not source chroma accuracy.',
            'regions':regions,
        })
        # Complete contact sheets on light and dark grounds, all frames in order.
        for start in range(0,len(frames),8):
            sheet=Image.new('RGB',(w*4,(h+22)*4),(24,27,33));draw=ImageDraw.Draw(sheet)
            for i,im in enumerate(frames[start:start+8]):
                for side,color in enumerate([(24,27,33),(230,230,224)]):
                    x=i%4*w;y=(i//4*2+side)*(h+22)
                    sheet.paste(color,(x,y+22,x+w,y+22+h));sheet.paste(im,(x,y+22),im)
                    draw.text((x+6,y+4),f"{spec['stance']} {start+i}",fill='white')
            sheet.save(path/'review'/f'contact-{start:02d}.png')
        results[spec['stance']]={'frames':len(frames),'duration_ms':sum(times),'source_visible_rgb_exact_except_recorded_contour_repairs':True,
            'repaired_contour_pixels':sum(len(e['restore']) for e in corrections.get(spec['stance'],{}).values()),
            'source_chroma_and_root_translation_exact':True,'binary_alpha':True,
            'independent_aseprite_export_exact':True,'atlas_rgba_exact':True,'apng_rgba_and_timing_exact':True,
            'independent_foreground_mask_for_video':False}
    write(META/'manifest.json',manifest)
    write(REVIEW/'media-validation.json',results)
    print(json.dumps(results))


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--prepare',action='store_true');args=parser.parse_args()
    config=json.loads((META/'production.json').read_text('utf-8'))
    prepare(config) if args.prepare else package(config)
