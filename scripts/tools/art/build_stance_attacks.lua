-- 按制作配置封装三段攻击，以逐帧时间调节蓄力、出手和顿帧，首尾使用对应待机。
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
local W,H=config.canvas[1],config.canvas[2]
-- Uniform 1 ms playback ticks avoid AnimatedSprite3D consuming a new frame
-- with the previous frame's speed when a short pose enters a long hold.
local runtimeFps=1000
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
local manifest={schema=1,date=config.date,revision=config.revision,canvas=config.canvas,foot_anchor=config.foot_anchor,preview=config.preview,
 model='Local MiniMax H3 int8; 28 steps; acceleration LoRA disabled',
 reference_document='docs/art/greatsword-attack-references.md',
 alpha='Green chroma dominance > '..config.chroma_dominance..'; retained video RGB is unchanged. Original idle supplies both endpoints.',
 timing='Selected source poses stay in order; authored windup and contact holds preserve the fast release. Rejected source frames are listed in production.json.',clips={}}
for s,spec in ipairs(config.clips)do
 local stance=spec.stance;local name='attack_'..stance;local path=tex..stance..'/'
 for _,dir in ipairs({'frames/','source-rgb/','sources/','review/'})do
  app.fs.makeAllDirectories(path..dir);write(path..dir..'.gdignore','')
 end
 local endpoint=Image(W,H,ColorMode.RGB)
 endpoint:drawImage(Image{fromFile=root..'/'..spec.idle_frame},Point(config.idle_offset[1],config.idle_offset[2]))
 local frames,durations,records,refs={},{},{},{}
 local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='双手大剑攻击 · 源帧与待机端点'
 local atlas=Image(W*6,H*math.ceil(#spec.indices/6),ColorMode.RGB)
 local total=0
 for i,index in ipairs(spec.indices)do
  local n=i-1;local ms
  for _,span in ipairs(spec.timing_spans)do if index>=span[1]and index<=span[2]then ms=span[3]break end end
  assert(ms and ms>0,'Missing timing for '..name..' '..index)
  local isEndpoint=i==1 or i==#spec.indices
  local rgb=Image{fromFile=path..'source-rgb/'..string.format('%03d.png',index)}
  assert(rgb.width==W and rgb.height==H)
  local im=isEndpoint and Image(endpoint)or keyed(rgb)
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
  for tick=1,ms do refs[#refs+1]=string.format('{"duration": 1, "texture": SubResource("%s")}',id)end
  records[i]={frame=n,video_frame=index,source_kind=isEndpoint and'idle_endpoint'or'video',duration_ms=ms,
   runtime_first_frame=firstTick,runtime_frames=ms,bounds={left,top,right+1,bottom+1},opaque_pixels=count}
 end
 assert(total==spec.duration_ms,'Configured clip length differs from its frame timings')
 atlas:saveAs(path..'attack-atlas.png');sprite:saveAs(path..'sources/attack.aseprite');sprite:close()
 lines[#lines+1]=string.format('[ext_resource type="Texture2D" path="res://assets/textures/sprites/%s%s/attack-atlas.png" id="atlas%d"]',group,stance,s)
 animations[#animations+1]=string.format('{"frames": [%s], "loop": 0, "name": &"%s", "speed": %.9g}',table.concat(refs,', '),name,runtimeFps)
 manifest.clips[s]={name=name,stance=stance,frames=#frames,fps=spec.fps,duration=total/1000,loop=false,
  source_video=spec.video,prompt_id=spec.job,seed=spec.seed,source_parts=spec.source_parts,holds=spec.holds,
  runtime_fps=runtimeFps,runtime_frames=#refs,records=records}
 previewClips[s]={frames=frames,durations=durations,endpoint=endpoint,total=total}
 print(name..': '..#frames..' frames, '..total..' ms; complete margins and original idle endpoints')
end
lines[#lines+1]='';lines[#lines+1]=table.concat(subs,'\n')
lines[#lines+1]='[resource]\nanimations = ['..table.concat(animations,',\n')..']\n'
write(meta..'sprite_frames.tres',table.concat(lines,'\n'));write(meta..'manifest.json',json.encode(manifest))
local preview=Sprite(W*#previewClips,H,ColorMode.RGB);local previous=0
for n=0,119 do
 local time=(n/30*1000)%1000-200
 local im=Image(W*#previewClips,H,ColorMode.RGB);im:clear(pc.rgba(28,31,39,255))
 for s,clip in ipairs(previewClips)do
  local chosen=clip.endpoint;local elapsed=0
  if time>=0 and time<clip.total then for i,ms in ipairs(clip.durations)do
   elapsed=elapsed+ms;if time<elapsed then chosen=clip.frames[i]break end
  end end
  im:drawImage(chosen,Point((s-1)*W,0))
 end
 local ticks=math.floor((n+1)/30*100+.5);put(preview,n+1,im,(ticks-previous)*10);previous=ticks
end
preview:saveAs(root..'/docs/characters/media/protagonist/'..config.preview);preview:close()
