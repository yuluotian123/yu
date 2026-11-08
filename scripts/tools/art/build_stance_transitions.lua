-- Package approved first/last-frame video motion, keying only the green background.
-- Decode source videos to .godot/stance-switch/aligned-rgb/<pair>/%03d.png at
-- 320x384 with ffmpeg scale=320:384:flags=neighbor before running this script.
local here=debug.getinfo(1,'S').source:gsub('^@',''):gsub('\\','/')
local root=app.params['project'] or assert(here:match('^(.*)/scripts/tools/art/[^/]+$'))
local group='amber_fox_style_v2/greatsword_stance_transition/'
local tex=root..'/assets/textures/sprites/'..group
local meta=root..'/assets/resources/sprites/'..group
local pc=app.pixelColor
local W,H,FPS=320,384,62.4
local bladeCorrections=app.open(tex..'high-mid/sources/blade-corrections.aseprite')
local specs={
 {from='high',to='mid',id='94d0ea6b-004b-4863-b43a-9155bbfbf350',video='94d0ea6b_000.mov',indices={0,1,2,3,4,5,6}},
 {from='mid',to='low',id='3d53cb0d-cb8c-45db-81d0-78c544d44b73',video='3d53cb0d_000.mov',indices={0,2,3,4,5,6,7,8,9,10}},
 {from='high',to='low',id='255a7c33-f1ad-4b9a-8ecf-825ee31bd767',video='255a7c33_000.mov',indices={0,3,4,5,6,7,8,9,10,11}}
}
local function write(path,text)
 local f=assert(io.open(path,'wb'));f:write(text);f:close()
end
local function put(sprite,n,im)
 if n>1 then sprite:newEmptyFrame(n)end
 local cel=sprite.layers[1]:cel(n)
 if cel then cel.image=im else sprite:newCel(sprite.layers[1],n,im,Point(0,0))end
 sprite.frames[n].duration=1/FPS
end
local lines={'[gd_resource type="SpriteFrames" format=3]',''}
local subresources,animations={},{}
local manifest={schema=1,date='2026-11-08',canvas={W,H},foot_anchor={160,347},fps=FPS,
 model='MiniMax H3 local int8, 28 steps, no acceleration LoRA',
 method='Three guided motions; reverse order supplies opposite edges. Exact idle endpoints. Four high-mid blade-only bitmap corrections; no runtime weapon layer. Playback 62.4 fps: 1.3 times the previous 48 fps, 2.6 times source 24 fps.',
 alpha='Green chroma: g-r>18 and g-b>18. Original character RGB retained; four edited blade regions use the image edit as a reference and the existing steel palette. No global palette quantization.',
 audit='All 27 selected forward frames visually checked. Numerical checks cover RGB outside recorded local blade edits, binary alpha, margins and exact endpoints. No independent full foreground annotation.',
 blade_edit={source='high-mid/sources/blade-corrections.aseprite',output_frames={1,2,3,4},length=118,width=19.2},clips={}}
