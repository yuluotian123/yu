extends "res://scripts/tools/art/forest_village_interiors/build_scenes.gd"
## Reuse the room baker without regenerating models, entrances or world records.

func check_routes(nav: NavigationMesh, level: Node3D, asset: Dictionary) -> Dictionary:
	var map := NavigationServer3D.map_create()
	NavigationServer3D.map_set_use_async_iterations(map,false)
	NavigationServer3D.map_set_active(map,true)
	NavigationServer3D.map_set_cell_size(map,nav.cell_size)
	NavigationServer3D.map_set_cell_height(map,nav.cell_height)
	var region := NavigationServer3D.region_create()
	NavigationServer3D.region_set_use_async_iterations(region,false)
	NavigationServer3D.region_set_navigation_mesh(region,nav)
	NavigationServer3D.region_set_map(region,map)
	NavigationServer3D.map_force_update(map)
	for i: int in 3: await physics_frame
	var start: Vector3 = level.get_node("EntryDefault/Arrival").position
	var targets: Array[Vector3] = [Vector3.ZERO]
	for marker: Marker3D in level.find_children("UsePosition","Marker3D",true,false):
		var at := marker.position
		var parent := marker.get_parent()
		while parent != level:
			at = (parent as Node3D).transform*at
			parent = parent.get_parent()
		targets.append(at)
	var failures: Array[String] = []
	for target: Vector3 in targets:
		var path := NavigationServer3D.map_get_path(map,start,target,true)
		if path.is_empty() or Vector2(path[-1].x-target.x,path[-1].z-target.z).length()>.55:
			failures.append(str(target))
	NavigationServer3D.free_rid(region)
	NavigationServer3D.free_rid(map)
	return {"room":asset.name,"polygons":nav.get_polygon_count(),"routes":targets.size(),"failures":failures}

func run() -> void:
	create_timer(60).timeout.connect(func(): push_error("Interior navigation rebuild timed out"); quit(1))
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
	var rebuilt: Array[Dictionary] = []
	var report: Array[Dictionary] = []
	var failure_count := 0
	for asset: Dictionary in manifest.assets:
		var path: String = LEGACY if asset.household == "Host" else LEVELS+asset.name+".tscn"
		var level: Node3D = load(path).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
		var old: NavigationMesh = level.get_node("World/NavigationRegion3D").navigation_mesh
		var destination := old.resource_path
		assert(destination.begins_with("res://assets/resources/world/"))
		var nav := navigation(level,asset)
		nav.resource_name = old.resource_name
		var result: Dictionary = await check_routes(nav,level,asset)
		failure_count += result.failures.size()
		report.append(result)
		rebuilt.append({"nav":nav,"path":destination})
		level.free()
		print("INTERIOR_NAVIGATION_CHECK ",asset.name," polygons=",result.polygons," routes=",result.routes," failures=",result.failures)
	if failure_count>0:
		push_error("Interior navigation disconnected; existing resources preserved")
		quit(1)
		return
	for item: Dictionary in rebuilt: saver.save_resource(item.nav,item.path)
	DirAccess.make_dir_recursive_absolute("res://.godot/forest-navigation-rebuild/")
	var file := FileAccess.open("res://.godot/forest-navigation-rebuild/interiors.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"all_passed":true,"rooms":report},"\t"))
	print("INTERIORS_NAVIGATION_SAVED: ",rebuilt.size()," rooms; all entrances and interaction routes connected")
	quit()
