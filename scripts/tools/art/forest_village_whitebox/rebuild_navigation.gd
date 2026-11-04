extends SceneTree
## Refresh the registered village navmesh without rewriting scene placements.

const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const NAV := "res://assets/resources/world/navigation/forest_town.tres"

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var village: Node3D = load(ENV).instantiate()
	root.add_child(village)
	await physics_frame
	var nav := preload("res://scripts/tools/art/forest_village_whitebox/village_navigation.gd").new().bake(village)
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
	for i in range(4): await physics_frame
	var targets: Array[Vector3] = []
	for home in village.get_node("Households").get_children():
		targets.append(home.get_node("DoorApproach").global_position)
	targets.append_array([Vector3(-1.6,0,1.5),Vector3(28.08,0.015,2.34),Vector3(32.76,0,2.34)])
	for target in targets:
		var path := NavigationServer3D.map_get_path(map,Vector3(1.56,0.55,22.62),target,true)
		assert(not path.is_empty() and Vector2(path[-1].x-target.x,path[-1].z-target.z).length() < 0.55,"Disconnected village route: "+str(target))
	var floor_point := NavigationServer3D.map_get_closest_point(map,Vector3(-1.6,0,1.5))
	assert(absf(floor_point.y) < 0.15,"Resident's path must stay within its 20cm waypoint tolerance")
	NavigationServer3D.free_rid(region)
	NavigationServer3D.free_rid(map)
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(nav,NAV)
	print("VILLAGE_NAVIGATION_SAVED: ",nav.get_polygon_count()," polygons; cell height=",nav.cell_height,"; 13 routes connected; resident floor=",floor_point.y)
	village.queue_free()
	await process_frame
	quit()
