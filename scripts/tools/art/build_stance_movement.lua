-- 从已审核 RGB 序列封装移动；局部握剑修帧保留腰胯和腿脚源像素。
-- --script-param config=... 可覆盖配置，默认使用已归档的 production.json。
local here=debug.getinfo(1,'S').source:gsub('^@',''):gsub('\\','/')
local root=app.params['project'] or assert(here:match('^(.*)/scripts/tools/art/[^/]+$'))
local function read(path)local f=assert(io.open(path,'rb'));local s=f:read('*a');f:close();return s end
local function write(path,s)local f=assert(io.open(path,'wb'));f:write(s);f:close()end
local group='amber_fox_style_v2/greatsword_stance_movement/'
local tex=root..'/assets/textures/sprites/'..group
local meta=root..'/assets/resources/sprites/'..group
local stored=meta..'production.json'
local config=json.decode(read(app.params['config'] or stored))
local pc=app.pixelColor
local W,H=320,384
app.fs.makeAllDirectories(meta)
local function key(rgb)
 local out=Image(W,H,ColorMode.RGB)
 for y=0,H-1 do for x=0,W-1 do
  local c=rgb:getPixel(x,y);local r,g,b=pc.rgbaR(c),pc.rgbaG(c),pc.rgbaB(c)
  if not(g-r>18 and g-b>18)then out:putPixel(x,y,pc.rgba(r,g,b,255))end
 end end
 return out
end
local function put(sprite,n,im,seconds)
 if n>1 then sprite:newEmptyFrame(n)end
 local layer=sprite.layers[1];local cel=layer:cel(n)
 if cel then cel.image=im else sprite:newCel(layer,n,im,Point(0,0))end
 sprite.frames[n].duration=seconds
end
local lines={'[gd_resource type="SpriteFrames" format=3]',''}
local subresources,animations,allFrames={},{},{}
local manifest={schema=1,date=config.date or os.date('%Y-%m-%d'),revision=config.revision,canvas={W,H},foot_anchor={160,347},
 model='MiniMax H3 local int8, 28 steps, acceleration LoRA disabled',
 reference_document='docs/art/greatsword-movement-references.md',
 alpha='Green chroma g-r>18 and g-b>18. Foreground RGB retained outside documented local guard repairs; no whole-body warp, palette quantization or per-frame crop/realignment.',
 audit_limit='No independent full foreground masks. RGB checks cover retained pixels; every output frame requires visual review.',clips={}}
