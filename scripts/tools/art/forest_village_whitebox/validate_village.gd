extends SceneTree
## Checks the authored environment against the player's collision footprint.
## Run with --headless --path . --script res://scripts/tools/art/forest_village_whitebox/validate_village.gd

const SCENE_PATH := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const GRID := 0.5
const MIN_X := -27.0
const MAX_X := 34.0
const MIN_Z := -24.0
const MAX_Z := 33.0
const MAX_STEP := 0.3
const MIN_GROUND := -0.35
const MIN_FLOOR_NORMAL_Y := 0.70710678 # CharacterBody3D's default 45 degree floor angle.
const RADIUS := 0.25
const HEIGHT := 0.936
const START := Vector2(1.56, 22.62)
const DIRECTIONS := [Vector2i.LEFT, Vector2i.RIGHT, Vector2i.UP, Vector2i.DOWN]

var checks := 0
var failures := 0
var space: PhysicsDirectSpaceState3D
var ray := PhysicsRayQueryParameters3D.new()
var clearance := PhysicsShapeQueryParameters3D.new()
var heights: Dictionary = {}
var samples: Dictionary = {}
var support_normals: Dictionary = {}
var reached: Dictionary = {}

func _initialize() -> void:
	call_deferred("run")

func check(passed: bool, description: String) -> void:
	checks += 1
	if not passed:
		failures += 1
	print("VILLAGE %s: %s" % ["PASS" if passed else "FAIL", description])

