-- 构建三段完整跳跃；源视频的垂直根位移由物理跳跃接管，保留收腿与屈膝。
local here=debug.getinfo(1,'S').source:gsub('^@',''):gsub('\\','/')
local root=app.params.project or assert(here:match('^(.*)/scripts/tools/art/[^/]+$'))
local function read(path)local f=assert(io.open(path,'rb'));local s=f:read('*a');f:close();return s end
local function write(path,s)local f=assert(io.open(path,'wb'));f:write(s);f:close()end
local function plain(v)
 if type(v)~='table'and type(v)~='userdata'then return v end
 local t={};for k,x in pairs(v)do t[k]=plain(x)end;for k,x in ipairs(v)do t[k]=plain(x)end;return t
end
local group='amber_fox_style_v2/greatsword_jump/'
local meta=root..'/assets/resources/sprites/'..group
local tex=root..'/assets/textures/sprites/'..group
local config=plain(json.decode(read(meta..'production.json')))
local corrections=config.pixel_corrections and plain(json.decode(read(root..'/'..config.pixel_corrections))).clips or {}
local W,H=config.canvas[1],config.canvas[2]
local sourceSize=config.source_canvas or config.canvas
local sourceOffset=config.source_offset or {0,0}
local referenceOffset=config.reference_offset or config.idle_offset
local pc=app.pixelColor
local function put(s,n,im,ms)
 if n>1 then s:newEmptyFrame(n)end
 local layer=s.layers[1];local cel=layer:cel(n)
 if cel then cel.image=im else s:newCel(layer,n,im,Point(0,0))end
 s.frames[n].duration=ms/1000
end
local function key(rgb)
 local im=Image(rgb.width,rgb.height,ColorMode.RGB)
 for y=0,rgb.height-1 do for x=0,rgb.width-1 do
  local c=rgb:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
  if not(g-r>config.chroma_dominance and g-b>config.chroma_dominance)then im:putPixel(x,y,pc.rgba(r,g,b,255))end
 end end
 return im
end
local lines={'[gd_resource type="SpriteFrames" format=3]',''}
local subs,animations,clips={},{},{}
local manifest={schema=1,revision=config.revision,date=config.date,authoring_mode='comfyui',canvas=config.canvas,source_canvas=sourceSize,source_offset=sourceOffset,
 foot_anchor=config.foot_anchor,preview=config.preview,phases=config.phases,clips={},
 timing='Shared phase durations; physical velocity selects airborne phases. Landing lasts 0.2 seconds.',
 root_extraction='Only the annotated vertical hip translation is removed; feet are not aligned per frame. Physics owns jump height.',
 alpha='Binary green chroma alpha; only recorded two-pixel contour green-spill repairs alter RGB. Independent Aseprite export verifies packaging, not artistic quality.'}
