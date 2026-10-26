"""One refinement of the existing v0.22 rig (22 Actions). Keep a backup first.
Run with Blender Python on the saved original source. No new skeleton or body.
"""
import bpy, math, json
from mathutils import Vector, Matrix, Quaternion
from pathlib import Path

project=str(Path(__file__).resolve().parents[3])
output=str(Path(project)/'Artifacts'/'ArcherRefinementSource')
Path(output).mkdir(parents=True,exist_ok=True)
rig=bpy.data.objects['Archer_Rig']; scene=bpy.context.scene
names=[b.name for b in rig.data.bones]
rest={b.name:b.matrix_local.copy() for b in rig.data.bones}
parent={b.name:b.parent.name if b.parent else None for b in rig.data.bones}
def V(values): return Vector(values)
def q(axis,degrees): return Quaternion(V(axis),math.radians(degrees))
def around(m,rotation):
    result=(rotation@m.to_quaternion()).to_matrix().to_4x4();result.translation=m.translation;return result
def signature(action):
    return json.dumps([(fc.data_path,fc.array_index,[(tuple(k.co),k.interpolation) for k in fc.keyframe_points]) for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves])
def geometry(o):
    return json.dumps(([tuple(v.co) for v in o.data.vertices],[tuple(p.vertices) for p in o.data.polygons],[[(g.group,g.weight) for g in v.groups] for v in o.data.vertices]))
before_actions={a.name:signature(a) for a in bpy.data.actions}
assert len(before_actions)==22 and len(names)==54,'Start from the saved v0.22 source, never rerun on the refined result'
editable={'Quiver_Case','Bow_Wood','Arrow_01','Arrow_02','Arrow_03','Arrow_04','Arrow_05'}
unchanged={o.name:geometry(o) for o in scene.objects if o.type=='MESH' and o.name not in editable}

# Quiver: image-space reference, H=606px, case axis ~216px, top ~75px,
# bottom ~37px, shoulder breadth ~150px; perspective estimates, not 3D facts.
# Existing case length already ~0.38H; increase its missing cross-sectional mass.
case=bpy.data.objects['Quiver_Case']; vertices=[case.matrix_world@v.co for v in case.data.vertices]
bottom=sum(vertices[0:8],V((0,0,0)))/8;top=sum(vertices[16:24],V((0,0,0)))/8
axis=(top-bottom).normalized();length=(top-bottom).length
side=V((axis.z,0,-axis.x)).normalized();depth=axis.cross(side).normalized()
old_width=(vertices[0]-vertices[4]).length;old_top=(vertices[16]-vertices[20]).length
old_top_center=top.copy();new_bottom=bottom+V((0,.04,-.01));new_length=.64;new_top=new_bottom+axis*new_length
new_world=[]
for p in vertices:
    t=(p-bottom).dot(axis)/length;old_center=bottom+axis*(t*length);center=new_bottom+axis*(t*new_length)
    old_w=(.044+.034*t);old_d=(.0375+.020*t)
    x=(p-old_center).dot(side)/old_w;y=(p-old_center).dot(depth)/old_d
    angle=math.atan2(y,x)+math.pi/8;radius=math.sqrt(x*x+y*y)
    # Broad chamfered trapezoid planes, a visible hollow lip, no round tube.
    new_w=.055+.065*t;new_d=.0425+.0225*t
    nx=math.copysign(abs(math.cos(angle))**.5,math.cos(angle))*radius
    ny=math.copysign(abs(math.sin(angle))**.5,math.sin(angle))*radius
    new_world.append(center+side*new_w*nx+depth*new_d*ny)
faces=[tuple(p.vertices) for p in case.data.polygons];material_indices=[p.material_index for p in case.data.polygons]
weights=[[(g.group,g.weight) for g in v.groups] for v in case.data.vertices]
group_names=[g.name for g in case.vertex_groups]
materials=list(case.data.materials)
# Small shield-shaped front flap seen on the original quiver.
flap=[]
for z,x in [(0,-.083),(0,.083),(-.105,.059),(-.15,0),(-.105,-.059)]:
    p=new_top+axis*(-.045+z)+side*x+depth*.069;flap.append(p)