for s,spec in ipairs(config.clips)do
 local stance=spec.stance;local asset=spec.asset or stance;local name=spec.animation or('run_'..stance);local path=tex..asset..'/'
 local loop=spec.loop~=false
 for _,dir in ipairs({'frames/','source-rgb/','sources/'})do
  app.fs.makeAllDirectories(path..dir);write(path..dir..'.gdignore','')
 end
 local sprite=Sprite(W,H,ColorMode.RGB);sprite.layers[1].name='双手大剑移动 · 源视频色块'
 local repair,guard,guardLayer
 if spec.guard_repair_config then
  repair=json.decode(read(root..'/'..spec.guard_repair_config))
  guard=Image{fromFile=root..'/'..repair.layer}
  assert(guard.width==W and guard.height==H)
  guardLayer=sprite:newLayer();guardLayer.name='双手握剑修正 · 随肩部运动'
  app.fs.makeAllDirectories(path..'sources/guard-edit-masks/')
 end
 local atlas=Image(W*6,H*math.ceil(#spec.indices/6),ColorMode.RGB)
 local records,refs,frames={},{},{}
 for i,index in ipairs(spec.indices)do
  local n=i-1
  local rgb=Image{fromFile=root..'/'..spec.rgb_directory..'/'..string.format('%03d.png',index)}
  assert(rgb.width==W and rgb.height==H)
  local source=key(rgb);local im=source;local body=im;local guardOffset;local editedPixels=0
  if repair then
   local track=repair.tracks[index+1];assert(track.source_frame==index)
   guardOffset=Point(track.offset[1],track.offset[2])
   body=Image(im);local editMask=Image(W,H,ColorMode.RGB)
   for _,row in ipairs(repair.erase_rows)do
    for y=math.max(0,row[1]+guardOffset.y),math.min(H,row[2]+guardOffset.y)-1 do
     for x=math.max(0,row[3]+guardOffset.x),math.min(W,repair.erase_right+guardOffset.x)-1 do
      body:putPixel(x,y,0);editMask:putPixel(x,y,pc.rgba(255,255,255,255))
     end
    end
   end
   im=Image(body);im:drawImage(guard,guardOffset)
   for y=0,H-1 do for x=0,W-1 do
    local gx,gy=x-guardOffset.x,y-guardOffset.y
    if gx>=0 and gx<W and gy>=0 and gy<H and pc.rgbaA(guard:getPixel(gx,gy))>0 then editMask:putPixel(x,y,pc.rgba(255,255,255,255))end
    if y>=repair.preserve_below_y then assert(im:getPixel(x,y)==source:getPixel(x,y),'Repair touched the approved lower body')end
    if pc.rgbaA(editMask:getPixel(x,y))>0 then editedPixels=editedPixels+1 end
   end end
   editMask:saveAs(path..'sources/guard-edit-masks/'..string.format('mask_%03d.png',n))
  end
  local minX,minY,maxX,maxY,count=W,H,-1,-1,0
  for y=0,H-1 do for x=0,W-1 do
   if pc.rgbaA(im:getPixel(x,y))>0 then
    count=count+1;minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x);maxY=math.max(maxY,y)
   end
  end end
  assert(count>1000 and minX>0 and minY>0 and maxX<W-1 and maxY<H-1,'Empty or clipped frame '..name..' '..n)
  im:saveAs(path..'frames/'..string.format('move_%03d.png',n))
  rgb:saveAs(path..'source-rgb/'..string.format('%03d.png',index))
  atlas:drawImage(im,Point(n%6*W,math.floor(n/6)*H));put(sprite,i,body,1/spec.fps)
  if guardLayer then sprite:newCel(guardLayer,i,guard,guardOffset)end
  frames[i]=im
  local id=name..'_'..n
  subresources[#subresources+1]=string.format('[sub_resource type="AtlasTexture" id="%s"]\natlas = ExtResource("atlas%d")\nregion = Rect2(%d, %d, %d, %d)\nfilter_clip = true\n',id,s,n%6*W,math.floor(n/6)*H,W,H)
  refs[#refs+1]=string.format('{"duration": 1.0, "texture": SubResource("%s")}',id)
  records[i]={frame=n,video_frame=index,bounds={minX,minY,maxX+1,maxY+1},opaque_pixels=count,
   guard_edit_pixels=editedPixels,guard_offset=guardOffset and{guardOffset.x,guardOffset.y}or nil}
 end
 atlas:saveAs(path..'move-atlas.png');sprite:saveAs(path..'sources/movement.aseprite');sprite:close()
 lines[#lines+1]=string.format('[ext_resource type="Texture2D" path="res://assets/textures/sprites/%s%s/move-atlas.png" id="atlas%d"]',group,asset,s)
 animations[#animations+1]=string.format('{"frames": [%s], "loop": %d, "name": &"%s", "speed": %.12g}',table.concat(refs,', '),loop and 1 or 0,name,spec.fps)
 manifest.clips[s]={name=name,stance=stance,asset=asset,frames=#frames,fps=spec.fps,duration=#frames/spec.fps,loop=loop,source_video=spec.video,prompt_id=spec.job,seed=spec.seed,records=records,guard_repair_config=spec.guard_repair_config}
 if loop then allFrames[#allFrames+1]={frames=frames,fps=spec.fps}end
 print(name..': '..#frames..' frames; '..(repair and'local guard repair, 'or'alpha key only, ')..'margins verified')
end
lines[#lines+1]='';lines[#lines+1]=table.concat(subresources,'\n')
lines[#lines+1]='[resource]\nanimations = ['..table.concat(animations,',\n')..']\n'
write(meta..'sprite_frames.tres',table.concat(lines,'\n'))
write(meta..'manifest.json',json.encode(manifest))
-- GIF 采用累计时间舍入，避免逐帧舍入累计改变步频。
local duration=0
for _,clip in ipairs(allFrames)do duration=math.max(duration,#clip.frames/clip.fps)end
duration=config.preview_duration_seconds or duration
local preview=Sprite(W*3,H,ColorMode.RGB)
local fps=30;local previousTicks=0
for n=0,math.ceil(duration*fps)-1 do
 local im=Image(W*3,H,ColorMode.RGB);im:clear(pc.rgba(28,31,39,255))
 for s,clip in ipairs(allFrames)do
  local index=math.floor(n/fps*clip.fps)%#clip.frames+1
  im:drawImage(clip.frames[index],Point((s-1)*W,0))
 end
 local ticks=math.floor((n+1)/fps*100+.5)
 put(preview,n+1,im,(ticks-previousTicks)/100);previousTicks=ticks
end
preview:saveAs(root..'/docs/characters/media/protagonist/'..assert(config.preview));preview:close()
print('Saved three movement loops and preview, ordered high / mid / low')
