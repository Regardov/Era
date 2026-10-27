"""Derive component assets from the approved archer. No synthesis of hidden anatomy."""
import sys
import json
import hashlib
from collections import Counter
from pathlib import Path
import bpy

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
from era_character_factory.audit import scene_report, skeleton_payload, action_payload, digest
from era_character_factory.components import extract_parts, clone_assembly, mark_asset
from era_character_factory.rig import CORE, SCHEMA
from era_character_factory.render import review_views
from era_character_factory.materials import compact_slots, hex_from_linear

source = ROOT/'ArtSource/Characters/Archer/Archer_Animated.blend'
source_hash = hashlib.sha256(source.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(source), use_scripts=False)
scene = bpy.context.scene
rig = bpy.data.objects['Archer_Rig']
objects = [o for o in scene.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' and m.object == rig for m in o.modifiers)]
reference_actions = {a.name: digest(action_payload(a)) for a in bpy.data.actions}
report = scene_report(rig, objects)
(HERE/'Measurements').mkdir(exist_ok=True)
(HERE/'Measurements/archer-reference.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf8')
original = {o.name: o for o in objects}
scene.name = 'ERA Character Library'
scene.unit_settings.system = 'METRIC'
scene.unit_settings.scale_length = 1
root = bpy.data.collections.new('ERA_CHARACTER_FACTORY')
scene.collection.children.link(root)
reference = bpy.data.collections.new('ERA_REFERENCE_ARCHER')
root.children.link(reference)
for obj in [rig]+objects:
    original_name = obj.name
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    reference.objects.link(obj)
    obj['era_source_object'] = original_name
    obj.name = 'REF_'+original_name
    obj['era_reference'], obj['era_editable_copy'] = True, False
for obj in list(scene.objects):
    if obj not in [rig]+objects:
        bpy.data.objects.remove(obj, do_unlink=True)

base = bpy.data.collections.new('ERA_BASE_COMPATIBLE')
root.children.link(base)
base['era_body_coverage'] = 'Clothed construction base: no unseen anatomical torso/limbs were invented.'
base_rig = rig.copy()
base_rig.data = rig.data.copy()
base.objects.link(base_rig)
base_rig.name = 'Archer_Rig'
base_rig.data.name = SCHEMA
base_rig.animation_data_clear()
base_rig['era_reference'], base_rig['era_editable_copy'] = False, True
base_rig['era_schema'] = SCHEMA
base_rig.show_in_front = True
for bone in base_rig.pose.bones:
    from mathutils import Matrix
    bone.matrix_basis = Matrix.Identity(4)
base_rig.data.pose_position = 'POSE'
components = bpy.data.collections.new('ERA_OPTIONAL_COMPONENTS')
root.children.link(components)
components.hide_render = True
optional_archer = bpy.data.collections.new('ERA_ARCHER_ONLY_COMPONENTS')
root.children.link(optional_archer)
optional_archer.hide_render = True
inventory = []

def part(host, names, name, group, role, archer_only=False):
    obj = extract_parts(original[host], names, 'CF_'+name, group, base_rig)
    compact_slots(obj)
    obj['era_role'] = role
    obj['era_archer_specific'] = archer_only
    mark_asset(obj, f'{role}. Source PARTs: {", ".join(names)}. Rebind with append_component().', role)
    inventory.append({'asset': obj.name, 'host': host, 'parts': names, 'role': role, 'archer_specific': archer_only,
                      'vertices': len(obj.data.vertices), 'faces': len(obj.data.polygons)})
    return obj

part('Body_Head', ['Neck hidden under collar'], 'Body_Neck', base, 'base_body')
head_parts = [g.name[5:] for g in original['Body_Head'].vertex_groups if g.name.startswith('PART_') and g.name != 'PART_Neck hidden under collar']
part('Body_Head', head_parts, 'Head_Elf', base, 'head', True)
part('Clothing_Tunic_Coat', ['Tunic bodice'], 'Body_ClothedTorso', base, 'clothed_body_proxy')
for side in ['L','R']:
    part('Arm_'+side, [p+' '+side for p in ['Linen sleeve','Green sleeve lower tier','Green sleeve','Linen lower sleeve']], 'Arms_Sleeved_'+side, base, 'clothed_arms')
    part('Arm_'+side, [p+' '+side for p in ['Glove','Glove folded fingertips','Thumb','Glove knuckle shell']], 'Gloves_'+side, base, 'gloves')
    part('Arm_'+side, ['Bracer '+side,'Bracer band '+side+'0.31','Bracer band '+side+'0.7'], 'Bracers_'+side, base, 'bracers')
    part('Leg_'+side, ['Trouser leg '+side], 'Legs_Trousers_'+side, base, 'clothed_legs')
    part('Leg_'+side, ['Boot shaft '+side,'Boot foot '+side,'Ankle band '+side], 'Boots_'+side, base, 'boots')
    part('Leg_'+side, ['Knee lower fold '+side,'Knee cuff '+side,'Knee front overlap '+side], 'KneeArmor_'+side, components, 'knee_armor')
    part('Shoulder_Armor_'+side, ['Shoulder plate '+side,'Shoulder articulated layer '+side], 'ShoulderArmor_'+side, components, 'shoulder_armor')
part('Clothing_Tunic_Coat', ['Chest layered yoke'], 'ChestYoke', components, 'clothing')
panels = [g.name[5:] for g in original['Clothing_Tunic_Coat'].vertex_groups if g.name.startswith('PART_') and g.name[5:] not in ['Tunic bodice','Chest layered yoke']]
part('Clothing_Tunic_Coat', panels, 'LayeredPanels', components, 'clothing')
for host, name, role, group, unique in [('Hair','Hair_Elf','hair',optional_archer,True),('Hood','Hood_Archer','hood',optional_archer,True),('Scarf','Scarf','scarf',components,False),('Cloak','Cloak_Pointed','cloak',optional_archer,True),('Belt_Gear','BeltAndPouches','belts',components,False),('Bow_Wood','Bow_Wood','weapon',optional_archer,True),('Bow_String','Bow_String','weapon',optional_archer,True),('Quiver_Case','Quiver','quiver',optional_archer,True)]:
    part(host, [g.name[5:] for g in original[host].vertex_groups if g.name.startswith('PART_')], name, group, role, unique)
part('Harness', ['Main chest strap','Secondary chest strap','Harness clasp'], 'Harness', components, 'straps')
part('Harness', ['Quiver shoulder support'], 'QuiverSupport', optional_archer, 'quiver_attachment', True)
for i in range(5):
    part(f'Arrow_{i+1:02}', [f'Arrow shaft {i}',f'Arrow feather {i}'], f'Arrow_{i+1:02}', optional_archer, 'arrow', True)
nocked = original['NockedArrow'].copy()
nocked.data = original['NockedArrow'].data.copy()
nocked.name = 'CF_NockedArrow'
optional_archer.objects.link(nocked)
nocked.parent = base_rig
for modifier in nocked.modifiers:
    if modifier.type == 'ARMATURE':
        modifier.object = base_rig
nocked['era_reference'], nocked['era_editable_copy'], nocked['era_archer_specific'] = False, True, True
mark_asset(nocked, 'Complete nocked arrow; optional archer equipment.', 'arrow')
inventory.append({'asset': nocked.name, 'host': 'NockedArrow', 'parts': [], 'role': 'arrow', 'archer_specific': True,
                  'vertices': len(nocked.data.vertices), 'faces': len(nocked.data.polygons)})

def face_signature(obj):
    return Counter((tuple(tuple(round(c,7) for c in obj.matrix_world@obj.data.vertices[i].co) for i in p.vertices),
                    obj.data.materials[p.material_index].name) for p in obj.data.polygons)
source_faces, component_faces = Counter(), Counter()
for obj in objects:
    source_faces.update(face_signature(obj))
for obj in list(base.objects)+list(components.objects)+list(optional_archer.objects):
    if obj.type == 'MESH':
        component_faces.update(face_signature(obj))
assert source_faces == component_faces, 'Component extraction lost, duplicated or recolored source faces'
for material in bpy.data.materials:
    if material.name in report['materials']:
        material['era_palette_key'] = material.name
        mark_asset(material, 'Local base color '+hex_from_linear(report['materials'][material.name]['base_rgba_linear'])+' (sRGB); do not infer from lighting.', 'palette')

for action in bpy.data.actions:
    action.use_fake_user = True
    action['era_source_sha256'] = reference_actions[action.name]
    action['era_shared_bank_read_only'] = True
    mark_asset(action, 'Existing archer action, unchanged. Copy before editing; Generic v1 compatibility only.', 'animation')
mark_asset(base, 'Compatible clothed construction sample. Elf head is replaceable, not a universal human base.', 'template')
mark_asset(base_rig, '54-bone v1 Generic-compatible export rig; optional equipment chains retained for exact paths.', 'rig')

for obj in [rig]+objects:
    obj.hide_render = False
    obj.hide_set(False)
scene.frame_set(1)
review_views(rig, objects, HERE/'Previews', 'Archer-reference')
for obj in [rig]+objects:
    obj.hide_render = True
    obj.hide_set(True)
for obj in list(components.objects)+list(optional_archer.objects):
    obj.hide_set(True)
reference.hide_render = True
schema = {'schema': SCHEMA, 'source_sha256': source_hash, 'rig_object_name': 'Archer_Rig', 'source_height_m': 1.8,
          'forward': '-Y', 'up': 'Z', 'units_m': 1, 'skeleton': skeleton_payload(base_rig),
          'core_bones': sorted(CORE), 'optional_extension_bones': sorted(set(base_rig.data.bones.keys())-CORE),
          'actions': [{'name': a.name, 'sha256': reference_actions[a.name], 'frames': list(a.frame_range)} for a in bpy.data.actions]}
(HERE/'rig-schema.json').write_text(json.dumps(schema, indent=2), encoding='utf8')
(HERE/'component-index.json').write_text(json.dumps({'source_sha256': source_hash, 'base_coverage': base['era_body_coverage'],
    'partition_preserves_all_source_faces_and_materials': True, 'components': inventory}, ensure_ascii=False, indent=2), encoding='utf8')
assert reference_actions == {a.name: digest(action_payload(a)) for a in bpy.data.actions}
assert hashlib.sha256(source.read_bytes()).hexdigest() == source_hash
bpy.ops.object.select_all(action='DESELECT')
base_rig.select_set(True)
bpy.context.view_layer.objects.active = base_rig
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type == 'VIEW_3D':
            area.spaces.active.shading.color_type = 'MATERIAL'
            area.spaces.active.region_3d.view_distance = 3.2
            area.spaces.active.region_3d.view_location = (0,0,.9)
for collection in list(bpy.data.collections):
    if collection != root and not collection.all_objects:
        bpy.data.collections.remove(collection)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'ERA_CharacterLibrary.blend'), compress=True)
print('FACTORY_LIBRARY', len(inventory), 'components', len(base.objects), 'base objects', source_hash)
