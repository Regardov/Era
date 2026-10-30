"""V8 selected paint -> fitted Lumen rig. Never writes the V8 or archer source."""
import bpy, sys, math, json, hashlib
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

HERE=Path(__file__).resolve().parent
SOURCE=HERE.parent
ROOT=SOURCE.parents[2]
sys.path[:0]=[str(SOURCE),str(ROOT/'ArtSource/CharacterFactory')]
from animate_aura_weaver import set_action, action_curves, solve_two, world_axis_rotation
from era_character_factory.qa import mesh_report, evaluated_bounds, surface_contacts

src=SOURCE/'PatternV8/AuraWeaver_V8.blend'
original_hash=hashlib.sha256(src.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(src))
s=bpy.context.scene;r=bpy.data.objects['Archer_Rig']
r.animation_data_clear();r.data.pose_position='REST'
for p in r.pose.bones:p.matrix_basis.identity()
optional={'AW_RunicFocus_Independent','AW_FocusHolster'}
meshes=[o for o in r.children if o.type=='MESH' and o.name not in optional]

# Upper tip keeps the existing downward sweep, but lies 30 degrees off +Y/back.
hat=bpy.data.objects['AW_Hat'];group=hat.vertex_groups['PART_V3_HatBentCrown']
vv=[v for v in hat.data.vertices if any(w.group==group.index and w.weight>.5 for w in v.groups)]
centers=[sum((v.co for v in vv[k*8:k*8+8]),Vector())/8 for k in range(7)]
pivot=centers[3];old=centers[-1]-pivot
angle=math.pi/3-math.atan2(old.y,old.x)
turn=Matrix.Rotation(angle,3,'Z')
for k in range(4,7):
    center=centers[k];new=pivot+turn@(center-pivot)
    fullness=[1.32,1.15,1][k-4]
    for v in vv[k*8:k*8+8]:v.co=new+turn@((v.co-center)*fullness)
tip=sum((v.co for v in vv[48:56]),Vector())/8
tail=tip-pivot
tail_angle=math.degrees(math.atan2(tail.x,tail.y))
assert abs(tail_angle-30)<.01

# Match bind landmarks to the V6 continuous mesh warp inherited by V8.
# This is a mage-only bind; shared names do not imply identical Generic rigs.
anchors=[(0,0),(.15,.15),(.78,.85),(.90,1),(1.05,1.225),(1.35,1.535),(1.48,1.665),(1.75,1.805),(2.009,2.009)]
def warp(z):
    for (a,b),(c,d) in zip(anchors,anchors[1:]):
        if z<=c:return b+(z-a)*(d-b)/(c-a)
    return z
def fitted(v,name):
    v=v.copy();v.z=warp(v.z)
    if any(t in name for t in ['Arm.','Hand.','Digit_','Clavicle.']):v.x*=.87;v.y*=.90
    elif any(t in name for t in ['Thigh.','Shin.','Foot.','Toe.']):v.x*=.95
    elif name.startswith('Coat'):v.x*=.80;v.y*=.85
    return v
old_rest={b.name:(b.head_local.copy(),b.tail_local.copy()) for b in r.data.bones}
bpy.context.view_layer.objects.active=r;r.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
for b in r.data.edit_bones:
    b.use_connect=False
    h,t=old_rest[b.name];roll=b.roll
    b.head=fitted(h,b.name);b.tail=fitted(t,b.name);b.roll=roll
bpy.ops.object.mode_set(mode='OBJECT')

# Fingers in V8 are already curled in the mesh: avoid adding the old double curl.
# Key a small uncurl only during the two-handed channel/cast gesture.
actions=sorted(list(bpy.data.actions),key=lambda a:a.name)
r.data.pose_position='POSE'
for a in actions:
    set_action(r,a);first,last=map(round,a.frame_range)
    for f in action_curves(a):
        if f.data_path.startswith('pose.bones["Digit_'):
            for key in f.keyframe_points:
                key.co.y=1 if f.array_index==0 else 0
    for frame in range(first,last+1):
        s.frame_set(frame)
        if a.name.endswith(('AuraChannel','Cast')):
            phase=(frame-first)/max(1,last-first)
            blend=1 if a.name.endswith('AuraChannel') else math.sin(math.pi*phase)**.65
            # Save the locomotion pose, solve the raised mage hand targets, then blend.
            names=[x+'.'+side for side in ['L','R'] for x in ['UpperArm','Forearm','Hand']]
            previous={n:r.pose.bones[n].rotation_quaternion.copy() for n in names}
            bpy.context.view_layer.update()
            for side,target,pole,direction in [
                ('L',(.115,-.36,1.36),(.39,-.08,1.33),(-.85,-.05,.40)),
                ('R',(-.13,-.34,1.50),(-.39,-.08,1.40),(.65,-.15,.65))]:
                upper=r.pose.bones['UpperArm.'+side];lower=r.pose.bones['Forearm.'+side];hand=r.pose.bones['Hand.'+side]
                solve_two(upper,lower,target,pole)
                rest=hand.bone.tail_local-hand.bone.head_local
                q=rest.rotation_difference(Vector(direction).normalized())@hand.bone.matrix_local.to_quaternion()
                hand.matrix=Matrix.LocRotScale(hand.head.copy(),q,Vector((1,1,1)))
                bpy.context.view_layer.update()
            for n in names:
                p=r.pose.bones[n];p.rotation_quaternion=previous[n].slerp(p.rotation_quaternion,blend)
                p.keyframe_insert('rotation_quaternion',frame=frame,group=n)
            for p in r.pose.bones:
                if p.name.startswith('Digit_'):
                    world_axis_rotation(p,(1,0,0),-.30*blend)
                    p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
        # Less violent robe articulation keeps attached leather cases closer to cloth.
        if a.name.endswith(('Walk','Run')):
            for p in r.pose.bones:
                if p.name.startswith('Coat'):
                    p.rotation_quaternion=Quaternion().slerp(p.rotation_quaternion,.60)
                    p.keyframe_insert('rotation_quaternion',frame=frame,group=p.name)
        bpy.context.view_layer.update()

