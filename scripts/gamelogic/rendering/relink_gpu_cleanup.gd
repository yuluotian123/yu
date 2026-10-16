@tool
extends RefCounted

# 原生脚本回调在 C# 域重载后仍有效；清理过程始终在渲染线程运行。
var device: RenderingDevice
var owned: Array
var buffers: Array
var context: StringName

func schedule() -> void:
	RenderingServer.call_on_render_thread(release.bind(self))

func release(_keep_alive: RefCounted) -> void:
	for buffer in buffers:
		buffer.clear_context(context)
	# 最后一个原生缓冲引用也在渲染线程释放。
	buffers.clear()
	for rid in owned:
		if rid.is_valid():
			device.free_rid(rid)
	owned.clear()
	device = null
