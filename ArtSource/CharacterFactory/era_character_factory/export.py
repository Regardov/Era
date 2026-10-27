"""Selected-only Generic FBX using the verified archer settings. Never writes Assets/."""
from pathlib import Path
import bpy
from .rig import compatibility
from .components import require_copy
from .qa import mesh_report


def export_unity(rig, meshes, filepath, schema, animations=True):
    filepath = Path(filepath).resolve()
    require_copy(rig)
    for obj in meshes:
        require_copy(obj)
        if obj.type != 'MESH' or not any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers):
            raise ValueError('Every exported mesh must be bound to the working rig')
    if filepath.suffix.lower() != '.fbx':
        raise ValueError('Export destination must be an FBX, never a source .blend')
    # Integration is a separate task. Factory smoke outputs belong outside ERA Assets.
    project = Path(__file__).resolve().parents[3]
    if filepath.is_relative_to(project/'Assets'):
        raise ValueError('Factory export must not modify the live Unity Assets directory')
    if rig.name != 'Archer_Rig':
        raise ValueError('Export a standalone copy with rig object named Archer_Rig for v1 paths')
    if animations and not compatibility(rig, schema)['compatible']:
        raise ValueError('Skeleton/rest changed: direct animation reuse is not verified')
    if any(abs(v-1) > 1e-5 for v in rig.scale):
        raise ValueError('Export at the source 1.8m scale; apply visual scale in Unity explicitly')
    if animations and rig.animation_data and any(not t.mute for t in rig.animation_data.nla_tracks):
        raise ValueError('Mute NLA first; do not export Actions and NLA twice')
    if animations and any(not a.name.startswith('Archer_') for a in bpy.data.actions):
        raise ValueError('Export in an isolated file containing only the chosen compatible Action bank')
    checks = mesh_report(rig,meshes)
    if any(not m['finite'] or m['negative_scale'] or m['zero_area_faces'] or m['subdivision'] for m in checks['meshes']):
        raise ValueError('Geometry failed export preflight')
    if any(m['unweighted'] or m['non_normalized'] or m['max_influences']>4 for m in checks['weights']):
        raise ValueError('Skin weights failed export preflight')
    filepath.parent.mkdir(parents=True, exist_ok=True)
    old_selection, old_active = list(bpy.context.selected_objects), bpy.context.view_layer.objects.active
    hidden = {o: (o.hide_get(), o.hide_render) for o in [rig]+list(meshes)}
    try:
        bpy.ops.object.select_all(action='DESELECT')
        for obj in hidden:
            obj.hide_set(False)
            obj.hide_render = False
            obj.select_set(True)
        bpy.context.view_layer.objects.active = rig
        bpy.ops.export_scene.fbx(filepath=str(filepath), use_selection=True, object_types={'MESH','ARMATURE'},
            global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
            use_space_transform=True, bake_space_transform=False, axis_forward='-Z', axis_up='Y',
            use_mesh_modifiers=True, mesh_smooth_type='FACE', use_tspace=False, use_custom_props=False,
            add_leaf_bones=False, primary_bone_axis='Y', secondary_bone_axis='X',
            use_armature_deform_only=False, armature_nodetype='NULL', bake_anim=animations,
            bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=animations,
            bake_anim_force_startend_keying=True, bake_anim_step=1, bake_anim_simplify_factor=0,
            path_mode='AUTO', embed_textures=False)
    finally:
        bpy.ops.object.select_all(action='DESELECT')
        for obj, (hide, render) in hidden.items():
            obj.hide_set(hide)
            obj.hide_render = render
        for obj in old_selection:
            obj.select_set(True)
        bpy.context.view_layer.objects.active = old_active
    return str(filepath)
