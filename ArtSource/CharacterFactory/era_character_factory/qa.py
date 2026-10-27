"""Non-destructive mesh/skin checks and explicit BVH surface-contact samples."""
import math
import bmesh
import bpy
from mathutils.bvhtree import BVHTree
from .audit import islands
from .rig import weight_report


def mesh_report(rig, objects):
    report = []
    for obj in objects:
        data = obj.data
        bm = bmesh.new()
        bm.from_mesh(data)
        item = {'object': obj.name, 'islands': len(islands(data)),
                'boundary_edges': sum(e.is_boundary for e in bm.edges),
                'nonmanifold_nonboundary_edges': sum(not e.is_manifold and not e.is_boundary for e in bm.edges),
                'zero_area_faces': sum(f.calc_area() < 1e-12 for f in bm.faces),
                'finite': all(all(math.isfinite(x) for x in v.co) for v in data.vertices),
                'negative_scale': obj.matrix_world.determinant() < 0,
                'subdivision': any(m.type == 'SUBSURF' for m in obj.modifiers),
                'smooth_faces': sum(p.use_smooth for p in data.polygons)}
        bm.free()
        report.append(item)
    return {'meshes': report, 'weights': weight_report(rig, objects)}


def surface_contacts(objects, pairs, allowed=None, max_pairs=100):
    """Triangle intersections in evaluated world space. No volume/depth or no-clipping guarantee.

    Pairs are explicit names. Intentional layer contacts must be reviewed and may be
    allowlisted with a reason. Full containment, grazing and future motion can escape it.
    """
    graph = bpy.context.evaluated_depsgraph_get()
    by_name = {o.name: o for o in objects}
    cache, result = {}, []
    for a, b in pairs:
        for name in [a, b]:
            if name in cache:
                continue
            obj = by_name[name].evaluated_get(graph)
            mesh = obj.to_mesh()
            try:
                cache[name] = BVHTree.FromPolygons([obj.matrix_world @ v.co for v in mesh.vertices], [list(p.vertices) for p in mesh.polygons])
            finally:
                obj.to_mesh_clear()
        contacts = cache[a].overlap(cache[b])
        reason = (allowed or {}).get((a, b), (allowed or {}).get((b, a)))
        result.append({'a': a, 'b': b, 'face_pairs': len(contacts), 'sample_pairs': contacts[:max_pairs],
                       'allow_reason': reason, 'requires_review': bool(contacts) and not reason})
    return result


def evaluated_bounds(objects):
    from .audit import bounds
    graph, points = bpy.context.evaluated_depsgraph_get(), []
    for source in objects:
        obj = source.evaluated_get(graph)
        data = obj.to_mesh()
        try:
            points.extend(obj.matrix_world @ v.co for v in data.vertices)
        finally:
            obj.to_mesh_clear()
    return bounds(points)
