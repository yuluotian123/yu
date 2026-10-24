@tool
extends Node3D
## Temporary editor helper: replace only this task's authored RPGTown subtree.
## Attach to RPGTown, call sync_authored_town(), and the fresh instance has no script.

func sync_authored_town() -> Dictionary:
	assert(Engine.is_editor_hint(), "Editor-only art synchronization")
	assert(name == "RPGTown" and get_parent() != null, "Only the authored town may be replaced")
	var scene_root := get_parent()
	assert(scene_root.scene_file_path == "res://assets/scenes/levels/forest_town_level.tscn", "Unexpected edited scene")
	var protected: Array = []
	for sibling: Node in scene_root.get_children():
		if sibling != self:
			protected.append({"node":sibling,"transform":sibling.transform if sibling is Node3D else null})
	var backup := PackedScene.new()
	assert(backup.pack(self) == OK, "Could not snapshot current town")
	var backup_path := "res://.godot/forest-town/editor-town-before-" + Time.get_datetime_string_from_system().replace(":", "-") + ".tscn"
	assert(ResourceSaver.save(backup, backup_path) == OK, "Could not save town snapshot")
	var packed: PackedScene = ResourceLoader.load("res://assets/scenes/environment/forest_town/forest_town.tscn", "PackedScene", ResourceLoader.CACHE_MODE_REPLACE_DEEP)
	assert(packed != null)
	var replacement: Node3D = packed.instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	assert(replacement.get_node("Buildings").get_child_count() == 8)
	assert(replacement.has_node("Props/LaneSteps/WalkableStairRamp"))
	assert(replacement.has_node("Water/ForegroundRiver/Model"))
	var town_index := get_index()
	var previous_transform := transform
	scene_root.remove_child(self)
	replacement.name = "RPGTown"
	scene_root.add_child(replacement)
	scene_root.move_child(replacement, town_index)
	replacement.owner = scene_root
	replacement.transform = previous_transform
	for item: Dictionary in protected:
		assert(item.node.get_parent() == scene_root, "Unrelated scene node changed")
		if item.node is Node3D:
			assert(item.node.transform == item.transform, "Unrelated scene transform changed")
	queue_free()
	return {"updated":"RPGTown","buildings":8,"trees":replacement.get_node("Trees").get_child_count(),"plants":replacement.get_node("Plants").get_child_count(),"props":replacement.get_node("Props").get_child_count(),"water":replacement.get_node("Water").get_child_count(),"backup":backup_path,"other_scene_nodes_preserved":true}

func sync_authored_trees() -> Dictionary:
	assert(Engine.is_editor_hint() and name == "RPGTown")
	var trees: Node3D = get_node("Trees")
	var protected: Array = []
	for sibling in get_children():
		if sibling != trees:
			protected.append({"node":sibling,"transform":sibling.transform,"children":sibling.get_children()})
	var snapshot := PackedScene.new()
	assert(snapshot.pack(self) == OK)
	var backup_path := "res://.godot/forest-town/editor-before-tree-upgrade-"+Time.get_datetime_string_from_system().replace(":","-")+".tscn"
	assert(ResourceSaver.save(snapshot,backup_path) == OK)
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_town_v1/models.json"))
	var models := {}
	for name: String in manifest.tree_upgrade.variants:
		models[name] = ResourceLoader.load("res://assets/scenes/environment/forest_town/models/"+name+".tscn","PackedScene",ResourceLoader.CACHE_MODE_REPLACE_DEEP)
	for child in trees.get_children():
		trees.remove_child(child)
		child.queue_free()
	for placement: Dictionary in manifest.instances:
		if not models.has(placement.asset): continue
		var instance: Node3D = models[placement.asset].instantiate()
		instance.name = placement.name
		instance.position = Vector3(placement.position[0],placement.position[1],placement.position[2])
		instance.rotation.y = deg_to_rad(placement.rotation_y)
		instance.scale = Vector3.ONE*placement.scale
		trees.add_child(instance)
		instance.owner = self
	assert(trees.get_child_count() == int(manifest.tree_upgrade.trees_after))
	for item: Dictionary in protected:
		assert(item.node.get_parent() == self and item.node.transform == item.transform)
		assert(item.node.get_children() == item.children)
	call_deferred("set_script",null)
	return {"updated":"Trees","trees":trees.get_child_count(),"variants":models.size(),"backup":backup_path,"other_groups_preserved":true}
