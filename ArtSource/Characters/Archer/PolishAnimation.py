"""Edit existing Actions only. Blender CLI: --python this.py -- --stage idle.
Input must be an explicitly saved current archer .blend. No mesh/rest edits.
"""
import bpy,math,json,sys,argparse
from pathlib import Path
from mathutils import Vector,Quaternion

root=Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser();parser.add_argument('--stage',choices=['idle','actions','export'],required=True);parser.add_argument('--preview-output',type=Path)
args=parser.parse_args(sys.argv[sys.argv.index('--')+1:])
scene=bpy.context.scene;rig=bpy.data.objects['Archer_Rig']
names=[b.name for b in rig.data.bones];parents={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
def descendant(name,ancestor):
    while name is not None:
        if name==ancestor:return True
        name=parents[name]
    return False
def set_pose(P,name,matrix):
    delta=matrix@P[name].inverted()
    for child in names:
        if descendant(child,name):P[child]=delta@P[child]
def rotate(P,name,angles):
    q=Quaternion((1,0,0),math.radians(angles[0]))@Quaternion((0,1,0),math.radians(angles[1]))@Quaternion((0,0,1),math.radians(angles[2]))
    matrix=(q@P[name].to_quaternion()).to_matrix().to_4x4();matrix.translation=P[name].translation;set_pose(P,name,matrix)
def move(P,name,offset):
    matrix=P[name].copy();matrix.translation+=Vector(offset);set_pose(P,name,matrix)
def solve_leg(P,side,target,orientation):
    upper='Thigh.'+side;lower='Shin.'+side;foot='Foot.'+side
    A=P[upper].translation.copy();a=(rest[lower].translation-rest[upper].translation).length;b=(rest[foot].translation-rest[lower].translation).length
    delta=target-A;d=min(a+b-.0001,max(abs(a-b)+.0001,delta.length));axis=delta.normalized()
    pole=Vector((.025 if side=='L' else -.025,-.5,-.2));bend=(pole-axis*pole.dot(axis)).normalized()
    along=(a*a-b*b+d*d)/(2*d);E=A+axis*along+bend*math.sqrt(max(0,a*a-along*along));C=A+axis*d
    for n,start,end in [(upper,A,E),(lower,E,C)]:
        direction=(P[n].to_3x3()@Vector((0,1,0))).normalized();q=direction.rotation_difference((end-start).normalized())
        matrix=(q@P[n].to_quaternion()).to_matrix().to_4x4();matrix.translation=start;set_pose(P,n,matrix)
    matrix=orientation.to_matrix().to_4x4();matrix.translation=C;set_pose(P,foot,matrix)
def sample(action):
    rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    first,last=[int(x) for x in action.frame_range]
    result={}
    for frame in range(first,last+1):
        scene.frame_set(frame);result[frame]={n:rig.pose.bones[n].matrix.copy() for n in names}
    return result
def bake(action,poses):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in list(bag.fcurves):bag.fcurves.remove(fc)
    rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];previous={}
    for frame,P in poses.items():
        for n in names:
            parent=parents[n];local=(P[parent].inverted()@P[n]) if parent else P[n]
            rest_local=rest[parent].inverted()@rest[n] if parent else rest[n]
            location,rotation,scale=(rest_local.inverted()@local).decompose()
            if n in previous and rotation.dot(previous[n])<0:rotation.negate()
            previous[n]=rotation.copy();bone=rig.pose.bones[n];bone.rotation_mode='QUATERNION';bone.location=location;bone.rotation_quaternion=rotation;bone.scale=scale
            for prop in ['location','rotation_quaternion','scale']:bone.keyframe_insert(data_path=prop,frame=frame,group=n)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points:key.interpolation='LINEAR'

def mix_pose(a,b,t):
    result={}
    for n in names:
        parent=parents[n];la=a[parent].inverted()@a[n] if parent else a[n];lb=b[parent].inverted()@b[n] if parent else b[n]
        pa,qa,sa=la.decompose();pb,qb,sb=lb.decompose();local=qa.slerp(qb,t).to_matrix().to_4x4();local.translation=pa.lerp(pb,t)
        result[n]=result[parent]@local if parent else local
    return result
def keyed(keys,t):
    for (a,pa),(b,pb) in zip(keys,keys[1:]):
        if a<=t<=b:
            u=(t-a)/(b-a);return mix_pose(pa,pb,u*u*(3-2*u))
    return {n:m.copy() for n,m in keys[-1][1].items()}
