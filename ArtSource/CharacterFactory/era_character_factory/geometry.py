"""Small hand-directed faceted builders. Inputs define the silhouette explicitly."""
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from .components import require_copy


def mesh_object(name, vertices, faces, collection, material=None):
    if not vertices or not faces:
        raise ValueError('Geometry is empty')
    data = bpy.data.meshes.new(name)
    data.from_pydata(vertices, [], faces)
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    for p in data.polygons:
        p.use_smooth = False
    if material:
        data.materials.append(material)
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj['era_editable_copy'] = True
    return obj


def triangulate_faces(obj, indices):
    """Deterministic triangulation of chosen faces only. Never blanket-decimate a character."""
    require_copy(obj)
    if obj.data.shape_keys:
        raise ValueError('Explicitly verify shape keys before topology changes')
    bm = bmesh.new()
    try:
        bm.from_mesh(obj.data)
        bm.faces.ensure_lookup_table()
        bmesh.ops.triangulate(bm, faces=[bm.faces[i] for i in indices], quad_method='FIXED', ngon_method='EAR_CLIP')
        bm.to_mesh(obj.data)
        obj.data.update()
    finally:
        bm.free()


def faceted_loft(name, rings, collection, material=None, cap=True):
    """Rings are explicit equal-count, consistently ordered 3D sections; no subdivision."""
    count = len(rings[0])
    if count < 3 or len(rings) < 2 or any(len(r) != count for r in rings):
        raise ValueError('Need >=2 matching rings with >=3 vertices')
    vertices = [p for ring in rings for p in ring]
    faces = [(i*count+j, i*count+(j+1)%count, (i+1)*count+(j+1)%count, (i+1)*count+j)
             for i in range(len(rings)-1) for j in range(count)]
    if cap:
        faces.extend([tuple(reversed(range(count))), tuple(range((len(rings)-1)*count, len(vertices)))])
    return mesh_object(name, vertices, faces, collection, material)


def clothing_panel(name, outline, thickness, collection, material=None, normal=(0, 1, 0)):
    """Closed thin panel from a convex planar polygon. Split concave/nonplanar designs explicitly."""
    if thickness <= 0 or len(outline) < 3:
        raise ValueError('Positive thickness and >=3 points required')
    points = list(map(Vector, outline))
    n = (points[1]-points[0]).cross(points[2]-points[0]).normalized()
    if any(abs((p-points[0]).dot(n)) > 1e-5 for p in points):
        raise ValueError('Panel outline must be planar; use loft rings for folded cloth')
    if abs(Vector(normal).normalized().dot(n)) < 1e-5:
        raise ValueError('Extrusion direction must not lie in the panel plane')
    orientation = None
    for i, p in enumerate(points):
        turn = (points[(i+1)%len(points)]-p).cross(points[(i+2)%len(points)]-points[(i+1)%len(points)]).dot(n)
        if abs(turn) > 1e-9:
            if orientation is not None and orientation*turn < 0:
                raise ValueError('Concave panel: provide explicit faces instead')
            orientation = turn
    delta, count = Vector(normal).normalized()*thickness, len(points)
    vertices = points+[p+delta for p in points]
    faces = [tuple(reversed(range(count))), tuple(range(count, count*2))]
    faces += [(i, (i+1)%count, (i+1)%count+count, i+count) for i in range(count)]
    return mesh_object(name, vertices, faces, collection, material)


def surface_ribbon(name, path, width, thickness, support, collection, material=None, clearance=.0015, step=.025):
    """Front-facing straps: conform BOTH edges to the evaluated surface, in world -Y.

    This is not a generic wrap/UV solver. Model back straps with a separate path/support.
    """
    if width <= 0 or thickness <= 0 or step <= 0 or len(path) < 2:
        raise ValueError('Invalid ribbon dimensions/path')
    graph = bpy.context.evaluated_depsgraph_get()
    evaluated = support.evaluated_get(graph)
    data = evaluated.to_mesh()
    try:
        vertices = [evaluated.matrix_world @ v.co for v in data.vertices]
        tree = BVHTree.FromPolygons(vertices, [list(p.vertices) for p in data.polygons])
    finally:
        evaluated.to_mesh_clear()
    points = []
    for a, b in zip(path, path[1:]):
        a, b = Vector(a), Vector(b)
        count = max(1, int((a-b).length/step)+1)
        points.extend(a.lerp(b, i/count) for i in range(count))
    points.append(Vector(path[-1]))
    front_y = min(v.y for v in vertices)-1
    result = []
    for i, point in enumerate(points):
        tangent = points[min(i+1, len(points)-1)]-points[max(0, i-1)]
        side = Vector((-tangent.z, 0, tangent.x)).normalized()*width/2
        if side.length < 1e-8:
            raise ValueError('Path tangent is parallel to projection direction')
        for edge in [point-side, point+side]:
            hit = tree.ray_cast(Vector((edge.x, front_y, edge.z)), Vector((0, 1, 0)))[0]
            if hit is None:
                raise ValueError(f'No support under ribbon edge at {list(edge)}')
            result.append(Vector((edge.x, hit.y-clearance-thickness, edge.z)))
    count = len(result)
    vertices = result+[p+Vector((0, thickness, 0)) for p in result]
    faces = []
    for i in range(len(points)-1):
        q = (2*i, 2*i+1, 2*i+3, 2*i+2)
        faces += [q, tuple(j+count for j in reversed(q))]
    edge = [0]+list(range(1, count, 2))+list(reversed(range(2, count, 2)))
    faces += [(a, b, b+count, a+count) for a, b in zip(edge, edge[1:]+edge[:1])]
    return mesh_object(name, vertices, faces, collection, material)
