extends SceneTree

const OUTPUT := "res://assets/resources/npc/riverside_navigation.tres"

func _initialize() -> void:
    _run.call_deferred()

func _run() -> void:
    var town: Node3D = load("res://assets/scenes/town/riverside_town.tscn").instantiate()
    root.add_child(town)
    await process_frame
    var mesh := NavigationMesh.new()
    mesh.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
    mesh.agent_height = 0.8
    mesh.agent_radius = 0.3
    mesh.agent_max_climb = 0.32
    mesh.agent_max_slope = 35.0
    mesh.cell_size = 0.15
    mesh.cell_height = 0.08
    var geometry := NavigationMeshSourceGeometryData3D.new()
    NavigationServer3D.parse_source_geometry_data(mesh, geometry, town)
    NavigationServer3D.bake_from_source_geometry_data(mesh, geometry)
    if mesh.get_polygon_count() == 0:
        push_error("NPC_NAVIGATION_BAKE_EMPTY")
        quit(1)
        return
    var region := NavigationRegion3D.new()
    region.navigation_mesh = mesh
    root.add_child(region)
    var map := region.get_world_3d().navigation_map
    for attempt in range(120):
        await physics_frame
        if NavigationServer3D.region_get_iteration_id(region.get_rid()) > 0 and NavigationServer3D.map_get_iteration_id(map) > 1:
            break
    var targets: Array[Vector3] = [Vector3(-10, 0, 2), Vector3(10, 0, 3), Vector3(0, 0, -15)]
    var valid := true
    print("NPC_NAVIGATION_MAP iteration=", NavigationServer3D.map_get_iteration_id(map), " region_iteration=", NavigationServer3D.region_get_iteration_id(region.get_rid()))
    for i in range(targets.size() - 1):
        var path := NavigationServer3D.map_get_path(map, targets[i], targets[i + 1], true)
        print("NPC_NAVIGATION_PROBE ", targets[i], " -> ", targets[i + 1], " points=", path.size(), " closest=", NavigationServer3D.map_get_closest_point(map, targets[i + 1]))
        valid = valid and not path.is_empty() and path[-1].distance_to(targets[i + 1]) < 0.8
    region.queue_free()
    if not valid:
        push_error("NPC_NAVIGATION_BAKE_DISCONNECTED home/shop/bridge")
        quit(1)
        return
    var error := ResourceSaver.save(mesh, OUTPUT)
    print("NPC_NAVIGATION_BAKE ", JSON.stringify({"polygons": mesh.get_polygon_count(), "output": OUTPUT, "error": error, "home_shop_bridge_connected": valid}))
    town.queue_free()
    await process_frame
    quit(0 if error == OK else 1)
