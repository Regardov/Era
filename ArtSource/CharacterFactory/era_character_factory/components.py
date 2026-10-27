"""Append, extract and duplicate assets without welding or editing the reference."""
import bpy
from mathutils import Matrix


def require_copy(obj):
    if not obj.get('era_editable_copy', False) or obj.get('era_reference', False):
        raise ValueError(f'{obj.name}: edit an explicit working copy, not the library/reference')


def mark_asset(data, description, category):
    data.asset_mark()
    data.asset_data.description = description
    for tag in ['ERA', 'low-poly', category, 'ERA_Humanoid54_v1']:
        data.asset_data.tags.new(tag)


def extract_parts(source, part_names, name, collection, rig):
    """Keep source indices/materials/weights. Refuse faces cut by a PART selection."""
    if source.data.shape_keys:
        raise ValueError('Extracting a shape-key mesh requires an explicit all-keys transfer')
    groups = {source.vertex_groups['PART_'+p].index for p in part_names}
    ids = [v.index for v in source.data.vertices if any(g.group in groups and g.weight > 0 for g in v.groups)]
    chosen = set(ids)
    if any(any(i in chosen for i in p.vertices) and not all(i in chosen for i in p.vertices) for p in source.data.polygons):
        raise ValueError(f'{name}: selected PART groups cut a face; include its connected neighbours')
    polygons = [p for p in source.data.polygons if all(i in chosen for i in p.vertices)]
    if not polygons:
        raise ValueError(f'{name}: no faces')
    index = {old: new for new, old in enumerate(ids)}
    data = bpy.data.meshes.new(name)
    data.from_pydata([source.data.vertices[i].co for i in ids], [], [[index[i] for i in p.vertices] for p in polygons])
    data.update()
    for material in source.data.materials:
        data.materials.append(material)
    for p, original in zip(data.polygons, polygons):
        p.material_index, p.use_smooth = original.material_index, original.use_smooth
    loop_map = [i for p in polygons for i in p.loop_indices]
    for layer in source.data.uv_layers:
        target = data.uv_layers.new(name=layer.name)
        for new, old in enumerate(loop_map):
            target.data[new].uv = layer.data[old].uv
    for layer in source.data.color_attributes:
        if layer.domain not in {'POINT','CORNER'}:
            raise ValueError('Unsupported color domain')
        target = data.color_attributes.new(name=layer.name, type=layer.data_type, domain=layer.domain)
        for new, old in enumerate(ids if layer.domain=='POINT' else loop_map):
            target.data[new].color = layer.data[old].color
    obj = bpy.data.objects.new(name, data)
    collection.objects.link(obj)
    obj.parent, obj.matrix_parent_inverse, obj.matrix_world = rig, source.matrix_parent_inverse.copy(), source.matrix_world.copy()
    for group in source.vertex_groups:
        target = obj.vertex_groups.new(name=group.name)
        for old in ids:
            for entry in source.data.vertices[old].groups:
                if entry.group == group.index:
                    target.add([index[old]], entry.weight, 'REPLACE')
    modifier = obj.modifiers.new('ERA Armature', 'ARMATURE')
    modifier.object = rig
    source_modifier = next((m for m in source.modifiers if m.type == 'ARMATURE'), None)
    if source_modifier:
        modifier.use_deform_preserve_volume = source_modifier.use_deform_preserve_volume
        modifier.use_vertex_groups = source_modifier.use_vertex_groups
        modifier.use_bone_envelopes = source_modifier.use_bone_envelopes
    obj['era_source_object'] = source.get('era_source_object', source.name)
    obj['era_source_parts'] = '|'.join(part_names)
    obj['era_editable_copy'] = True
    return obj


