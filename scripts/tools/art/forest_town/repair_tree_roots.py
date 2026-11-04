"""Blender root-collar repair; leaves and upper branches keep their authored data.

Run repair_tree_roots(obj, radius) on the revision-2 tree produced by upgrade_trees.
The separate tapered tubes are replaced with a single welded, lobed trunk foot.
"""
import math
import random

import bpy
from mathutils import Vector


def repair_tree_roots(obj, radius):
    source = obj.data
    assert obj.get('root_collar_revision', 0) < 3, obj.name
    # Revision 2: 21 trunk strips of 24 quads, two caps, seven 50-face roots.
    sides, upper_strip = 24, 3
    assert all(len(p.vertices) == 4 for p in source.polygons[:504])
    assert len(source.polygons[504].vertices) == sides
    assert len(source.polygons[506].vertices) == 4
    keep = [p for p in source.polygons
            if p.index >= upper_strip*sides and p.index != 504
            and not 506 <= p.index < 856]
    old_uv = source.uv_layers.active
    old_paint = source.color_attributes['Palette']
    old_normals = [n.vector.copy() for n in source.corner_normals]
    vertices, faces, uv_faces, paint_faces, normal_faces, settings = [], [], [], [], [], []
    lookup = {}

    def vertex(point):
        key = tuple(round(float(v), 6) for v in point)
        if key not in lookup:
            lookup[key] = len(vertices)
            vertices.append(tuple(point))
        return lookup[key]

    def face(points, uvs, colors, normals, material=0, smooth=True):
        faces.append([vertex(p) for p in points])
        uv_faces.append(uvs)
        paint_faces.append(colors)
        normal_faces.append(normals)
        settings.append((material, smooth))

    for polygon in keep:
        loops = list(polygon.loop_indices)
        face([source.vertices[source.loops[i].vertex_index].co for i in loops],
             [old_uv.data[i].uv.copy() for i in loops],
             [tuple(old_paint.data[i].color) for i in loops],
             [old_normals[i] for i in loops], polygon.material_index, polygon.use_smooth)

    seam = [source.polygons[upper_strip*sides+j] for j in range(sides)]
    top = [source.vertices[p.vertices[0]].co.copy() for p in seam]
    center = sum(top, Vector())/sides
    phase = math.atan2(top[0].y-center.y, top[0].x-center.x)
    rng = random.Random(3100+round(radius*100))
    roots = [(phase+j*math.tau/5+rng.uniform(-.16, .16),
              rng.uniform(.80, 1.12), rng.uniform(.29, .41)) for j in range(5)]
    # The widest section and the bottom cap are below soil. Exposed ribs fade
    # gently into the trunk rather than ending as separate pointed appendages.
    profiles = [(-.22, 1.01, .60), (-.08, 1.06, .62),
                (.015, 1.06, .58), (.15, 1.035, .45),
                (.34, 1.00, .30), (.60, .97, .15), (1.0, .92, .035)]
    rings = []
    for z, base, flare in profiles:
        fraction = max(0, z*radius/center.z)
        drift = Vector((center.x*fraction, center.y*fraction, 0))
        ring = []
        for j in range(sides):
            angle = phase+j*math.tau/sides
            lobe = sum(strength*math.exp(-.5*(math.atan2(math.sin(angle-a),
                       math.cos(angle-a))/width)**2) for a, strength, width in roots)
            r = radius*(base+flare*lobe)*(1+.018*math.sin(angle*7+.4))
            ring.append(drift+Vector((r*math.cos(angle), r*math.sin(angle), z*radius)))
        rings.append(ring)
    # A middle ring avoids a long straight taper on the tall narrow variant.
    rings.append([rings[-1][j].lerp(top[j], .5) for j in range(sides)])
    rings.append(top)
    normals = []
    for i, ring in enumerate(rings):
        row = []
        for j in range(sides):
            if i == len(rings)-1:
                row.append(old_normals[seam[j].loop_indices[0]])
            else:
                around = ring[(j+1) % sides]-ring[(j-1) % sides]
                vertical = rings[min(i+1, len(rings)-1)][j]-rings[max(i-1, 0)][j]
                row.append(around.cross(vertical).normalized())
        normals.append(row)
    for i in range(len(rings)-1):
        for j in range(sides):
            k = (j+1) % sides
            polygon = seam[j]
            color = tuple(old_paint.data[polygon.loop_indices[0]].color)
            u0 = old_uv.data[polygon.loop_indices[0]].uv.x
            u1 = old_uv.data[polygon.loop_indices[1]].uv.x
            v0, v1 = .503+.244*i/len(rings), .503+.244*(i+1)/len(rings)
            face([rings[i][j], rings[i][k], rings[i+1][k], rings[i+1][j]],
                 [(u0, v0), (u1, v0), (u1, v1), (u0, v1)], [color]*4,
                 [normals[i][j], normals[i][k], normals[i+1][k], normals[i+1][j]])
    bottom_color = tuple(old_paint.data[seam[0].loop_indices[0]].color)
    face(list(reversed(rings[0])), [(.375, .625)]*sides,
         [bottom_color]*sides, [Vector((0, 0, -1))]*sides, smooth=False)

    result = bpy.data.meshes.new(obj.name+'_RootCollar_v3')
    result.from_pydata(vertices, [], faces)
    result.update()
    for material in source.materials:
        result.materials.append(material)
    uv = result.uv_layers.new(name=old_uv.name)
    paint = result.color_attributes.new(name='Palette', type='FLOAT_COLOR', domain='CORNER')
    custom_normals = []
    for p, uvs, colors, ns, setting in zip(result.polygons, uv_faces, paint_faces, normal_faces, settings):
        p.material_index, p.use_smooth = setting
        for i, tex, color, normal in zip(p.loop_indices, uvs, colors, ns):
            uv.data[i].uv = tex
            paint.data[i].color = color
            custom_normals.append(normal)
    result.color_attributes.active_color = paint
    result.normals_split_custom_set(custom_normals)
    result.calc_loop_triangles()
    assert len(result.loop_triangles) <= 8000
    # All linked layout instances receive the same corrected source mesh.
    for instance in bpy.data.objects:
        if instance.type == 'MESH' and instance.data == source:
            instance.data = result
    obj['root_collar_revision'] = 3
    obj['forest_tree_revision'] = 3
    return {'polygons': len(result.polygons), 'triangles': len(result.loop_triangles),
            'vertices': len(result.vertices), 'root_collar_revision': 3,
            'root_max_radius': round(max(math.hypot(p.x, p.y) for p in rings[2]), 4)}
