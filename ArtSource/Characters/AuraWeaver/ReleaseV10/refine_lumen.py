"""Focused V9 fixes: sound hat topology, raised cases, robe support, distinct gaits."""
import bpy,bmesh,sys,math,json,hashlib
from pathlib import Path
from mathutils import Vector,Matrix,Quaternion
HERE=Path(__file__).resolve().parent;SOURCE=HERE.parent;ROOT=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ROOT/'ArtSource/CharacterFactory')]
from animate_aura_weaver import set_action,world_axis_rotation,solve_two
from era_character_factory.qa import mesh_report
src=SOURCE/'ReleaseV9/AuraWeaver_Lumen_V9.blend';source_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src));r=bpy.data.objects['Archer_Rig'];s=bpy.context.scene
r.data.pose_position='REST';r.animation_data_clear()
for p in r.pose.bones:p.matrix_basis.identity()
objects=[o for o in r.children if o.type=='MESH' and o.name not in {'AW_RunicFocus_Independent','AW_FocusHolster'}]

hat=bpy.data.objects['AW_Hat'];g=hat.vertex_groups['PART_V3_HatBentCrown'];group_names=[vg.name for vg in hat.vertex_groups]
old=[v for v in hat.data.vertices if any(w.group==g.index and w.weight>.5 for w in v.groups)];ids={v.index for v in old}
old_faces=[(p.center.copy(),p.material_index) for p in hat.data.polygons if all(i in ids for i in p.vertices)]
keep=[v for v in hat.data.vertices if v.index not in ids];remap={v.index:i for i,v in enumerate(keep)}
coords=[v.co.copy() for v in keep];weights=[[(w.group,w.weight) for w in v.groups] for v in keep]
faces=[tuple(remap[i] for i in p.vertices) for p in hat.data.polygons if not any(i in ids for i in p.vertices)]
mats=[p.material_index for p in hat.data.polygons if not any(i in ids for i in p.vertices)]
start=len(coords);ring2=[v.co.copy() for v in old[16:24]];center=sum(ring2,Vector())/8
direction=Vector((.5,math.sqrt(3)/2,0));p0=center;p1=p0+Vector((0,0,.105));p2=p0+direction*.13+Vector((0,0,.055));p3=p0+direction*.235+Vector((0,0,-.085))
def bez(t):return p0*(1-t)**3+p1*3*(1-t)**2*t+p2*3*(1-t)*t*t+p3*t**3
def tangent(t):return ((p1-p0)*3*(1-t)**2+(p2-p1)*6*(1-t)*t+(p3-p2)*3*t*t).normalized()
rings=[[v.co.copy() for v in old[k*8:k*8+8]] for k in range(3)]
for t in [.12,.24,.37,.50,.63,.76,.87,.95,1]:
    q=Vector((0,0,1)).rotation_difference(tangent(t));scale=(1-t)**.9*.99+.012
    rings.append([bez(t)+q@Vector((v.x-center.x,v.y-center.y,0))*scale for v in ring2])
head_group=hat.vertex_groups['Head'].index
for ring in rings:
    for v in ring:coords.append(v);weights.append([(head_group,1),(g.index,1)])
if not any(m.name=='AW_charcoal' for m in hat.data.materials):hat.data.materials.append(bpy.data.materials['AW_charcoal'])
slots={m.name:i for i,m in enumerate(hat.data.materials)}
cream=slots.get('AW_ivory',0);sand=slots.get('AW_sand',cream);dark=slots['AW_charcoal']
for k in range(len(rings)-1):
    for j in range(8):
        faces.append((start+k*8+j,start+k*8+(j+1)%8,start+(k+1)*8+(j+1)%8,start+(k+1)*8+j))
        face_center=sum((coords[i] for i in faces[-1]),Vector())/4
        original_mat=min(old_faces,key=lambda item:(item[0]-face_center).length_squared)[1]
        mats.append(original_mat if k<2 else dark if k>=len(rings)-3 else sand if j in [1,2,5] else cream)