start=len(new_world);new_world+=flap+[p+depth*.007 for p in flap]
faces.extend([tuple(start+i for i in reversed(range(5))),tuple(start+5+i for i in range(5))])
for i in range(5):faces.append((start+i,start+(i+1)%5,start+5+(i+1)%5,start+5+i))
data=bpy.data.meshes.new('Quiver refined chamfered shell');inv=case.matrix_world.inverted()
data.from_pydata([inv@v for v in new_world],[],faces);data.update();case.vertex_groups.clear();case.data=data
for name in group_names:case.vertex_groups.new(name=name)
for mat in materials:data.materials.append(mat)
for i,p in enumerate(data.polygons):p.material_index=material_indices[i] if i<len(material_indices) else 0;p.use_smooth=False
for i,groups in enumerate(weights):
    for group,weight in groups:case.vertex_groups[group].add([i],weight,'REPLACE')
flap_group=case.vertex_groups.new(name='PART_Quiver flap');flap_group.add(list(range(start,len(new_world))),1,'REPLACE')
case.vertex_groups['QuiverAttach'].add(list(range(start,len(new_world))),1,'REPLACE')
for name in ['Arrow_01','Arrow_02','Arrow_03','Arrow_04','Arrow_05']:
    obj=bpy.data.objects[name];inv=obj.matrix_world.inverted()
    for vertex in obj.data.vertices:
        p=obj.matrix_world@vertex.co;d=p-old_top_center
        p=new_top+axis*(d.dot(axis)+.04)+side*d.dot(side)*1.45+depth*d.dot(depth)*1.12
        vertex.co=inv@p

# Bow: preserve grip, nock and tip centers; add tapered faceted wood volume.
bow=bpy.data.objects['Bow_Wood'];inv=bow.matrix_world.inverted()
wood=bow.vertex_groups['PART_Bow wood'].index
ids=[v.index for v in bow.data.vertices if any(g.group==wood and g.weight>.5 for g in v.groups)]
assert len(ids)==102
rings=[[bow.matrix_world@bow.data.vertices[i].co for i in ids[j:j+6]] for j in range(0,len(ids),6)]
centers=[sum(r,V((0,0,0)))/6 for r in rings]
grip=rig.data.bones['BowRoot'].head_local
bow_axis=(centers[-1]-centers[0]).normalized();curve=grip-(centers[0]+centers[-1])*.5;curve-=bow_axis*curve.dot(bow_axis);curve.normalize()
normal=bow_axis.cross(curve).normalized()
for j,ring in enumerate(rings):
    t=j/(len(rings)-1);tangent=(centers[min(j+1,16)]-centers[max(0,j-1)]).normalized();cross=tangent.cross(normal).normalized()
    half_w=max(abs((p-centers[j]).dot(cross)) for p in ring);half_d=max(abs((p-centers[j]).dot(normal)) for p in ring)
    target_w=.006+.013*math.sin(math.pi*t)**.7;target_d=.005+.005*math.sin(math.pi*t)
    center=centers[j]+curve*(.012*math.sin(2*math.pi*t)**2)
    for k,p in enumerate(ring):
        delta=p-centers[j];pos=center+cross*delta.dot(cross)*(target_w/max(half_w,.001))+normal*delta.dot(normal)*(target_d/max(half_d,.001))+tangent*delta.dot(tangent)
        bow.data.vertices[ids[j*6+k]].co=inv@pos
bow.data.update()

def samples(action,phase):
    rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0]
    a,b=action.frame_range;position=a+(b-a)*phase;scene.frame_set(int(math.floor(position)),subframe=position%1)
    return {n:rig.pose.bones[n].matrix.copy() for n in names}
original_run=bpy.data.actions['Archer_Run'];original_walk=bpy.data.actions['Archer_Walk'];idle=samples(bpy.data.actions['Archer_Idle'],0)
run_source={i:samples(original_run,i/36) for i in range(37)}
walk_source={i:samples(original_walk,i/28) for i in range(29)}
clamps=[]
def solve_leg(P,old,side_name,target,rotation):
    upper='Thigh.'+side_name;lower='Shin.'+side_name;foot='Foot.'+side_name
    A=P[upper].translation.copy();a=(rest[lower].translation-rest[upper].translation).length;b=(rest[foot].translation-rest[lower].translation).length
    delta=target-A;distance=delta.length;d=min(a+b-.0001,max(abs(a-b)+.0001,distance))
    clamps.append(max(0,distance-d));axis_leg=delta.normalized()
    pole=A+V((.03 if side_name=='L' else -.03,-.5,-.25));bend=(pole-A-axis_leg*(pole-A).dot(axis_leg)).normalized()
    along=(a*a-b*b+d*d)/(2*d);E=A+axis_leg*along+bend*math.sqrt(max(0,a*a-along*along));C=A+axis_leg*d
    for n,startp,endp in [(upper,A,E),(lower,E,C)]:
        direction=(P[n].to_3x3()@V((0,1,0))).normalized();P[n]=around(P[n],direction.rotation_difference((endp-startp).normalized()))
        P[n].translation=startp
    P[foot]=rotation.to_matrix().to_4x4();P[foot].translation=C
    for n in names:
        if n not in (upper,lower,foot) and parent[n] in (upper,lower,foot):P[n]=P[parent[n]]@old[parent[n]].inverted()@old[n]