local clips={}
for s,spec in ipairs(specs)do
 local pair=spec.from..'-'..spec.to
 local path=tex..pair..'/'
 for _,dir in ipairs({'frames/','source-rgb/','sources/'})do app.fs.makeAllDirectories(path..dir)end
 local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='换势动作 · 保留源视频色块'
 local atlas=Image(W*5,H*math.ceil(#spec.indices/5),ColorMode.RGB)
 local frames,records={},{}
 for n,sourceIndex in ipairs(spec.indices)do
  local rgb=Image{fromFile=root..'/.godot/stance-switch/aligned-rgb/'..pair..'/'..string.format('%03d.png',sourceIndex)}
  assert(rgb.width==W and rgb.height==H)
  rgb:saveAs(path..'source-rgb/'..string.format('%03d.png',n-1))
  local im=Image(W,H,ColorMode.RGB)
  local endpoint=n==1 and spec.from or n==#spec.indices and spec.to or nil
  if endpoint then
   im=Image{fromFile=root..'/assets/textures/sprites/amber_fox_style_v2/greatsword_stance_idle/'..endpoint..'/frames/idle_000.png'}
  else
   for y=0,H-1 do for x=0,W-1 do
    local c=rgb:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
    if not(g-r>18 and g-b>18)then im:putPixel(x,y,pc.rgba(r,g,b,255))end
   end end
  end
  local repaired,repairCount={},0
  if pair=='high-mid' and n>=2 and n<=5 then
   local cel=bladeCorrections.layers[1]:cel(n-1)
   local corrected=Image(W,H,ColorMode.RGB)
   corrected:drawImage(cel.image,cel.position)
   for y=0,H-1 do for x=0,W-1 do
    if corrected:getPixel(x,y)~=im:getPixel(x,y)then repaired[y*W+x]=true;repairCount=repairCount+1 end
   end end
   im=corrected
  end
  local minX,minY,maxX,maxY,count=W,H,-1,-1,0
  for y=0,H-1 do for x=0,W-1 do
   local c=im:getPixel(x,y);local a=pc.rgbaA(c);assert(a==0 or a==255)
   if a>0 then
    minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x);maxY=math.max(maxY,y);count=count+1
    if not endpoint and not repaired[y*W+x] then local original=rgb:getPixel(x,y);assert(pc.rgbaR(c)==pc.rgbaR(original)and pc.rgbaG(c)==pc.rgbaG(original)and pc.rgbaB(c)==pc.rgbaB(original))end
   end
  end end
  assert(minX>0 and minY>0 and maxX<W-1 and maxY<H-1,'Clipped motion: '..pair..' '..n)
  frames[n]=im
  im:saveAs(path..'frames/'..string.format('transition_%03d.png',n-1))
  local ax,ay=(n-1)%5*W,math.floor((n-1)/5)*H
  atlas:drawImage(im,Point(ax,ay));put(sprite,n,im)
  records[n]={frame=n-1,video_frame=sourceIndex,endpoint_idle=endpoint,bounds={minX,minY,maxX+1,maxY+1},opaque_pixels=count,blade_edit_pixels=repairCount,visible_rgb_exact_outside_edits=not endpoint}
  local id='pair'..s..'_'..n
  subresources[#subresources+1]=string.format('[sub_resource type="AtlasTexture" id="%s"]\natlas = ExtResource("atlas%d")\nregion = Rect2(%d, %d, %d, %d)\nfilter_clip = true\n',id,s,ax,ay,W,H)
 end
 atlas:saveAs(path..'transition-atlas.png')
 sprite:saveAs(path..'sources/transition.aseprite');sprite:close()
 lines[#lines+1]=string.format('[ext_resource type="Texture2D" path="res://assets/textures/sprites/%s%s/transition-atlas.png" id="atlas%d"]',group,pair,s)
 for _,reverse in ipairs({false,true})do
  local from,to=reverse and spec.to or spec.from,reverse and spec.from or spec.to
  local refs={}
  for n=1,#frames do refs[n]=string.format('{"duration": 1.0, "texture": SubResource("pair%d_%d")}',s,reverse and #frames+1-n or n)end
  animations[#animations+1]=string.format('{"frames": [%s], "loop": 0, "name": &"stance_%s_to_%s", "speed": %.1f}',table.concat(refs,', '),from,to,FPS)
 end
 clips[s]=frames
 manifest.clips[s]={pair=pair,forward='stance_'..spec.from..'_to_'..spec.to,reverse='stance_'..spec.to..'_to_'..spec.from,frames=#frames,duration=#frames/FPS,source_video='assets/videos/characters/'..group..spec.video,prompt_id=spec.id,workflow=spec.from..'-to-'..spec.to..'-workflow.json',records=records}
 print(pair..': '..#frames..' frames; binary alpha and visible RGB verified')
end
bladeCorrections:close()
lines[#lines+1]='';lines[#lines+1]=table.concat(subresources,'\n')
lines[#lines+1]='[resource]\nanimations = ['..table.concat(animations,',\n')..']\n'
write(meta..'sprite_frames.tres',table.concat(lines,'\n'))
write(meta..'manifest.json',json.encode(manifest))
-- Three columns: high<->mid, mid<->low, high<->low. Idle holds make each edge readable.
local preview=Sprite(W*3,H,ColorMode.RGB)
-- GIF players clamp 10 ms frames; resample the preview to 50 fps so it stays fast.
local previewFPS=50
for n=1,math.floor(56/FPS*previewFPS+.5)do
 local sourceFrame=math.floor((n-1)*FPS/previewFPS)+1
 local im=Image(W*3,H,ColorMode.RGB)
 im:clear(pc.rgba(28,31,39,255))
 for s,frames in ipairs(clips)do
  local idx=1
  if sourceFrame>=13 and sourceFrame<=22 then idx=math.min(#frames,sourceFrame-12)
  elseif sourceFrame>=23 and sourceFrame<=34 then idx=#frames
  elseif sourceFrame>=35 and sourceFrame<=44 then idx=math.max(1,#frames-(sourceFrame-35))end
  im:drawImage(frames[idx],Point((s-1)*W,0))
 end
 put(preview,n,im)
 preview.frames[n].duration=1/previewFPS
end
preview:saveAs(root..'/docs/characters/media/protagonist/greatsword-stance-transitions-62fps.gif')
preview:close()
