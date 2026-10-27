"""Integration smoke test: append library, independently edit sample, animate and export.

Run in a separate factory-startup Blender. Never touches ERA Assets or the archer.
"""
import sys
import json
import hashlib
import math
from pathlib import Path
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1]
sys.path.insert(0, str(HERE))
from era_character_factory.components import append_template, append_component, attach_rigid, require_copy, clone_assembly
from era_character_factory.materials import material_from_hex, replace_material, hex_from_linear
from era_character_factory.geometry import clothing_panel, faceted_loft, surface_ribbon, triangulate_faces
from era_character_factory.proportions import scale_region
from era_character_factory.rig import load_actions, bind_action, compatibility, action_bank_hash
from era_character_factory.qa import mesh_report, surface_contacts, evaluated_bounds
from era_character_factory.render import review_views
from era_character_factory.export import export_unity

schema = json.loads((HERE/'rig-schema.json').read_text(encoding='utf8'))
out = ROOT/'Artifacts/CharacterFactory'
out.mkdir(parents=True, exist_ok=True)
source = ROOT/'ArtSource/Characters/Archer/Archer_Animated.blend'
before_source = hashlib.sha256(source.read_bytes()).hexdigest()
library = HERE/'ERA_CharacterLibrary.blend'
before_library = hashlib.sha256(library.read_bytes()).hexdigest()
bpy.ops.wm.read_factory_settings(use_empty=True)
collection, rig, meshes = append_template(library)
assert rig.name == 'Archer_Rig'
rig.animation_data_clear()
for obj in [rig]+meshes:
    obj['era_editable_copy'] = True
    obj['era_reference'] = False
reference_compatibility = compatibility(rig, schema)
assert reference_compatibility['compatible']
baseline_checks = mesh_report(rig, meshes)
original_vertex_positions = {o.name: [list(v.co) for v in o.data.vertices] for o in meshes}
renders_before = review_views(rig, meshes, HERE/'Previews', 'Factory-base', views=('front','side'))

# One restrained silhouette variation affects all passed layers with the same field.
# This changes width/depth, not bone lengths/rest pose or total height.
scale_region(rig, meshes, center=(0,0,1.09), radius=(.24,.28,.23), scale=(.91,.97,1))
assert compatibility(rig, schema)['compatible']
cloth = material_from_hex('Factory_Test_Cloth', '#556270')
replace_material(meshes, 'tunic', cloth)
outline = [(x, -.082+(z-1.045)*.213, z) for x,z in [(-.07,1.045),(.07,1.045),(.08,.85),(0,.81),(-.08,.85)]]
panel = clothing_panel('Factory_Test_Panel', outline, .006, collection, cloth)
attach_rigid(panel, rig, 'Pelvis')
meshes.append(panel)
shoulder = append_component(library, 'CF_ShoulderArmor_L', rig, collection)
meshes.append(shoulder)
torso = next(o for o in meshes if o.name == 'CF_Body_ClothedTorso')
strap = surface_ribbon('Factory_Test_Strap', [(-.10,-.2,1.30),(.07,-.2,1.15)], .018,.005, torso, collection,
                       material_from_hex('Factory_Test_Leather','#604A38'))
attach_rigid(strap, rig, 'Chest')
meshes.append(strap)
# Test the section builder as a small isolated collar swatch, not a new character.
rings = [[(math.cos(i*math.tau/6)*w, math.sin(i*math.tau/6)*d, z) for i in range(6)] for w,d,z in [(.045,.04,1.37),(.05,.045,1.41)]]
swatch = faceted_loft('Factory_Test_Loft', rings, collection, cloth)
triangulate_faces(swatch,[0])
attach_rigid(swatch, rig, 'Neck')
meshes.append(swatch)
checks = mesh_report(rig, meshes)
assert all(m['finite'] and not m['negative_scale'] and not m['subdivision'] and m['zero_area_faces']==0 for m in checks['meshes'])
assert all(not m['unweighted'] and not m['non_normalized'] and m['max_influences']<=4 for m in checks['weights'])
contacts = surface_contacts(meshes, [(torso.name, strap.name), (torso.name, panel.name)])
assert contacts[0]['face_pairs'] == 0, 'Projected strap intersects the torso'
probe = torso.copy()
probe.data = torso.data.copy()
probe.name = 'QA_IntentionalIntersection'
collection.objects.link(probe)
probe.location.x += .03
positive_control = surface_contacts(meshes+[probe], [(torso.name, probe.name)])[0]
assert positive_control['requires_review'] and positive_control['face_pairs']>0
bpy.data.objects.remove(probe, do_unlink=True)
actions = load_actions(library)
bank_before = action_bank_hash(actions.values())
movements = {}
for name, action in actions.items():
    bind_action(rig, action)
    first, last = action.frame_range
    samples = []
    for t in [0,.25,.5,.75,1]:
        frame = first+(last-first)*t
        bpy.context.scene.frame_set(int(frame), subframe=frame%1)
        bpy.context.view_layer.update()
        pose = [rig.pose.bones[n].matrix.copy() for n in ['Pelvis','Spine','Thigh.L','Hand.R']]
        samples.append(pose)
        box = evaluated_bounds(meshes)
        assert all(math.isfinite(v) for v in box['min']+box['max'])
    angle = max(a.to_quaternion().rotation_difference(b.to_quaternion()).angle for a,b in zip(samples[0],samples[2]))
    delta = max((a.translation-b.translation).length for a,b in zip(samples[0],samples[2]))
    movements[name] = {'max_sample_angle_degrees': math.degrees(angle), 'max_sample_position_m': delta}
    # Loop endpoints/midpoints may be symmetric; inspect all quarter samples too.
    assert any((a.translation-b.translation).length>1e-5 or a.to_quaternion().rotation_difference(b.to_quaternion()).angle>1e-4 for row in samples[1:] for a,b in zip(samples[0],row)), name
