@tool
extends SceneTree

func _initialize():
    call_deferred("run_probe")

func frames(count):
    for i in range(count):
        await process_frame

func run_probe():
    await frames(80)
    EditorInterface.open_scene_from_path("res://.godot/world-validation/authoring/level.tscn")
    await frames(30)
    var root = EditorInterface.get_edited_scene_root()
    if root == null:
        push_error("WORLD_EDITOR: missing edited root")
        quit(1)
        return
    var original = root.get_child(0)
    var key = str(original.get_meta("_world_placement", ""))
    var copied = original.duplicate()
    root.add_child(copied)
    copied.owner = root
    root.move_child(copied, 0)
    await frames(8)
    if str(copied.get_meta("_world_placement", "")) == key or str(original.get_meta("_world_placement", "")) != key:
        push_error("WORLD_EDITOR: duplication changed original or kept duplicate identity")
        quit(1)
        return
    EditorInterface.save_scene()
    await frames(15)
    print("WORLD_EDITOR_OK: plugin handles live duplicate before original and ordinary save")
    quit()
