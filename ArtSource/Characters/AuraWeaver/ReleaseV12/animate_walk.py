"""V12: opposed arm swing and a coordinated walk/run cycle on the unchanged V11 rig."""
import bpy,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ROOT=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ROOT/'ArtSource/CharacterFactory')]
from animate_aura_weaver import set_action,action_curves,world_axis_rotation,solve_two
from era_character_factory.qa import mesh_report
src=SOURCE/'ReleaseV11/AuraWeaver_Lumen_V11.blend';source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));r=bpy.data.objects['Archer_Rig'];s=bpy.context.scene
def digest(a):return hashlib.sha256(repr([(f.data_path,f.array_index,[(tuple(k.co),k.interpolation) for k in f.keyframe_points]) for f in sorted(action_curves(a),key=lambda f:(f.data_path,f.array_index))]).encode()).hexdigest()
actions_before={a.name:digest(a) for a in bpy.data.actions}
rig_before=[(b.name,tuple(tuple(row) for row in b.matrix_local)) for b in r.data.bones]
objects=[o for o in r.children if o.type=='MESH' and o.name not in {'AW_RunicFocus_Independent','AW_FocusHolster'}]
feet={side:r.data.bones['Foot.'+side].matrix_local.copy() for side in ['L','R']}
rest_pelvis=r.data.bones['Pelvis'].matrix_local.copy();r.data.pose_position='POSE'
measurements={}
for name,last in [('Walk',25),('Run',19)]:
    a=bpy.data.actions['AuraWeaver_'+name];set_action(r,a)
    for f in action_curves(a):f.keyframe_points.clear()
    hand_samples={side:[] for side in ['L','R']};run=name=='Run'
    for frame in range(1,last+1):
        s.frame_set(frame);phase=2*math.pi*(frame-1)/(last-1)
        for p in r.pose.bones:p.matrix_basis=Matrix.Identity(4);p.rotation_mode='QUATERNION'
        r.pose.bones['Root'].location=r.data.bones['Root'].matrix_local.to_quaternion().inverted()@Vector((0,0,.004+.009*(1-math.cos(phase*2))))
        for bn,lean,roll,twist in [('Pelvis',.075 if run else .025,.025,.045),('Spine',.10 if run else .045,.015,-.035),('Chest',.035 if run else .020,.012,-.055),('Neck',-.055 if run else -.025,0,0),('Head',-.035 if run else -.015,0,0)]:
            p=r.pose.bones[bn];inv=p.bone.matrix_local.to_quaternion().inverted()
            p.rotation_quaternion=Quaternion(inv@Vector((1,0,0)),lean+.012*math.cos(phase*2))@Quaternion(inv@Vector((0,1,0)),roll*math.sin(phase))@Quaternion(inv@Vector((0,0,1)),twist*math.sin(phase))
        bpy.context.view_layer.update()
        goals={};drop=0
        for side,offset,sign in [('L',0,1),('R',.5,-1)]:
            t=((frame-1)/(last-1)+offset)%1;stance=.56 if run else .62;stride=.36 if run else .25
            if t<stance:y=-stride+2*stride*t/stance;lift=0
            else:
                swing=(t-stance)/(1-stance);ease=swing*swing*(3-2*swing);y=stride-2*stride*ease;lift=(.13 if run else .075)*math.sin(math.pi*swing)
            goal=feet[side].translation+Vector((0,y,lift-.006));goals[side]=goal
            if t<stance:
                upper=r.pose.bones['Thigh.'+side];lower=r.pose.bones['Shin.'+side];difference=upper.head-goal
                reach=upper.length+lower.length-.010
                vertical=math.sqrt(max(.001,reach*reach-difference.x*difference.x-difference.y*difference.y))
                drop=max(drop,difference.z-vertical)
        r.pose.bones['Root'].location-=r.data.bones['Root'].matrix_local.to_quaternion().inverted()@Vector((0,0,max(0,drop)))
        bpy.context.view_layer.update()
        for side in ['L','R']:
            upper=r.pose.bones['Thigh.'+side];lower=r.pose.bones['Shin.'+side]
            solve_two(upper,lower,goals[side],upper.head+Vector((0,-1,.05)))
            foot=r.pose.bones['Foot.'+side];foot.matrix=Matrix.LocRotScale(foot.head.copy(),feet[side].to_quaternion(),Vector((1,1,1)))
        bpy.context.view_layer.update()
        chest_delta=r.pose.bones['Chest'].matrix@r.data.bones['Chest'].matrix_local.inverted()
        for side,sign in [('L',1),('R',-1)]:
            swing=sign*math.cos(phase);amplitude=.30 if run else .23
            target=Vector((sign*(.31 if run else .30),-.055+swing*amplitude,1.04+max(0,-swing)*(.065 if run else .035)))
            pole=Vector((sign*.43,.14,1.26))
            solve_two(r.pose.bones['UpperArm.'+side],r.pose.bones['Forearm.'+side],chest_delta@target,chest_delta@pole)
            hand=r.pose.bones['Hand.'+side];direction=chest_delta.to_quaternion()@Vector((sign*.06,-.10,-1)).normalized()
            q=(hand.bone.tail_local-hand.bone.head_local).rotation_difference(direction)@hand.bone.matrix_local.to_quaternion()
            hand.matrix=Matrix.LocRotScale(hand.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update()
            hand_samples[side].append(list(hand.head))
        for p in r.pose.bones:
            if p.name.startswith('Digit_'):world_axis_rotation(p,(1,0,0),-.10 if 'Thumb' in p.name else -.16)
            if p.name.startswith('Coat'):
                side=1 if p.name.endswith('L') else -1
                world_axis_rotation(p,(1,0,0),side*(.085 if run else .045)*math.sin(phase)*(.5 if p.name.startswith('Coat2') else 1))
        bpy.context.view_layer.update()
        delta=r.pose.bones['Pelvis'].matrix@rest_pelvis.inverted()
        for p in r.pose.bones:
            if p.name.startswith('PouchAnchor.'):p.matrix=delta@p.bone.matrix_local
        bpy.context.view_layer.update()
        for p in r.pose.bones:
            for path in ['location','rotation_quaternion','scale']:p.keyframe_insert(path,frame=frame,group=p.name)
    for f in action_curves(a):
        for key in f.keyframe_points:key.interpolation='LINEAR'
    measurements[name]={side:{'forward_travel_m':max(p[1] for p in hand_samples[side])-min(p[1] for p in hand_samples[side])} for side in ['L','R']}

assert rig_before==[(b.name,tuple(tuple(row) for row in b.matrix_local)) for b in r.data.bones]
unchanged={name:actions_before[name]==digest(bpy.data.actions[name]) for name in actions_before if name not in ['AuraWeaver_Walk','AuraWeaver_Run']}
assert all(unchanged.values())
set_action(r,bpy.data.actions['AuraWeaver_Idle']);s.frame_set(1);qa=mesh_report(r,objects)
HERE.mkdir(exist_ok=True);previews=HERE/'Previews';previews.mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_Lumen_V12.blend'),compress=True)
manifest=json.loads((SOURCE/'ReleaseV11/AuraWeaver_Manifest.json').read_text());manifest['sourceBlendSha256']=hashlib.sha256((HERE/'AuraWeaver_Lumen_V12.blend').read_bytes()).hexdigest()
for clip in manifest['clips']:
    if clip['name'] in ['AuraWeaver_Walk','AuraWeaver_Run']:clip.update(start=1,end=25 if clip['name'].endswith('Walk') else 19,source='V12 coordinated stride and opposed arm swing')
(HERE/'AuraWeaver_Manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
for o in objects:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=r
bpy.ops.export_scene.fbx(filepath=str(HERE/'AuraWeaver_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
s.render.resolution_x=900;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.cycles.samples=12
cam=bpy.data.objects['Camera_Perspective'];s.camera=cam;cam.data.type='ORTHO';cam.data.ortho_scale=2.4
for name,fraction,view in [('Walk',.0,'Front'),('Walk',.25,'Front'),('Walk',.5,'Front'),('Run',.25,'Side')]:
    action=bpy.data.actions['AuraWeaver_'+name];set_action(r,action);s.frame_set(round(1+(action.frame_range[1]-1)*fraction))
    center=Vector((0,-.05,1.05));cam.location=center+Vector((3,-6,1.1) if view=='Front' else (5,0,1));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler()
    s.render.filepath=str(previews/(name+'-'+str(fraction)+'-'+view+'.png'));bpy.ops.render.render(write_still=True)
assert source_hash==hashlib.sha256(src.read_bytes()).hexdigest()
(HERE/'validation.json').write_text(json.dumps({'source_sha256':source_hash,'source_unchanged':True,'skeleton_unchanged':True,'retained_actions':unchanged,'hand_travel':measurements,'qa':qa},indent=2))
print('ERA_LUMEN_V12_EXPORTED',measurements)