func run() -> void:
	var packed: PackedScene = load(SCENE_PATH)
	check(packed != null, "Environment PackedScene loads")
	if packed == null:
		quit(1)
		return
	var village: Node3D = packed.instantiate()
	root.add_child(village)
	await physics_frame
	await physics_frame
	space = village.get_world_3d().direct_space_state
	ray.collision_mask = 1
	ray.collide_with_areas = false
	var capsule := CapsuleShape3D.new()
	capsule.radius = RADIUS
	capsule.height = HEIGHT
	clearance.shape = capsule
	clearance.margin = 0.005
	clearance.collision_mask = 1
	clearance.collide_with_areas = false

	var houses: Array[Node] = get_nodes_in_group("village_house")
	var entrances: Array[Node] = get_nodes_in_group("village_entrance")
	check(houses.size() == 10, "Exactly 10 village_house nodes (found %d)" % houses.size())
	check(village.has_node("Households/Chief"), "Chief household exists")
	check(get_nodes_in_group("forest_tree").size() >= 180, "Continuous woodland has at least 180 mature trees")
	check(get_nodes_in_group("village_tree").size() >= 35, "At least 35 mature trees stand between households")
	var tree_sources: Dictionary = {}
	var shrub_sources: Dictionary = {}
	var imported_trees := true
	var imported_shrubs := true
	for tree in get_nodes_in_group("forest_tree"):
		tree_sources[tree.scene_file_path] = true
		var model := tree.get_node_or_null("Model") as MeshInstance3D
		imported_trees = imported_trees and tree.scene_file_path.begins_with("res://assets/scenes/environment/forest_vegetation/") and model != null and model.mesh is ArrayMesh
	for shrub in get_nodes_in_group("forest_shrub"):
		shrub_sources[shrub.scene_file_path] = true
		var model := shrub.get_node_or_null("Model") as MeshInstance3D
		imported_shrubs = imported_shrubs and shrub.scene_file_path.begins_with("res://assets/scenes/environment/forest_vegetation/") and model != null and model.mesh is ArrayMesh
	check(imported_trees, "All trees instance the forest level's mesh scenes")
	check(imported_shrubs and get_nodes_in_group("forest_shrub").size() == 488, "All 488 shrubs instance the forest level's mesh scenes")
	check(tree_sources.size() == 3, "Three forest oak variants are present")
	check(shrub_sources.size() == 3, "Three forest shrub variants are present")
	check(entrances.size() == 10, "Exactly 10 village_entrance markers (found %d)" % entrances.size())
	var forest_houses := 0
	for house in houses:
		var building := house.get_node_or_null("Building")
		if building != null and building.scene_file_path.begins_with("res://assets/scenes/environment/forest_village/houses/") and building.get_meta("complete_exterior",false):
			forest_houses += 1
		var door: Node3D = house.get_node_or_null("DoorApproach")
		check(door != null and door.is_in_group("village_entrance"), "%s has its own DoorApproach" % house.name)
		var neighbours := 0
		for tree in get_nodes_in_group("forest_tree"):
			if (tree as Node3D).global_position.distance_to((house as Node3D).global_position) <= 12.0:
				neighbours += 1
		check(neighbours >= 3, "%s clearing is surrounded by mature trees (%d within 12m)" % [house.name,neighbours])
	check(forest_houses == 10, "All 10 households use the complete V2 building scenes")
	var path_mesh: MeshInstance3D = village.get_node("Paths/Surface")
	check(path_mesh.get_meta("source_material","") == "res://assets/materials/environment/forest_town/ground.tres" and (path_mesh.get_active_material(0) as ShaderMaterial).shader.resource_path == "res://assets/shaders/town/town_toon.gdshader", "Path joints use the opaque forest weather shader")
	var paving: Node3D = village.get_node("Paths/Flagstones")
	check(paving.get_meta("slab_count",0) > 700, "Flagstones cover the village path network")
	check(paving.find_children("*","CollisionObject3D",true,false).is_empty(), "Decorative flagstones have no collision")
	var contact: Node3D = village.get_node("GroundContact")
	check(contact.find_children("*","CollisionObject3D",true,false).is_empty(), "Contact grass and root litter have no collision")
	check(contact.get_meta("root_count",0) == get_nodes_in_group("forest_tree").size(), "Every forest tree has a terrain contact transition")
	var root_materials := true
	for tree in get_nodes_in_group("forest_tree"):
		var bark := tree.get_node("Model").get_active_material(0) as ShaderMaterial
		root_materials = root_materials and bark.get_shader_parameter("use_ground_contact") == true and is_equal_approx(float(bark.get_shader_parameter("ground_contact_height")),0.38)
	check(root_materials, "Scene-local moss materials survive PackedScene instance overrides")

	var columns := roundi((MAX_X - MIN_X) / GRID) + 1
	var rows := roundi((MAX_Z - MIN_Z) / GRID) + 1
	for x in range(columns):
		for z in range(rows):
			var cell := Vector2i(x, z)
			var height := sample_height(cell_position(cell))
			if is_finite(height):
				heights[cell] = height
	var start_cell := position_cell(START)
	check(heights.has(start_cell), "Village entrance has player clearance")
	reached = flood(start_cell)
	print("VILLAGE GRID: %.2fm; radius %.2fm; height %.3fm; %d/%d clear cells reachable" % [GRID, RADIUS, HEIGHT, reached.size(), heights.size()])
	for door in entrances:
		var at: Vector3 = (door as Node3D).global_position
		check_target(Vector2(at.x, at.z), "%s door" % door.get_parent().name)

	var well: Node3D = village.get_node("Commons/Well")
	var well_center := Vector2(well.global_position.x, well.global_position.z)
	for side in DIRECTIONS:
		check_target(well_center + Vector2(side) * 2.0, "Well outer edge %s" % side)
	var deck: CollisionShape3D = village.get_node("Bridge/Collision/Deck")
	var bridge_center := Vector2(deck.global_position.x, deck.global_position.z)
	var bridge_size: Vector3 = (deck.shape as BoxShape3D).size
	check_target(bridge_center - Vector2(bridge_size.x * 0.5 + 0.5, 0), "Bridge west approach")
	check_target(bridge_center + Vector2(bridge_size.x * 0.5 + 0.5, 0), "Bridge east approach")
	check_target(bridge_center, "Bridge crossing")
	check_target(Vector2(32.76, 2.34), "East-bank road")

	# Negative controls ensure the search cannot walk through buildings or the creek.
	var chief: Node3D = village.get_node("Households/Chief")
	var chief_body: Node3D = chief.get_node("Building/Collision1/Shape00")
	check(not reached.has(position_cell(Vector2(chief_body.global_position.x, chief_body.global_position.z))), "House collision excludes its footprint from connected walking space")
	check(not is_finite(sample_height(bridge_center + Vector2(0, 8))), "Unbridged creek bed is not walkable")
	check(is_finite(sample_height(bridge_center)), "Bridge deck remains a valid low step")
	var without_bridge := flood(start_cell, bridge_center.x)
	check(not without_bridge.has(position_cell(Vector2(32.76, 2.34))), "Removing crossing cells disconnects the east bank; no dry bypass")

	print("VILLAGE RESULT: %d/%d checks passed; %d failed" % [checks - failures, checks, failures])
	village.queue_free()
	quit(0 if failures == 0 else 1)

