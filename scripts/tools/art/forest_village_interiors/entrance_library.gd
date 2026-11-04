extends RefCounted
## Authoring only: doorway triggers and safe arrivals at the existing household doors.

const MANIFEST := "res://assets/resources/art/forest_village/interiors_models.json"
const HOST := "res://scripts/gamelogic/gameobject/GameObject3D.cs"
const TRANSITION := "res://scripts/gamelogic/world/components/WorldTransitionComponent3D.cs"
const SPAWN := "res://scripts/gamelogic/world/components/WorldSpawnPointComponent3D.cs"

func component(host: Node, file: String) -> Resource:
	for item: Resource in host.get("Components"):
		if item.get_script().resource_path.ends_with("/"+file+".cs"): return item
	return null

func configure(portal: Node3D, level: Resource, target_entry: String, return_entry: String, at: Transform3D, arrival: Transform3D) -> void:
	portal.transform = at
	var transition := load(TRANSITION).new() as Resource
	transition.set("TargetLevel",level)
	transition.set("TargetEntry",target_entry)
	var spawn := load(SPAWN).new() as Resource
	spawn.set("Entry",return_entry)
	portal.set("Components",[transition,spawn])
	for child: Node in portal.get_children(): child.free()
	var marker := Marker3D.new()
	marker.name = "Arrival"
	marker.transform = at.affine_inverse()*arrival
	portal.add_child(marker)
	var area := Area3D.new()
	area.name = "Area3D"
	area.collision_layer = 0
	area.collision_mask = 1
	portal.add_child(area)
	var shape := CollisionShape3D.new()
	shape.name = "DoorwayTrigger"
	var box := BoxShape3D.new()
	box.size = Vector3(.82,1.65,.36)
	shape.shape = box
	area.add_child(shape)
	var use := Marker3D.new()
	use.name = "UsePosition"
	use.position = Vector3(0,-.40,.15)
	portal.add_child(use)
	portal.editor_description = "正门自动进入；触发区贴合门槛，返回落点位于触发区外。后侧不开通路。"

func apply(level: Node3D) -> void:
	var existing := level.get_node_or_null("HouseEntrances")
	if existing != null: existing.free()
	var entrances := Node3D.new()
	entrances.name = "HouseEntrances"
	level.add_child(entrances)
	var village: Node3D = level.get_node("Village")
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
	for asset: Dictionary in manifest.assets:
		var home: Node3D = village.get_node("Households/"+asset.household)
		var building: Node3D = home.get_node("Building")
		var door: Node3D = building.get_node("DoorAnchor")
		# Compute authored transforms without adding EC hosts to the runtime scene tree.
		var door_transform := home.transform*building.transform*door.transform
		var at := door_transform
		at.origin += at.basis*Vector3(0,.68,.30)
		var stair_approach: Node3D = building.get_node("ApproachAnchor")
		var arrival := home.transform*building.transform*stair_approach.transform
		arrival.origin += arrival.basis*Vector3(0,0,.30)
		arrival.origin.y += .035
		arrival.basis = home.basis
		var target_path: String = "res://assets/resources/world/levels/forest_house_"+asset.name+".tres"
		var portal: Node3D
		if asset.household == "Host":
			portal = level.get_node("WorkshopPortal")
			target_path = "res://assets/resources/world/levels/world_workshop.tres"
		else:
			portal = load(HOST).new()
			portal.name = asset.household
			portal.set_meta("_world_placement",("forest_house/"+asset.name).md5_text())
			entrances.add_child(portal)
		configure(portal,load(target_path),"from_town","from_workshop" if asset.household == "Host" else "from_house_"+asset.household.to_snake_case(),at,arrival)
		portal.get_node("UsePosition").position = at.affine_inverse()*(home.transform*building.transform*stair_approach.position)
		portal.set_meta("household",asset.household)
		portal.set_meta("interior_name",asset.display_name)
		portal.add_to_group("forest_house_entrance",true)
		print("FOREST_HOUSE_ENTRANCE ",asset.household," trigger=",at.origin," arrival=",arrival.origin)

func own_children(parent: Node, root: Node) -> void:
	for child: Node in parent.get_children():
		child.owner = root
		if child.scene_file_path.is_empty(): own_children(child,root)
