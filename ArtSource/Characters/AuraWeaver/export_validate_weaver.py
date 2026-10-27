"""Pose/deformation checks, logical mesh organization, and selected-only FBX export."""
import bpy,sys,json,math,hashlib
from pathlib import Path
from mathutils import Matrix,Vector

HERE=Path(__file__).resolve().parent;ERA=HERE.parents[2];FACTORY=ERA/'ArtSource/CharacterFactory'
sys.path[:0]=[str(HERE),str(FACTORY)]
from era_character_factory.qa import mesh_report,surface_contacts,evaluated_bounds
from era_character_factory.audit import skeleton_payload,islands
from animate_aura_weaver import set_action

rig=bpy.data.objects['Archer_Rig'];scene=bpy.context.scene
meshes=[o for o in rig.children if o.type=='MESH'];schema=json.loads((FACTORY/'rig-schema.json').read_text());meta=json.loads((HERE/'construction.json').read_text())
base={b['name']:b for b in schema['skeleton']};current={b['name']:b for b in skeleton_payload(rig)}
matrix_error=max(abs(current[n]['matrix'][i][j]-base[n]['matrix'][i][j]) for n in base for i in range(4) for j in range(4))
assert matrix_error<1e-5 and all(current[n]['parent']==base[n]['parent'] for n in base)
source_hash=hashlib.sha256((ERA/'ArtSource/Characters/Archer/Archer_Animated.blend').read_bytes()).hexdigest();assert source_hash==meta['source_archer_sha256']
rig.data.pose_position='POSE'
for track in list(rig.animation_data.nla_tracks):rig.animation_data.nla_tracks.remove(track)
actions=sorted([a for a in bpy.data.actions if a.name.startswith('AuraWeaver_')],key=lambda a:a.name)
assert len(actions)==11
qa=mesh_report(rig,meshes)
assert all(m['finite'] and not m['negative_scale'] and not m['zero_area_faces'] and not m['subdivision'] for m in qa['meshes']),str([m for m in qa['meshes'] if m['zero_area_faces']])
assert all(not m['unweighted'] and not m['non_normalized'] and m['max_influences']<=4 for m in qa['weights'])

# Test actual evaluated skins at five times per clip. Contacts below are reported,
# never blanket-welded away. Proximal cloth joins deliberately overlap.
pairs=[('AW_Coat_Front.'+s,'AW_Leg_'+s) for s in ['L','R']]+[('AW_Coat_Side.'+s,'AW_Boot_'+s) for s in ['L','R']]
samples=[];graph=bpy.context.evaluated_depsgraph_get()
for action in actions:
    set_action(rig,action);first,last=action.frame_range
    for t in [0,.25,.5,.75,1]:
        frame=first+(last-first)*t;scene.frame_set(math.floor(frame),subframe=frame%1);bpy.context.view_layer.update()
        bounds=evaluated_bounds(meshes)
        assert all(math.isfinite(x) and abs(x)<8 for x in bounds['min']+bounds['max'])
        contacts=surface_contacts(meshes,pairs,max_pairs=4)
        samples.append({'action':action.name,'frame':frame,'bounds':bounds,'cloth_contacts':contacts})

# Render pose checks in the same studio; the orthographic scene scale is fixed.
for name in ['Walk','Run','AuraChannel','JumpStart','Cast','Death']:
    a=bpy.data.actions['AuraWeaver_'+name];set_action(rig,a);first,last=a.frame_range;scene.frame_set(round(first+(last-first)*(.88 if name=='Death' else .28)))
    scene.camera=bpy.data.objects['Camera_Perspective'];scene.render.filepath=str(HERE/'Previews'/('Pose-'+name+'.png'));bpy.ops.render.render(write_still=True)
set_action(rig,bpy.data.actions['AuraWeaver_Idle']);scene.frame_set(1);rig.data.pose_position='REST'

# Joining does not weld islands. Each source part receives a non-deforming
# selection group for future editing; actual bone weights are preserved by Blender.
joined=[]
def join_group(name,predicate):
    group=[o for o in meshes if predicate(o.name)]
    if len(group)<2:return
    bpy.ops.object.select_all(action='DESELECT')
    source=[o.name for o in group]
    for o in group:
        marker=o.vertex_groups.new(name='PART_'+o.name);marker.add(list(range(len(o.data.vertices))),1,'REPLACE');o.select_set(True)
    bpy.context.view_layer.objects.active=group[0];bpy.ops.object.join();obj=group[0];obj.name=name;obj['era_source_parts']='|'.join(source)
    for o in group[1:]:meshes.remove(o)
    joined.append({'result':name,'source':source,'welded':False})

