"""Refine existing actions only. Run on a COPY of Archer_Animated.blend in Blender.
Meshes, materials, rest bones and weights are never edited. Existing 15 other
actions remain byte-for-byte equivalent at the FCurve/keyframe data level.
"""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

project=Path(__file__).resolve().parents[3]
if '--era-project' in sys.argv:
    project=Path(sys.argv[sys.argv.index('--era-project')+1]).resolve()
assert (project/'Assets/Era').is_dir(), 'Select the existing ERA project, never the original cowboy file'
rig=bpy.data.objects['Archer_Rig']
scene=bpy.context.scene
names=[b.name for b in rig.data.bones]
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
parent={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
def V(values): return Vector(values)
def rotation(axis,degrees): return Quaternion(V(axis),math.radians(degrees))
def around(m,q):
    result=(q@m.to_quaternion()).to_matrix().to_4x4()
    result.translation=m.translation
    return result
def signature(action):
    keys=[(fc.data_path,fc.array_index,[(tuple(k.co),k.interpolation) for k in fc.keyframe_points]) for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]
    return json.dumps(keys,sort_keys=True)
def geometry_signature():
    payload=[(o.name,[tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons],[(g.name,g.index) for g in o.vertex_groups],[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices]) for o in scene.objects if o.type=='MESH']
    return json.dumps(payload,sort_keys=True)

before_geometry=geometry_signature()
before_actions={a.name:signature(a) for a in bpy.data.actions}
assert len(names)==54 and len(before_actions)==16, 'Start from the original v0.21 source copy'
def samples(action):
    rig.animation_data.action=action
    rig.animation_data.action_slot=action.slots[0]
    frames={}
    for frame in range(int(action.frame_range[0]),int(action.frame_range[1])+1):
        scene.frame_set(frame)
        frames[frame]={n:rig.pose.bones[n].matrix.copy() for n in names}
    return frames

run=bpy.data.actions['Archer_Run']
run_old=samples(run)
walk_old=samples(bpy.data.actions['Archer_Walk'])
clamps=[]
def solve_leg(P,old,side,target,foot_rotation):
    upper='Thigh.'+side; lower='Shin.'+side; foot='Foot.'+side
    A=P[upper].translation.copy()
    a=(rest[lower].translation-rest[upper].translation).length
    b=(rest[foot].translation-rest[lower].translation).length
    delta=target-A; distance=delta.length
    d=min(a+b-.0001,max(abs(a-b)+.0001,distance))
    if distance-d>.005: clamps.append(distance-d)
    axis=delta.normalized()
    pole=V((.17 if side=='L' else -.17,-1,.55))-A
    bend=(pole-axis*pole.dot(axis)).normalized()
    along=(a*a-b*b+d*d)/(2*d)
    E=A+axis*along+bend*math.sqrt(max(0,a*a-along*along))
    C=A+axis*d
    for n,start,end in [(upper,A,E),(lower,E,C)]:
        initial=(P[n].to_3x3()@V((0,1,0))).normalized()
        P[n]=around(P[n],initial.rotation_difference((end-start).normalized()))
    P[foot]=foot_rotation.to_matrix().to_4x4();P[foot].translation=C
    for n in names:
        if n not in (upper,lower,foot) and parent[n] in (upper,lower,foot):
            P[n]=P[parent[n]]@old[parent[n]].inverted()@old[n]

refined={}
for frame,old in run_old.items():
    phase=(frame-1)/20*2*math.pi
    wave=math.sin(phase)
    P={}
    for n in names:
        p=parent[n]
        P[n]=P[p]@old[p].inverted()@old[n] if p else old[n].copy()
        if n=='Pelvis':
            P[n]=around(P[n],rotation((0,0,1),1.5*wave)@rotation((0,1,0),1.4*wave))
            P[n].translation+=V((.007*wave,0,0))
        elif n=='Spine':P[n]=around(P[n],rotation((1,0,0),1.1*math.sin(2*phase-.3)))
        elif n=='Chest':P[n]=around(P[n],rotation((0,0,1),-1.5*wave)@rotation((1,0,0),-1.2*math.sin(2*phase-.3)))
        elif n.startswith('Clavicle.'):
            sign=1 if n.endswith('.L') else -1
            P[n]=around(P[n],rotation((0,0,1),sign*1.8*wave)@rotation((0,1,0),sign*1.1*math.sin(phase+.3)))
        elif n=='Head':
            # Counter only the new torso drift, retain the original authored head pose.
            P[n]=around(P[n],rotation((0,1,0),-.7*wave)@rotation((1,0,0),.7*math.sin(2*phase-.3)))
    for side in ('L','R'):
        solve_leg(P,old,side,old['Foot.'+side].translation,old['Foot.'+side].to_quaternion())
    refined[frame]=P

def bake(action,frames):
    rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    previous={}
    for frame,P in frames.items():
        scene.frame_set(frame)
        for n in names:
            p=parent[n]
            basis=rest[n].inverted()@(rest[p]@P[p].inverted() if p else Matrix.Identity(4))@P[n]
            pb=rig.pose.bones[n];pb.matrix_basis=basis
            q=pb.rotation_quaternion.copy()
            if n in previous and q.dot(previous[n])<0:q.negate()
            pb.rotation_quaternion=q;previous[n]=q
            for prop in ('location','rotation_quaternion','scale'):pb.keyframe_insert(prop,frame=frame,group=n)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points:key.interpolation='LINEAR'

bake(run,refined)
created=[]
for gait,base_frames in [('Walk',walk_old),('Run',refined)]:
    for label,angle,scale in [('Left',90,.55),('Right',-90,.55),('Back',180,.68)]:
        action=bpy.data.actions['Archer_'+gait].copy()
        action.name='Archer_'+gait+label;action.use_fake_user=True
        frames={}
        for frame,old in base_frames.items():
            P={n:old[n].copy() for n in names}
            for side in ('L','R'):
                foot='Foot.'+side;anchor=rest[foot].translation
                stride=rotation((0,0,1),angle)@V((0,old[foot].translation.y-anchor.y,0))*scale
                target=V((old[foot].translation.x+stride.x,anchor.y+stride.y,old[foot].translation.z))
                pitch=(old[foot].to_quaternion()@rest[foot].to_quaternion().inverted()).to_euler('XYZ').x
                orientation=rotation((1,0,0),math.degrees(pitch)*(-.6 if label=='Back' else .25))@rest[foot].to_quaternion()
                solve_leg(P,old,side,target,orientation)
            frames[frame]=P
        bake(action,frames);created.append(action.name)

assert geometry_signature()==before_geometry
assert all(signature(bpy.data.actions[n])==s for n,s in before_actions.items() if n!='Archer_Run')
rig.animation_data.action=bpy.data.actions['Archer_Idle'];rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_start=1;scene.frame_end=73;scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=str(project/'ArtSource/Characters/Archer/Archer_Animated.blend'),compress=True)
meshes=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=str(project/'Assets/Era/Art/Characters/Archer/Archer_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,use_mesh_modifiers_render=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,path_mode='AUTO',embed_textures=False)
print('MOTION_SOURCE_AUDIT',json.dumps({'geometryUnchanged':True,'bones':len(names),'originalActionsPreserved':15,'refined':'Archer_Run','directionalDerived':created,'maxIKClampMeters':max(clamps,default=0),'originalActionNames':list(before_actions)},sort_keys=True))