def solve_arm(P,side,target,orientation):
    upper='UpperArm.'+side;lower='Forearm.'+side;hand='Hand.'+side;A=P[upper].translation.copy()
    a=(rest[lower].translation-rest[upper].translation).length;b=(rest[hand].translation-rest[lower].translation).length
    delta=target-A;d=min(a+b-.0001,max(abs(a-b)+.0001,delta.length));axis=delta.normalized()
    pole=Vector((.5 if side=='L' else -.5,0,-.1));bend=(pole-axis*pole.dot(axis)).normalized();along=(a*a-b*b+d*d)/(2*d)
    E=A+axis*along+bend*math.sqrt(max(0,a*a-along*along));C=A+axis*d
    for n,start,end in [(upper,A,E),(lower,E,C)]:
        q=(P[n].to_3x3()@Vector((0,1,0))).normalized().rotation_difference((end-start).normalized());m=(q@P[n].to_quaternion()).to_matrix().to_4x4();m.translation=start;set_pose(P,n,m)
    m=orientation.to_matrix().to_4x4();m.translation=C;set_pose(P,hand,m)
def apply_pose(P):
    for n in names:
        parent=parents[n];local=P[parent].inverted()@P[n] if parent else P[n];rl=rest[parent].inverted()@rest[n] if parent else rest[n]
        location,rotation,scale=(rl.inverted()@local).decompose();b=rig.pose.bones[n];b.rotation_mode='QUATERNION';b.location=location;b.rotation_quaternion=rotation;b.scale=scale
    bpy.context.view_layer.update()
def floor_pose(P):
    apply_pose(P);dg=bpy.context.evaluated_depsgraph_get();lowest=10
    for obj in scene.objects:
        if obj.type!='MESH' or obj.name=='NockedArrow' or not any(m.type=='ARMATURE' and m.object==rig for m in obj.modifiers):continue
        evaluated=obj.evaluated_get(dg)
        lowest=min(lowest,min((evaluated.matrix_world@v.co).z for v in evaluated.data.vertices))
    if lowest<.008:move(P,'Pelvis',(0,0,.008-lowest))
    return lowest

geometry={o.name:([tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons]) for o in scene.objects if o.type=='MESH'}
if args.stage=='idle':
    action=bpy.data.actions['Archer_Idle'];poses=sample(action);first=min(poses);last=max(poses)
    targets={s:(poses[first]['Foot.'+s].translation.copy(),poses[first]['Foot.'+s].to_quaternion()) for s in ['L','R']}
    for frame,P in poses.items():
        t=(frame-first)/(last-first);breath=math.sin(math.tau*t);weight=math.sin(math.tau*t)*.003
        move(P,'Pelvis',(weight,0,0));rotate(P,'Pelvis',(0,.25*breath,0))
        rotate(P,'Spine',(.65*breath,0,-.20*breath));rotate(P,'Chest',(.40*breath,0,.12*breath))
        rotate(P,'Clavicle.L',(0,0,.35*breath));rotate(P,'Clavicle.R',(0,0,-.35*breath))
        rotate(P,'Head',(-.80*breath,0,.08*breath))
        for side,(target,orientation) in targets.items():solve_leg(P,side,target,orientation)
    bake(action,poses)