# Bake four rigid leather-case anchors from the actual moving garment surface.
# Mounts blend back to Pelvis at the belt. Cases do not shear with robe skinning.
from mathutils.bvhtree import BVHTree
coat=bpy.data.objects['AW_OuterCoat'];coat.data.calc_loop_triangles()
r.data.pose_position='REST';bpy.context.view_layer.update()
specs=[]
for name,sign in [('AW_PouchAssembly_L',-1),('AW_PouchAssembly_R',-1),('AW_RearPouch_Long',1),('AW_RearPouch_Short',1)]:
    o=bpy.data.objects[name];g=o.vertex_groups.get('PART_FrontContactBack') or o.vertex_groups['PART_BagContactBack']
    contact=[v.co.copy() for v in o.data.vertices if any(w.group==g.index and w.weight>.5 for w in v.groups)]
    center=sum(contact,Vector())/len(contact)
    tris=[tuple(t.vertices) for t in coat.data.loop_triangles if sum(coat.data.vertices[i].co.y for i in t.vertices)*sign>0]
    tree=BVHTree.FromPolygons([v.co for v in coat.data.vertices],tris,all_triangles=True)
    hit=tree.ray_cast(Vector((center.x,sign,center.z)),Vector((0,-sign,0)))
    if hit[0] is None:hit=tree.find_nearest(center)
    point,normal,face,_=hit;ids=tris[face];a,b,c=[coat.data.vertices[i].co for i in ids]
    v0=b-a;v1=c-a;v2=point-a;d00=v0.dot(v0);d01=v0.dot(v1);d11=v1.dot(v1);den=d00*d11-d01*d01
    wb=(d11*v2.dot(v0)-d01*v2.dot(v1))/den;wc=(d00*v2.dot(v1)-d01*v2.dot(v0))/den
    specs.append({'object':o,'name':'PouchAnchor.'+name.removeprefix('AW_'),'sign':sign,'center':center,'ids':ids,'bary':(1-wb-wc,wb,wc),'rest_normal':normal,'surface':point})
bpy.context.view_layer.objects.active=r;bpy.ops.object.mode_set(mode='EDIT')
for spec in specs:
    b=r.data.edit_bones.new(spec['name']);b.head=spec['center'];b.tail=spec['center']+Vector((0,0,.08));b.parent=r.data.edit_bones['Pelvis']
bpy.ops.object.mode_set(mode='OBJECT')
for spec in specs:
    o=spec['object'];bone=o.vertex_groups.new(name=spec['name']);pelvis=o.vertex_groups.get('Pelvis') or o.vertex_groups.new(name='Pelvis')
    for v in o.data.vertices:
        for g in o.vertex_groups:
            if g.name in r.data.bones:g.remove([v.index])
        t=max(0,min(1,(1.225-v.co.z)/.065))
        if t>0:bone.add([v.index],t,'REPLACE')
        if t<1:pelvis.add([v.index],1-t,'REPLACE')
r.data.pose_position='POSE';anchor_samples=[]
for action in actions:
    set_action(r,action);lo,hi=map(round,action.frame_range)
    for frame in range(lo,hi+1):
        s.frame_set(frame);graph=bpy.context.evaluated_depsgraph_get();ev=coat.evaluated_get(graph);mesh=ev.to_mesh()
        try:points=[v.co.copy() for v in mesh.vertices]
        finally:ev.to_mesh_clear()
        tree=BVHTree.FromPolygons(points,[list(p.vertices) for p in coat.data.polygons])
        max_offset=0
        for spec in specs:
            a,b,c=[points[i] for i in spec['ids']];n=(b-a).cross(c-a).normalized()
            if n.y*spec['sign']<0:n=-n
            restn=spec['rest_normal'].copy()
            if restn.y*spec['sign']<0:restn=-restn
            rotation=restn.rotation_difference(n)
            surface=sum((points[i]*w for i,w in zip(spec['ids'],spec['bary'])),Vector())
            center=surface+rotation@(spec['center']-spec['surface'])
            pb=r.pose.bones[spec['name']];q=rotation@pb.bone.matrix_local.to_quaternion()
            offset=0
            for attempt in range(13):
                pb.matrix=Matrix.LocRotScale(center+Vector((0,spec['sign']*offset,0)),q,Vector((1,1,1)))
                bpy.context.view_layer.update();ev=spec['object'].evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
                try:bag_tree=BVHTree.FromPolygons([v.co.copy() for v in mesh.vertices],[list(p.vertices) for p in mesh.polygons])
                finally:ev.to_mesh_clear()
                if not bag_tree.overlap(tree):break
                offset+=.005
            max_offset=max(max_offset,offset)
            pb.rotation_mode='QUATERNION';pb.keyframe_insert('location',frame=frame,group=pb.name);pb.keyframe_insert('rotation_quaternion',frame=frame,group=pb.name)
        anchor_samples.append({'action':action.name,'frame':frame,'max_clearance_offset':max_offset})

