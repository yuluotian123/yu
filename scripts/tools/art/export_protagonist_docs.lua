-- 从当前武器 SpriteFrames 导出主角档案 GIF，仅更新文档中的自动生成区。
-- 用法：aseprite -b --script scripts/tools/art/export_protagonist_docs.lua
-- 可选：--script-param project=D:/Project/GitHub/yu
local script = debug.getinfo(1, "S").source:gsub("^@", ""):gsub("\\", "/")
local root = app.params["project"] or script:match("^(.*)/scripts/tools/art/[^/]+$")
assert(root, "无法定位项目；请通过 project 参数指定根目录")
root = root:gsub("\\", "/"):gsub("/$", "")
local output = root .. "/docs/characters/media/protagonist"
app.fs.makeAllDirectories(output)

local function read(path)
  local f = assert(io.open(path, "rb"), "无法读取：" .. path)
  local text = f:read("*a"):gsub("\r\n", "\n")
  f:close()
  return text
end

local function write(path, text)
  local f = assert(io.open(path, "wb"), "无法写入：" .. path)
  f:write(text)
  f:close()
end

local function external_paths(text)
  local paths = {}
  for path, id in text:gmatch('%[ext_resource [^\n]-path="([^"]+)"[^\n]-id="([^"]+)"%]') do
    paths[id] = path:gsub("^res://", "")
  end
  return paths
end

-- 工程约定：主角当前仅使用大剑；跳跃文档只展示完整动作。
local order = {"run", "jump", "attack", "hurt", "death"}
local included = {}
for _, name in ipairs(order) do included[name] = true end
local labels = {idle="待机", run="跑步", attack="普攻", jump="完整跳跃", hurt="受击", death="倒地"}
local status = {idle="循环播放", run="循环播放", jump="跳高约 0.6 米；全程可平移", attack="冷却 1.35 秒", hurt="已有素材，当前玩家图未接入", death="已有素材，当前玩家图未接入"}
local profiles = {{key="greatsword", label="大剑"}}
local existing_manifest = output .. "/manifest.json"
local manifest = app.fs.isFile(existing_manifest) and json.decode(read(existing_manifest)) or {schema=1}
manifest.generated_at=os.date("%Y-%m-%d %H:%M:%S")
manifest.weapons={}

