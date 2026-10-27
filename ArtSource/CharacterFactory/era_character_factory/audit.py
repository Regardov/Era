"""Read-only, explicit raw/rest measurements and geometry/animation provenance."""
import hashlib
import json
from collections import Counter
import bpy
from mathutils import Vector


def digest(value):
    return hashlib.sha256(json.dumps(value, sort_keys=True, separators=(",", ":")).encode()).hexdigest()


def bounds(points):
    points = list(points)
    if not points:
        return None
    low = [min(p[i] for p in points) for i in range(3)]
    high = [max(p[i] for p in points) for i in range(3)]
    return {"min": low, "max": high, "size": [b-a for a, b in zip(low, high)]}


def islands(mesh):
    adjacency = [[] for _ in mesh.vertices]
    for edge in mesh.edges:
        a, b = edge.vertices
        adjacency[a].append(b)
        adjacency[b].append(a)
    seen, result = set(), []
    for vertex in mesh.vertices:
        if vertex.index in seen:
            continue
        todo, group = [vertex.index], []
        seen.add(vertex.index)
        while todo:
            current = todo.pop()
            group.append(current)
            for other in adjacency[current]:
                if other not in seen:
                    seen.add(other)
                    todo.append(other)
        result.append(group)
    return result


def action_payload(action):
    # Blender 5 slotted/layered Actions, not the removed legacy action.fcurves API.
    return [(curve.data_path, curve.array_index,
             [(list(k.co), k.interpolation, list(k.handle_left), list(k.handle_right)) for k in curve.keyframe_points])
            for layer in action.layers for strip in layer.strips
            for bag in strip.channelbags for curve in bag.fcurves]


def mesh_payload(obj):
    return {"vertices": [list(v.co) for v in obj.data.vertices],
            "faces": [list(p.vertices) for p in obj.data.polygons],
            "materials": [m.name if m else None for m in obj.data.materials],
            "face_materials": [p.material_index for p in obj.data.polygons],
            "weights": [[(obj.vertex_groups[g.group].name, g.weight) for g in v.groups] for v in obj.data.vertices]}


def skeleton_payload(rig):
    return [{"name": b.name, "parent": b.parent.name if b.parent else None,
             "head": list(b.head_local), "tail": list(b.tail_local),
             "matrix": [list(row) for row in b.matrix_local], "deform": b.use_deform}
            for b in rig.data.bones]


def scene_report(rig, objects):
    parts, meshes, all_points = {}, [], []
    for obj in objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        mesh.calc_loop_triangles()
        points = [obj.matrix_world @ v.co for v in mesh.vertices]
        all_points.extend(points)
        components = islands(mesh)
        item = {"name": obj.name, "vertices": len(mesh.vertices), "faces": len(mesh.polygons),
                "triangles": len(mesh.loop_triangles), "islands": len(components),
                "bounds_rest_world_m": bounds(points), "geometry_sha256": digest(mesh_payload(obj)),
                "parent": obj.parent.name if obj.parent else None,
                "transform": [list(row) for row in obj.matrix_world],
                "modifiers": [(m.name, m.type) for m in obj.modifiers],
                "smooth_faces": sum(p.use_smooth for p in mesh.polygons),
                "face_sizes": dict(Counter(len(p.vertices) for p in mesh.polygons)),
                "materials_faces": dict(Counter(mesh.materials[p.material_index].name for p in mesh.polygons)),
                "shape_keys": list(mesh.shape_keys.key_blocks.keys()) if mesh.shape_keys else []}
        meshes.append(item)
        for group in obj.vertex_groups:
            if not group.name.startswith("PART_"):
                continue
            ids = {v.index for v in mesh.vertices if any(g.group == group.index and g.weight > 0 for g in v.groups)}
            faces = [p for p in mesh.polygons if all(i in ids for i in p.vertices)]
            parts[group.name[5:]] = {"object": obj.name, "vertices": len(ids), "faces": len(faces),
                                     "bounds_rest_world_m": bounds(points[i] for i in ids),
                                     "face_materials": dict(Counter(mesh.materials[p.material_index].name for p in faces))}
    materials = {}
    for obj in objects:
        if obj.type != "MESH":
            continue
        for material in obj.data.materials:
            if material is None or material.name in materials:
                continue
            node = next((n for n in material.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None) if material.use_nodes else None
            materials[material.name] = {"display_rgba_linear": list(material.diffuse_color),
                "base_rgba_linear": list(node.inputs['Base Color'].default_value) if node else list(material.diffuse_color),
                "roughness": float(node.inputs['Roughness'].default_value) if node else material.roughness,
                "metallic": float(node.inputs['Metallic'].default_value) if node else material.metallic}
    def section(part, z):
        obj = next(o for o in objects if o.name == parts[part]['object'])
        group = obj.vertex_groups['PART_'+part].index
        chosen = {v.index for v in obj.data.vertices if any(g.group == group for g in v.groups)}
        points = []
        for edge in obj.data.edges:
            if not all(i in chosen for i in edge.vertices):
                continue
            a, b = [obj.matrix_world @ obj.data.vertices[i].co for i in edge.vertices]
            if abs(a.z-z) < 1e-7:
                points.append(a)
            if (a.z-z)*(b.z-z) < 0:
                points.append(a.lerp(b, (z-a.z)/(b.z-a.z)))
        return {'plane_z_m': z, 'bounds': bounds(points), 'method': 'Exact edge intersections of the raw part with a horizontal plane'}
    sections = {n: section('Tunic bodice', z) for n, z in [('chest_tunic',1.28),('waist_tunic',1.09),('hip_tunic',1.0)]} if 'Tunic bodice' in parts else {}
    mask = None
    if 'Head and connected ears' in parts:
        obj = next(o for o in objects if o.name == parts['Head and connected ears']['object'])
        group = obj.vertex_groups['PART_Head and connected ears'].index
        mask = {'definition': 'Raw head PART vertices with abs(world X)<=0.11m; geometric crop, not an anatomical skull measurement',
                'bounds': bounds(obj.matrix_world@v.co for v in obj.data.vertices if any(g.group==group for g in v.groups) and abs((obj.matrix_world@v.co).x)<=.11)}
    return {"blender": bpy.app.version_string, "units": {"system": bpy.context.scene.unit_settings.system,
            "scale_length": bpy.context.scene.unit_settings.scale_length},
            "measurement_space": "Raw mesh vertices transformed by matrix_world, before armature deformation. Blender XYZ; forward -Y.",
            "bounds_rest_world_m": bounds(all_points), "meshes": meshes, "parts": parts,
            "materials": materials, "skeleton": skeleton_payload(rig),
            "skeleton_sha256": digest(skeleton_payload(rig)),
            "actions": [{"name": a.name, "frame_range": list(a.frame_range), "sha256": digest(action_payload(a)),
                         "slots": len(a.slots), "curves": len(action_payload(a))} for a in bpy.data.actions],
            "sections": sections, "head_central_crop": mask,
            "bone_lengths_m": {b.name: (b.tail_local-b.head_local).length for b in rig.data.bones},
            "constraints": sum(len(b.constraints) for b in rig.pose.bones),
            "fps": bpy.context.scene.render.fps, "nla": len(rig.animation_data.nla_tracks) if rig.animation_data else 0}
