-- 在选定视频整理稿上增加呼吸与尾摆；像素编辑与导出统一由 Aseprite 完成。
-- aseprite -b --script scripts/tools/art/build_protagonist_idle.lua
local here=debug.getinfo(1,'S').source:gsub('^@',''):gsub('\\','/')
local root=app.params['project'] or assert(here:match('^(.*)/scripts/tools/art/[^/]+$'))
local tex=root..'/assets/textures/sprites/amber_fox_style_v2/greatsword_stance_idle/'
local meta=root..'/assets/resources/sprites/amber_fox_style_v2/greatsword_stance_idle/'
local doc=root..'/docs/characters/media/protagonist/'
local file=assert(io.open(meta..'motion.json','r'));local cfg=json.decode(file:read('*a'));file:close()
local W,H,N,FPS=cfg.canvas[1],cfg.canvas[2],cfg.frames,cfg.fps
local pc=app.pixelColor
local function round(v)return math.floor(v+.5)end
local function clamp(v)return math.max(0,math.min(1,v))end
local function put(s,l,n,im)
 local c=l:cel(n);if c then c.image=im;c.position=Point(0,0)else s:newCel(l,n,im,Point(0,0))end
end
local function full(layer,n)
 local im=Image(W,H,ColorMode.RGB);local c=layer:cel(n);if c then im:drawImage(c.image,c.position)end;return im
end
local function twice(im)
 local out=Image(im.width*2,im.height*2,ColorMode.RGB)
 for y=0,out.height-1 do for x=0,out.width-1 do out:putPixel(x,y,im:getPixel(math.floor(x/2),math.floor(y/2)))end end
 return out
end
local function tidyTail(tail,body,rootY)
 local merged=Image(W,H,ColorMode.RGB);merged:drawImage(tail);merged:drawImage(body)
 local function neighbors(x,y)
  local found={}
  for dy=-1,1 do for dx=-1,1 do
   local nx,ny=x+dx,y+dy
   if (dx~=0 or dy~=0) and nx>=0 and nx<W and ny>=0 and ny<H and pc.rgbaA(merged:getPixel(nx,ny))>0 then found[#found+1]={nx,ny}end
  end end
  return found
 end
 local count=0
 -- 摆动和遮挡后偶发的一两像素悬空尾毛，只清理尾巴层，保留前景和主体轮廓。
 for y=rootY,H-1 do for x=0,W-1 do
  if pc.rgbaA(tail:getPixel(x,y))>0 and pc.rgbaA(body:getPixel(x,y))==0 then
   local near=neighbors(x,y)
   if #near==0 or (#near==1 and #neighbors(near[1][1],near[1][2])==1)then
    tail:putPixel(x,y,0);count=count+1
   end
  end
 end end
 return count
end
local comparison=Sprite(W*3,H,ColorMode.RGB);comparison.layers[1].name='上段 · 中段 · 下段'
for n=2,N do comparison:newEmptyFrame(n)end
local transforms={}
for column,pose in ipairs({'high','mid','low'})do
 local source=app.open(tex..pose..'/sources/source.aseprite')
 assert(#source.frames==N and source.width==W and source.height==H)
 local tailLayer,bodyLayer
 for _,l in ipairs(source.layers)do
  if l.name=='尾巴基准'then tailLayer=l elseif l.name=='身体与大剑基准'then bodyLayer=l end
 end
 assert(tailLayer and bodyLayer,'缺少逐帧制作基准层')
 local dst=Sprite(W,H,ColorMode.RGB);dst.layers[1].name='尾巴摆动';local bodyOut=dst:newLayer();bodyOut.name='呼吸与持剑'
 local gif=Sprite(W*2,H*2,ColorMode.RGB)
 local atlas=Image(W*8,H*math.ceil(N/8),ColorMode.RGB)
 local baseline=root..'/.godot/protagonist-idle-cleanup/'..pose..'/baseline/'
 app.fs.makeAllDirectories(baseline)
 transforms[pose]={}
 for n=1,N do
  if n>1 then dst:newEmptyFrame(n);gif:newEmptyFrame(n)end
  local rise=round(cfg.upper_extra_lift_px*.5*(1-math.cos(2*math.pi*(n-1)/N)))
  local wave=cfg.poses[pose].extra_tail_amplitude_px*math.cos(2*math.pi*((n-1)-(N/2+cfg.tail_lag_frames))/N)
  local body,tail=full(bodyLayer,n),full(tailLayer,n)
  local original=Image(W,H,ColorMode.RGB);original:drawImage(tail);original:drawImage(body)
  original:saveAs(baseline..string.format('idle_%03d.png',n-1))
  local a,b=Image(W,H,ColorMode.RGB),Image(W,H,ColorMode.RGB)
  for y=0,H-1 do
   local sy=y+round(rise*clamp((cfg.fixed_feet_start_y-y)/(cfg.fixed_feet_start_y-cfg.rigid_upper_end_y)))
   if sy<H then
    local p=cfg.poses[pose];local t=clamp((sy-p.tail_root_y)/(p.tail_tip_y-p.tail_root_y))
    local dx=round(wave*t*t*(3-2*t))
    for x=0,W-1 do
     a:putPixel(x,y,body:getPixel(x,sy))
     if x-dx>=0 and x-dx<W then b:putPixel(x,y,tail:getPixel(x-dx,sy))end
    end
   end
  end
  local cleaned=tidyTail(b,a,cfg.poses[pose].tail_root_y)
  put(dst,dst.layers[1],n,b);put(dst,bodyOut,n,a)
  dst.frames[n].duration=(round(n*1000/FPS)-round((n-1)*1000/FPS))/1000
  local frame=Image(W,H,ColorMode.RGB);frame:drawImage(b);frame:drawImage(a)
  frame:saveAs(tex..pose..string.format('/frames/idle_%03d.png',n-1))
  atlas:drawImage(frame,Point((n-1)%8*W,math.floor((n-1)/8)*H))
  put(gif,gif.layers[1],n,twice(frame));gif.frames[n].duration=(round(n*100/FPS)-round((n-1)*100/FPS))/100
  local row=Image(W*3,H,ColorMode.RGB);local cel=comparison.layers[1]:cel(n);if cel then row:drawImage(cel.image,cel.position)end
  row:drawImage(frame,Point((column-1)*W,0));put(comparison,comparison.layers[1],n,row)
  comparison.frames[n].duration=gif.frames[n].duration
  transforms[pose][n]={frame=n-1,upper_lift_px=rise,tail_extra_tip_dx_px=round(wave),isolated_tail_pixels_removed=cleaned}
 end
 dst:newTag(dst.frames[1],dst.frames[N]).name='idle_'..pose
 dst:saveAs(tex..pose..'/sources/idle.aseprite');dst:close();source:close()
 atlas:saveAs(tex..pose..'/idle-atlas.png')
 app.activeSprite=gif;app.command.ColorQuantization{ui=false,maxColors=256,withAlpha=true}
 gif:saveAs(doc..'greatsword-idle-'..pose..'.gif');gif:close()
 print(pose..'：72 帧呼吸与尾摆已更新')
end
app.activeSprite=comparison;app.command.ColorQuantization{ui=false,maxColors=256,withAlpha=true}
comparison:saveAs(doc..'greatsword-stance-idle-current.gif');comparison:close()
file=assert(io.open(meta..'transforms.json','w'));file:write(json.encode(transforms));file:close()
print('三段待机导出完成：3 秒循环，脚底固定，头脸、双手、大剑同步平移。')