join_group('AW_Head',lambda n:n.startswith(('AW_HumanFace','AW_HumanEar','AW_Nose','AW_Eye','AW_Brow','AW_Mouth')))
join_group('AW_Hair',lambda n:n.startswith(('AW_HairScalp','AW_FrontLock','AW_SideLock')))
join_group('AW_Hat',lambda n:n.startswith(('AW_WideHat','AW_BentHat','AW_HatLeather','AW_HatBuckle')))
join_group('AW_HoodAssembly',lambda n:n.startswith('AW_Hood'))
join_group('AW_Scarf',lambda n:n.startswith('AW_Scarf'))
join_group('AW_ShoulderMantle',lambda n:n.startswith(('AW_ShoulderCape','AW_ShoulderLeather','AW_BackMantle','AW_BackCollar','AW_BackDiamond')))
join_group('AW_OuterCoat',lambda n:n.startswith(('AW_Coat_','AW_CoatEdge_')))
join_group('AW_InnerCoat',lambda n:n.startswith(('AW_InnerRobe','AW_CentralTabard')))
join_group('AW_Belt',lambda n:n.startswith(('AW_WaistBelt','AW_BeltLower','AW_BeltKeeper','AW_Buckle')))
for s in ['L','R']:
    join_group('AW_PouchAssembly_'+s,lambda n,s=s:n.endswith('_'+s) and n.startswith(('AW_Pouch_','AW_PouchFlap_','AW_PouchStrap_','AW_PouchClasp_','AW_HangingStrap_','AW_StrapTip_')))
    join_group('AW_Sleeve_'+s,lambda n,s=s:n in ['AW_BellSleeve_'+s,'AW_Cuff_'+s])
post=mesh_report(rig,meshes)
assert all(not w['unweighted'] and not w['non_normalized'] and w['max_influences']<=4 for w in post['weights'])

fbx=HERE/'AuraWeaver_Rigged.fbx'
bpy.ops.object.select_all(action='DESELECT')
for o in [rig]+meshes:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=rig;rig.data.pose_position='POSE';scene.frame_set(1)
idle=bpy.data.actions['AuraWeaver_Idle'];set_action(rig,idle)
bpy.ops.export_scene.fbx(filepath=str(fbx),use_selection=True,object_types={'MESH','ARMATURE'},
    global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,
    axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,
    add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',
    bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,
    bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
rig.data.pose_position='POSE';set_action(rig,idle);scene.frame_set(1);scene.camera=bpy.data.objects['Camera_Perspective']
rig['era_actions_ready']='Idle Walk Run JumpStart Airborne Fall Land AuraChannel Cast Hit Death'
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver.blend'),compress=True)
manifest={'character':'AuraWeaver','fps':30,'sourceHeight':meta['designed_height_m'],'baseRig':'ERA_Humanoid54_v1',
 'coreRestMaxError':matrix_error,'extraBones':sorted(set(current)-set(base)), 'bones':len(current),
 'clips':meta['actions'],'paletteSrgb':meta['palette_srgb'],'materials':[{'name':'AW_'+n,'hex':'#'+c} for n,c in meta['palette_srgb'].items()],
 'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),
 'meshes':len(meshes),'sourceBlendSha256':hashlib.sha256((HERE/'AuraWeaver.blend').read_bytes()).hexdigest()}
(HERE/'AuraWeaver_Manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
report={'ok':True,'baseSkeletonPreserved':True,'coreRestMaxError':matrix_error,'archerSourceUnchanged':source_hash==meta['source_archer_sha256'],
 'sourceHash':source_hash,'meshObjectsBefore':len(qa['meshes']),'meshObjectsAfter':len(meshes),'joined':joined,'meshHealth':post,
 'poseSamples':samples,'actions':meta['actions'],'rigKind':'Generic, not certified Unity Humanoid','animationReadiness':'55 evaluated pose samples; see contact records and rendered poses. Continuous gameplay deformation is not certified.'}
(HERE/'blender-validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('AURA_WEAVER_EXPORT_OK',json.dumps({'meshes':len(meshes),'bones':len(current),'clips':len(actions),'triangles':manifest['triangles'],'fbx':str(fbx)}))
