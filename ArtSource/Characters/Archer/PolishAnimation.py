"""Edit existing Actions only. Blender CLI: --python this.py -- --stage idle.
Input must be an explicitly saved current archer .blend. No mesh/rest edits.
"""
import bpy,math,json,sys,argparse
from pathlib import Path
from mathutils import Vector,Quaternion

root=Path(__file__).resolve().parents[3]
parser=argparse.ArgumentParser();parser.add_argument('--stage',choices=['idle'],required=True)
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
assert all(geometry[o.name]==([tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons]) for o in scene.objects if o.type=='MESH')
assert all(tuple(tuple(v) for v in rig.data.bones[n].matrix_local)==tuple(tuple(v) for v in rest[n]) for n in names)
rig.animation_data.action=bpy.data.actions['Archer_Idle'];rig.animation_data.action_slot=rig.animation_data.action.slots[0];scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(root/'ArtSource/Characters/Archer/Archer_Animated.blend'),compress=True)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in scene.objects:
    if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers):o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(root/'Assets/Era/Art/Characters/Archer/Archer_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0)
print('QUALITY_POLISH_OK',args.stage,'actions',len(bpy.data.actions),'bones',len(names))
