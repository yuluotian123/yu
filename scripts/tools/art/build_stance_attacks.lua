-- 按制作配置封装各架势攻击；单击完整回架，配置续招时从上一击落点接入。
local here=debug.getinfo(1,'S').source:gsub('^@',''):gsub('\\','/')
local root=app.params.project or assert(here:match('^(.*)/scripts/tools/art/[^/]+$'))
local function read(p)local f=assert(io.open(p,'rb'));local s=f:read('*a');f:close();return s end
local function write(p,s)local f=assert(io.open(p,'wb'));f:write(s);f:close()end
-- Aseprite JSON containers must become ordinary tables before nested re-encoding.
local function editable(value)
 if type(value)~='table'and type(value)~='userdata'then return value end
 local result={}
 for k,v in pairs(value)do result[k]=editable(v)end
 for k,v in ipairs(value)do result[k]=editable(v)end
 return result
end
local group='amber_fox_style_v2/greatsword_stance_attack/'
local meta=root..'/assets/resources/sprites/'..group
local tex=root..'/assets/textures/sprites/'..group
local config=editable(json.decode(read(app.params.config or meta..'production.json')))
local corrections=config.pixel_corrections and editable(json.decode(read(root..'/'..config.pixel_corrections))).clips or {}
local W,H=config.canvas[1],config.canvas[2]
-- Uniform playback avoids the previous-frame speed issue. New clips use one
-- entry per sampled pose; legacy configurations retain the 1 ms fallback.
local pc=app.pixelColor
local function keyed(rgb)
 local im=Image(W,H,ColorMode.RGB)
 for y=0,H-1 do for x=0,W-1 do
  local c=rgb:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
  if not(g-r>config.chroma_dominance and g-b>config.chroma_dominance)then im:putPixel(x,y,pc.rgba(r,g,b,255))end
 end end
 return im
end
local function put(sprite,n,im,ms)
 if n>1 then sprite:newEmptyFrame(n)end
 local layer=sprite.layers[1];local cel=layer:cel(n)
 if cel then cel.image=im else sprite:newCel(layer,n,im,Point(0,0))end
 sprite.frames[n].duration=ms/1000
end
local lines={'[gd_resource type="SpriteFrames" format=3]',''}
local subs,animations,previewClips={},{},{}
local manifest={schema=1,date=config.date,revision=config.revision,authoring_mode=config.authoring_mode,canvas=config.canvas,foot_anchor=config.foot_anchor,preview=config.preview,
 model='Local MiniMax H3 int8; 28 steps; acceleration LoRA disabled',keyposes=config.keyposes,reference_design=config.reference_design,authoring_steps=config.authoring_steps,authoring_sources=config.authoring_sources,art_revision=config.art_revision,stability_revision=config.stability_revision,
 reference_document='docs/art/greatsword-attack-references.md',
 alpha='Green chroma dominance > '..config.chroma_dominance..'; per-pixel Aseprite matte and steel repairs are recorded in pixel-corrections.json. Body and grip come from complete source poses; canonical idle endpoints are exact.',
 timing='Source-part indices define pose order. Uniform runtime frames use runtime_fps; Aseprite/APNG durations round cumulative boundaries to milliseconds. Holds and optional end-exclusive phases describe the intended rhythm.',clips={}}