func cell_position(cell: Vector2i) -> Vector2:
	return Vector2(MIN_X + cell.x * GRID, MIN_Z + cell.y * GRID)

func position_cell(at: Vector2) -> Vector2i:
	return Vector2i(roundi((at.x - MIN_X) / GRID), roundi((at.y - MIN_Z) / GRID))

func ground_height(at: Vector2) -> float:
	ray.from = Vector3(at.x, 10.0, at.y)
	ray.to = Vector3(at.x, -2.0, at.y)
	var hit: Dictionary = space.intersect_ray(ray)
	if hit.is_empty():
		return INF
	var height: float = hit.position.y
	var normal: Vector3 = hit.normal
	support_normals[at] = normal
	# MAX_STEP limits transitions, not absolute elevation: raised decks remain valid.
	return height if height >= MIN_GROUND and normal.y >= MIN_FLOOR_NORMAL_Y else INF

func sample_height(at: Vector2) -> float:
	if samples.has(at):
		return samples[at]
	var center := ground_height(at)
	if is_finite(center):
		# Require support/clearance across the footprint, not only a point in a crack.
		for side in DIRECTIONS:
			var edge := ground_height(at + Vector2(side) * (RADIUS + 0.01))
			if not is_finite(edge) or absf(edge - center) > MAX_STEP + 0.001:
				center = INF
				break
	if is_finite(center):
		# Lift the capsule's lower sphere to the actual tangent point on a ramp.
		var normal: Vector3 = support_normals[at]
		var slope_lift := RADIUS * (1.0 / normal.y - 1.0)
		clearance.transform = Transform3D(Basis.IDENTITY, Vector3(at.x, center + HEIGHT * 0.5 + slope_lift + 0.025, at.y))
		if not space.intersect_shape(clearance, 1).is_empty():
			center = INF
	samples[at] = center
	return center

func can_step(a: Vector2, b: Vector2) -> bool:
	var previous := sample_height(a)
	if not is_finite(previous):
		return false
	var steps := maxi(1, ceili(a.distance_to(b) / (GRID * 0.5)))
	for i in range(1, steps + 1):
		var next := sample_height(a.lerp(b, float(i) / steps))
		if not is_finite(next) or absf(next - previous) > MAX_STEP + 0.001:
			return false
		previous = next
	return true

func flood(start: Vector2i, cut_x := INF) -> Dictionary:
	var visited: Dictionary = {}
	if not heights.has(start):
		return visited
	var queue: Array[Vector2i] = [start]
	visited[start] = true
	var head := 0
	while head < queue.size():
		var cell := queue[head]
		head += 1
		for direction in DIRECTIONS:
			var next: Vector2i = cell + direction
			if visited.has(next) or not heights.has(next):
				continue
			var at := cell_position(next)
			if is_finite(cut_x) and absf(at.x - cut_x) <= GRID:
				continue
			if can_step(cell_position(cell), at):
				visited[next] = true
				queue.append(next)
	return visited

func check_target(at: Vector2, description: String) -> void:
	var accessible := false
	var anchor := position_cell(at)
	if is_finite(sample_height(at)):
		for dx in range(-1, 2):
			for dz in range(-1, 2):
				var candidate := anchor + Vector2i(dx, dz)
				if reached.has(candidate) and can_step(cell_position(candidate), at):
					accessible = true
	check(accessible, "%s reachable at (%.2f, %.2f)" % [description, at.x, at.y])
	if not accessible:
		var nearest := Vector2(INF, INF)
		var distance := INF
		for cell in reached:
			var point := cell_position(cell)
			var length := point.distance_to(at)
			if length < distance:
				nearest = point
				distance = length
		print("VILLAGE BLOCKED: %s; exact point clear=%s; closest reachable=%s (%.2fm away)" % [description, is_finite(sample_height(at)), nearest, distance])
