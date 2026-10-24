@tool
extends SceneTree

var checks = 0

func _initialize():
    call_deferred("run_probe")

func frames(count):
    for i in range(count):
        await process_frame

func check(ok, message):
    if not ok:
        push_error("WORLD_LEVEL_EXPORT_EDITOR: " + message)
        quit(1)
        return false
    checks += 1
    return true

func run_probe():
    await frames(80)
    var validator = load("res://scripts/tools/world/WorldAuthoringEditorValidation.cs").new()
    EditorInterface.open_scene_from_path("res://.godot/world-validation/level-export/B.tscn")
    await frames(20)
    var level = EditorInterface.get_edited_scene_root()
    if not check(level != null and level.name == "B", "The fixture opens in the actual editor"):
        return
    var portal = level.get_node("Return")
    portal.position += Vector3(3, 0, 0)
    if not check(validator.ExportLevelFixture(level).contains("关卡配置已导出"), "Export completes through EditorInterface.save_scene"):
        return
    await frames(20)
    if not check(validator.ValidateLevelExport(level), "The saved Scene and generated route contain the edited portal position"):
        return
    if not check(validator.ExportLevelFixture(level).contains("关卡配置已导出") and validator.ValidateLevelExport(level), "Repeated editor export succeeds without a nested save or build"):
        return
    await frames(20)
    print("WORLD_LEVEL_EXPORT_EDITOR_OK: %s checks" % checks)
    quit()
