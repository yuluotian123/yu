@tool
extends Node3D

# Optional editor-only inspection helper. No automatic scene mutation.
func scan_assets() -> Dictionary:
    EditorInterface.get_resource_filesystem().scan()
    return {"scan_requested": true}

func inspect_scene() -> Dictionary:
    var town: Node = get_node_or_null("TownContent")
    if town == null:
        return {"error": "No TownContent"}
    var report: Dictionary = {"scanning": EditorInterface.get_resource_filesystem().is_scanning()}
    for node_name in ["Town_Trees", "Town_Flowers"]:
        var mesh_node: MeshInstance3D = town.get_node("Architecture/" + node_name)
        var mesh_data: Mesh = mesh_node.mesh
        var surfaces: Array = []
        for i in range(mesh_data.get_surface_count()):
            var mat: Material = mesh_data.surface_get_material(i)
            var detail: Dictionary = {"name": mat.resource_name}
            if mat is BaseMaterial3D:
                detail["transparency"] = mat.transparency
                detail["cull_mode"] = mat.cull_mode
                detail["vertex_color_use_as_albedo"] = mat.vertex_color_use_as_albedo
            surfaces.append(detail)
        report[node_name] = {"triangles": mesh_data.get_faces().size() / 3, "surfaces": surfaces}
    return report
