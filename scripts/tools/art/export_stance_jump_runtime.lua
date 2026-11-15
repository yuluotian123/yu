-- Assemble the real 60 fps Godot capture at 30 fps for the character document.
local here=debug.getinfo(1,'S').source:gsub('^@',''):gsub('\\','/')
local root=app.params.project or assert(here:match('^(.*)/scripts/tools/art/[^/]+$'))
local input=root..'/.godot/art-validation/greatsword_jump/runtime/'
local file=assert(io.open(root..'/assets/resources/sprites/amber_fox_style_v2/greatsword_jump/production.json','rb'))
local config=json.decode(file:read('*a'));file:close()
local output=root..'/docs/characters/media/protagonist/'..config.preview:gsub('jumps%-v','jumps-runtime-v')
local sprite=Sprite(1366,768,ColorMode.RGB)
local layer=sprite.layers[1]
for n=0,89 do
  local im=Image{fromFile=input..string.format('frame-%03d.png',n*2)}
  assert(im.width==sprite.width and im.height==sprite.height,'Capture dimensions changed')
  if n>0 then sprite:newEmptyFrame(n+1)end
  local cel=layer:cel(n+1)
  if cel then cel.image=im else sprite:newCel(layer,n+1,im,Point(0,0))end
  -- GIF uses centiseconds; round cumulative boundaries to preserve all 3 seconds.
  sprite.frames[n+1].duration=(math.floor((n+1)*100/30+.5)-math.floor(n*100/30+.5))/100
end
sprite:saveAs(output)
sprite:close()
print('Runtime jump preview: 90 frames, 3 seconds, both facings; '..output)
