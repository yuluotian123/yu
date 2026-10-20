extends SceneTree

func _initialize() -> void:
	call_deferred("run_validation")

func run_validation() -> void:
	var validation = load("res://scripts/tools/rendering/DdgiCaptureValidation.cs").new()
	var count: int = validation.call("Run", root)
	quit(0 if count == 12 else 1)