assert bank_before == action_bank_hash(actions.values())
assert all(bank_before[n] == next(a['sha256'] for a in schema['actions'] if a['name']==n) for n in actions)

bind_action(rig, actions['Archer_Idle'])
bpy.context.scene.frame_set(1)
renders_after = review_views(rig, meshes, HERE/'Previews', 'Factory-test', views=('front','side','back','perspective'))
review_views(rig, meshes, HERE/'Previews', 'Factory-test', views=('front',), clay=True)
screens = {}
for name,t in [('Archer_Run',.25),('Archer_Cast',.2)]:
    bind_action(rig, actions[name])
    first,last=actions[name].frame_range
    frame=first+(last-first)*t
    bpy.context.scene.frame_set(int(frame),subframe=frame%1)
    screens[name]=review_views(rig, meshes, HERE/'Previews', 'Factory-test-'+name, views=('perspective',))
bind_action(rig, actions['Archer_Idle'])
bpy.context.scene.frame_set(1)
exported = export_unity(rig, meshes, out/'FactorySmoke.fbx', schema)
assert bank_before == action_bank_hash(actions.values())
assert hashlib.sha256(source.read_bytes()).hexdigest()==before_source
assert hashlib.sha256(library.read_bytes()).hexdigest()==before_library
bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=str(out/'FactorySmoke.blend'), compress=True)
# Check independent clone data on the already posed sample, then discard the test
# clone without saving it as another character.
clone_collection, clone_rig, clone_meshes=clone_assembly(rig,meshes,'QA_IndependentClone',copy_materials=True)
assert clone_rig.data!=rig.data and clone_rig.animation_data is None
assert all(a.data!=b.data for a,b in zip(meshes,clone_meshes))
assert all(all(mod.object==clone_rig for mod in obj.modifiers if mod.type=='ARMATURE') for obj in clone_meshes)
saved_vertex=meshes[0].data.vertices[0].co.copy()
clone_meshes[0].data.vertices[0].co.x+=.01
assert (meshes[0].data.vertices[0].co-saved_vertex).length==0
for obj in clone_meshes+[clone_rig]:bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.collections.remove(clone_collection)
# Fail-closed checks after saving/exporting the successful compatible sample.
rig.animation_data_clear()
scale_region(rig, meshes, center=(.18,0,1.20), radius=(.3,.3,.3), scale=(1.02,1,1), change_rest=True)
assert not compatibility(rig,schema)['compatible']
rejected_rest = False
try:
    bind_action(rig,actions['Archer_Run'])
except ValueError:
    rejected_rest=True
assert rejected_rest
with bpy.data.libraries.load(str(library),link=False) as (src,dst):
    dst.objects=['REF_Archer_Rig']
rejected_reference=False
try:
    require_copy(dst.objects[0])
except ValueError:
    rejected_reference=True
assert rejected_reference
report = {'ok':True, 'blender':bpy.app.version_string, 'source_sha256_before':before_source,
          'source_sha256_after':hashlib.sha256(source.read_bytes()).hexdigest(), 'library_unchanged':True,
          'compatible_after_width_edit':reference_compatibility, 'actions_unchanged':True,
          'changed_rest_rejects_action_reuse':rejected_rest, 'reference_edit_guard':rejected_reference,
          'independent_clone_data':True,
          'intersection_positive_control':positive_control,
          'actions_sampled':movements, 'base_checks':baseline_checks, 'test_checks':checks, 'surface_contacts':contacts,
          'test_changes':['Coordinated waist width/depth edit','Independent cloth/leather material copies','Thin panel on Pelvis','Appended existing shoulder plate','Surface-fitted Chest strap','Faceted collar swatch'],
          'export':str(Path(exported).relative_to(ROOT)), 'renders_before': [Path(p).name for p in renders_before],
          'renders_after':[Path(p).name for p in renders_after],
          'limits':['Clothed base is not a complete naked body','Contact test is sampled surface intersection only','Blender sample checks do not prove Unity runtime deformation quality']}
(HERE/'Measurements/factory-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print('FACTORY_SMOKE_OK',len(meshes),'meshes',len(actions),'unchanged Actions',exported)