for s,spec in ipairs(config.clips)do
 local stance=spec.stance;local key=spec.key or stance;local name='attack_'..key;local path=tex..key..'/'
 local uniformPlayback=spec.runtime_fps~=nil
 local runtimeFps=spec.runtime_fps or 1000
 for _,dir in ipairs({'frames/','source-rgb/','sources/','review/'})do
  app.fs.makeAllDirectories(path..dir);write(path..dir..'.gdignore','')
 end
 local endpoint=Image(W,H,ColorMode.RGB)
 endpoint:drawImage(Image{fromFile=root..'/'..spec.idle_frame},Point(config.idle_offset[1],config.idle_offset[2]))
 local startpoint=spec.start_frame and Image{fromFile=root..'/'..spec.start_frame}or endpoint
 local frames,durations,records,refs={},{},{},{}
 local endpointFrames={};for _,n in ipairs(spec.idle_endpoint_frames or {})do endpointFrames[n]=true end
 local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='双手大剑攻击 · 源帧与待机端点'
 local atlas=Image(W*6,H*math.ceil(#spec.indices/6),ColorMode.RGB)
 local total=0
 for i,index in ipairs(spec.indices)do
  local n=i-1;local ms
  for _,span in ipairs(spec.timing_spans)do if index>=span[1]and index<=span[2]then ms=span[3]break end end
  assert(ms and ms>0,'Missing timing for '..name..' '..index)
  local isEndpoint=i==1 or i==#spec.indices or endpointFrames[n]
  local rgb=Image{fromFile=path..'source-rgb/'..string.format('%03d.png',index)}
  assert(rgb.width==W and rgb.height==H)
  local im=isEndpoint and Image(i==1 and startpoint or endpoint)or keyed(rgb)
  local edit=not isEndpoint and corrections[key] and corrections[key][tostring(n)]
  if edit then
   for _,p in ipairs(edit.erase)do im:putPixel(p[1],p[2],0)end
   for _,p in ipairs(edit.restore)do im:putPixel(p[1],p[2],pc.rgba(p[3],p[4],p[5],p[6]))end
  end
  -- Source centering stays fixed when the gameplay step is retimed.
  local motion=spec.root_motion
  local sourceCurve=motion and(motion.source_forward_pixels or motion.forward_pixels)
  local rootX=sourceCurve and sourceCurve[i]or 0
  if not isEndpoint and rootX~=0 then
   local centered=Image(W,H,ColorMode.RGB)
   centered:drawImage(im,Point(-rootX,0));im=centered
  end
  local left,top,right,bottom,count=W,H,-1,-1,0
  for y=0,H-1 do for x=0,W-1 do if pc.rgbaA(im:getPixel(x,y))>0 then
   left=math.min(left,x);top=math.min(top,y);right=math.max(right,x);bottom=math.max(bottom,y);count=count+1
  end end end
  assert(count>1000 and left>0 and top>0 and right<W-1 and bottom<H-1,'Empty or clipped attack '..name..' '..n)
  im:saveAs(path..'frames/'..string.format('attack_%03d.png',n))
  atlas:drawImage(im,Point(n%6*W,math.floor(n/6)*H));put(sprite,i,im,ms)
  frames[i]=im;durations[i]=ms;total=total+ms
  local id=name..'_'..n
  subs[#subs+1]=string.format('[sub_resource type="AtlasTexture" id="%s"]\natlas = ExtResource("atlas%d")\nregion = Rect2(%d, %d, %d, %d)\nfilter_clip = true\n',id,s,n%6*W,math.floor(n/6)*H,W,H)
  local firstTick=#refs
  local runtimeCount=uniformPlayback and 1 or ms
  for tick=1,runtimeCount do refs[#refs+1]=string.format('{"duration": 1, "texture": SubResource("%s")}',id)end
  records[i]={frame=n,video_frame=index,source_kind=isEndpoint and(i==1 and spec.start_frame and'contact_endpoint'or'idle_endpoint')or'video',duration_ms=ms,
   runtime_first_frame=firstTick,runtime_frames=runtimeCount,runtime_duration_ms=uniformPlayback and 1000/runtimeFps or ms,
   source_pose=spec.source_pose_map and spec.source_pose_map[i]or nil,root_motion_pixels=rootX,runtime_forward_pixels=motion and motion.forward_pixels[i]or 0,bounds={left,top,right+1,bottom+1},opaque_pixels=count}
 end
 assert(total==spec.duration_ms,'Configured clip length differs from its frame timings')
 if uniformPlayback then assert(math.abs(#refs/runtimeFps-total/1000)<0.000001,'Uniform playback clock mismatch')end
 sprite:newTag(1,#frames).name=name
 atlas:saveAs(path..'attack-atlas.png');sprite:saveAs(path..'sources/attack.aseprite');sprite:close()
 lines[#lines+1]=string.format('[ext_resource type="Texture2D" path="res://assets/textures/sprites/%s%s/attack-atlas.png" id="atlas%d"]',group,key,s)
 animations[#animations+1]=string.format('{"frames": [%s], "loop": 0, "name": &"%s", "speed": %.9g}',table.concat(refs,', '),name,runtimeFps)
 manifest.clips[s]={name=name,key=key,stance=stance,step=spec.step or 1,frames=#frames,fps=spec.fps,duration=total/1000,loop=false,
  source_video=spec.video,prompt_id=spec.job,seed=spec.seed,source_parts=spec.source_parts,holds=spec.holds,phases=spec.phases,
  link_time_ms=spec.link_time_ms,start_frame=spec.start_frame,root_motion=spec.root_motion,runtime_fps=runtimeFps,runtime_frames=#refs,records=records}
 previewClips[s]={stance=stance,frames=frames,durations=durations,endpoint=endpoint,root_motion=spec.root_motion,total=spec.link_time_ms or total}
 print(name..': '..#frames..' frames, '..total..' ms; complete margins and original idle endpoints')
end
lines[#lines+1]='';lines[#lines+1]=table.concat(subs,'\n')
lines[#lines+1]='[resource]\nanimations = ['..table.concat(animations,',\n')..']\n'
write(meta..'sprite_frames.tres',table.concat(lines,'\n'));write(meta..'manifest.json',json.encode(manifest))
local groups={};local longest=0
for _,stance in ipairs({'high','mid','low'})do
 local group={clips={},total=0}
 for _,clip in ipairs(previewClips)do if clip.stance==stance then group.clips[#group.clips+1]=clip;group.total=group.total+clip.total end end
 groups[#groups+1]=group;longest=math.max(longest,group.total)
end
local cycle=math.ceil((longest+450)/1000*30)*1000/30
local preview=Sprite(W*3,H,ColorMode.RGB);local previous=0
for n=0,math.floor(cycle/1000*30)*2-1 do
 local time=(n/30*1000)%cycle-200
 local im=Image(W*3,H,ColorMode.RGB);im:clear(pc.rgba(28,31,39,255))
 for s,group in ipairs(groups)do
  local chosen=group.clips[1].endpoint;local offset=0;local rootX=0
  for _,clip in ipairs(group.clips)do
   if time>=offset and time<offset+clip.total then
    local elapsed=offset
    for i,ms in ipairs(clip.durations)do elapsed=elapsed+ms;if time<elapsed then chosen=clip.frames[i];rootX=clip.root_motion and clip.root_motion.forward_pixels[i]or 0;break end end
    break
   end
   if time>=offset+clip.total and clip.root_motion then rootX=clip.root_motion.forward_pixels[#clip.frames]end
   offset=offset+clip.total
  end
  im:drawImage(chosen,Point((s-1)*W+rootX,0))
 end
 local ticks=math.floor((n+1)/30*100+.5);put(preview,n+1,im,(ticks-previous)*10);previous=ticks
end
preview:saveAs(root..'/docs/characters/media/protagonist/'..config.preview);preview:close()