# Export only the current character; the optional old wand remains in the .blend.
set_action(r,bpy.data.actions['AuraWeaver_Idle']);s.frame_set(1)
qa=mesh_report(r,meshes)
assert all(m['finite'] and not m['zero_area_faces'] and not m['nonmanifold_nonboundary_edges'] for m in qa['meshes'])
HERE.mkdir(exist_ok=True);previews=HERE/'Previews';previews.mkdir(exist_ok=True)
s.render.resolution_percentage=80;s.cycles.samples=24
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'AuraWeaver_Lumen_V9.blend'),compress=True)
palette=[]
def srgb(x):return 12.92*x if x<=.0031308 else 1.055*x**(1/2.4)-.055
for m in sorted({m for o in meshes for m in o.data.materials if m},key=lambda m:m.name):
    color=m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value
    palette.append({'name':m.name,'hex':'#'+''.join(f'{round(max(0,min(1,srgb(c)))*255):02X}' for c in color[:3])})
manifest={'bones':len(r.data.bones),'meshes':len(meshes),'triangles':sum(len(p.vertices)-2 for o in meshes for p in o.data.polygons),'fps':30,
 'sourceBlendSha256':hashlib.sha256((HERE/'AuraWeaver_Lumen_V9.blend').read_bytes()).hexdigest(),
 'clips':[{'name':a.name,'source':'V8 fitted mage copy','start':a.frame_range[0],'end':a.frame_range[1],'loop':a.name.endswith(('Idle','Walk','Run','Airborne','Fall','AuraChannel'))} for a in actions], 'materials':palette}
(HERE/'AuraWeaver_Manifest.json').write_text(json.dumps(manifest,indent=2))
bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
for o in meshes:o.hide_set(False);o.select_set(True)
bpy.context.view_layer.objects.active=r
bpy.ops.export_scene.fbx(filepath=str(HERE/'AuraWeaver_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',use_space_transform=True,bake_space_transform=False,axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',use_tspace=False,use_custom_props=False,add_leaf_bones=False,primary_bone_axis='Y',secondary_bone_axis='X',use_armature_deform_only=False,armature_nodetype='NULL',bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='AUTO',embed_textures=False)
samples=[]
pairs=[(n,'AW_OuterCoat') for n in ['AW_PouchAssembly_L','AW_PouchAssembly_R','AW_RearPouch_Long','AW_RearPouch_Short']]
for a in actions:
    set_action(r,a)
    for t in [0,.25,.5,.75,1]:
        lo,hi=a.frame_range;s.frame_set(round(lo+(hi-lo)*t));bpy.context.view_layer.update()
        bounds=evaluated_bounds(meshes)
        assert all(math.isfinite(x) and abs(x)<8 for x in bounds['min']+bounds['max'])
        samples.append({'action':a.name,'frame':s.frame_current,'bounds':bounds,'contacts':surface_contacts(meshes,pairs,max_pairs=1)})
for name,t in [('Idle',.25),('Walk',.28),('Run',.28),('AuraChannel',.25),('Cast',.4),('Death',.9)]:
    a=bpy.data.actions['AuraWeaver_'+name];set_action(r,a);lo,hi=a.frame_range;s.frame_set(round(lo+(hi-lo)*t))
    s.camera=bpy.data.objects['Camera_Perspective'];s.render.filepath=str(previews/(name+'.png'));bpy.ops.render.render(write_still=True)
set_action(r,bpy.data.actions['AuraWeaver_Idle']);s.frame_set(1)
for view in ['Front','Side','Back']:
    s.camera=bpy.data.objects['Camera_'+view];s.render.filepath=str(previews/(view+'.png'));bpy.ops.render.render(write_still=True)
report={'source':str(src),'source_unchanged':original_hash==hashlib.sha256(src.read_bytes()).hexdigest(),'tail_azimuth_from_back_degrees':tail_angle,'tail_root_fullness':1.32,'fitted_bones':len(old_rest),'qa':qa,'samples':samples,'pouch_anchor_samples':anchor_samples,'manifest':manifest}
(HERE/'validation.json').write_text(json.dumps(report,indent=2))
assert report['source_unchanged']
print('ERA_LUMEN_V9_READY',json.dumps({'tail_angle':tail_angle,'meshes':len(meshes),'clips':len(actions)}))