faces.extend([tuple(start+j for j in reversed(range(8))),tuple(start+(len(rings)-1)*8+j for j in range(8))]);mats.extend([cream,dark])
new=bpy.data.meshes.new('V10 closed swept crown');new.from_pydata(coords,[],faces)
for m in hat.data.materials:new.materials.append(m)
hat.data=new
for name in group_names:
    if hat.vertex_groups.get(name) is None:hat.vertex_groups.new(name=name)
for p,mi in zip(new.polygons,mats):p.material_index=mi
for i,groups in enumerate(weights):
    for group,weight in groups:hat.vertex_groups[group].add([i],weight,'REPLACE')
bm=bmesh.new();bm.from_mesh(new);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));hat_volume=bm.calc_volume(signed=True)
assert all(e.is_manifold for e in bm.edges),'Hat must be closed, including brim and tip'
bm.to_mesh(new);bm.free()
# All outside faces now have coherent outward winding. No transparent material.
for mat in hat.data.materials:
    mat.diffuse_color[3]=1
    if mat.use_nodes:mat.node_tree.nodes.get('Principled BSDF').inputs['Alpha'].default_value=1

# Raise the cases and shorten their suspension. Belt attachment vertices stay put.
for name in ['AW_PouchAssembly_L','AW_PouchAssembly_R']:
    o=bpy.data.objects[name]
    for v in o.data.vertices:v.co.z+=.105*max(0,min(1,(1.25-v.co.z)/.09))

# More sampling support where legs can touch the long robe. Materials are inherited.
for name in ['AW_OuterCoat','AW_InnerCoat','AW_RearRobeInlay']:
    o=bpy.data.objects[name];bm=bmesh.new();bm.from_mesh(o.data)
    for _ in range(2):
        edges=[e for e in bm.edges if max(v.co.z for v in e.verts)<.95 and e.calc_length()>.065]
        if edges:bmesh.ops.subdivide_edges(bm,edges=edges,cuts=1,use_grid_fill=True)
    bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(o.data);bm.free()

actions=sorted(list(bpy.data.actions),key=lambda a:a.name)
rest_chest=r.data.bones['Chest'].matrix_local.copy()
feet={side:r.data.bones['Foot.'+side].matrix_local.copy() for side in ['L','R']}
r.data.pose_position='POSE'
for name in ['Idle','Walk','Run']:
    a=bpy.data.actions['AuraWeaver_'+name];set_action(r,a);first,last=map(round,a.frame_range)
    for frame in range(first,last+1):
        s.frame_set(frame);phase=2*math.pi*(frame-first)/(last-first);moving=name!='Idle';run=name=='Run'
        for bn in ['Root','Pelvis','Spine','Chest','Neck','Head','Thigh.L','Thigh.R','Shin.L','Shin.R','Foot.L','Foot.R']:
            r.pose.bones[bn].matrix_basis=Matrix.Identity(4)
        r.pose.bones['Root'].location=r.data.bones['Root'].matrix_local.to_quaternion().inverted()@Vector((0,0,.004))
        for bn,lean,roll,twist in [('Pelvis',.105 if run else .025 if moving else 0,.025,.035),('Spine',.145 if run else .065 if moving else .008,.018,-.035),('Chest',.035 if run else .015 if moving else .003,.012,-.045),('Neck',-.07 if run else -.035,0,0),('Head',-.04 if run else -.015,0,0)]:
            p=r.pose.bones[bn];inv=p.bone.matrix_local.to_quaternion().inverted()
            amp=1 if moving else .15
            p.rotation_quaternion=Quaternion(inv@Vector((1,0,0)),lean+(.012 if moving else .004)*math.cos(phase*2))@Quaternion(inv@Vector((0,1,0)),roll*amp*math.sin(phase))@Quaternion(inv@Vector((0,0,1)),twist*amp*math.sin(phase))
        bpy.context.view_layer.update()
        for side,offset in [('L',0),('R',.5)]:
            if moving:
                t=((frame-first)/(last-first)+offset)%1;stride=.255 if run else .145
                y=(-stride+4*stride*t) if t<.5 else (stride-4*stride*(t-.5))
                z=0 if t<.5 else (.14 if run else .045)*math.sin(2*math.pi*(t-.5))
                goal=feet[side].translation+Vector((0,y,z));upper=r.pose.bones['Thigh.'+side];lower=r.pose.bones['Shin.'+side]
                solve_two(upper,lower,goal,upper.head+Vector((0,-1,.05)))
                foot=r.pose.bones['Foot.'+side];foot.matrix=Matrix.LocRotScale(foot.head.copy(),feet[side].to_quaternion(),Vector((1,1,1)))
        bpy.context.view_layer.update()
        # The casting support hand and raised guiding hand carry a rune in locomotion.
        delta=r.pose.bones['Chest'].matrix@rest_chest.inverted()
        for side,target,pole,d in [('R',(-.105,-.40,1.30),(-.40,-.08,1.36),(.9,-.10,.20)),('L',(.16,-.36,1.49),(.40,-.1,1.40),(-.50,-.05,.78))]:
            upper=r.pose.bones['UpperArm.'+side];lower=r.pose.bones['Forearm.'+side];hand=r.pose.bones['Hand.'+side]
            solve_two(upper,lower,delta@Vector(target),delta@Vector(pole))
            q=(hand.bone.tail_local-hand.bone.head_local).rotation_difference(delta.to_quaternion()@Vector(d).normalized())@hand.bone.matrix_local.to_quaternion()
            hand.matrix=Matrix.LocRotScale(hand.head.copy(),q,Vector((1,1,1)));bpy.context.view_layer.update()
        for p in r.pose.bones:
            if p.name.startswith('Digit_'):world_axis_rotation(p,(1,0,0),-.26)
            if p.name.startswith('Coat'):
                label=p.name.split('.',1)[1];side='L' if label.endswith('L') else 'R';t=phase+(0 if side=='L' else math.pi)
                angle=(.12 if run else .065)*math.sin(t) if moving else .006*math.sin(phase)
                world_axis_rotation(p,(1,0,0),angle*(.5 if p.name.startswith('Coat2') else 1))
            p.rotation_mode='QUATERNION';p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
            if p.name=='Root':p.keyframe_insert('location',frame=frame,group=p.name)

