"""Replace detached tubular knots with shallow, tapered scars on the bark.

Run in Blender on the authored forest trees. Unrelated geometry and corner data
are copied exactly; the same helper also finishes freshly generated trees.
"""
import math
import random
from collections import defaultdict

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


def _components(mesh):
    parents = list(range(len(mesh.vertices)))

    def find(index):
        while parents[index] != index:
            parents[index] = parents[parents[index]]
            index = parents[index]
        return index

    for face in mesh.polygons:
        root = find(face.vertices[0])
        for vertex in face.vertices[1:]:
            parents[find(vertex)] = root
    groups = defaultdict(list)
    for face in mesh.polygons:
        groups[find(face.vertices[0])].append(face.index)
    return list(groups.values())


def repair_tree_knots(obj, spec, remove_existing=True):
    assert obj.get('bark_scar_revision', 0) < 4, obj.name
    source = obj.data
    radius, height = spec['radius'], spec['height']
    phase = random.Random(spec['seed']).uniform(0, math.tau)
    groups = _components(source)
    trunk = max(groups, key=len)
    knots = []
    if remove_existing:
        for group in groups:
            if len(group) != 50:
                continue
            points = [source.vertices[v].co for i in group for v in source.polygons[i].vertices]
            middle_z = (min(p.z for p in points) + max(p.z for p in points)) * .5
            if any(abs(middle_z-height*t) < radius*.04 for t in (.16, .32)):
                knots.append(group)
        assert len(knots) == 2, (obj.name, 'Expected two tubular knots', len(knots))
    removed = {index for group in knots for index in group}
    old_uv = source.uv_layers.active
    old_paint = source.color_attributes['Palette']
    old_normals = [normal.vector.copy() for normal in source.corner_normals]
    source.calc_loop_triangles()
    triangles = defaultdict(list)
    for triangle in source.loop_triangles:
        triangles[triangle.polygon_index].append(tuple(triangle.loops))
    bvh = BVHTree.FromPolygons([v.co for v in source.vertices],
                              [list(source.polygons[i].vertices) for i in trunk])
    vertices, faces, uv_faces, paint_faces, normal_faces, settings = [], [], [], [], [], []
    lookup = {}

    def vertex(point):
        key = tuple(float(v) for v in point)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(key)
        return lookup[key]

    def face(points, uvs, colors, normals, material=0, smooth=True):
        faces.append([vertex(p) for p in points])
        uv_faces.append(uvs)
        paint_faces.append(colors)
        normal_faces.append(normals)
        settings.append((material, smooth))

    for polygon in source.polygons:
        if polygon.index in removed:
            continue
        loops = list(polygon.loop_indices)
        face([source.vertices[source.loops[i].vertex_index].co for i in loops],
             [old_uv.data[i].uv.copy() for i in loops],
             [tuple(old_paint.data[i].color) for i in loops],
             [old_normals[i] for i in loops], polygon.material_index, polygon.use_smooth)

    def stem(t):
        bend = spec['lean'] * (math.sin(t*3.8)*.75 + t*t*.75)
        return Vector((bend, radius*.45*math.sin(t*4.5+phase)*t, height*t))

    def sample(origin, direction):
        point, normal, index, distance = bvh.ray_cast(origin, direction, radius*4)
        assert point is not None, (obj.name, 'Scar must land on the trunk')
        polygon = trunk[index]
        for loops in triangles[polygon]:
            a, b, c = [source.vertices[source.loops[i].vertex_index].co for i in loops]
            ab, ac, ap = b-a, c-a, point-a
            aa, bb, cc = ab.dot(ab), ab.dot(ac), ac.dot(ac)
            determinant = aa*cc-bb*bb
            if abs(determinant) < 1e-12:
                continue
            v = (cc*ap.dot(ab)-bb*ap.dot(ac))/determinant
            w = (aa*ap.dot(ac)-bb*ap.dot(ab))/determinant
            weights = (1-v-w, v, w)
            if min(weights) < -1e-4:
                continue
            uv = sum((old_uv.data[i].uv*weight for i, weight in zip(loops, weights)), Vector((0, 0)))
            color = tuple(sum(old_paint.data[i].color[k]*weight for i, weight in zip(loops, weights)) for k in range(4))
            smooth_normal = sum((old_normals[i]*weight for i, weight in zip(loops, weights)), Vector()).normalized()
            return point, smooth_normal, uv, color
        raise AssertionError((obj.name, 'Cannot interpolate trunk corner data', polygon))

    max_relief = 0.0
    for t, angle in ((.16, phase+.6), (.32, phase+3.4)):
        outward = Vector((math.cos(angle), math.sin(angle), 0))
        right = Vector((-math.sin(angle), math.cos(angle), 0))
        rows = []
        for i in range(9):
            s = i/8
            taper = math.sin(math.pi*s)
            center = stem(t) + Vector((0, 0, (s-.5)*radius*.55))
            center += right*radius*(.055*math.sin(s*math.pi*1.4)-.025)
            width = radius*(.002+.018*taper)
            row = []
            for column in (-1, 0, 1):
                point, normal, uv, tone = sample(center+right*width*column+outward*radius*2, -outward)
                relief = radius*(.0007+.003*taper*(1-abs(column)))
                max_relief = max(max_relief, relief)
                # Match the trunk at both edges; only the narrow crease is darker.
                shade = 1-.10*taper*(1-abs(column))
                color = tuple(tone[k]*shade for k in range(3))+(tone[3],)
                row.append((point+normal*relief, uv, color, normal))
            rows.append(row)
        for i in range(8):
            for j in range(2):
                quad = [rows[i][j], rows[i][j+1], rows[i+1][j+1], rows[i+1][j]]
                points = [entry[0] for entry in quad]
                normal = (points[1]-points[0]).cross(points[2]-points[0])
                if normal.dot(outward) < 0:
                    quad.reverse()
                face(*[list(values) for values in zip(*quad)])

    result = bpy.data.meshes.new(obj.name+'_BarkScars_v4')
    result.from_pydata(vertices, [], faces)
    result.update()
    for material in source.materials:
        result.materials.append(material)
    uv = result.uv_layers.new(name=old_uv.name)
    paint = result.color_attributes.new(name='Palette', type='FLOAT_COLOR', domain='CORNER')
    custom_normals = []
    for polygon, uvs, colors, normals, setting in zip(result.polygons, uv_faces, paint_faces, normal_faces, settings):
        polygon.material_index, polygon.use_smooth = setting
        for i, tex, color, normal in zip(polygon.loop_indices, uvs, colors, normals):
            uv.data[i].uv = tex
            paint.data[i].color = color
            custom_normals.append(normal)
    result.color_attributes.active_color = paint
    result.normals_split_custom_set(custom_normals)
    result.calc_loop_triangles()
    for instance in bpy.data.objects:
        if instance.type == 'MESH' and instance.data == source:
            instance.data = result
            instance['forest_tree_revision'] = 4
            instance['bark_scar_revision'] = 4
    return {'polygons': len(result.polygons), 'triangles': len(result.loop_triangles),
            'vertices': len(result.vertices), 'bark_scar_revision': 4,
            'removed_tubular_knots': len(knots), 'scar_max_relief': round(max_relief, 6)}
