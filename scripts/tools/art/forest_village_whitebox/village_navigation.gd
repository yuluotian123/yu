extends RefCounted
## Bake only physical walkable surfaces; paving and contact plants are decoration.

func bake(village: Node3D) -> NavigationMesh:
	var nav := NavigationMesh.new()
	# The old 20cm voxels raised the floor beyond the NPC's 20cm waypoint tolerance.
	nav.cell_height = 0.05
	nav.agent_height = 1.0
	nav.agent_radius = 0.25
	nav.agent_max_climb = 0.25
	nav.agent_max_slope = 40.0
	nav.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
	nav.geometry_collision_mask = 1
	nav.filter_baking_aabb = AABB(Vector3(-41,-1.2,-38),Vector3(82,2.4,76))
	nav.filter_ledge_spans = true
	nav.filter_walkable_low_height_spans = true
	nav.region_min_size = 2.0
	var source := NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(nav,source,village)
	assert(source.has_data(),"Village colliders must provide navigation geometry")
	NavigationServer3D.bake_from_source_geometry_data(nav,source)
	assert(nav.get_polygon_count() > 30,"Navigation must follow the village geometry")
	return nav