# Raised geometry retains the existing independent leather-case anchor tracks.
set_action(r,bpy.data.actions['AuraWeaver_Idle']);s.frame_set(1)
qa=mesh_report(r,objects)
assert all(m['finite'] and not m['zero_area_faces'] and not m['nonmanifold_nonboundary_edges'] for m in qa['meshes'])
HERE.mkdir(exist_ok=True);previews=HERE/'Previews';previews.mkdir(exist_ok=True)
s.cycles.samples=20;s.render.resolution_percentage=75
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_Lumen_V10.blend'),compress=True)
manifest=json.loads((SOURCE/'ReleaseV9/AuraWeaver_Manifest.json').read_text());manifest['sourceBlendSha256']=hashlib.sha256((HERE/'AuraWeaver_Lumen_V10.blend').read_bytes()).hexdigest();manifest['triangles']=sum(len(p.vertices)-2 for o in objects for p in o.data.polygons)
(HERE/'AuraWeaver_Manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
for o in objects:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=r
bpy.ops.export_scene.fbx(filepath=str(HERE/'AuraWeaver_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
for name in ['Idle','Walk','Run']:
    a=bpy.data.actions['AuraWeaver_'+name];set_action(r,a);s.frame_set(round(a.frame_range[0]+(a.frame_range[1]-a.frame_range[0])*.22));s.camera=bpy.data.objects['Camera_Perspective'];s.render.filepath=str(previews/(name+'.png'));bpy.ops.render.render(write_still=True)
set_action(r,bpy.data.actions['AuraWeaver_Idle']);s.frame_set(1)
for view in ['Side','Back']:
    s.camera=bpy.data.objects['Camera_'+view];s.render.filepath=str(previews/(view+'.png'));bpy.ops.render.render(write_still=True)
(HERE/'validation.json').write_text(json.dumps({'source_sha256':source_hash,'source_unchanged':source_hash==hashlib.sha256(src.read_bytes()).hexdigest(),'hat_signed_volume':hat_volume,'hat_closed':True,'front_case_raise_m':.105,'qa':qa,'triangles':manifest['triangles'],'runtime_robe_clearance_required':True},indent=2))
print('ERA_LUMEN_V10_EXPORTED',manifest['triangles'])