if args.stage=='actions':
    neutral=sample(bpy.data.actions['Archer_Idle'])[1]
    copy=lambda:{n:m.copy() for n,m in neutral.items()}
    rig.animation_data.action=None
    impact=copy();rotate(impact,'Spine',(-4,0,-2));rotate(impact,'Chest',(-3,-4,0));rotate(impact,'Head',(3,0,1));rotate(impact,'UpperArm.R',(-18,0,-5));rotate(impact,'Clavicle.L',(-2,0,1))
    follow=copy();rotate(follow,'Spine',(3,0,1));rotate(follow,'UpperArm.R',(-8,0,2))
    cast_keys=[(0,impact),(.18,follow),(.62,copy()),(1,copy())]
    flinch=copy();rotate(flinch,'Spine',(9,0,5));rotate(flinch,'Chest',(3,0,2));rotate(flinch,'Head',(-7,0,-3));rotate(flinch,'Clavicle.R',(2,0,3))
    hit_keys=[(0,copy()),(.16,flinch),(.55,mix_pose(flinch,neutral,.8)),(1,copy())]
    reach=copy();rotate(reach,'Spine',(6,-3,0));rotate(reach,'Head',(-3,0,0));solve_arm(reach,'R',neutral['Hand.R'].translation+Vector((0,-.12,-.04)),neutral['Hand.R'].to_quaternion())
    interact_keys=[(0,copy()),(.28,reach),(.55,reach),(1,copy())]
    crouch=copy();move(crouch,'Pelvis',(0,0,-.045));rotate(crouch,'Spine',(6,0,0))
    for side in ['L','R']:solve_leg(crouch,side,neutral['Foot.'+side].translation,neutral['Foot.'+side].to_quaternion())
    lunge=copy();move(lunge,'Pelvis',(0,0,-.065));rotate(lunge,'Spine',(11,0,0));rotate(lunge,'Head',(-7,0,0))
    for side,y,z in [('L',-.20,.11),('R',.17,.14)]:solve_leg(lunge,side,Vector((.12 if side=='L' else -.12,y,z)),neutral['Foot.'+side].to_quaternion())
    dash_keys=[(0,copy()),(.12,crouch),(.42,lunge),(.72,mix_pose(lunge,neutral,.65)),(1,copy())]
    stagger=copy();move(stagger,'Pelvis',(0,0,-.055));rotate(stagger,'Spine',(10,0,3));rotate(stagger,'Head',(-4,0,0))
    for side in ['L','R']:solve_leg(stagger,side,neutral['Foot.'+side].translation,neutral['Foot.'+side].to_quaternion())
    prone=copy()
    for side in ['L','R']:
        goal=prone['Hand.'+side].translation.copy();goal.y=0;goal.z=.90;solve_arm(prone,side,goal,prone['Hand.'+side].to_quaternion());rotate(prone,'Foot.'+side,(50,0,8 if side=='L' else -8))
    rotate(prone,'Pelvis',(86,0,4));m=prone['Pelvis'].copy();m.translation=Vector((.025,.10,.14));set_pose(prone,'Pelvis',m)
    collapse=copy();move(collapse,'Pelvis',(0,0,-.35));rotate(collapse,'Pelvis',(25,0,2));rotate(collapse,'Spine',(12,0,0));rotate(collapse,'Head',(-6,0,0))
    for side in ['L','R']:solve_leg(collapse,side,neutral['Foot.'+side].translation,neutral['Foot.'+side].to_quaternion())
    death_keys=[(0,copy()),(.10,stagger),(.28,collapse),(.60,prone),(.78,prone),(1,prone)]
    plan={'Archer_Cast':cast_keys,'Archer_Hit':hit_keys,'Archer_Interact':interact_keys,'Archer_Dash':dash_keys,'Archer_Death':death_keys}
    for name,keys in plan.items():
        action=bpy.data.actions[name];first,last=[int(x) for x in action.frame_range];poses={}
        rig.animation_data.action=None
        for frame in range(first,last+1):
            t=(frame-first)/(last-first);P=keyed(keys,t)
            if name=='Archer_Death':
                support=max(0,min(1,(.45-t)/.17));support=support*support*(3-2*support)
                if support>0:
                    for side in ['L','R']:
                        target=P['Foot.'+side].translation.lerp(neutral['Foot.'+side].translation,support)
                        orientation=P['Foot.'+side].to_quaternion().slerp(neutral['Foot.'+side].to_quaternion(),support)
                        solve_leg(P,side,target,orientation)
                floor_pose(P)
            poses[frame]=P
        bake(action,poses);print('POLISHED_ACTION',name,first,last)
assert all(geometry[o.name]==([tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons]) for o in scene.objects if o.type=='MESH')
assert all(tuple(tuple(v) for v in rig.data.bones[n].matrix_local)==tuple(tuple(v) for v in rest[n]) for n in names)
rig.animation_data.action=bpy.data.actions['Archer_Idle'];rig.animation_data.action_slot=rig.animation_data.action.slots[0];scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(args.preview_output or root/'ArtSource/Characters/Archer/Archer_Animated.blend'),compress=True)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in scene.objects:
    if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers):o.select_set(True)
bpy.context.view_layer.objects.active=rig
if not args.preview_output:
    bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Era/Art/Characters/Archer/Archer_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0)
print('QUALITY_POLISH_OK',args.stage,'actions',len(bpy.data.actions),'bones',len(names))
