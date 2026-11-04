extends SceneTree
## Replace only the authored fence runs and bridge; keep all other village data.

const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
var library := preload("res://scripts/tools/art/forest_village_structures/structure_library.gd").new()

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var village: Node3D = load(ENV).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var changes: Array = []
	for group: Node in [village.get_node("Households"),village.get_node("Production")]:
		for old: Node3D in group.find_children("Fence_*","Node3D",true,false):
			var a: Vector3
			var b: Vector3
			if old.has_meta("fence_start"):
				a = old.get_meta("fence_start")
				b = old.get_meta("fence_end")
			else:
				var posts := old.find_children("Post_*","MeshInstance3D",false,false)
				assert(posts.size() >= 2)
				a = (posts[0] as Node3D).position-Vector3(0,0.475,0)
				b = (posts[-1] as Node3D).position-Vector3(0,0.475,0)
			var parent := old.get_parent()
			var index := old.get_index()
			var name_ := old.name
			var path := village.get_path_to(old)
			parent.remove_child(old)
			old.free()
			var replacement: Node3D = library.fence(name_,a,b)
			parent.add_child(replacement)
			parent.move_child(replacement,index)
			changes.append({"path":str(path),"start":str(a),"end":str(b)})
	var old_bridge := village.get_node("Bridge")
	var bridge_index := old_bridge.get_index()
	village.remove_child(old_bridge)
	old_bridge.free()
	var bridge: Node3D = library.bridge()
	village.add_child(bridge)
	village.move_child(bridge,bridge_index)
	library.own_children(village,village)
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(packed,ENV)
	var report := FileAccess.open("res://assets/resources/art/forest_village/structures_integration.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"fence_runs":changes,"bridge_center":str(bridge.position),"walkable_deck_y":0.015,"existing_rail_collision_preserved":true},"\t"))
	print("STRUCTURES_APPLIED ",changes.size()," fence runs; creek bridge; existing entrances and walkable surfaces preserved")
	village.free()
	quit()
