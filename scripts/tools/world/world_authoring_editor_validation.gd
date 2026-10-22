@tool
extends SceneTree

const FOLDER = "res://.godot/world-validation/authoring/"
var checks = 0

func _initialize():
    call_deferred("run_probe")

func frames(count):
    for i in range(count):
        await process_frame

func check(ok, message):
    if not ok:
        push_error("WORLD_EDITOR: " + message)
        quit(1)
        return false
    checks += 1
    return true

func saved_service():
    return ResourceLoader.load(FOLDER + "service.tscn", "PackedScene", ResourceLoader.CACHE_MODE_IGNORE).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)

func run_probe():
    await frames(80)
    var validator = load("res://scripts/tools/world/WorldAuthoringEditorValidation.cs").new()
    EditorInterface.open_scene_from_path(FOLDER + "service.tscn")
    await frames(20)
    var service = EditorInterface.get_edited_scene_root()
    var components = service.get("Components")
    if not check(components.size() == 5, "Service fixture contains every declared provider"):
        return
    var point = components[1]
    point.set("RequireLineOfSight", false)
    components.resize(2)
    EditorInterface.mark_scene_as_unsaved()
    # Save the service while another tab is active, before its polling sync can repair it.
    EditorInterface.open_scene_from_path(FOLDER + "level.tscn")
    EditorInterface.save_all_scenes()
    await frames(15)
    var saved = saved_service()
    if not check(saved.get("Components").size() == 5 and validator.ValidateComponents(saved), "Saving an inactive Scene restores missing capability providers"):
        return
    if not check(not saved.get("Components")[1].get("RequireLineOfSight"), "Synchronization retains the existing interaction configuration"):
        return
    saved.free()
    for i in range(3):
        if not check(validator.CompileFixture() and validator.BuildProject(), "Repeated cached compilation and build succeed"):
            return
        if not check(service.get("Components").size() == 5 and service.get("Components")[1] == point and validator.ValidateComponents(service), "Compilation preserves open Scene arrays and resource identities"):
            return
    saved = saved_service()
    if not check(saved.get("Components").size() == 5 and validator.ValidateComponents(saved), "Repeated builds keep serialized providers intact"):
        return
    saved.free()
    EditorInterface.open_scene_from_path(FOLDER + "service.tscn")
    await frames(10)
    validator.SetExtraCapability(true)
    await create_timer(0.25).timeout
    if not check(service.get("Components").size() == 6 and validator.ValidateComponents(service), "Changing capabilities on the same type triggers synchronization"):
        return
    validator.SetExtraCapability(false)
    await create_timer(0.25).timeout
    if not check(service.get("Components").size() == 5 and validator.ValidateComponents(service), "Removing a capability removes only its provider"):
        return
    EditorInterface.save_scene()
    EditorInterface.open_scene_from_path(FOLDER + "level.tscn")
    await frames(10)
    var root = EditorInterface.get_edited_scene_root()
    var original = root.get_child(0)
    var key = str(original.get_meta("_world_placement", ""))
    var copied = original.duplicate()
    root.add_child(copied)
    copied.owner = root
    root.move_child(copied, 0)
    await frames(8)
    if not check(str(copied.get_meta("_world_placement", "")) != key and str(original.get_meta("_world_placement", "")) == key, "Duplication changes only the new placement identity"):
        return
    EditorInterface.save_scene()
    await frames(15)
    print("WORLD_EDITOR_OK: %s checks; inactive saves, cached builds, live type changes and placement duplication" % checks)
    quit()