def clear(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in list(bag.fcurves):bag.fcurves.remove(fc)
def bake(action,frames):
    clear(action);rig.animation_data.action=action;rig.animation_data.action_slot=action.slots[0];previous={}
    for frame,P in frames.items():
        scene.frame_set(frame)
        for n in names:
            p=parent[n];basis=rest[n].inverted()@(rest[p]@P[p].inverted() if p else Matrix.Identity(4))@P[n]
            pb=rig.pose.bones[n];pb.matrix_basis=basis;rotation=pb.rotation_quaternion.copy()
            if n in previous and rotation.dot(previous[n])<0:rotation.negate()
            pb.rotation_quaternion=rotation;previous[n]=rotation
            for prop in ['location','rotation_quaternion','scale']:pb.keyframe_insert(prop,frame=frame,group=n)
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves:
                    for key in fc.keyframe_points:key.interpolation='LINEAR'
def smooth(t):return t*t*(3-2*t)
def inherit_pose(old,pelvis_z):
    P={}
    for n in names:
        p=parent[n];P[n]=P[p]@old[p].inverted()@old[n] if p else old[n].copy()
        if n=='Pelvis':P[n].translation.z=pelvis_z
    return P

gait_metrics={}
for gait,last,duty,amplitude,lift,drop in [('Walk',28,.62,.24,.045,.028),('Run',18,.22,.37,.155,.10)]:
    duration=last/30;speed=2*amplitude/(duty*duration)
    for suffix,direction,scale in [('',V((0,-1,0)),1),('Left',V((1,0,0)),.9 if gait=='Walk' else .88),('Right',V((-1,0,0)),.9 if gait=='Walk' else .88),('Back',V((0,1,0)),.9 if gait=='Walk' else .82)]:
        action=bpy.data.actions['Archer_'+gait+suffix];frames={}
        actual_duty=duty/scale
        for i in range(last+1):
            phase=i/last;old=(run_source[round(phase*36)] if gait=='Run' else walk_source[round(phase*28)])
            # Low support pelvis and controlled flight, no high-knee marching.
            bounce=.014*math.sin(phase*2*math.pi)**2 if gait=='Walk' else .025*math.sin(phase*2*math.pi)**2
            P=inherit_pose(old,rest['Pelvis'].translation.z-drop+bounce)
            for side_name,offset in [('L',0),('R',.5)]:
                u=(phase+offset)%1
                if u<actual_duty:
                    s=u/actual_duty;travel=amplitude*(1-2*s);height=0
                    pitch=(5*(1-s/.2) if s<.2 else -22*smooth((s-.68)/.32) if s>.68 else 0)
                else:
                    s=(u-actual_duty)/(1-actual_duty);travel=amplitude*(-1+2*smooth(s));height=lift*math.sin(math.pi*s)**1.3
                    pitch=-20*(1-s)+6*s
                pitch=-pitch
                if suffix=='Back':pitch=-pitch*.6
                elif suffix:pitch*=.45
                anchor=V((.115 if side_name=='L' else -.115,-.006,.105))
                if suffix in ['Left','Right']:anchor.y+=(-.035 if side_name=='L' else .035)
                target=anchor+direction*travel;target.z+=height
                foot_rotation=q((1,0,0),pitch)@rest['Foot.'+side_name].to_quaternion()
                solve_leg(P,old,side_name,target,foot_rotation)
            # Clothing follows the new knee/thigh action rather than the old high kick.
            for side_name in ['L','R']:
                sign=1 if side_name=='L' else -1
                u=(phase+(0 if side_name=='L' else .5))%1
                for part in ['PanelFront1.','PanelFront2.','PanelSide.']:
                    n=part+side_name;P[n]=around(P[n],q((1,0,0),(-5 if gait=='Walk' else -9)*math.sin(2*math.pi*u)))
            frames[i+1]=P
        bake(action,frames);gait_metrics[action.name]={'duration':duration,'supportDuty':actual_duty,'authoredSpeed':speed*scale,'footHalfTravel':amplitude,'lift':lift}

air_names=['Archer_JumpStart','Archer_Airborne','Archer_Fall','Archer_Land']
for name,last in [('Archer_JumpStart',6),('Archer_Airborne',30),('Archer_Fall',24),('Archer_Land',8)]:
    action=bpy.data.actions['Archer_Idle'].copy();action.name=name;action.use_fake_user=True;frames={}
    for i in range(last+1):
        t=i/last;old=idle
        if name=='Archer_JumpStart':
            crouch=.055*math.sin(math.pi*min(1,t/.45));pelvis_z=.965-crouch
            legs=[(-.12,.105+.13*smooth(t)),(.10,.105+.09*smooth(t))]
            lean=-4*math.sin(math.pi*t)
        elif name=='Archer_Airborne':
            pelvis_z=.945+.003*math.sin(2*math.pi*t);legs=[(-.14,.255+.006*math.sin(2*math.pi*t)),(.13,.225+.006*math.sin(2*math.pi*t+.5))];lean=-3
        elif name=='Archer_Fall':
            pelvis_z=.94+.002*math.sin(2*math.pi*t);legs=[(-.11,.185+.004*math.sin(2*math.pi*t)),(.10,.155+.004*math.sin(2*math.pi*t))];lean=2
        else:
            amount=math.sin(math.pi*t)**1.1;pelvis_z=.965-.075*amount;legs=[(-.05,.105),(.045,.105)];lean=-5*amount
        P=inherit_pose(old,pelvis_z);P['Spine']=around(P['Spine'],q((1,0,0),lean))
        for n in names:
            p=parent[n]
            if p=='Spine' and n!='Spine':P[n]=P[p]@old[p].inverted()@old[n]
            elif p and n not in ['Pelvis','Spine'] and p not in ['Root','Pelvis']:P[n]=P[p]@old[p].inverted()@old[n]
        for index,side_name in enumerate(['L','R']):
            y,z=legs[index];target=V((.12 if side_name=='L' else -.12,y,z));pitch=-8 if name=='Archer_Airborne' else 3 if name=='Archer_Fall' else 0
            solve_leg(P,old,side_name,target,q((1,0,0),pitch)@rest['Foot.'+side_name].to_quaternion())
        for n in ['Cape1.L','Cape1.C','Cape1.R']:P[n]=around(P[n],q((1,0,0),-2 if name=='Archer_Fall' else 2))
        frames[i+1]=P
    bake(action,frames)

assert all(geometry(bpy.data.objects[n])==value for n,value in unchanged.items())
assert all(signature(bpy.data.actions[n])==value for n,value in before_actions.items() if not n.startswith(('Archer_Walk','Archer_Run')))
assert len(bpy.data.actions)==26
for obj in scene.objects:
    if obj.type=='MESH':
        for poly in obj.data.polygons:poly.use_smooth=False
rig.animation_data.action=bpy.data.actions['Archer_Idle'];rig.animation_data.action_slot=rig.animation_data.action.slots[0]
scene.frame_start=1;scene.frame_end=73;scene.frame_set(1)
report={'gaits':gait_metrics,'airActions':air_names,'preservedOriginalActions':14,'unchangedBodyMeshes':list(unchanged),'quiver':{'oldAxisLength':length,'newAxisLength':new_length,'oldBottomWidth':old_width,'oldTopWidth':old_top,'newBottomWidth':.11,'newTopWidth':.24,'angleDegrees':math.degrees(math.acos(axis.z)),'arrowExtensionRelativeToMouthAdded':.04},'maxIKClampMeters':max(clamps,default=0),'bones':len(names)}
bpy.ops.wm.save_as_mainfile(filepath=output+'/Archer_Refined.blend',compress=True)
bpy.ops.wm.save_as_mainfile(filepath=project+'/ArtSource/Characters/Archer/Archer_Animated.blend',compress=True)
meshes=[o for o in scene.objects if o.type=='MESH' and any(m.type=='ARMATURE' and m.object==rig for m in o.modifiers)]
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=rig
bpy.ops.export_scene.fbx(filepath=project+'/Assets/Era/Art/Characters/Archer/Archer_Rigged.fbx',use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1.0,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,use_mesh_modifiers_render=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1.0,bake_anim_simplify_factor=0.0,path_mode='AUTO',embed_textures=False)
print('ARCHER_REFINEMENT_AUDIT',json.dumps(report))
