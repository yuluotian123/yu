extends SceneTree
## Apply the village layout to the registered forest level, preserving world identities.

const LEVEL := "res://assets/scenes/levels/forest_town_level.tscn"
const VILLAGE := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const NAVIGATION := "res://assets/resources/world/navigation/forest_town.tres"
const LEVEL_UID := "uid://d0yq2aerjgd6i"
const SPAWN := Vector3(1.56, 0.55, 22.62)

func _initialize() -> void:
	run.call_deferred()

func component(host: Node, name_: String) -> Resource:
	for item: Resource in host.get("Components"):
		if item.get_script().resource_path.get_file() == name_+".cs":
			return item
	assert(false,"Missing component: "+name_)
	return null

func run() -> void:
	var before_text := FileAccess.get_file_as_string(LEVEL)
	var dependency_uids := {}
	var uid_pattern := RegEx.new()
	uid_pattern.compile('uid="([^"]+)" path="([^"]+)"')
	for match_: RegExMatch in uid_pattern.search_all(before_text):
		dependency_uids[match_.get_string(2)] = match_.get_string(1)
	# Keep EC hosts out of the live tree while authoring their serialized state.
	var level: Node3D = load(LEVEL).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var previous := level.get_node_or_null("RPGTown")
	if previous == null: previous = level.get_node_or_null("Village")
	if previous != null: previous.free()
	var village: Node3D = load(VILLAGE).instantiate()
	root.add_child(village)
	await physics_frame
	await physics_frame

	var nav := preload("res://scripts/tools/art/forest_village_whitebox/village_navigation.gd").new().bake(village)
	var map := NavigationServer3D.map_create()
	NavigationServer3D.map_set_use_async_iterations(map,false)
	NavigationServer3D.map_set_active(map,true)
	NavigationServer3D.map_set_cell_size(map,nav.cell_size)
	NavigationServer3D.map_set_cell_height(map,nav.cell_height)
	var region := NavigationServer3D.region_create()
	NavigationServer3D.region_set_use_async_iterations(region,false)
	NavigationServer3D.region_set_enabled(region,true)
	NavigationServer3D.region_set_navigation_mesh(region,nav)
	NavigationServer3D.region_set_map(region,map)
	NavigationServer3D.map_force_update(map)
	for i in range(12):
		await physics_frame
	print("NAVIGATION_DIAGNOSTIC: polygons=",nav.get_polygon_count()," iterations=",NavigationServer3D.map_get_iteration_id(map),"/",NavigationServer3D.region_get_iteration_id(region)," spawn_closest=",NavigationServer3D.map_get_closest_point(map,SPAWN))
	var failed_doors := 0
	for household in village.get_node("Households").get_children():
		var door: Vector3 = household.get_node("DoorApproach").global_position
		var path := NavigationServer3D.map_get_path(map,SPAWN,door,true)
		var reachable := not path.is_empty() and path[-1].distance_to(door) < 0.8
		if not reachable: failed_doors += 1
		print("NAVIGATION_DOOR: ",household.name," reachable=",reachable," target=",door," closest=",NavigationServer3D.map_get_closest_point(map,door)," path_end=",path[-1] if not path.is_empty() else Vector3.INF)
	assert(failed_doors == 0,"Some household doors are unreachable")
	print("FOREST_REPLACEMENT: all 10 doors reachable; navigation polygons=",nav.get_polygon_count())
	var host_door: Vector3 = village.get_node("Households/Host/DoorApproach").global_position
	NavigationServer3D.free_rid(region)
	NavigationServer3D.free_rid(map)

	root.remove_child(village)
	village.name = "Village"
	level.add_child(village)
	village.owner = level
	level.editor_description = "正式森林关卡：十户林间聚落，村长家、公共井、穿林小径与溪桥。沿用世界身份、角色、天气与跨关卡入口。"
	component(level,"WorldLevelContextComponent3D").set("RequireNavigation",true)
	component(level.get_node("EnvironmentRig"),"WeatherSurfaceComponent3D").set("SurfaceRootPath",NodePath("../Village"))
	var lighting: Resource = component(level.get_node("EnvironmentRig"),"DayNightEnvironmentComponent3D").get("Profile")
	lighting.set("ShadowDistance",80.0)
	lighting.set("ShadowFilterBlur",6.0)
	lighting.set("ShadowCascadeMode",DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS)
	lighting.set("ShadowSplit1",.2)
	lighting.set("ShadowSplit2",.42)
	lighting.set("ShadowSplit3",.72)
	for light_name in ["Sun","Moon"]:
		var light: DirectionalLight3D = level.get_node("EnvironmentRig/"+light_name)
		light.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
		light.directional_shadow_max_distance = 80.0
		light.shadow_blur = 6.0
		light.directional_shadow_split_1 = .2
		light.directional_shadow_split_2 = .42
		light.directional_shadow_split_3 = .72
	preload("res://scripts/tools/art/forest_village_whitebox/forest_light_shafts.gd").new().apply(level)
	component(level.get_node("CameraRig"),"CameraOcclusionComponent3D").set("OccluderRootPath",NodePath("../Village"))
	# Camera3D and its components retain the shot/settings authored in the formal level.
	level.get_node("EntryDefault/Arrival").position = SPAWN
	var portal: Node3D = level.get_node("WorkshopPortal")
	portal.position = host_door+Vector3(0,0.4,0)
	portal.get_node("Arrival").position = Vector3(1.4,-0.35,0)
	var old_door := portal.get_node_or_null("Door")
	if old_door != null: old_door.visible = false
	var old_label := portal.get_node_or_null("Label3D")
	if old_label != null: old_label.position = Vector3(0,1.0,0)
	portal.editor_description = "杂货与待客户门前通往仓储间；返回落点位于门外，避免再次进入触发区。"
	if FileAccess.file_exists("res://assets/resources/art/forest_village/interiors_models.json"):
		var entrances := preload("res://scripts/tools/art/forest_village_interiors/entrance_library.gd").new()
		entrances.apply(level)
		entrances.own_children(level,level)
	# The old disabled placeholder floor is superseded by the village's colliders.
	var placeholder := level.get_node_or_null("Ground")
	if placeholder != null: placeholder.free()
	DirAccess.make_dir_recursive_absolute(NAVIGATION.get_base_dir())
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(nav,NAVIGATION)
	nav.take_over_path(NAVIGATION)
	level.get_node("World/NavigationRegion3D").navigation_mesh = nav
	var packed := PackedScene.new()
	assert(packed.pack(level) == OK)
	assert(ResourceSaver.save(packed,LEVEL) == OK)
	# Standalone authoring may have no UID cache; retain the source's sidecar links.
	var saved_lines := FileAccess.get_file_as_string(LEVEL).split("\n")
	var path_pattern := RegEx.new()
	path_pattern.compile('path="([^"]+)"')
	for i in range(saved_lines.size()):
		if not saved_lines[i].begins_with("[ext_resource ") or saved_lines[i].contains(" uid="): continue
		var path_match := path_pattern.search(saved_lines[i])
		if path_match != null and dependency_uids.has(path_match.get_string(1)):
			saved_lines[i] = saved_lines[i].replace(" path=",' uid="%s" path=' % dependency_uids[path_match.get_string(1)])
	var saved_file := FileAccess.open(LEVEL,FileAccess.WRITE)
	saved_file.store_string("\n".join(saved_lines))
	saved_file.close()
	assert(ResourceSaver.set_uid(LEVEL,ResourceUID.text_to_id(LEVEL_UID)) == OK)
	level.free()
	print("FOREST_REPLACEMENT_SAVED: ",LEVEL," spawn=",SPAWN," portal=",host_door)
	quit()