for _, weapon in ipairs(profiles) do
  local profile_path = "assets/resources/weapons/" .. weapon.key .. "_animation.tres"
  local profile = read(root .. "/" .. profile_path)
  local frames_id = assert(profile:match('Frames = ExtResource%("([^"]+)"%)'))
  local resource_path = assert(external_paths(profile)[frames_id])
  local resource = read(root .. "/" .. resource_path)
  local paths, regions, atlas_id = external_paths(resource), {}, nil
  for header, body in (resource .. "\n\n"):gmatch('(%[[^\n]+%])\n(.-)\n\n') do
    local id = header:match('^%[sub_resource type="AtlasTexture" id="([^"]+)"%]$')
    if id then
      local region = assert(body:match("region = Rect2%(([^)]+)%)"))
      local values = {}
      for number in region:gmatch("[-%d.]+") do values[#values+1] = tonumber(number) end
      assert(#values == 4)
      regions[id] = values
      local current_atlas = assert(body:match('atlas = ExtResource%("([^"]+)"%)'))
      assert(not atlas_id or atlas_id == current_atlas, "当前导出器要求每套动作使用一个图集")
      atlas_id = current_atlas
    end
  end
  local atlas_path = assert(paths[atlas_id])
  local atlas = Image{fromFile=root .. "/" .. atlas_path}
  local animations = assert(resource:match("animations = (.+)"))
  local data = {profile=profile_path, sprite_frames=resource_path, atlas=atlas_path, animations={}}
  local count = 0
  for frame_text, loop, name, speed in animations:gmatch('{\n"frames": %[(.-)%],\n"loop": ([^,\n]+),\n"name": &"([^"]+)",\n"speed": ([%d.eE+-]+)\n}') do
    if included[name] then
    assert(name:match("^[a-z0-9_]+$"), "无效的动作文件名")
    local fps, frames = assert(tonumber(speed)), {}
    for duration, id in frame_text:gmatch('"duration": ([%d.eE+-]+),\n"texture": SubResource%("([^"]+)"%)') do
      frames[#frames+1] = {duration=assert(tonumber(duration)), region=assert(regions[id])}
    end
    assert(fps > 0 and #frames > 0, "动作缺少有效帧：" .. name)
    local width, height = frames[1].region[3], frames[1].region[4]
    local sprite = Sprite(width, height, ColorMode.RGB)
    local layer = sprite.layers[1]
    local elapsed, previous_ticks = 0, 0
    for i, frame in ipairs(frames) do
      local r = frame.region
      assert(r[3] == width and r[4] == height and r[1] >= 0 and r[2] >= 0 and r[1]+width <= atlas.width and r[2]+height <= atlas.height)
      if i > 1 then sprite:newEmptyFrame(i) end
      local crop = Image(atlas, Rectangle(r[1], r[2], width, height))
      local cel = layer:cel(i)
      if cel then cel.image = crop else sprite:newCel(layer, i, crop, Point(0, 0)) end
      elapsed = elapsed + frame.duration / fps
      local ticks = math.floor(elapsed * 100 + 0.5)
      sprite.frames[i].duration = math.max(1, ticks - previous_ticks) / 100
      previous_ticks = ticks
    end
    local filename = weapon.key .. "-" .. name .. ".gif"
    sprite:saveAs(output .. "/" .. filename)
    sprite:close()
    data.animations[name] = {frames=#frames, fps=fps, source_duration_seconds=elapsed, gif_duration_seconds=previous_ticks/100, loop=(loop == "1" or loop == "true"), file=filename}
    count = count + 1
    end
  end
  assert(count > 0, "未解析出动作：" .. resource_path)
  manifest.weapons[weapon.key] = data
  print(weapon.label .. "：已导出 " .. count .. " 个动作")
end

-- 当前三段待机预览；未通过的三段攻击不再展示。
local current_path = root .. "/assets/resources/sprites/amber_fox_style_v2/greatsword_stance_idle/manifest.json"
local current = json.decode(read(current_path))
local idle_preview = {file=current.previews.idle, frames=72, fps=24, source_duration_seconds=3, runtime_replaced=true}
assert(app.fs.isFile(output .. "/" .. idle_preview.file), "当前三段待机预览缺失")
manifest.previews = manifest.previews or {}
manifest.previews.idle = idle_preview
local rows = {"| 动作 | GIF | 动作相关信息 |", "| --- | --- | --- |"}
rows[#rows+1] = string.format("| 三段待机 | ![上、中、下段待机](media/protagonist/%s) | 各 72 帧 · 24 fps · 3 秒循环<br>已加大肩部起伏与尾摆；保留眨眼，脚底不增加位移 |", idle_preview.file)
rows[#rows+1] = "| 三段攻击 | 暂缓 | 先完成三段移动；后续每段一招普攻，按新动作确定时长与冷却 |"
rows[#rows+1] = "| 架势过渡 | ![六向架势切换](media/protagonist/greatsword-stance-transitions-62fps.gif) | 上↔中 7 帧 / 0.112 秒，中↔下及上↔下 10 帧 / 0.160 秒；均为 62.4 fps |"
for _, name in ipairs(order) do
  if name == "run" and manifest.previews.stance_movement then
    local movement = manifest.previews.stance_movement
    rows[#rows+1] = string.format("| 三段移动 | ![上、中、下段持剑移动](media/protagonist/%s) | %s |", movement.file, movement.description)
  else
  local a = assert(manifest.weapons.greatsword.animations[name])
  local label = name == "attack" and "游戏现用普攻" or labels[name]
  rows[#rows+1] = string.format("| %s | ![主角%s](media/protagonist/%s) | %d 帧 · %.3g fps · %.3g 秒<br>%s |", label, labels[name], a.file, a.frames, a.fps, a.source_duration_seconds, status[name])
  end
end

local document_path = root .. "/docs/characters/protagonist.md"
local document = read(document_path)
local begin_marker, end_marker = "<!-- BEGIN GENERATED ACTIONS -->", "<!-- END GENERATED ACTIONS -->"
local begin_at, begin_end = document:find(begin_marker, 1, true)
local end_at = document:find(end_marker, (begin_end or 0)+1, true)
assert(begin_at and end_at, "人物档案缺少动作生成区标记，已保留用户文字")
document = document:sub(1, begin_end) .. "\n\n" .. table.concat(rows, "\n") .. "\n\n" .. document:sub(end_at)
write(document_path, document)
write(output .. "/manifest.json", json.encode(manifest))
print("主角档案动作区已同步；用户需求和改动记录保持原样")
