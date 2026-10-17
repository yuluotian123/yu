extends RefCounted
## Native cleanup callback remains callable after a C# domain reload.
var device: RenderingDevice
var owned: Array

func schedule() -> void:
	RenderingServer.call_on_render_thread(_release.bind(self))

func _release(_keep_alive: RefCounted) -> void:
	if device != null:
		for rid in owned:
			if rid.is_valid(): device.free_rid(rid)
	owned.clear()
	device = null