def clone_assembly(rig, meshes, name, parent=None, copy_materials=False):
    """Independent meshes/rest skeleton. Actions stay shared until explicitly duplicated."""
    collection = bpy.data.collections.new(name)
    (parent or bpy.context.scene.collection).children.link(collection)
    result = rig.copy()
    result.data = rig.data.copy()
    result.name = name+'_Rig'
    collection.objects.link(result)
    result.parent = None
    result.matrix_world = rig.matrix_world.copy()
    result['era_reference'] = False
    result['era_editable_copy'] = True
    result.animation_data_clear()
    for bone in result.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    result.data.pose_position = 'POSE'
    material_copies, copies = {}, []
    for source in meshes:
        obj = source.copy()
        obj.data = source.data.copy()
        obj.name = name+'_'+source.name
        collection.objects.link(obj)
        obj.parent = result if source.parent == rig else source.parent
        obj.matrix_parent_inverse = source.matrix_parent_inverse.copy()
        obj.matrix_world = source.matrix_world.copy()
        for modifier in obj.modifiers:
            if modifier.type == 'ARMATURE' and modifier.object == rig:
                modifier.object = result
        if copy_materials:
            for slot in obj.material_slots:
                if slot.material:
                    original = slot.material
                    if original not in material_copies:
                        material_copies[original] = original.copy()
                    slot.material = material_copies[original]
        obj['era_reference'], obj['era_editable_copy'] = False, True
        obj.hide_render = False
        obj.hide_set(False)
        copies.append(obj)
    return collection, result, copies


def append_template(filepath):
    """Append one complete compatible assembly; dependencies keep weights and materials."""
    with bpy.data.libraries.load(str(filepath), link=False, assets_only=False) as (source, target):
        if 'ERA_BASE_COMPATIBLE' not in source.collections:
            raise ValueError('ERA_BASE_COMPATIBLE missing')
        target.collections = ['ERA_BASE_COMPATIBLE']
    collection = target.collections[0]
    bpy.context.scene.collection.children.link(collection)
    rig = next(o for o in collection.all_objects if o.type == 'ARMATURE')
    return collection, rig, [o for o in collection.all_objects if o.type == 'MESH']


def append_component(filepath, asset_name, rig, target):
    """Append only a selected component, duplicate its data, bind to the working rig."""
    require_copy(rig)
    before_objects, before_materials = set(bpy.data.objects), list(bpy.data.materials)
    with bpy.data.libraries.load(str(filepath), link=False) as (source, data):
        if asset_name not in source.objects:
            raise ValueError(asset_name)
        data.objects = [asset_name]
    obj = data.objects[0]
    if obj.type != 'MESH':
        raise ValueError('Expected a mesh component')
    target.objects.link(obj)
    obj.data = obj.data.copy()
    obj.parent = rig
    for modifier in obj.modifiers:
        if modifier.type == 'ARMATURE':
            modifier.object = rig
    obj['era_editable_copy'], obj['era_reference'] = True, False
    obj.hide_render = False
    obj.hide_set(False)
    from .materials import reuse_palette_slots
    reuse_palette_slots(obj, before_materials)
    # The append may have pulled an unlinked dependency rig. Once rebound, discard
    # only newly created unlinked armatures; never purge any pre-existing scene data.
    for dependency in set(bpy.data.objects)-before_objects-{obj}:
        if dependency.type == 'ARMATURE' and not dependency.users_collection:
            bpy.data.objects.remove(dependency, do_unlink=True)
    return obj


def attach_rigid(obj, rig, bone_name):
    """Rigid accessory with a one-bone Armature weight; no loose parenting offset."""
    require_copy(rig)
    require_copy(obj)
    if bone_name not in rig.data.bones:
        raise ValueError(bone_name)
    for group in list(obj.vertex_groups):
        if group.name in rig.data.bones:
            obj.vertex_groups.remove(group)
    group = obj.vertex_groups.new(name=bone_name)
    group.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    world = obj.matrix_world.copy()
    obj.parent = rig
    obj.matrix_world = world
    modifier = next((m for m in obj.modifiers if m.type == 'ARMATURE'), None) or obj.modifiers.new('ERA Armature', 'ARMATURE')
    modifier.object = rig