for s,spec in ipairs(config.clips)do
 local path=tex..spec.stance..'/'
 for _,dir in ipairs({'frames/','sources/','sources/aseprite-export/','review/'})do
  app.fs.makeAllDirectories(path..dir);write(path..dir..'.gdignore','')
 end
 local idle=Image(W,H,ColorMode.RGB)
 idle:drawImage(Image{fromFile=root..'/'..(spec.reference_frame or spec.idle_frame)},Point(referenceOffset[1],referenceOffset[2]))
 local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='完整跳跃 · 双手持剑与全身源姿势'
 local atlas=Image(W*6,H*math.ceil(#spec.frames/6),ColorMode.RGB)
 local images,records={},{}
 local msTotal=0
 for i,frame in ipairs(spec.frames)do
  local im
  if frame.reference or frame.idle then im=Image(idle)else
   im=key(Image{fromFile=path..'source-rgb/'..string.format('%03d.png',frame.source)})
   local edits=corrections[spec.stance] and corrections[spec.stance][tostring(i-1)]
   if edits then for _,p in ipairs(edits.restore)do im:putPixel(p[1],p[2],pc.rgba(p[3],p[4],p[5],p[6]))end end
   local shifted=Image(W,H,ColorMode.RGB)
   shifted:drawImage(im,Point(sourceOffset[1],sourceOffset[2]+(frame.root_y or 0)));im=shifted
  end
  local bounds={W,H,0,0};local count=0
  for y=0,H-1 do for x=0,W-1 do if pc.rgbaA(im:getPixel(x,y))>0 then
   bounds[1]=math.min(bounds[1],x);bounds[2]=math.min(bounds[2],y)
   bounds[3]=math.max(bounds[3],x+1);bounds[4]=math.max(bounds[4],y+1);count=count+1
  end end end
  assert(count>1000 and bounds[1]>0 and bounds[2]>0 and bounds[3]<W and bounds[4]<H,'Clipped jump '..spec.stance..' '..i)
  put(sprite,i,im,frame.duration_ms)
  im:saveAs(path..'frames/'..string.format('jump_%03d.png',i-1))
  atlas:drawImage(im,Point((i-1)%6*W,math.floor((i-1)/6)*H))
  images[i]=im;msTotal=msTotal+frame.duration_ms
  local id=spec.stance..'_'..(i-1)
  subs[#subs+1]=string.format('[sub_resource type="AtlasTexture" id="%s"]\natlas = ExtResource("atlas%d")\nregion = Rect2(%d, %d, %d, %d)\nfilter_clip = true\n',id,s,(i-1)%6*W,math.floor((i-1)/6)*H,W,H)
  records[i]={frame=i-1,source=frame.source,reference=frame.reference or frame.idle or false,root_y=frame.root_y or 0,duration_ms=frame.duration_ms,bounds=bounds,opaque_pixels=count}
 end
 local first=1
 for _,phase in ipairs(config.phases)do
  local count=spec.phase_counts[phase.name];local refs={}
  for i=first,first+count-1 do refs[#refs+1]=string.format('{"duration": 1, "texture": SubResource("%s_%d")}',spec.stance,i-1)end
  local name=phase.name..'_'..spec.stance
  sprite:newTag(first,first+count-1).name=name
  animations[#animations+1]=string.format('{"frames": [%s], "loop": 0, "name": &"%s", "speed": %.12g}',table.concat(refs,', '),name,count/phase.seconds)
  first=first+count
 end
 assert(first-1==#images,'All frames must belong to one phase')
 sprite:newTag(1,#images).name='jump_'..spec.stance
 atlas:saveAs(path..'jump-atlas.png');sprite:saveAs(path..'sources/jump.aseprite');sprite:close()
 -- Reopen the saved source independently, keeping its canvas and actual frame times.
 local saved=app.open(path..'sources/jump.aseprite')
 local exported={}
 for i,f in ipairs(saved.frames)do
  local im=Image(W,H,ColorMode.RGB);im:drawSprite(saved,i)
  im:saveAs(path..'sources/aseprite-export/'..string.format('jump_%03d.png',i-1))
  exported[i]=math.floor(f.duration*1000+.5)
 end
 saved:close()
 lines[#lines+1]=string.format('[ext_resource type="Texture2D" path="res://assets/textures/sprites/%s%s/jump-atlas.png" id="atlas%d"]',group,spec.stance,s)
 manifest.clips[s]={stance=spec.stance,frames=#images,duration_ms=msTotal,source_video=spec.video,prompt_id=spec.job,
  seed=spec.seed,reference_frame=spec.reference_frame or spec.idle_frame,phase_counts=spec.phase_counts,records=records,aseprite_durations_ms=exported}
 local walk={}
 if spec.walk_frames then for n=0,spec.walk_frames-1 do
  local im=Image(W,H,ColorMode.RGB)
  im:drawImage(Image{fromFile=root..'/'..spec.walk_directory..string.format('/move_%03d.png',n)},Point(referenceOffset[1],referenceOffset[2]))
  walk[#walk+1]=im
 end end
 clips[s]={images=images,records=records,idle=idle,walk=walk,walk_seconds=spec.walk_seconds}
 print(spec.stance..': '..#images..' complete poses, '..msTotal..' ms')
end
lines[#lines+1]='';lines[#lines+1]=table.concat(subs,'\n')
lines[#lines+1]='[resource]\nanimations = ['..table.concat(animations,',\n')..']\n'
write(meta..'sprite_frames.tres',table.concat(lines,'\n'))
write(meta..'manifest.json',json.encode(manifest))
-- Complete three-guard preview; add room above the high guard for physical rise.
local preview=Sprite(672,512,ColorMode.RGB)
for tick=0,95 do
 local time=tick/60-.25;local im=Image(672,512,ColorMode.RGB);im:clear(pc.rgba(24,27,33,255))
 local flight=time-.08
 local height=(flight>0 and flight<.5)and math.max(0,4*flight-8*flight*flight)/(.004137931*1.3)or 0
 for s,clip in ipairs(clips)do
  local chosen=clip.idle;local endTime=0
  if #clip.walk>0 and (time<0 or time>=.78)then
   local phase=time<0 and time or time-.78
   chosen=clip.walk[math.floor((phase%clip.walk_seconds)/clip.walk_seconds*#clip.walk)+1]
  end
  if time>=0 then for i,record in ipairs(clip.records)do
   endTime=endTime+record.duration_ms/1000
   if time<endTime then chosen=clip.images[i];break end
  end end
  local cropped=Image(chosen,Rectangle(144,0,224,384))
  im:drawImage(cropped,Point((s-1)*224,128-math.floor(height+.5)))
 end
 local ms=math.floor((tick+1)*1000/60+.5)-math.floor(tick*1000/60+.5)
 put(preview,tick+1,im,ms)
end
preview:saveAs(root..'/docs/characters/media/protagonist/'..config.preview);preview:close()
