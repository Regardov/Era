"""Coordinated edits across body AND garments; no automatic animation retarget promise."""
import math
import bpy
from mathutils import Vector
from .components import require_copy


def scale_region(rig, objects, center, radius, scale, change_rest=False):
    """Ellipsoidal smooth falloff in world space, shared by every passed layer.

    mesh-only mode keeps the compatible skeleton. Editing rest geometry invalidates
    direct action reuse and requires rebaking/retargeting before animated export.
    """
    require_copy(rig)
    if rig.animation_data and rig.animation_data.action:
        raise ValueError('Clear the working rig Action and work in rest pose first')
    if rig.animation_data and any(not t.mute for t in rig.animation_data.nla_tracks):
        raise ValueError('Mute NLA before editing proportions')
    if bpy.context.object and bpy.context.object.mode != 'OBJECT':
        raise ValueError('Use Object Mode for a coordinated edit')
    if any(abs(x-1) > 1e-5 for x in rig.scale):
        raise ValueError('Use unit rig scale for local proportion edits')
    center, radius, scale = Vector(center), Vector(radius), Vector(scale)
    if min(radius) <= 0 or any(v < .75 or v > 1.25 for v in scale):
        raise ValueError('Use positive radii and one conservative .75–1.25 scale step')
    # Preflight every layer before the first mutation: a rejected reference or shape
    # key must not leave the preceding objects half-edited.
    for obj in objects:
        require_copy(obj)
        if obj.data.shape_keys:
            raise ValueError('Shape-key mesh: edit all keys deliberately; this helper refuses it')
    def mapped(point):
        d = point-center
        t = min(1, math.sqrt(sum((d[i]/radius[i])**2 for i in range(3))))
        weight = 1-t*t*(3-2*t)
        return point+Vector((d.x*(scale.x-1), d.y*(scale.y-1), d.z*(scale.z-1)))*weight
    for obj in objects:
        inv = obj.matrix_world.inverted()
        for vertex in obj.data.vertices:
            vertex.co = inv @ mapped(obj.matrix_world @ vertex.co)
        obj.data.update()
    if change_rest:
        bpy.ops.object.select_all(action='DESELECT')
        rig.select_set(True)
        bpy.context.view_layer.objects.active = rig
        inv = rig.matrix_world.inverted()
        bpy.ops.object.mode_set(mode='EDIT')
        try:
            for bone in rig.data.edit_bones:
                bone.head = inv @ mapped(rig.matrix_world @ bone.head)
                bone.tail = inv @ mapped(rig.matrix_world @ bone.tail)
        finally:
            bpy.ops.object.mode_set(mode='OBJECT')
        rig['era_requires_rebake'] = True
    rig['era_requires_deformation_review'] = True
    bpy.context.view_layer.update()


def scale_character(rig, factor):
    """Uniform object scale preserves Action translation units inside the rig.

    Keep this as an explicit visual wrapper/scale in Unity. Do not Apply Scale to
    a keyed skeleton as a substitute for translating/rebaking its animation curves.
    """
    require_copy(rig)
    if not .5 <= factor <= 2:
        raise ValueError('Unexpected character scale')
    rig.scale *= factor
