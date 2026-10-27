"""Adapt copies of the verified archer actions; add coat FK and a no-VFX channel pose."""
import math
import bpy
from mathutils import Vector,Matrix,Quaternion


def action_curves(action):
    return [f for layer in action.layers for strip in layer.strips for bag in strip.channelbags for f in bag.fcurves]


def set_action(rig,action):
    rig.animation_data_create();rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]


def world_axis_rotation(bone,axis,angle):
    local=bone.bone.matrix_local.to_quaternion().inverted()@Vector(axis)
    bone.rotation_mode='QUATERNION';bone.rotation_quaternion=Quaternion(local,angle)


def solve_two(upper,lower,target,pole):
    origin=upper.head.copy();a=upper.length;b=lower.length;delta=Vector(target)-origin;distance=min(a+b-.001,max(abs(a-b)+.001,delta.length));axis=delta.normalized()
    bend=Vector(pole)-origin;bend=(bend-axis*bend.dot(axis)).normalized();along=(a*a-b*b+distance*distance)/(2*distance)
    elbow=origin+axis*along+bend*math.sqrt(max(0,a*a-along*along));goal=origin+axis*distance
    def aim(bone,head,end):
        rotation=(bone.tail-bone.head).rotation_difference(end-head)
        bone.matrix=Matrix.Translation(head)@rotation.to_matrix().to_4x4()@Matrix.Translation(-head)@bone.matrix
        bpy.context.view_layer.update()
    aim(upper,origin,elbow);aim(lower,lower.head.copy(),goal)


def solve_arm(rig,side,target,pole):
    upper=rig.pose.bones['UpperArm.'+side];lower=rig.pose.bones['Forearm.'+side];hand=rig.pose.bones['Hand.'+side]
    solve_two(upper,lower,target,pole)
    direction=Vector((-.70,-.20,.20)) if side=='L' else Vector((.35,-.35,.85))
    rest=hand.bone.tail_local-hand.bone.head_local;q=rest.rotation_difference(direction.normalized())@hand.bone.matrix_local.to_quaternion()
    hand.matrix=Matrix.LocRotScale(hand.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update()


def prepare_actions(rig,sources,metadata):
    scene=bpy.context.scene;made=[]
    for original,source in sources.items():
        action=source.copy();action.name=original.replace('Archer_','AuraWeaver_');action.use_fake_user=True;set_action(rig,action)
        first,last=map(round,action.frame_range)
        for p in rig.pose.bones:p.rotation_mode='QUATERNION'
        for frame in range(first,last+1):
            scene.frame_set(frame);bpy.context.view_layer.update()
            pelvis_inverse=rig.pose.bones['Pelvis'].matrix.to_quaternion().inverted()
            pelvis_rest_inverse=rig.data.bones['Pelvis'].matrix_local.to_quaternion().inverted()
            for spec in metadata['coat']:
                side='L' if spec['x']>0 else 'R';leg=rig.pose.bones['Thigh.'+side];rest=pelvis_rest_inverse@(leg.bone.tail_local-leg.bone.head_local);posed=pelvis_inverse@(leg.tail-leg.head)
                angle=math.atan2(-posed.y,-posed.z)-math.atan2(-rest.y,-rest.z)
                if spec['back']:angle=min(0,angle)*.82
                elif spec['label'].startswith('Side'):angle*=.78
                else:angle=max(0,angle)*.86
                angle=max(-.95,min(.95,angle))
                for k,weight in [(1,1),(2,-.23)]:
                    p=rig.pose.bones['Coat'+str(k)+'.'+spec['label']];world_axis_rotation(p,(1,0,0),-angle*weight)
                    p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
            if original=='Archer_Death':
                # A one-metre robe cannot inherit the short archer panel response
                # to a falling pelvis: fold it toward the shins/feet instead.
                for spec in metadata['coat']:
                    side='L' if spec['x']>0 else 'R';upper=rig.pose.bones['Coat1.'+spec['label']];lower=rig.pose.bones['Coat2.'+spec['label']]
                    foot=rig.pose.bones['Foot.'+side];knee=rig.pose.bones['Shin.'+side]
                    goal=foot.tail.copy()+Vector((0,0,.075 if not spec['back'] else .030));goal.z=max(.035,goal.z)
                    solve_two(upper,lower,goal,knee.head+Vector((0,0,.25 if not spec['back'] else .13)))
                    for p in [upper,lower]:p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
            for p in rig.pose.bones:
                if p.name.startswith('Digit_'):
                    world_axis_rotation(p,(1,0,0),.07 if '2.' in p.name else .035);p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
            if original in ['Archer_Idle','Archer_Walk','Archer_Run','Archer_Land','Archer_Cast','Archer_Hit','Archer_Death']:
                # New costume clearance only: preserve the old bank and bake a
                # vertical offset into this copy where its hat/boots meet ground.
                root=rig.pose.bones['Root'];baseline=Vector((0,0,0))
                for curve in action_curves(source):
                    if curve.data_path=='pose.bones["Root"].location':baseline[curve.array_index]=curve.evaluate(frame)
                root.location=baseline;bpy.context.view_layer.update();graph=bpy.context.evaluated_depsgraph_get();minimum=100
                for o in rig.children:
                    if o.type!='MESH':continue
                    ev=o.evaluated_get(graph);mesh=ev.to_mesh()
                    try:minimum=min(minimum,min((ev.matrix_world@v.co).z for v in mesh.vertices))
                    finally:ev.to_mesh_clear()
                root.location=baseline+root.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,max(0,.003-minimum)))
                root.keyframe_insert('location',frame=frame,group='Root')
        made.append({'name':action.name,'source':original,'start':first,'end':last,'loop':original.endswith(('Idle','Walk','Run','Airborne','Fall'))})
    for source in sources.values():bpy.data.actions.remove(source)
    idle=next(a for a in bpy.data.actions if a.name=='AuraWeaver_Idle');channel=idle.copy();channel.name='AuraWeaver_AuraChannel';channel.use_fake_user=True
    # Remove old arm curves from the copy; base rig/locomotion assets are untouched.
    arm_names=['UpperArm.L','Forearm.L','Hand.L','UpperArm.R','Forearm.R','Hand.R']
    for layer in channel.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for curve in list(bag.fcurves):
                    if any('"'+n+'"' in curve.data_path for n in arm_names):bag.fcurves.remove(curve)
    set_action(rig,channel)
    for frame in range(1,74):
        scene.frame_set(frame)
        for name in arm_names:rig.pose.bones[name].matrix_basis=Matrix.Identity(4)
        bpy.context.view_layer.update()
        solve_arm(rig,'L',(.145,-.343,1.155),(.36,-.10,1.07));solve_arm(rig,'R',(-.157,-.297,1.323),(-.34,-.04,1.18))
        for name in arm_names:
            p=rig.pose.bones[name];p.rotation_mode='QUATERNION';p.keyframe_insert('rotation_quaternion',frame=frame,group=name)
    made.append({'name':channel.name,'source':'AuraWeaver_Idle + new two-hand FK pose','start':1,'end':73,'loop':True})
    metadata['actions']=made
    set_action(rig,idle);scene.frame_start=1;scene.frame_end=73;scene.frame_set(1)
